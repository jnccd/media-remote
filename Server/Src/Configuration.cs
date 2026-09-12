using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Server.Input;
using Server.Services;
using NSwag;

namespace Server;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
public class RegisterImplementation(ServiceRegisterType serviceRegisterType, Type serviceType) : Attribute
{
    public readonly ServiceRegisterType serviceRegisterType = serviceRegisterType;
    public readonly Type serviceType = serviceType;
}

public enum ServiceRegisterType { Singleton, Transient }

public static class Configuration
{
    public static void RegisterServices(this WebApplicationBuilder builder)
    {
        // Local Assembly Services
        Type[] serviceTypes = (from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                               from declaringType in domainAssembly.GetTypes()
                               where declaringType.Module == typeof(Configuration).Module
                                   && declaringType.CustomAttributes.Any(x => x.AttributeType == typeof(RegisterImplementation))
                               select declaringType).ToArray();
        foreach (var declaringType in serviceTypes)
        {
            var attr = declaringType.GetCustomAttribute<RegisterImplementation>();

            if (attr == null || attr?.serviceType == null || attr?.serviceRegisterType == null) continue;

            if (attr.serviceRegisterType == ServiceRegisterType.Singleton)
                builder.Services.AddSingleton(declaringType, attr.serviceType);
            else if (attr?.serviceRegisterType == ServiceRegisterType.Transient)
                builder.Services.AddTransient(declaringType, attr.serviceType);
        }

        // Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApiDocument(config =>
        {
            config.Title = "MediaRemote API";
            config.Version = "v1";
        });

        // Controller
        builder.Services.AddControllers();

        // Platform-specific input/media backends (Windows vs Linux).
        builder.Services.AddSingleton<IInputSimulator>(_ => InputBackendFactory.CreateInputSimulator());
        builder.Services.AddSingleton<IMediaController>(_ => InputBackendFactory.CreateMediaController());
    }

    public static void ConfigureWebApp(this WebApplication app)
    {
        var logger = app.Services.GetService(typeof(LoggerService)) as LoggerService;

        // CORS is required in all environments: the remote UI may be opened same-origin
        // (localhost:7779), through the Tauri webview (the asset protocol origin), or
        // cross-origin from a dev server / another host. The Authorization header makes
        // those cross-origin requests trigger a CORS preflight. See IsAllowedOrigin.
        app.UseCors(policy => policy
            .SetIsOriginAllowed(IsAllowedOrigin)
            .AllowAnyMethod()
            .AllowAnyHeader());

#if DEBUG
        logger!.WriteLine("Launching in development mode!");
        app.UseOpenApi();
        app.UseSwaggerUi();
#endif
    }

    public static void ConfigureWebhost(this WebApplicationBuilder builder)
    {
        ushort port = string.IsNullOrWhiteSpace(builder.Configuration["PORT"]) ?
            (ushort)7779 :
            Convert.ToUInt16(builder.Configuration["PORT"]);

        //X509Certificate2 x509 = GetCertificateFromConfig(builder);
        builder.WebHost.ConfigureKestrel(options =>
        {
            // ListenAnyIP binds IPv4 *and* IPv6. Binding IPAddress.Any (0.0.0.0)
            // alone serves IPv4 only, which makes a perfectly healthy server look
            // dead to anything that prefers IPv6: `localhost` resolves to ::1
            // first (/etc/hosts) so the Tauri webview gets ECONNREFUSED, and LAN
            // clients holding a global IPv6 address (this host has 2a03:... and a
            // ULA on wlo1) never reach it either. Both showed up as "the server is
            // only reachable from this machine itself".
            options.ListenAnyIP(port, listenOptions =>
            {
                //listenOptions.UseHttps(x509);
            });
        });

        builder.Services.AddCors();
    }

    /// <summary>
    /// Which page origins may call this API.
    ///
    /// The browser cases are same-origin (the server serves the UI) or the Vite
    /// dev server; LAN clients reach it by LAN address. The desktop wrapper is
    /// the awkward one: its webview serves the page from Tauri's own asset
    /// protocol, so requests are cross-origin and the Origin header is
    /// `tauri://localhost` or `http://tauri.localhost` depending on platform.
    /// Those are accepted, plus any localhost/private address so the app keeps
    /// working from a phone or another PC on the network.
    /// </summary>
    private static bool IsAllowedOrigin(string origin)
    {
        if (string.IsNullOrEmpty(origin)) return true; // same-origin / non-browser client

        // Tauri's asset protocol: tauri://localhost (macOS) or
        // http://tauri.localhost (Linux/Windows).
        if (origin.StartsWith("tauri://", StringComparison.OrdinalIgnoreCase) ||
            origin.Contains("tauri.localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;

        if (uri.Host is "localhost" or "127.0.0.1" or "0.0.0.0" or "::1") return true;

        // Private LAN ranges, so a phone pointing at http://<host>:7779 works.
        var host = uri.Host;
        return host.StartsWith("10.") ||
               host.StartsWith("192.168.") ||
               System.Text.RegularExpressions.Regex.IsMatch(host, @"^172\.(1[6-9]|2[0-9]|3[01])\.");
    }

    // // Enable if HTTPS is needed
    // private static X509Certificate2 GetCertificateFromConfig(WebApplicationBuilder builder)
    // {
    //     char _s = Path.DirectorySeparatorChar;
    //     if (string.IsNullOrWhiteSpace(builder.Configuration["CERT_PATH"]))
    //     {
    //         Logger.WriteLine($"CERT_PATH is empty!");
    //         throw new ArgumentException("CERT_PATH is empty!");
    //     }
    //     var certPem = File.ReadAllText($"{builder.Configuration["CERT_PATH"]}{_s}fullchain.pem");
    //     var keyPem = File.ReadAllText($"{builder.Configuration["CERT_PATH"]}{_s}privkey.pem");
    //     var x509 = X509Certificate2.CreateFromPem(certPem, keyPem);
    //     return x509;
    // }
}
