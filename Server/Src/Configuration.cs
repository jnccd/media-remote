using System.Net;
using System.Reflection;
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
    }

    public static void ConfigureWebApp(this WebApplication app)
    {
        var logger = app.Services.GetService(typeof(LoggerService)) as LoggerService;
#if DEBUG
        logger!.WriteLine("Launching in development mode!");
        app.UseOpenApi();
        app.UseSwaggerUi();

        app.UseCors(policy => policy
            .WithOrigins("http://localhost:5173", "http://pc-ryzen:7779") // Sadly, this is needed for CORS to work preflight options auth reqs
            .AllowAnyMethod()
            .AllowAnyHeader());
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
