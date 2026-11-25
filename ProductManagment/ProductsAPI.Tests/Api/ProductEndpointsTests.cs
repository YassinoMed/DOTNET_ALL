using System.Net;
using System.Net.Http.Json;
using ApplicationLayer.DTOs;

namespace ProductsAPI.Tests.Api;

[Collection("Api")]
public class ProductEndpointsTests
{
    [Fact]
    public async Task GetProducts_ReturnsThreeSeededProducts()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/Product");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductDTO>>();
        Assert.NotNull(products);
        Assert.Equal(3, products!.Count);
        Assert.Equal(new[] { "Laptop", "Smartphone", "Wireless Headphones" }, products.Select(p => p.Name));
        Assert.Equal(new[] { 1500.00m, 800.00m, 200.00m }, products.Select(p => p.Price));
        Assert.Equal(new[] { 10, 25, 40 }, products.Select(p => p.Stock));
    }

    [Fact]
    public async Task GetProductById_WithSeededId_ReturnsLaptop()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var product = await client.GetFromJsonAsync<ProductDTO>("/api/Product/1");

        Assert.NotNull(product);
        Assert.Equal(1, product!.Id);
        Assert.Equal("Laptop", product.Name);
        Assert.Equal(1500.00m, product.Price);
    }

    [Fact]
    public async Task GetProductById_WithUnknownId_Returns404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/Product/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_WithValidData_Returns201AndPersists()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Product", new CreateProductDTO
        {
            Name = "Mechanical Keyboard",
            Description = "RGB keyboard",
            Price = 149.99m,
            Stock = 12
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<ProductDTO>();
        Assert.NotNull(created);
        Assert.True(created!.Id > 3);

        var all = await client.GetFromJsonAsync<List<ProductDTO>>("/api/Product");
        Assert.NotNull(all);
        Assert.Contains(all!, p => p.Id == created.Id && p.Name == "Mechanical Keyboard");
    }

    [Fact]
    public async Task PostProduct_WithZeroPrice_Returns400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Product", new CreateProductDTO
        {
            Name = "Free Product",
            Price = 0m,
            Stock = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_WithNegativePrice_Returns400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Product", new CreateProductDTO
        {
            Name = "Invalid Product",
            Price = -50m,
            Stock = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutProduct_WithExistingId_Returns204AndPersists()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/Product/1", new UpdateProductDTO
        {
            Id = 1,
            Name = "Laptop Pro",
            Description = "Updated laptop",
            Price = 1800m,
            Stock = 8
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await client.GetFromJsonAsync<ProductDTO>("/api/Product/1");
        Assert.NotNull(updated);
        Assert.Equal("Laptop Pro", updated!.Name);
        Assert.Equal(1800m, updated.Price);
        Assert.Equal(8, updated.Stock);
    }

    [Fact]
    public async Task PutProduct_WithUnknownId_Returns404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/Product/99999", new UpdateProductDTO
        {
            Id = 99999,
            Name = "Ghost",
            Price = 10m,
            Stock = 1
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutProduct_WithMismatchedBodyId_Returns400()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/Product/1", new UpdateProductDTO
        {
            Id = 2,
            Name = "Mismatch",
            Price = 10m,
            Stock = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_WithExistingId_Returns204AndRemovesProduct()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/Product/3");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync("/api/Product/3");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_WithUnknownId_Returns404()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/Product/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocument_IsAvailableAndContainsProductRoutes()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/Product", content);
        Assert.Contains("/api/Product/{id}", content);
    }
}
