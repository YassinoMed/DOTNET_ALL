using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Entité métier représentant un produit.
    /// Cette couche ne dépend d'aucune autre couche du projet (ni EF Core, ni ASP.NET Core).
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        [Required, StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [StringLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
        public int Stock { get; set; }

        /// <summary>
        /// Règles métier du produit :
        /// - le nom est obligatoire et doit contenir entre 2 et 100 caractères ;
        /// - la description ne peut pas dépasser 500 caractères ;
        /// - le prix doit être strictement positif ;
        /// - le stock ne peut pas être négatif.
        /// </summary>
        public void ValidateBusinessRules()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("Product name is required.");

            if (Name.Length < 2 || Name.Length > 100)
                throw new InvalidOperationException("Product name must contain between 2 and 100 characters.");

            if (Description is { Length: > 500 })
                throw new InvalidOperationException("Product description cannot exceed 500 characters.");

            if (Price <= 0)
                throw new InvalidOperationException("Product price must be positive.");

            if (Stock < 0)
                throw new InvalidOperationException("Product stock cannot be negative.");
        }
    }
}
