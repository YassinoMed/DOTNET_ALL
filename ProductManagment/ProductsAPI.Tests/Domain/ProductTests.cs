using DomainLayer.Entities;

namespace ProductsAPI.Tests.Domain;

public class ProductTests
{
    [Fact]
    public void ValidateBusinessRules_WithValidProduct_DoesNotThrow()
    {
        var product = new Product
        {
            Name = "Laptop",
            Description = "High-end gaming laptop",
            Price = 1500.00m,
            Stock = 10
        };

        var exception = Record.Exception(() => product.ValidateBusinessRules());

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateBusinessRules_WithZeroPrice_ThrowsInvalidOperationException()
    {
        var product = CreateValidProduct();
        product.Price = 0m;

        var exception = Assert.Throws<InvalidOperationException>(() => product.ValidateBusinessRules());
        Assert.Contains("price", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateBusinessRules_WithNegativePrice_ThrowsInvalidOperationException()
    {
        var product = CreateValidProduct();
        product.Price = -1m;

        Assert.Throws<InvalidOperationException>(() => product.ValidateBusinessRules());
    }

    [Fact]
    public void ValidateBusinessRules_WithNegativeStock_ThrowsInvalidOperationException()
    {
        var product = CreateValidProduct();
        product.Stock = -1;

        var exception = Assert.Throws<InvalidOperationException>(() => product.ValidateBusinessRules());
        Assert.Contains("stock", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void ValidateBusinessRules_WithInvalidName_ThrowsInvalidOperationException(string name)
    {
        var product = CreateValidProduct();
        product.Name = name;

        Assert.Throws<InvalidOperationException>(() => product.ValidateBusinessRules());
    }

    [Fact]
    public void Properties_AreAssignedAndReadCorrectly()
    {
        var product = new Product
        {
            Id = 7,
            Name = "Keyboard",
            Description = "Mechanical keyboard",
            Price = 120.50m,
            Stock = 5
        };

        Assert.Equal(7, product.Id);
        Assert.Equal("Keyboard", product.Name);
        Assert.Equal("Mechanical keyboard", product.Description);
        Assert.Equal(120.50m, product.Price);
        Assert.Equal(5, product.Stock);
    }

    private static Product CreateValidProduct() => new()
    {
        Name = "Laptop",
        Description = "High-end gaming laptop",
        Price = 1500.00m,
        Stock = 10
    };
}
