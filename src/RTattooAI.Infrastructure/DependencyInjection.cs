using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Services;
using RTattooAI.Infrastructure.Blob;
using RTattooAI.Infrastructure.IaService;
using RTattooAI.Infrastructure.Imaging;
using RTattooAI.Infrastructure.Persistence;
using RTattooAI.Infrastructure.Persistence.Repositories;
using RTattooAI.Infrastructure.Security;

namespace RTattooAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Default"),
                // El contenedor de SQL Server tarda en aceptar conexiones tras arrancar,
                // y una caida momentanea no deberia tumbar una peticion en curso.
                sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRolRepository, RolRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUsersService, UsersService>();
        services.AddScoped<IDashboardService, RTattooAI.Infrastructure.Services.DashboardService>();

        services.AddScoped<IConsultaRepository, ConsultaRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITatuajeCatalogoRepository, TatuajeCatalogoRepository>();
        services.AddScoped<IBandaCriminalRepository, BandaCriminalRepository>();
        services.AddScoped<IBandsService, BandsService>();
        services.AddScoped<IVersionModeloRepository, VersionModeloRepository>();
        services.AddScoped<IModelsService, ModelsService>();
        services.AddScoped<IGalleryConsistencyService, GalleryConsistencyService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddSingleton<IFileUrlSigner, FileUrlSigner>();

        // "local" (predeterminado) guarda en disco y sirve las imagenes con URL firmada;
        // "azure" mantiene el camino original con Blob Storage y SAS.
        var storageProvider = configuration["Storage:Provider"] ?? "local";
        var encryptionKey = ParseEncryptionKey(configuration["Storage:EncryptionKey"]);

        services.AddSingleton<IBlobStorage>(sp =>
        {
            IBlobStorage almacenamiento = string.Equals(storageProvider, "azure", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<AzureBlobStorage>(sp)
                : ActivatorUtilities.CreateInstance<LocalFileStorage>(sp);

            // Cifrado en reposo (RNF-06). En Azure lo daba el propio Blob Storage; en
            // disco propio hay que hacerlo aqui. Sin clave configurada el sistema sigue
            // funcionando sin cifrar: es preferible a que un despliegue no arranque.
            return encryptionKey is null ? almacenamiento : new EncryptedBlobStorage(almacenamiento, encryptionKey);
        });
        services.AddSingleton<IImageCropper, ImageSharpCropper>();

        var iaServiceBaseUrl = configuration["IaService:BaseUrl"]
            ?? throw new InvalidOperationException("IaService:BaseUrl no está configurado.");
        services.AddHttpClient<IIaServiceClient, IaServiceClient>(client =>
        {
            client.BaseAddress = new Uri(iaServiceBaseUrl);
            // El servicio IA puede escalar desde 0 réplicas (scale-to-zero); un cold start
            // tarda en cargar los modelos, más que el timeout HTTP por defecto.
            client.Timeout = TimeSpan.FromSeconds(100);
        });

        return services;
    }

    /// <summary>
    /// Clave AES-256 en base64 (32 bytes). Una clave mal formada aborta el arranque en
    /// vez de degradar en silencio a "sin cifrado": creer que se cifra sin hacerlo es
    /// peor que no cifrar.
    /// </summary>
    private static byte[]? ParseEncryptionKey(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configured.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Storage:EncryptionKey debe estar en base64.", ex);
        }

        if (key.Length != 32)
        {
            throw new InvalidOperationException(
                $"Storage:EncryptionKey debe decodificar a 32 bytes (AES-256); son {key.Length}.");
        }

        return key;
    }
}
