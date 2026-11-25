using System.ComponentModel.DataAnnotations;

namespace BanqueApi.Models;

public class Operation
{
    public int Id { get; set; }

    [Required]
    [RegularExpression("^(Retrait|Versement|Virement)$")]
    public string TypeOperation { get; set; } = string.Empty;

    [Required]
    [Range(0.01, 1000000000)]
    public double Montant { get; set; }

    public DateTime DateOperation { get; set; } = DateTime.Now;

    [Required]
    public int CompteId { get; set; }

    public Compte? Compte { get; set; }
}
