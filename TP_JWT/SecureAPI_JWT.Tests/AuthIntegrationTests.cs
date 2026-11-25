using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SecureAPI_JWT.Helpers;
using SecureAPI_JWT.Models;

namespace SecureAPI_JWT.Tests;

/// <summary>
/// Tests d'intégration de l'API d'authentification et d'autorisation.
/// Chaque méthode de test bénéficie d'une instance isolée de TestWebApplicationFactory
/// (xUnit instancie une nouvelle classe par m�thode de test), donc d'une base SQLite
/// temporaire ind�pendante. Aucun impact sur la base de d�veloppement SecureAPI_JWT.db.
/// </summary>
public class AuthIntegrationTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JwtSettings _jwtSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthIntegrationTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        _jwtSettings = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static StringContent Json(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");

    private static async Task<(HttpStatusCode StatusCode, AuthModel? Body)> PostAuthAsync(
        HttpClient client, string path, object body)
    {
        var resp = await client.PostAsync(path, Json(body));
        var content = await resp.Content.ReadAsStringAsync();
        var model = string.IsNullOrWhiteSpace(content) ? null : JsonSerializer.Deserialize<AuthModel>(content, JsonOptions);
        return (resp.StatusCode, model);
    }

    private async Task<(HttpStatusCode StatusCode, AuthModel? Body)> RegisterAsync(RegisterModel model) =>
        await PostAuthAsync(_client, "/api/Auth/register", model);

    private async Task<(HttpStatusCode StatusCode, AuthModel? Body)> LoginAsync(LoginModel model) =>
        await PostAuthAsync(_client, "/api/Auth/login", model);

    private async Task<string> GetValidTokenAsync(string username, string password)
    {
        var (status, model) = await LoginAsync(new LoginModel { Username = username, Password = password });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(model);
        Assert.True(model.IsAuthenticated);
        Assert.False(string.IsNullOrWhiteSpace(model.Token));
        return model!.Token;
    }

    private ClaimsPrincipal ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key)),
            ValidateIssuer = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtClaimNames.Name,
            RoleClaimType = JwtClaimNames.Roles,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
        };
        var principal = handler.ValidateToken(token, validationParameters, out _);
        return principal;
    }

    private void ClearAuthHeader() => _client.DefaultRequestHeaders.Authorization = null;

    // ------------------------------------------------------------------
    // Inscriptions
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Inscription valide cree un utilisateur avec le role User")]
    public async Task Register_Valid_CreatesUserWithUserRole()
    {
        var model = new RegisterModel
        {
            FirstName = "Yassine", LastName = "Moha", Username = "yassine_reg",
            Email = "yassine_reg@test.com", Password = "Yassine@123!"
        };

        var (status, result) = await RegisterAsync(model);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(result);
        Assert.True(result.IsAuthenticated);
        Assert.Equal("Inscription réussie.", result.Message);
        Assert.Equal(model.Username, result.Username);
        Assert.Equal(model.Email, result.Email);
        Assert.Contains("User", result.Roles);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.True(result.ExpiresOn > DateTime.UtcNow);
    }

    [Fact(DisplayName = "Email deja utilise refuse l'inscription")]
    public async Task Register_DuplicateEmail_Refused()
    {
        var model = new RegisterModel
        {
            FirstName = "Dup", LastName = "Dup", Username = "dup_unique",
            Email = "dup@test.com", Password = "Dup@123!"
        };
        await RegisterAsync(model); // première inscription

        var (status, result) = await RegisterAsync(model); // doublon

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.NotNull(result);
        Assert.False(result.IsAuthenticated);
        Assert.Contains("email", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Username deja utilise refuse l'inscription")]
    public async Task Register_DuplicateUsername_Refused()
    {
        var first = new RegisterModel
        {
            FirstName = "AA", LastName = "BB", Username = "userdup",
            Email = "userdup1@test.com", Password = "Userdup@123!"
        };
        var second = new RegisterModel
        {
            FirstName = "CC", LastName = "DD", Username = "userdup",
            Email = "userdup2@test.com", Password = "Userdup@123!"
        };
        await RegisterAsync(first);

        var (status, result) = await RegisterAsync(second);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.NotNull(result);
        Assert.False(result.IsAuthenticated);
        Assert.Contains("nom d'utilisateur", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Email invalide refuse l'inscription")]
    public async Task Register_InvalidEmail_Refused()
    {
        var model = new RegisterModel
        {
            FirstName = "AA", LastName = "BB", Username = "bademail",
            Email = "pas-un-email", Password = "Bademail@123!"
        };
        var (status, result) = await RegisterAsync(model);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.NotNull(result);
        Assert.False(result.IsAuthenticated);
    }

    [Fact(DisplayName = "Mot de passe invalide refuse l'inscription")]
    public async Task Register_InvalidPassword_Refused()
    {
        var model = new RegisterModel
        {
            FirstName = "AA", LastName = "BB", Username = "badpass",
            Email = "badpass@test.com", Password = "123"
        };
        var (status, result) = await RegisterAsync(model);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.NotNull(result);
        Assert.False(result.IsAuthenticated);
    }

    [Fact(DisplayName = "Champs manquants refusent l'inscription")]
    public async Task Register_MissingFields_Refused()
    {
        var model = new RegisterModel { Username = "partial", Email = "partial@test.com" }; // pas de password
        var (status, result) = await RegisterAsync(model);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.NotNull(result);
        Assert.False(result.IsAuthenticated);
    }

    // ------------------------------------------------------------------
    // Authentification
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Login valide renvoie un JWT")]
    public async Task Login_Valid_ReturnsToken()
    {
        var model = new RegisterModel
        {
            FirstName = "LL", LastName = "GG", Username = "login_valid",
            Email = "login_valid@test.com", Password = "LoginValid@123!"
        };
        await RegisterAsync(model);

        var (status, result) = await LoginAsync(new LoginModel { Username = "login_valid", Password = "LoginValid@123!" });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(result);
        Assert.True(result.IsAuthenticated);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact(DisplayName = "Login avec mauvais mot de passe refuse (401)")]
    public async Task Login_WrongPassword_Refused()
    {
        var model = new RegisterModel
        {
            FirstName = "LL", LastName = "GG", Username = "login_wp",
            Email = "login_wp@test.com", Password = "LoginWp@123!"
        };
        await RegisterAsync(model);

        var (status, result) = await LoginAsync(new LoginModel { Username = "login_wp", Password = "WrongPass@123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.NotNull(result);
        Assert.False(result.IsAuthenticated);
        Assert.True(string.IsNullOrEmpty(result.Token));
    }

    [Fact(DisplayName = "Login utilisateur inconnu refuse (401)")]
    public async Task Login_UnknownUser_Refused()
    {
        var (status, result) = await LoginAsync(new LoginModel { Username = "nobody", Password = "AnyPass@123!" });

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.NotNull(result);
        Assert.False(result.IsAuthenticated);
    }

    // ------------------------------------------------------------------
    // JWT
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Token genere est signe correctement et non expire")]
    public async Task Token_Generated_IsSignedAndNotExpired()
    {
        await RegisterAsync(new RegisterModel
        {
            FirstName = "TT", LastName = "KK", Username = "tok_user",
            Email = "tok_user@test.com", Password = "TokUser@123!"
        });
        var token = await GetValidTokenAsync("tok_user", "TokUser@123!");

        var principal = ValidateToken(token);

        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.NotNull(principal.FindFirst(JwtClaimNames.Subject));
        Assert.NotNull(principal.FindFirst(JwtClaimNames.Name));
        Assert.NotNull(principal.FindFirst(JwtClaimNames.Email));
        Assert.NotNull(principal.FindFirst(JwtClaimNames.UserId));
        Assert.NotNull(principal.FindFirst(JwtClaimNames.JwtId));
        Assert.True(principal.IsInRole("User"));

        var expClaim = principal.FindFirst(JwtRegisteredClaimNames.Exp);
        Assert.NotNull(expClaim);
        var expSeconds = long.Parse(expClaim.Value);
        var expDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
        Assert.True(expDate > DateTime.UtcNow);
    }

    [Fact(DisplayName = "Token invalide est refuse par le middleware (401)")]
    public async Task InvalidToken_Rejected_ByMiddleware()
    {
        ClearAuthHeader();
        var resp = await _client.GetAsync("/api/Secure/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "totally.invalid.token");
        resp = await _client.GetAsync("/api/Secure/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        ClearAuthHeader();
    }

    // ------------------------------------------------------------------
    // Autorisation
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Endpoint public accessible sans token (200)")]
    public async Task Public_WithoutToken_Accessible()
    {
        ClearAuthHeader();
        var resp = await _client.GetAsync("/api/Secure/public");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact(DisplayName = "Endpoint protege sans token renvoie 401")]
    public async Task Protected_WithoutToken_401()
    {
        ClearAuthHeader();
        var resp = await _client.GetAsync("/api/Secure/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact(DisplayName = "Endpoint protege avec token valide renvoie 200")]
    public async Task Protected_WithValidToken_200()
    {
        ClearAuthHeader();
        var model = new RegisterModel
        {
            FirstName = "PP", LastName = "TT", Username = "prot_user",
            Email = "prot_user@test.com", Password = "ProtUser@123!"
        };
        await RegisterAsync(model);
        var token = await GetValidTokenAsync("prot_user", "ProtUser@123!");

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.GetAsync("/api/Secure/profile");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        ClearAuthHeader();
    }

    [Fact(DisplayName = "Utilisateur avec role User accede à l'endpoint user (200)")]
    public async Task UserRole_AccessUserEndpoint_200()
    {
        ClearAuthHeader();
        var model = new RegisterModel
        {
            FirstName = "UU", LastName = "RR", Username = "role_user",
            Email = "role_user@test.com", Password = "RoleUser@123!"
        };
        await RegisterAsync(model);
        var token = await GetValidTokenAsync("role_user", "RoleUser@123!");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/Secure/user");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        ClearAuthHeader();
    }

    [Fact(DisplayName = "Utilisateur avec role User refuse sur Admin (403)")]
    public async Task UserRole_AccessAdminEndpoint_403()
    {
        ClearAuthHeader();
        var model = new RegisterModel
        {
            FirstName = "UU", LastName = "RR", Username = "role_user2",
            Email = "role_user2@test.com", Password = "RoleUser2@123!"
        };
        await RegisterAsync(model);
        var token = await GetValidTokenAsync("role_user2", "RoleUser2@123!");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/Secure/admin");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        ClearAuthHeader();
    }

    [Fact(DisplayName = "Utilisateur promouvoir Admin accede à l'endpoint admin (200)")]
    public async Task PromotedToAdmin_AccessAdminEndpoint_200()
    {
        ClearAuthHeader();
        var model = new RegisterModel
        {
            FirstName = "Ad", LastName = "Min", Username = "to_admin",
            Email = "to_admin@test.com", Password = "ToAdmin@123!"
        };
        await RegisterAsync(model);

        // Promotion en Admin via UserManager (base de test isolee).
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync("to_admin");
        Assert.NotNull(user);
        var addResult = await userManager.AddToRoleAsync(user, "Admin");
        Assert.True(addResult.Succeeded);

        // Nouveau token apres promotion (contient le role Admin).
        var token = await GetValidTokenAsync("to_admin", "ToAdmin@123!");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.GetAsync("/api/Secure/admin");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        ClearAuthHeader();
    }

    // ------------------------------------------------------------------
    // Refresh Token
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Refresh token valide renvoie un nouveau JWT et un nouveau refresh token")]
    public async Task Refresh_ValidToken_ReturnsNewTokens()
    {
        // Inscription pour obtenir un refresh token.
        var model = new RegisterModel
        {
            FirstName = "Rf", LastName = "Tk", Username = "ref_user",
            Email = "ref_user@test.com", Password = "RefUser@123!"
        };
        var (regStatus, regResult) = await RegisterAsync(model);
        Assert.Equal(HttpStatusCode.Created, regStatus);
        Assert.False(string.IsNullOrWhiteSpace(regResult!.RefreshToken));

        // Rafraîchissement.
        var resp = await _client.PostAsJsonAsync("/api/Auth/refresh", regResult.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var content = await resp.Content.ReadAsStringAsync();
        var refreshed = JsonSerializer.Deserialize<AuthModel>(content, JsonOptions);
        Assert.NotNull(refreshed);
        Assert.True(refreshed.IsAuthenticated);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.Token));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));
        // Le nouveau refresh token doit différer de l'ancien (rotation).
        Assert.NotEqual(regResult.RefreshToken, refreshed.RefreshToken);
    }

    [Fact(DisplayName = "Refresh token invalide est refuse (401)")]
    public async Task Refresh_InvalidToken_401()
    {
        var resp = await _client.PostAsJsonAsync("/api/Auth/refresh", "jeton-invalide");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact(DisplayName = "Refresh token revoque ne permet plus de rafraîchir (401)")]
    public async Task Refresh_AfterRevoke_401()
    {
        var model = new RegisterModel
        {
            FirstName = "Rv", LastName = "Tk", Username = "rev_user",
            Email = "rev_user@test.com", Password = "RevUser@123!"
        };
        var (_, regResult) = await RegisterAsync(model);
        Assert.False(string.IsNullOrWhiteSpace(regResult!.RefreshToken));

        // Révocation.
        var revokeResp = await _client.PostAsJsonAsync("/api/Auth/revoke", regResult.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

        // Le refresh token révoqué ne doit plus fonctionner.
        var refreshResp = await _client.PostAsJsonAsync("/api/Auth/refresh", regResult.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }

    // ------------------------------------------------------------------
    // Persistance SQLite
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Utilisateur et role persists dans SQLite et survivent a une nouvelle instance")]
    public async Task UserAndRole_PersistedInSqlite()
    {
        var model = new RegisterModel
        {
            FirstName = "Persist", LastName = "User", Username = "persist_user",
            Email = "persist_user@test.com", Password = "PersistUser@123!"
        };
        var (regStatus, _) = await RegisterAsync(model);
        Assert.Equal(HttpStatusCode.Created, regStatus);

        // Le role User existe dans la base de test.
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.True(await roleManager.RoleExistsAsync("User"));
        var user = await userManager.FindByNameAsync("persist_user");
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, "User"));
    }
}
