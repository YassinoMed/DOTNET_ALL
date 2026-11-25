using System.ComponentModel.DataAnnotations;

namespace ProductsAPI.Models;

/// <summary>
/// Produit exposé par ProductsAPI (propriétés conformes au PDF, page 10).
/// </summary>
public class Product
{
    public int ProductId { get; set; }

    [Required(ErrorMessage = "Le nom du produit est obligatoire.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Le nom doit contenir entre 1 et 100 caractères.")]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Le stock ne peut pas être négatif.")]
    public int StockQte { get; set; }

    [Range(0d, double.MaxValue, ErrorMessage = "Le prix ne peut pas être négatif.")]
    public double Price { get; set; }
}
