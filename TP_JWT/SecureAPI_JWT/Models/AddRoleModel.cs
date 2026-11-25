using System.ComponentModel.DataAnnotations;

namespace SecureAPI_JWT.Models;

/// <summary>
/// Données d'attribution d'un rôle à un utilisateur (endpoint réservé aux administrateurs).
/// </summary>
public class AddRoleModel
{
    [Required(ErrorMessage = "L'identifiant de l'utilisateur est obligatoire.")]
    [StringLength(450, ErrorMessage = "L'identifiant de l'utilisateur est trop long.")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le rôle est obligatoire.")]
    [StringLength(50, ErrorMessage = "Le rôle est trop long.")]
    public string Role { get; set; } = string.Empty;
}
