using System.ComponentModel.DataAnnotations;

namespace SecureAPI_JWT.Models;

/// <summary>
/// Modèle de connexion historique du TP (email + mot de passe), exposé par
/// <c>POST /api/Auth/token</c> pour rester fidèle à l'énoncé.
/// </summary>
public class TokenRequestModel
{
    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "L'email n'est pas au format valide.")]
    [StringLength(256, ErrorMessage = "L'email est trop long.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(100, ErrorMessage = "Le mot de passe est trop long.")]
    public string Password { get; set; } = string.Empty;
}
