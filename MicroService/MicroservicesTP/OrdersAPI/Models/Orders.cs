using System.ComponentModel.DataAnnotations;

namespace OrdersAPI.Models;

/// <summary>
/// Commande exposée par OrdersAPI (propriétés conformes au PDF, page 16).
/// </summary>
public class Orders
{
    public int OrdersId { get; set; }

    public DateTime OrderDate { get; set; }

    [StringLength(100, ErrorMessage = "Le nom du client ne peut pas dépasser 100 caractères.")]
    public string? CustomerName { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La commande doit référencer un produit valide.")]
    public int ProductId { get; set; }
}
