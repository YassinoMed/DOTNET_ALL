using System.Text.Json;
using Xunit;

namespace MicroservicesTP.Tests;

/// <summary>
/// Vérifie que Ocelot.json contient les quatre routes attendues, les ports réels
/// des microservices et toutes les méthodes HTTP du CRUD.
/// </summary>
public class OcelotConfigurationTests
{
    private static readonly JsonElement Routes = LoadRoutes();

    private static JsonElement LoadRoutes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Ocelot.json");
        Assert.True(File.Exists(path), $"Ocelot.json introuvable : {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("Routes").Clone();
    }

    [Fact]
    public void Ocelot_contient_quatre_routes()
    {
        Assert.Equal(4, Routes.GetArrayLength());
    }

    [Theory]
    [InlineData("/apigateway/Products", 7066, "http")]
    [InlineData("/apigateway/Products/{id}", 7066, "http")]
    [InlineData("/apigateway/Orders", 7065, "http")]
    [InlineData("/apigateway/Orders/{id}", 7065, "http")]
    public void Chaque_route_cible_le_bon_port_en_http(string upstream, int port, string scheme)
    {
        var route = FindRoute(upstream);

        Assert.Equal(scheme, route.GetProperty("DownstreamScheme").GetString());
        Assert.Equal(port, route.GetProperty("DownstreamHostAndPorts")[0].GetProperty("Port").GetInt32());
    }

    [Theory]
    [InlineData("/apigateway/Products", "GET")]
    [InlineData("/apigateway/Products", "POST")]
    [InlineData("/apigateway/Products/{id}", "GET")]
    [InlineData("/apigateway/Products/{id}", "PUT")]
    [InlineData("/apigateway/Products/{id}", "DELETE")]
    [InlineData("/apigateway/Orders", "GET")]
    [InlineData("/apigateway/Orders", "POST")]
    [InlineData("/apigateway/Orders/{id}", "GET")]
    [InlineData("/apigateway/Orders/{id}", "PUT")]
    [InlineData("/apigateway/Orders/{id}", "DELETE")]
    public void Chaque_route_expose_la_methode_attendue(string upstream, string method)
    {
        var route = FindRoute(upstream);

        var methods = route.GetProperty("UpstreamHttpMethod")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => value is not null)
            .Select(value => value!.ToUpperInvariant())
            .ToList();

        Assert.Contains(method, methods);
    }

    private static JsonElement FindRoute(string upstreamPathTemplate)
    {
        foreach (var route in Routes.EnumerateArray())
        {
            if (route.GetProperty("UpstreamPathTemplate").GetString() == upstreamPathTemplate)
            {
                return route;
            }
        }

        throw new Xunit.Sdk.XunitException($"Route Ocelot absente : {upstreamPathTemplate}");
    }
}
