namespace SecureAPI_JWT.Models;

/// <summary>
/// Jeton de rafraîchissement (refresh token) associé à un utilisateur.
/// Permet d'obtenir un nouveau JWT sans saisir à nouveau les identifiants,
/// et d'être révoqué en cas de compromission.
/// </summary>
public class RefreshToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresOn { get; set; }
    public DateTime? RevokedOn { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
    public bool IsActive => RevokedOn is null && !IsExpired;
}
