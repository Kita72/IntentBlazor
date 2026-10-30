using System.Security.Claims;
using IntentBlazor.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntentBlazor.Content;

public sealed record ContentDeliveryOptions(string RoutePrefix = "/intent-artifacts");
public static class ContentRegistration
{
    public static IServiceCollection AddIntentContent(this IServiceCollection services, string privateDirectory,
        ContentLimits? limits = null, ArtifactStorageOptions? storage = null, ContentDeliveryOptions? delivery = null)
    {
        limits ??= new(); delivery ??= new();
        if (limits.MaximumBytes is < 1 or > 16 * 1024 * 1024 || limits.MaximumFiles is < 1 or > 16 ||
            limits.MaximumTextCharacters is < 1 or > 64000 || limits.MaximumPixels is < 1 or > 16000000 || limits.MaximumPdfPages is < 1 or > 50)
            throw new ArgumentException("Content limits exceed the supported bounds.");
        if (!delivery.RoutePrefix.StartsWith('/') || delivery.RoutePrefix.StartsWith("//") || delivery.RoutePrefix.Length < 2 ||
            delivery.RoutePrefix.EndsWith('/') || !delivery.RoutePrefix.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_'))
            throw new ArgumentException("Use an application-local artifact route prefix.");
        services.AddIntentBlazor();
        services.TryAddSingleton(limits); services.TryAddSingleton(delivery);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IArtifactStore>(_ => new FileArtifactStore(storage ?? new(privateDirectory)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IContentAdapter, TextContentAdapter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IContentAdapter, ImageContentAdapter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IContentAdapter, PdfContentAdapter>());
        services.TryAddScoped<WorkspaceContent>();
        services.TryAddScoped<IWorkspaceContent>(sp => sp.GetRequiredService<WorkspaceContent>());
        return services;
    }

    public static IEndpointConventionBuilder MapIntentContent(this IEndpointRouteBuilder endpoints, Func<HttpContext, string?>? owner = null)
    {
        ContentDeliveryOptions options = endpoints.ServiceProvider.GetRequiredService<ContentDeliveryOptions>();
        return endpoints.MapGet(options.RoutePrefix + "/{id}/{version}", async (HttpContext context, IArtifactStore store,
            string id, string version, string type, bool download, CancellationToken cancellationToken) =>
        {
            string? identity = context.User.Identity?.IsAuthenticated == true
                ? (owner is null ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) : owner(context)) : null;
            return await ArtifactDownload.ReadAsync(context, store, identity, new(id, type, version), download, cancellationToken);
        }).RequireAuthorization();
    }
}

public static class ArtifactDownload
{
    public static async Task<IResult> ReadAsync(HttpContext context, IArtifactStore store, string? trustedOwner,
        ArtifactReference reference, bool download, CancellationToken token = default)
    {
        if (context.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(trustedOwner)) return Results.Unauthorized();
        try
        {
            ContentArtifact? artifact = await store.DescribeAsync(trustedOwner, reference, token);
            if (artifact is null) return Results.NotFound();
            byte[]? bytes = await store.ReadAsync(trustedOwner, reference, token);
            if (bytes is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            bool inline = !download && artifact.Reference.MediaType is "image/png" or "image/jpeg";
            return Results.File(bytes, artifact.Reference.MediaType, fileDownloadName: inline ? null : artifact.Name);
        }
        catch (ContentRejectedException) { return Results.NotFound(); }
        catch (ArgumentException) { return Results.NotFound(); }
    }
}
