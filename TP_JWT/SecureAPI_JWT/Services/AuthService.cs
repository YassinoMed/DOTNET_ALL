using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SecureAPI_JWT.Data;
using SecureAPI_JWT.Helpers;
using SecureAPI_JWT.Models;

namespace SecureAPI_JWT.Services;

/// <summary>
/// Implémentation du service d'authentification JWT, avec refresh tokens
/// à rotation et révocation.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<AuthServiceResult> RegisterAsync(RegisterModel model)
    {
        if (!TryValidate(model, out var validationMessage))
        {
            return AuthServiceResult.InvalidRequest(validationMessage);
        }

        if (await _userManager.FindByEmailAsync(model.Email) is not null)
        {
            return AuthServiceResult.Conflict("Un compte avec cet email existe déjà.");
        }

        if (await _userManager.FindByNameAsync(model.Username) is not null)
        {
            return AuthServiceResult.Conflict("Ce nom d'utilisateur est déjà pris.");
        }

        var user = new ApplicationUser
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            UserName = model.Username,
            Email = model.Email,
            EmailConfirmed = true
        };

        // Le hachage du mot de passe est assuré par Identity (PBKDF2, sel aléatoire).
        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            if (ContainsDuplicateIdentityError(createResult))
            {
                return AuthServiceResult.Conflict(DescribeIdentityFailure(createResult));
            }

            return AuthServiceResult.InvalidRequest(DescribeIdentityFailure(createResult));
        }

        await EnsureRoleExistsAsync(RoleNames.User);

        var addRoleResult = await _userManager.AddToRoleAsync(user, RoleNames.User);
        if (!addRoleResult.Succeeded)
        {
            // Évite de laisser un compte sans rôle si l'attribution échoue.
            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                _logger.LogError(
                    "Échec de l'attribution du rôle User et du nettoyage du compte {UserId}. " +
                    "Erreurs : {Errors}",
                    user.Id,
                    string.Join(", ", deleteResult.Errors.Select(error => error.Code)));
            }

            _logger.LogError(
                "Échec de l'attribution du rôle User pour {UserId}. Erreurs : {Errors}",
                user.Id,
                string.Join(", ", addRoleResult.Errors.Select(error => error.Code)));

            return AuthServiceResult.Failed("L'inscription n'a pas pu être finalisée.");
        }

        var roles = new List<string> { RoleNames.User };
        var (token, expiresOn) = CreateJwtToken(user, roles);
        var refreshToken = await CreateRefreshTokenAsync(user);

        var response = new AuthModel
        {
            IsAuthenticated = true,
            Message = "Inscription réussie.",
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles,
            Token = token,
            RefreshToken = refreshToken,
            ExpiresOn = expiresOn
        };

        return AuthServiceResult.Success(response);
    }

    public async Task<AuthServiceResult> LoginAsync(LoginModel model)
    {
        if (!TryValidate(model, out var validationMessage))
        {
            return AuthServiceResult.InvalidRequest(validationMessage);
        }

        // Connexion possible par nom d'utilisateur ou par email.
        var user = await _userManager.FindByNameAsync(model.Username)
                   ?? await _userManager.FindByEmailAsync(model.Username);

        // Message volontairement générique : ne révèle pas l'existence du compte.
        if (user is null)
        {
            return AuthServiceResult.Unauthorized("Identifiants invalides.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return AuthServiceResult.Unauthorized(
                "Identifiants invalides ou compte temporairement verrouillé.");
        }

        if (!await _userManager.CheckPasswordAsync(user, model.Password))
        {
            // Incrémente le compteur d'échecs ; Identity verrouille après 5 tentatives.
            var accessFailedResult = await _userManager.AccessFailedAsync(user);
            if (!accessFailedResult.Succeeded)
            {
                _logger.LogError(
                    "Impossible d'incrémenter le compteur d'échecs pour {UserId}. Erreurs : {Errors}",
                    user.Id,
                    string.Join(", ", accessFailedResult.Errors.Select(error => error.Code)));
            }

            return AuthServiceResult.Unauthorized("Identifiants invalides.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var (token, expiresOn) = CreateJwtToken(user, roles);
        var refreshToken = await CreateRefreshTokenAsync(user);

        var response = new AuthModel
        {
            IsAuthenticated = true,
            Message = "Connexion réussie.",
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles,
            Token = token,
            RefreshToken = refreshToken,
            ExpiresOn = expiresOn
        };

        return AuthServiceResult.Success(response);
    }

    public Task<AuthServiceResult> GetTokenAsync(TokenRequestModel model)
    {
        // Rétro-compatibilité avec l'énoncé du TP : POST /api/Auth/token (email + mot de passe).
        return LoginAsync(new LoginModel
        {
            Username = model.Email,
            Password = model.Password
        });
    }

    public async Task<AuthModel?> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var hash = HashRefreshToken(refreshToken);
        var stored = await _context.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.Token == hash);

        if (stored is null || !stored.IsActive)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(stored.UserId);
        if (user is null)
        {
            return null;
        }

        // Rotation atomique : un seul appel concurrent peut consommer le refresh token.
        var now = DateTime.UtcNow;
        var claimed = await _context.RefreshTokens
            .Where(token => token.Id == stored.Id &&
                            token.RevokedOn == null &&
                            token.ExpiresOn > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedOn, now));

        if (claimed != 1)
        {
            return null;
        }

        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var (token, expiresOn) = CreateJwtToken(user, roles);
        var newRefreshToken = await CreateRefreshTokenAsync(user);

        return new AuthModel
        {
            IsAuthenticated = true,
            Message = "Token rafraîchi.",
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Roles = roles,
            Token = token,
            RefreshToken = newRefreshToken,
            ExpiresOn = expiresOn
        };
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var hash = HashRefreshToken(refreshToken);
        var now = DateTime.UtcNow;
        var revoked = await _context.RefreshTokens
            .Where(token => token.Token == hash && token.RevokedOn == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedOn, now));

        return revoked == 1;
    }

    public async Task<RoleAssignmentResult> AddRoleAsync(AddRoleModel model)
    {
        if (!TryValidate(model, out var validationMessage))
        {
            return RoleAssignmentResult.InvalidRequest(validationMessage);
        }

        if (!RoleNames.All.Contains(model.Role))
        {
            return RoleAssignmentResult.InvalidRequest(
                "Rôle inconnu. Rôles autorisés : User, Admin.");
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null)
        {
            return RoleAssignmentResult.NotFound("Utilisateur introuvable.");
        }

        await EnsureRoleExistsAsync(model.Role);

        if (await _userManager.IsInRoleAsync(user, model.Role))
        {
            // Attribution idempotente : un rôle déjà présent n'est pas une erreur.
            return RoleAssignmentResult.Success();
        }

        var result = await _userManager.AddToRoleAsync(user, model.Role);
        if (!result.Succeeded)
        {
            _logger.LogError(
                "Échec de l'attribution du rôle {Role} pour {UserId}. Erreurs : {Errors}",
                model.Role,
                model.UserId,
                string.Join(", ", result.Errors.Select(error => error.Code)));

            return RoleAssignmentResult.Failed("L'attribution du rôle a échoué.");
        }

        return RoleAssignmentResult.Success();
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (await _roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Impossible de créer le rôle {roleName} : " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
        }
    }

    /// <summary>
    /// Crée un refresh token opaque, stocke uniquement son condensat SHA-256
    /// et retourne la valeur brute au client (jamais persistée en clair).
    /// </summary>
    private async Task<string> CreateRefreshTokenAsync(ApplicationUser user)
    {
        var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = HashRefreshToken(rawToken),
            ExpiresOn = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDurationInDays)
        });

        await _context.SaveChangesAsync();
        return rawToken;
    }

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool TryValidate<TModel>(TModel model, out string message)
        where TModel : class
    {
        if (model is null)
        {
            message = "Les données de la requête sont obligatoires.";
            return false;
        }

        var validationResults = new List<ValidationResult>();
        if (Validator.TryValidateObject(
                model,
                new ValidationContext(model),
                validationResults,
                validateAllProperties: true))
        {
            message = string.Empty;
            return true;
        }

        message = validationResults.FirstOrDefault()?.ErrorMessage
                  ?? "Les données fournies sont invalides.";
        return false;
    }

    private static bool ContainsDuplicateIdentityError(IdentityResult result) =>
        result.Errors.Any(error =>
            error.Code is "DuplicateEmail" or "DuplicateUserName");

    /// <summary>
    /// Traduit les erreurs Identity en messages fonctionnels, sans exposer de détail interne.
    /// </summary>
    private static string DescribeIdentityFailure(IdentityResult result)
    {
        var messages = result.Errors
            .Select(error => error.Code)
            .Select(code => code switch
            {
                "DuplicateEmail" => "Un compte avec cet email existe déjà.",
                "DuplicateUserName" => "Ce nom d'utilisateur est déjà pris.",
                "PasswordTooShort" or "PasswordRequiresDigit" or
                "PasswordRequiresLower" or "PasswordRequiresUpper" or
                "PasswordRequiresNonAlphanumeric" or "PasswordRequiresUniqueChars" =>
                    "Le mot de passe ne respecte pas la politique de sécurité.",
                "InvalidEmail" or "InvalidUserName" => "Les données fournies sont invalides.",
                _ => "Les données fournies ne permettent pas de créer le compte."
            })
            .Distinct(StringComparer.Ordinal);

        return "Échec de l'inscription : " + string.Join(" ", messages);
    }

    /// <summary>
    /// Génère un JWT signé HMAC SHA-256 et retourne aussi sa date d'expiration exacte.
    /// </summary>
    private (string Token, DateTime ExpiresOn) CreateJwtToken(
        ApplicationUser user,
        IReadOnlyCollection<string> roles)
    {
        var now = DateTime.UtcNow;
        var expiresOn = now.AddDays(_jwtSettings.DurationInDays);

        var claims = new List<Claim>
        {
            new(JwtClaimNames.Subject, user.Id),
            new(JwtClaimNames.JwtId, Guid.NewGuid().ToString("N")),
            new(JwtClaimNames.Name, user.UserName ?? string.Empty),
            new(JwtClaimNames.Email, user.Email ?? string.Empty),
            new(JwtClaimNames.UserId, user.Id)
        };

        foreach (var role in roles.Distinct(StringComparer.Ordinal))
        {
            claims.Add(new Claim(JwtClaimNames.Roles, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresOn,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresOn);
    }
}
