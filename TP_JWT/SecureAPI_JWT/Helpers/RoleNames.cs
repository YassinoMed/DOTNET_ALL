namespace SecureAPI_JWT.Helpers;

/// <summary>
/// Noms des rôles applicatifs. Ils sont créés de façon idempotente au démarrage.
/// </summary>
public static class RoleNames
{
    public const string User = "User";
    public const string Admin = "Admin";

    /// <summary>
    /// Rôles autorisés par l'API. Toute autre valeur est refusée par l'endpoint d'attribution.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        User,
        Admin
    };
}
