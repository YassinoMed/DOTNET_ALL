using ApplicationLayer.DTOs;
using ApplicationLayer.Interfaces.Repositories;
using ApplicationLayer.Services;
using DomainLayer.Entities;

namespace ProductsAPI.Tests.Application;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAllProductsAsync_ReturnsAllProductsMappedToDto()
    {
        var repository = new FakeProductRepository(
            new Product { Id = 1, Name = "Laptop", Price = 1500m, Stock = 10 },
            new Product { Id = 2, Name = "Smartphone", Price = 800m, Stock = 25 });
        var service = new ProductService(repository);

        var products = (await service.GetAllProductsAsync()).ToList();

        Assert.Equal(2, products.Count);
        Assert.Equal("Laptop", products[0].Name);
        Assert.Equal(1500m, products[0].Price);
    }

    [Fact]
    public async Task GetProductByIdAsync_WithExistingId_ReturnsProduct()
    {
        var repository = new FakeProductRepository(
            new Product { Id = 1, Name = "Laptop", Price = 1500m, Stock = 10 });
        var service = new ProductService(repository);

        var product = await service.GetProductByIdAsync(1);

        Assert.NotNull(product);
        Assert.Equal("Laptop", product!.Name);
    }

    [Fact]
    public async Task GetProductByIdAsync_WithUnknownId_ReturnsNull()
    {
        var service = new ProductService(new FakeProductRepository());

        var product = await service.GetProductByIdAsync(999);

        Assert.Null(product);
    }

    [Fact]
    public async Task AddProductAsync_WithValidData_PersistsAndReturnsDto()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        var created = await service.AddProductAsync(new CreateProductDTO
        {
            Name = "Keyboard",
            Description = "Mechanical keyboard",
            Price = 120.50m,
            Stock = 5
        });

        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
        Assert.Equal("Keyboard", created.Name);
        Assert.Single(repository.Products);
    }

    [Fact]
    public async Task AddProductAsync_WithInvalidPrice_ThrowsAndDoesNotPersist()
    {
        var repository = new FakeProductRepository();
        var service = new ProductService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddProductAsync(new CreateProductDTO
            {
                Name = "Keyboard",
                Price = 0m,
                Stock = 5
            }));

        Assert.Empty(repository.Products);
    }

    [Fact]
    public async Task UpdateProductAsync_WithExistingProduct_UpdatesValues()
    {
        var repository = new FakeProductRepository(
            new Product { Id = 1, Name = "Laptop", Price = 1500m, Stock = 10 });
        var service = new ProductService(repository);

        await service.UpdateProductAsync(new UpdateProductDTO
        {
            Id = 1,
            Name = "Laptop Pro",
            Description = "Updated",
            Price = 1800m,
            Stock = 7
        });

        var updated = repository.Products.Single();
        Assert.Equal("Laptop Pro", updated.Name);
        Assert.Equal(1800m, updated.Price);
        Assert.Equal(7, updated.Stock);
    }

    [Fact]
    public async Task UpdateProductAsync_WithUnknownId_ThrowsKeyNotFoundException()
    {
        var service = new ProductService(new FakeProductRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateProductAsync(new UpdateProductDTO
            {
                Id = 999,
                Name = "Ghost",
                Price = 10m,
                Stock = 1
            }));
    }

    [Fact]
    public async Task DeleteProductAsync_WithExistingProduct_RemovesIt()
    {
        var repository = new FakeProductRepository(
            new Product { Id = 1, Name = "Laptop", Price = 1500m, Stock = 10 });
        var service = new ProductService(repository);

        await service.DeleteProductAsync(1);

        Assert.Empty(repository.Products);
    }

    [Fact]
    public async Task DeleteProductAsync_WithUnknownId_ThrowsKeyNotFoundException()
    {
        var service = new ProductService(new FakeProductRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteProductAsync(999));
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private int _nextId;

        public FakeProductRepository(params Product[] products)
        {
            Products = products.ToList();
            _nextId = Products.Count == 0 ? 1 : Products.Max(p => p.Id) + 1;
        }

        public List<Product> Products { get; }

        public Task<IEnumerable<Product>> GetAllAsync() =>
            Task.FromResult<IEnumerable<Product>>(Products.ToList());

        public Task<Product?> GetByIdAsync(int id) =>
            Task.FromResult(Products.FirstOrDefault(p => p.Id == id));

        public Task<Product> AddAsync(Product product)
        {
            product.Id = _nextId++;
            Products.Add(product);
            return Task.FromResult(product);
        }

        public Task UpdateAsync(Product product)
        {
            var index = Products.FindIndex(p => p.Id == product.Id);
            if (index >= 0)
                Products[index] = product;

            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id)
        {
            Products.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }
    }
}
