using ApplicationLayer.DTOs;

namespace ApplicationLayer.Interfaces.Services
{
    /// <summary>
    /// Contrat des cas d'utilisation liés aux produits.
    /// </summary>
    public interface IProductService
    {
        Task<IEnumerable<ProductDTO>> GetAllProductsAsync();
        Task<ProductDTO?> GetProductByIdAsync(int id);
        Task<ProductDTO?> AddProductAsync(CreateProductDTO productDto);
        Task UpdateProductAsync(UpdateProductDTO productDto);
        Task DeleteProductAsync(int id);
    }
}
