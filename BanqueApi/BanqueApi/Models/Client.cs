using System.ComponentModel.DataAnnotations;

namespace BanqueApi.Models;

public class Client
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Nom { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Prenom { get; set; } = string.Empty;
    [Phone] public string? Tel { get; set; }
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [StringLength(200)] public string? Adresse { get; set; }

    public ICollection<Compte>? Comptes { get; set; }
}
