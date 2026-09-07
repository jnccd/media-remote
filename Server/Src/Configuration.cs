using System.Net;
using System.Reflection;
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
        // (localhost:7779) or cross-origin (a dev server on :5173, or a different host), and
        // the Authorization header makes those cross-origin requests trigger a CORS preflight.
        app.UseCors(policy => policy
            .WithOrigins("http://localhost:5173", "http://pc-ryzen:7779", "http://localhost:7779", "http://0.0.0.0:7779")
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
            options.Listen(IPAddress.Any, port, listenOptions =>
            {
                //listenOptions.UseHttps(x509);
            });
        });

        builder.Services.AddCors();
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
