using ApplicationLayer.DTOs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Interfaces.Services;
using DomainLayer.Entities;

namespace ApplicationLayer.Services
{
    /// <summary>
    /// Implémentation des cas d'utilisation produits.
    /// Le mapping est manuel, conformément au TP (pas d'AutoMapper).
    /// </summary>
    public class ProductService : IProductService
    {
        private readonly IProductRepository _repository;

        public ProductService(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<ProductDTO>> GetAllProductsAsync()
        {
            var products = await _repository.GetAllAsync();
            return products.Select(MapToDTO).ToList();
        }

        public async Task<ProductDTO?> GetProductByIdAsync(int id)
        {
            var product = await _repository.GetByIdAsync(id);
            return product is null ? null : MapToDTO(product);
        }

        public async Task<ProductDTO?> AddProductAsync(CreateProductDTO productDto)
        {
            var product = new Product
            {
                Name = productDto.Name.Trim(),
                Description = productDto.Description?.Trim(),
                Price = productDto.Price,
                Stock = productDto.Stock
            };

            product.ValidateBusinessRules();

            var created = await _repository.AddAsync(product);
            return MapToDTO(created);
        }

        public async Task UpdateProductAsync(UpdateProductDTO productDto)
        {
            var product = await _repository.GetByIdAsync(productDto.Id);

            if (product is null)
                throw new KeyNotFoundException($"Product with id {productDto.Id} was not found.");

            product.Name = productDto.Name.Trim();
            product.Description = productDto.Description?.Trim();
            product.Price = productDto.Price;
            product.Stock = productDto.Stock;

            product.ValidateBusinessRules();

            await _repository.UpdateAsync(product);
        }

        public async Task DeleteProductAsync(int id)
        {
            var product = await _repository.GetByIdAsync(id);

            if (product is null)
                throw new KeyNotFoundException($"Product with id {id} was not found.");

            await _repository.DeleteAsync(id);
        }

        private static ProductDTO MapToDTO(Product product)
        {
            return new ProductDTO
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock
            };
        }
    }
}
