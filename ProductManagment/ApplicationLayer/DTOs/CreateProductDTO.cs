using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs
{
    /// <summary>
    /// DTO utilisé pour la création d'un produit.
    /// </summary>
    public class CreateProductDTO
    {
        [Required(ErrorMessage = "Product name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Product name must contain between 2 and 100 characters.")]
        public string Name { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Product description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Range(0.01, 1000000, ErrorMessage = "Price must be positive and less than 1000000.")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
        public int Stock { get; set; }
    }
}
