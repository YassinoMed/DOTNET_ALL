using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureAPI_JWT.Helpers;
using SecureAPI_JWT.Models;

namespace SecureAPI_JWT.Data;

/// <summary>
/// Initialisation de la base de données au démarrage :
/// application des migrations EF Core, création idempotente des rôles User/Admin,
/// puis création optionnelle d'un administrateur de développement.
/// Aucune donnée existante n'est supprimée et aucune migration destructive n'est exécutée.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DbInitializer");

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Applique uniquement les migrations absentes. Ne supprime jamais la base.
        await context.Database.MigrateAsync();
        logger.LogInformation("Base SQLite à jour (migrations EF Core appliquées).");

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await EnsureRoleAsync(roleManager, RoleNames.User, logger);
        await EnsureRoleAsync(roleManager, RoleNames.Admin, logger);

        // Le seed d'un administrateur est strictement réservé au développement et aux tests.
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            logger.LogInformation(
                "Seed administrateur ignoré : environnement {Environment} (développement/tests uniquement).",
                environment.EnvironmentName);
            return;
        }

        var adminSection = configuration.GetSection("AdminUser");
        var username = adminSection["Username"];
        var email = adminSection["Email"];
        var password = adminSection["Password"];

        if (string.IsNullOrWhiteSpace(username) &&
            string.IsNullOrWhiteSpace(email) &&
            string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "Seed administrateur ignoré : section AdminUser absente. " +
                "Utilisez les User Secrets ou les variables d'environnement pour l'activer.");
            return;
        }

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Seed administrateur ignoré : AdminUser:Username, AdminUser:Email et " +
                "AdminUser:Password doivent être renseignés ensemble.");
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByNameAsync(username);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                FirstName = "Admin",
                LastName = "Application",
                UserName = username,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
            {
                // Les détails restent dans les logs serveur, jamais dans une réponse HTTP.
                logger.LogError(
                    "Création de l'administrateur impossible : {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Code)));
                return;
            }

            logger.LogInformation("Administrateur de développement créé : {Username}.", username);
        }

        if (!await userManager.IsInRoleAsync(admin, RoleNames.Admin))
        {
            var addRoleResult = await userManager.AddToRoleAsync(admin, RoleNames.Admin);
            if (!addRoleResult.Succeeded)
            {
                logger.LogError(
                    "Attribution du rôle Admin impossible : {Errors}",
                    string.Join(", ", addRoleResult.Errors.Select(e => e.Code)));
            }
        }
    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole> roleManager,
        string roleName,
        ILogger logger)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Impossible de créer le rôle {roleName} : " +
                string.Join(", ", result.Errors.Select(e => e.Code)));
        }

        logger.LogInformation("Rôle créé : {Role}.", roleName);
    }
}
