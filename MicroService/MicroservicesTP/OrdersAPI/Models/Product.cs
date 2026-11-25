using System.ComponentModel.DataAnnotations;

namespace OrdersAPI.Models;

/// <summary>
/// Projection locale et minimale d'un produit reçu depuis ProductsAPI
/// par l'événement <c>ProductCreated</c> (PDF page 44).
/// </summary>
/// <remarks>
/// OrdersAPI ne lit jamais Products.db : cette table <c>Products</c> vit
/// exclusivement dans Orders.db et ne contient que les informations utiles
/// aux commandes.
/// </remarks>
public class Product
{
    /// <summary>
    /// Identifiant provenant de ProductsAPI (<c>ProductCreated.Id</c>).
    /// Il sert de clé primaire : c'est la garantie d'idempotence.
    /// </summary>
    public int Id { get; set; }

    [Required(ErrorMessage = "Le nom du produit est obligatoire.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Le nom doit contenir entre 1 et 100 caractères.")]
    public string Name { get; set; } = string.Empty;
}
