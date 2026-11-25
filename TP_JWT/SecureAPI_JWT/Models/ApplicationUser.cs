using Microsoft.AspNetCore.Identity;

namespace SecureAPI_JWT.Models;

/// <summary>
/// Utilisateur de l'application héritant d'IdentityUser.
/// Aucun champ sensible (mot de passe, hash, sel) n'est exposé ici.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 2)]
    public string FirstName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 2)]
    public string LastName { get; set; } = string.Empty;
}
