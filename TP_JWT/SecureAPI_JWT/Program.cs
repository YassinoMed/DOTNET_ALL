using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SecureAPI_JWT.Data;
using SecureAPI_JWT.Helpers;
using SecureAPI_JWT.Models;
using SecureAPI_JWT.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Configuration JWT : aucune clé en dur dans le dépôt.
//    La clé provient des User Secrets, des variables d'environnement ou d'un
//    gestionnaire de secrets. La validation échoue au démarrage si elle manque.
// ---------------------------------------------------------------------------
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Key) &&
                    Encoding.UTF8.GetByteCount(settings.Key) >= 32,
        "JwtSettings:Key est obligatoire et doit contenir au moins 32 octets (256 bits). " +
        "Définissez-le avec : dotnet user-secrets set \"JwtSettings:Key\" \"<secret>\" " +
        "ou la variable d'environnement JwtSettings__Key.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Issuer),
        "JwtSettings:Issuer est obligatoire.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Audience),
        "JwtSettings:Audience est obligatoire.")
    .Validate(
        settings => settings.DurationInDays is >= 1 and <= 30,
        "JwtSettings:DurationInDays doit être compris entre 1 et 30 jours.")
    .Validate(
        settings => settings.RefreshTokenDurationInDays is >= 1 and <= 90,
        "JwtSettings:RefreshTokenDurationInDays doit être compris entre 1 et 90 jours.")
    .ValidateOnStart();

// ---------------------------------------------------------------------------
// 2. Base de données SQLite + Identity (sans cookie : API JWT uniquement)
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    // La connexion est relue au moment de créer le DbContext afin que les
    // surcharges tardives (tests WebApplicationFactory, variables d'environnement)
    // soient toujours prises en compte.
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection est obligatoire.");

    options.UseSqlite(connectionString);
});

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        options.User.RequireUniqueEmail = true;

        // Protection contre le bourrage d'identifiants.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---------------------------------------------------------------------------
// 3. Authentification JWT Bearer
//    Les options sont configurées via DI afin que les surcharges de test
//    (WebApplicationFactory) soient prises en compte.
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        // Conserve les noms de claims natifs (sub, name, email, roles, ...).
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtClaimNames.Name,
            RoleClaimType = JwtClaimNames.Roles,
            // Épingle explicitement HMAC SHA-256 : refuse HS384/HS512 et alg=none.
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    Message = "Authentification requise ou token invalide."
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    Message = "Accès interdit : rôle insuffisant."
                });
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// 4. Contrôleurs, ProblemDetails et CORS configurable (désactivé par défaut)
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ConfiguredOrigins", policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
}

// ---------------------------------------------------------------------------
// 5. Service d'authentification
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IAuthService, AuthService>();

// ---------------------------------------------------------------------------
// 6. Swagger / OpenAPI avec support JWT Bearer (développement et tests)
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "SecureAPI_JWT",
        Version = "v1",
        Description = "API REST sécurisée avec JWT — TP 9 (ASP.NET Core 7, SQLite, Identity)"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Saisir : Bearer {token}"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// 7. Validation fail-fast avant toute écriture en base
// ---------------------------------------------------------------------------
_ = app.Services.GetRequiredService<IOptions<JwtSettings>>().Value;

// ---------------------------------------------------------------------------
// 8. Migrations + seed idempotent (rôles User/Admin, admin optionnel en dev)
// ---------------------------------------------------------------------------
await DbInitializer.InitializeAsync(app.Services, app.Configuration, app.Environment);

// ---------------------------------------------------------------------------
// 9. Pipeline HTTP
//    Ordre : erreurs -> HSTS/HTTPS -> Swagger -> Routing -> CORS
//            -> Authentication -> Authorization -> Controllers
// ---------------------------------------------------------------------------
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var feature = context.Features.Get<IExceptionHandlerFeature>();
        if (feature?.Error is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(feature.Error, "Erreur non gérée sur {Path}.", context.Request.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Une erreur interne est survenue.",
            Detail = "Aucun détail technique n'est exposé."
        });
    });
});

if (!app.Environment.IsEnvironment("Testing"))
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

if (allowedOrigins.Length > 0)
{
    app.UseCors("ConfiguredOrigins");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Rendu public de la classe Program implicite pour WebApplicationFactory<Program>.
public partial class Program { }
