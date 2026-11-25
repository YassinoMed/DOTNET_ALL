using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureAPI_JWT.Helpers;

namespace SecureAPI_JWT.Controllers;

/// <summary>
/// Contrôleur d'endpoints sécurisés pour illustrer l'authentification et l'autorisation.
/// Route : api/Secure
/// </summary>
[ApiController]
[Route("api/Secure")]
[Produces("application/json")]
public class SecureController : ControllerBase
{
    /// <summary>
    /// Endpoint public accessible sans authentification.
    /// </summary>
    /// <response code="200">Accès autorisé.</response>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Public()
    {
        return Ok(new
        {
            Message = "Accès public : aucun token requis.",
            Timestamp = DateTime.UtcNow,
            Authenticated = false
        });
    }

    /// <summary>
    /// Endpoint protégé : accessible uniquement avec un JWT valide.
    /// Retourne les informations du profil de l'utilisateur connecté.
    /// </summary>
    /// <response code="200">Profil retourné.</response>
    /// <response code="401">Token absent ou invalide.</response>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Profile()
    {
        var userId = User.FindFirst(JwtClaimNames.UserId)?.Value
                     ?? User.FindFirst(JwtClaimNames.Subject)?.Value
                     ?? string.Empty;
        var username = User.Identity?.Name
                       ?? User.FindFirst(JwtClaimNames.Name)?.Value
                       ?? string.Empty;
        var email = User.FindFirst(JwtClaimNames.Email)?.Value ?? string.Empty;
        var roles = User.FindAll(JwtClaimNames.Roles)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Ok(new
        {
            Message = "Profil utilisateur connecté.",
            UserId = userId,
            Username = username,
            Email = email,
            Roles = roles,
            Authenticated = true
        });
    }

    /// <summary>
    /// Endpoint accessible à tout utilisateur authentifié (rôle User ou Admin).
    /// </summary>
    /// <response code="200">Accès autorisé.</response>
    /// <response code="401">Token absent ou invalide.</response>
    /// <response code="403">Accès interdit (authentifié mais non autorisé).</response>
    [HttpGet("user")]
    [Authorize(Roles = RoleNames.User)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult UserArea()
    {
        return Ok(new
        {
            Message = "Espace réservé aux utilisateurs authentifiés (rôle User).",
            Username = User.Identity?.Name ?? string.Empty,
            Authenticated = true
        });
    }

    /// <summary>
    /// Endpoint réservé aux administrateurs.
    /// </summary>
    /// <response code="200">Accès autorisé (Admin).</response>
    /// <response code="401">Token absent ou invalide.</response>
    /// <response code="403">Utilisateur non administrateur (403 Forbidden).</response>
    [HttpGet("admin")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult AdminArea()
    {
        return Ok(new
        {
            Message = "Espace réservé aux administrateurs (rôle Admin).",
            Username = User.Identity?.Name ?? string.Empty,
            Authenticated = true
        });
    }

    /// <summary>
    /// Endpoint accessible aux utilisateurs ET aux administrateurs.
    /// </summary>
    /// <response code="200">Accès autorisé (User ou Admin).</response>
    /// <response code="401">Token absent ou invalide.</response>
    /// <response code="403">Accès interdit.</response>
    [HttpGet("user-or-admin")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult UserOrAdmin()
    {
        return Ok(new
        {
            Message = "Espace accessible aux utilisateurs et administrateurs.",
            Username = User.Identity?.Name ?? string.Empty,
            Authenticated = true
        });
    }
}
