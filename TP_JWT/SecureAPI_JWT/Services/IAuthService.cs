using SecureAPI_JWT.Models;

namespace SecureAPI_JWT.Services;

/// <summary>
/// Contrat du service d'authentification (inscription, connexion, génération de JWT,
/// refresh tokens et attribution de rôles).
/// </summary>
public interface IAuthService
{
    Task<AuthServiceResult> RegisterAsync(RegisterModel model);

    Task<AuthServiceResult> LoginAsync(LoginModel model);

    /// <summary>Connexion historique du TP par email (route <c>/api/Auth/token</c>).</summary>
    Task<AuthServiceResult> GetTokenAsync(TokenRequestModel model);

    /// <summary>
    /// Consomme un refresh token valide, le révoque et retourne de nouveaux jetons.
    /// Retourne <c>null</c> si le refresh token est inconnu, expiré ou déjà révoqué.
    /// </summary>
    Task<AuthModel?> RefreshTokenAsync(string refreshToken);

    /// <summary>Révoque un refresh token. Retourne <c>true</c> si un jeton actif a été révoqué.</summary>
    Task<bool> RevokeRefreshTokenAsync(string refreshToken);

    /// <summary>Attribue un rôle à un utilisateur.</summary>
    Task<RoleAssignmentResult> AddRoleAsync(AddRoleModel model);
}
