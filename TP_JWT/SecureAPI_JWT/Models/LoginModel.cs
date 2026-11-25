using System.ComponentModel.DataAnnotations;

namespace SecureAPI_JWT.Models;

/// <summary>
/// Données de connexion d'un utilisateur.
/// Le champ <see cref="Username"/> accepte un nom d'utilisateur ou un email.
/// </summary>
public class LoginModel
{
    [Required(ErrorMessage = "Le nom d'utilisateur est obligatoire.")]
    [StringLength(256, ErrorMessage = "Le nom d'utilisateur est trop long.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(100, ErrorMessage = "Le mot de passe est trop long.")]
    public string Password { get; set; } = string.Empty;
}
