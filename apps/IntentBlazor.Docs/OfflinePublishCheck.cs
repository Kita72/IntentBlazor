using System.Runtime.InteropServices;
using IntentBlazor.Content;
using IntentBlazor.Runtime;
using Microsoft.Data.Sqlite;
using SkiaSharp;

namespace IntentBlazor.Docs;

// Explicit local artifact smoke check. No web host, model configuration or account is initialized.
public static class OfflinePublishCheck
{
    public static void Run()
    {
        using SKBitmap bitmap = new(2, 3); bitmap.Erase(SKColors.CornflowerBlue);
        using SKImage image = SKImage.FromBitmap(bitmap); using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        ContentAnalysis result = new ImageContentAdapter().Read(encoded.ToArray(), "image/png", new ContentLimits(), CancellationToken.None);
        if (result.Width != 2 || result.Height != 3) throw new InvalidOperationException("Published native image handling failed.");
        using SqliteConnection database = new("Data Source=:memory:"); database.Open();
        using SqliteCommand query = database.CreateCommand(); query.CommandText = "SELECT sqlite_version()";
        string sqlite = Convert.ToString(query.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? throw new InvalidOperationException("Published SQLite handling failed.");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            mode = "offline-native",
            width = result.Width,
            height = result.Height,
            sqlite,
            runtime = RuntimeInformation.FrameworkDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString()
        }));
    }
}
