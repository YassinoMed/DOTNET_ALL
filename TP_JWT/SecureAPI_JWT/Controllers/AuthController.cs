using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureAPI_JWT.Helpers;
using SecureAPI_JWT.Models;
using SecureAPI_JWT.Services;

namespace SecureAPI_JWT.Controllers;

/// <summary>
/// Contrôleur d'authentification : inscription, connexion JWT et attribution de rôles.
/// Route : api/Auth
/// </summary>
[ApiController]
[Route("api/Auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Inscription d'un nouvel utilisateur. Le rôle User est attribué automatiquement.
    /// </summary>
    /// <response code="201">Utilisateur créé et JWT émis.</response>
    /// <response code="400">Données invalides.</response>
    /// <response code="409">Email ou nom d'utilisateur déjà utilisé.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(typeof(AuthModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AuthModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AuthModel), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterModel model)
    {
        var result = await _authService.RegisterAsync(model);

        if (result.Status == AuthServiceStatus.Success)
        {
            return StatusCode(StatusCodes.Status201Created, result.Model);
        }

        return ToFailureResponse(result);
    }

    /// <summary>
    /// Connexion d'un utilisateur (nom d'utilisateur ou email) et émission d'un JWT.
    /// </summary>
    /// <response code="200">Authentification réussie, JWT retourné.</response>
    /// <response code="400">Données invalides.</response>
    /// <response code="401">Identifiants invalides ou compte verrouillé.</response>
    /// <response code="429">Limite de tentatives atteinte.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(typeof(AuthModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthModel), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var result = await _authService.LoginAsync(model);

        if (result.Status == AuthServiceStatus.Success)
        {
            return Ok(result.Model);
        }

        return ToFailureResponse(result);
    }

    /// <summary>
    /// Variante historique du TP : connexion par email et mot de passe.
    /// </summary>
    /// <response code="200">Authentification réussie, JWT retourné.</response>
    /// <response code="401">Identifiants invalides ou compte verrouillé.</response>
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(typeof(AuthModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthModel), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetToken([FromBody] TokenRequestModel model)
    {
        var result = await _authService.GetTokenAsync(model);

        if (result.Status == AuthServiceStatus.Success)
        {
            return Ok(result.Model);
        }

        return ToFailureResponse(result);
    }

    /// <summary>
    /// Échange un refresh token valide contre un nouveau JWT et un nouveau refresh token.
    /// L'ancien refresh token est révoqué (rotation).
    /// </summary>
    /// <response code="200">Nouveaux jetons émis.</response>
    /// <response code="401">Refresh token inconnu, expiré ou déjà révoqué.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(typeof(AuthModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh([FromBody] string refreshToken)
    {
        var result = await _authService.RefreshTokenAsync(refreshToken);
        if (result is null)
        {
            return Unauthorized(new { Message = "Refresh token invalide, expiré ou révoqué." });
        }

        return Ok(result);
    }

    /// <summary>
    /// Révoque un refresh token. L'opération est idempotente et ne révèle pas
    /// si le jeton existait réellement.
    /// </summary>
    /// <response code="200">Traitement effectué.</response>
    [HttpPost("revoke")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Revoke([FromBody] string refreshToken)
    {
        await _authService.RevokeRefreshTokenAsync(refreshToken);
        return Ok(new { Message = "Refresh token révoqué ou inconnu." });
    }

    /// <summary>
    /// Attribue un rôle User ou Admin à un utilisateur. Endpoint réservé aux administrateurs.
    /// </summary>
    /// <response code="200">Rôle attribué (ou déjà présent).</response>
    /// <response code="400">Rôle ou données invalides.</response>
    /// <response code="401">Token absent ou invalide.</response>
    /// <response code="403">Utilisateur authentifié mais non administrateur.</response>
    /// <response code="404">Utilisateur introuvable.</response>
    [HttpPost("addrole")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRole([FromBody] AddRoleModel model)
    {
        var result = await _authService.AddRoleAsync(model);

        switch (result.Status)
        {
            case RoleAssignmentStatus.Success:
                return Ok(new
                {
                    Message = $"Rôle '{model.Role}' attribué à l'utilisateur.",
                    model.UserId,
                    model.Role
                });

            case RoleAssignmentStatus.InvalidRequest:
                return BadRequest(new { Message = result.Message });

            case RoleAssignmentStatus.NotFound:
                return NotFound(new { Message = result.Message });

            default:
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "L'attribution du rôle a échoué.");
        }
    }

    private IActionResult ToFailureResponse(AuthServiceResult result) => result.Status switch
    {
        AuthServiceStatus.InvalidRequest => BadRequest(result.Model),
        AuthServiceStatus.Conflict => Conflict(result.Model),
        AuthServiceStatus.Unauthorized => Unauthorized(result.Model),
        _ => Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Une erreur interne est survenue.")
    };
}
