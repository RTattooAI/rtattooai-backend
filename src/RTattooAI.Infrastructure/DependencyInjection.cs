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
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IConsultaRepository, ConsultaRepository>();
        services.AddScoped<ITatuajeCatalogoRepository, TatuajeCatalogoRepository>();
        services.AddScoped<IBandaCriminalRepository, BandaCriminalRepository>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddSingleton<IBlobStorage, AzureBlobStorage>();
        services.AddSingleton<IImageCropper, ImageSharpCropper>();

        var iaServiceBaseUrl = configuration["IaService:BaseUrl"]
            ?? throw new InvalidOperationException("IaService:BaseUrl no está configurado.");
        services.AddHttpClient<IIaServiceClient, IaServiceClient>(client =>
        {
            client.BaseAddress = new Uri(iaServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
