using System.ComponentModel.DataAnnotations;

namespace SecureAPI_JWT.Models;

/// <summary>
/// Données d'inscription d'un nouvel utilisateur.
/// </summary>
public class RegisterModel
{
    [Required(ErrorMessage = "Le prénom est obligatoire.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Le prénom doit contenir entre 2 et 50 caractères.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est obligatoire.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Le nom doit contenir entre 2 et 50 caractères.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom d'utilisateur est obligatoire.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Le nom d'utilisateur doit contenir entre 3 et 50 caractères.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "L'email n'est pas au format valide.")]
    [StringLength(256, ErrorMessage = "L'email ne peut pas dépasser 256 caractères.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Le mot de passe doit contenir entre 8 et 100 caractères.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$",
        ErrorMessage = "Le mot de passe doit contenir au moins une majuscule, une minuscule, un chiffre et un caractère spécial.")]
    public string Password { get; set; } = string.Empty;
}
