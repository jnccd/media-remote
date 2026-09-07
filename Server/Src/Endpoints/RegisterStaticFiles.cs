using Microsoft.Extensions.FileProviders;

namespace Server.Endpoints;

public static class RegisterStaticFiles
{
    public static void RegisterFrontendStaticFiles(this WebApplication app)
    {
        // The frontend may live next to the published binary (packaged/self-contained
        // layout), next to the running exe's directory, or in the repo's dev layout. Check
        // several base directories so the same binary works when published, when wrapped
        // (spawned with a different content root), and when run from the repo.
        var bases = new[]
        {
            AppContext.BaseDirectory,
            app.Environment.ContentRootPath,
            Path.Combine(app.Environment.ContentRootPath, ".."),
        }.Distinct();

        string? dist = null;
        foreach (var baseDir in bases)
        {
            var candidate = Path.Combine(baseDir, "Frontend", "dist");
            if (Directory.Exists(candidate))
            {
                dist = candidate;
                break;
            }
        }

        if (dist is null)
        {
            app.Logger.LogWarning("No frontend build found; serving the API only.");
            return;
        }

        var fileProvider = new PhysicalFileProvider(dist);
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            DefaultFileNames = ["index.html"],
            FileProvider = fileProvider,
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
        });
    }
}
