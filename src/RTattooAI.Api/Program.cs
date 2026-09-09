using System.Text;
using System.Threading.RateLimiting;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RTattooAI.Api.Filters;
using RTattooAI.Api.Middleware;
using RTattooAI.Application.Abstractions;
using RTattooAI.Infrastructure;
using RTattooAI.Infrastructure.Persistence;

const string FrontendCorsPolicy = "Frontend";
const string LoginRateLimitPolicy = "Login";
const string LivePreviewRateLimitPolicy = "LivePreview";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter());
    options.Filters.Add<AuditActionFilter>();
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// Detras de nginx y del tunel de Cloudflare: sin esto la IP del cliente seria
// siempre la del proxy y el limitador de intentos de login no serviria de nada.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "RTattooAI API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Pega solo el token JWT (sin el prefijo 'Bearer ')."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = []
    });
});
builder.Services.AddInfrastructure(builder.Configuration);

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key no está configurado.");

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            // Desactivar una cuenta debe cortar tambien las sesiones ya abiertas: sin
            // esta comprobacion el token seguiria siendo valido hasta caducar.
            OnTokenValidated = async context =>
            {
                var usuarioIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(usuarioIdClaim, out var usuarioId))
                {
                    context.Fail("Token sin identificador de usuario.");
                    return;
                }

                var repository = context.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>();
                var usuario = await repository.GetByIdAsync(usuarioId, context.HttpContext.RequestAborted);
                if (usuario is null || !usuario.EstaActivo)
                {
                    context.Fail("La cuenta está desactivada o ya no existe.");
                }
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Limite general por IP: contiene el escaneo automatizado sin estorbar el uso normal.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 400,
                QueueLimit = 0
            }));

    // El modo asistido en vivo envia un fotograma tras otro: necesita un techo propio,
    // mas alto que el de una accion normal pero acotado para que no sirva de ariete.
    options.AddPolicy(LivePreviewRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 90,
                QueueLimit = 0
            }));

    options.AddPolicy(LoginRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 5,
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "RTattooAI API v1"));
}

// Migraciones y siembra en todos los entornos: en un despliegue propio no hay
// ninguna otra oportunidad de crear el esquema ni la primera cuenta, y sin cuenta
// inicial el sistema queda inaccesible.
if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
        app.Configuration,
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer"));
}
else
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.EnsureRolesAsync(db);
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        if (exceptionFeature is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(exceptionFeature.Error, "Excepcion no controlada en {Path}", exceptionFeature.Path);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "Ocurrio un error inesperado. Intentelo nuevamente." });
    });
});

app.UseMiddleware<SecurityHeadersMiddleware>();

// En el despliegue propio, TLS termina en Cloudflare y nginx: redirigir aqui
// provocaria un bucle porque el contenedor solo escucha HTTP.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Pensado para un ping externo periodico (GitHub Actions, etc.): ademas de
// confirmar que el backend esta despierto, llama al servicio IA (interno,
// tambien scale-to-zero) para evitar el cold start de ~30s en el primer
// analisis real tras un periodo de inactividad.
app.MapGet("/health/deep", async (IIaServiceClient iaServiceClient, CancellationToken cancellationToken) =>
{
    var iaUp = await iaServiceClient.PingAsync(cancellationToken);
    return Results.Ok(new { status = "ok", iaService = iaUp ? "ok" : "unreachable" });
}).AllowAnonymous();

app.MapControllers();

app.Run();
