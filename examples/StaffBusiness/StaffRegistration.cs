using IntentBlazor.ExampleHost;
using IntentBlazor.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntentBlazor.StaffBusiness;

public static class StaffRegistration
{
    public static IServiceCollection AddStaffBusiness(this IServiceCollection services, IConfiguration configuration, string directory)
    {
        string connection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "business.sqlite"), Pooling = false }.ToString();
        services.AddDbContextFactory<BusinessDbContext>(options => options.UseSqlite(connection));
        services.TryAddSingleton(TimeProvider.System); services.AddScoped<IStaffRepository, EfStaffRepository>();
        services.AddIntentAction<SaveAssignmentAction>(SaveAssignmentAction.Definition);
        services.AddExampleScenario<StaffScenario>(configuration["Ai:Provider"] is { Length: > 0 } provider && !provider.Equals("Fixture", StringComparison.OrdinalIgnoreCase));
        return services;
    }
}
