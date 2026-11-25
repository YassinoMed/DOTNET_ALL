namespace SecureAPI_JWT.Helpers;

/// <summary>
/// Noms des claims JWT utilisés par l'API.
/// Ces noms sont volontairement explicites (revendications courtes standard) afin
/// d'éviter toute dépendance au mapping automatique de Microsoft.IdentityModel.
/// </summary>
public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string JwtId = "jti";
    public const string Name = "name";
    public const string Email = "email";
    public const string UserId = "uid";
    public const string Roles = "roles";
}
