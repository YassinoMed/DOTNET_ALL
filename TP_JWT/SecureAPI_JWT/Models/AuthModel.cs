namespace SecureAPI_JWT.Models;

/// <summary>
/// Modèle de retour d'authentification.
/// Aucun champ sensible (mot de passe, hash, sel, informations internes Identity) n'est retourné.
/// </summary>
public class AuthModel
{
    public string Message { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresOn { get; set; }
}
