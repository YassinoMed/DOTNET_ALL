namespace SecureAPI_JWT.Helpers;

/// <summary>
/// Configuration JWT (clé de développement uniquement — jamais de clé de production en dur).
/// Les valeurs sont lues depuis appsettings.json et surchargeables par les variables
/// d'environnement / User Secrets.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    /// <summary>
    /// Clé de signature HMAC SHA-256. Doit faire au moins 256 bits (32 caractères).
    /// Valeur de développement à remplacer en production par un secret externe.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int DurationInDays { get; set; } = 1;

    /// <summary>Durée de vie des refresh tokens (7 jours par défaut).</summary>
    public int RefreshTokenDurationInDays { get; set; } = 7;
}
