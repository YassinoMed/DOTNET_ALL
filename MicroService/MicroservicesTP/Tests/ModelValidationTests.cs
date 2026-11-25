using System.ComponentModel.DataAnnotations;
using OrdersAPI.Models;
using Xunit;
using ProductModel = ProductsAPI.Models.Product;
using OrderModel = OrdersAPI.Models.Orders;

namespace MicroservicesTP.Tests;

/// <summary>
/// Vérifie les Data Annotations des modèles. Ces tests valident les règles
/// déclaratives ; le rejet HTTP 400 par le pipeline [ApiController] est vérifié
/// par le script E2E (appels HTTP réels).
/// </summary>
public class ModelValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Product_valide_ne_produit_aucune_erreur()
    {
        var product = new ProductModel { Name = "Clavier", StockQte = 10, Price = 49.9 };

        Assert.Empty(Validate(product));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Product_refuse_un_nom_vide(string name)
    {
        var product = new ProductModel { Name = name, StockQte = 1, Price = 1 };

        Assert.NotEmpty(Validate(product));
    }

    [Fact]
    public void Product_refuse_un_stock_negatif()
    {
        var product = new ProductModel { Name = "Clavier", StockQte = -1, Price = 1 };

        Assert.NotEmpty(Validate(product));
    }

    [Fact]
    public void Product_refuse_un_prix_negatif()
    {
        var product = new ProductModel { Name = "Clavier", StockQte = 1, Price = -0.01 };

        Assert.NotEmpty(Validate(product));
    }

    [Fact]
    public void Product_refuse_un_nom_de_plus_de_100_caracteres()
    {
        var product = new ProductModel { Name = new string('a', 101), StockQte = 1, Price = 1 };

        Assert.NotEmpty(Validate(product));
    }

    [Fact]
    public void Orders_valide_ne_produit_aucune_erreur()
    {
        var order = new OrderModel { OrderDate = DateTime.UtcNow, CustomerName = "Alice", ProductId = 1 };

        Assert.Empty(Validate(order));
    }

    [Fact]
    public void Orders_refuse_un_product_id_invalide()
    {
        var order = new OrderModel { OrderDate = DateTime.UtcNow, ProductId = 0 };

        Assert.NotEmpty(Validate(order));
    }

    [Fact]
    public void Orders_accepte_un_customer_name_null()
    {
        var order = new OrderModel { OrderDate = DateTime.UtcNow, CustomerName = null, ProductId = 1 };

        Assert.Empty(Validate(order));
    }

    [Fact]
    public void Orders_refuse_un_customer_name_de_plus_de_100_caracteres()
    {
        var order = new OrderModel
        {
            OrderDate = DateTime.UtcNow,
            CustomerName = new string('a', 101),
            ProductId = 1
        };

        Assert.NotEmpty(Validate(order));
    }
}
