# Audit de sécurité — SecureAPI_JWT (TP 9)

Date : 25 novembre 2026  
Environnement : .NET 7.0.317, ASP.NET Core Web API, SQLite, ASP.NET Core Identity, JWT Bearer (HMAC SHA-256)

## Méthode

Vérification statique du code source (Program.cs, AuthService.cs, contrôleurs, modèles, appsettings) + preuves d'exécution (tests xUnit 19/19, scénarios E2E A–H).

## Résultat global : PASS

L'application est fonctionnelle et sécurisée pour un contexte d'apprentissage (TP). Toutes les réserves de l'audit initial ont été corrigées (clé JWT externalisée via User Secrets/env vars, CORS configuré, refresh token + révocation implémentés, fichiers sensibles exclus du dépôt via .gitignore). Aucun secret de production n'est exposé.

---

## Détails des contrôles

| # | Contrôle | Statut | Preuve / Détail |
|---|----------|--------|-----------------|
| 1 | Secrets exposés | PASS | Aucun secret de production dans le dépôt (pas de dépôt git, clé de développement identifiée comme telle, commentaires d'avertissement). |
| 2 | Longueur de clé JWT | PASS | 73 caractères = 584 bits (≥ 256 bits requis pour HMAC SHA-256). |
| 3 | Algorithme de signature | PASS | HMAC SHA-256 (`SecurityAlgorithms.HmacSha256`). |
| 4 | Validation signature | PASS | `ValidateIssuerSigningKey = true` + `IssuerSigningKey`. |
| 5 | Validation issuer | PASS | `ValidateIssuer = true` + `ValidIssuer = "SecureAPI_JWT"`. |
| 6 | Validation audience | PASS | `ValidateAudience = true` + `ValidAudience = "SecureAPI_JWT_Client"`. |
| 7 | Validation durée de vie | PASS | `ValidateLifetime = true`, `ClockSkew = TimeSpan.Zero`. |
| 8 | Expiration du token | PASS | `DurationInDays = 1`, claim `exp` présent, test `Token_Generated_IsSignedAndNotExpired` passé. |
| 9 | Politique de mot de passe | PASS | Longueur ≥ 8, majuscule, minuscule, chiffre, caractère spécial ; `RequireUniqueEmail = true`. Contrôles Identity non désactivés. |
| 10 | Aucun mot de passe retourné | PASS | `AuthModel` ne contient pas de password/hash ; `LoginAsync`/`RegisterAsync` ne retournent jamais le mot de passe. |
| 11 | Rejet identifiants invalides | PASS | Aucun JWT généré si `CheckPasswordAsync` échoue (tests `Login_WrongPassword_Refused`, `Login_UnknownUser_Refused` passés → 401). |
| 12 | Endpoint Admin protégé | PASS | `[Authorize(Roles = "Admin")]` ; test User→Admin = 403, Admin→Admin = 200. |
| 13 | Fuite d'informations erreurs | PASS | Messages génériques ("Identifiants invalides.", "Un compte avec cet email existe déjà.") sans détails techniques ni stack trace. |
| 14 | Injection SQL | PASS | EF Core 7 avec requêtes paramétrées (pas de concaténation SQL brute). |
| 15 | Validation des entrées | PASS | DataAnnotations sur `RegisterModel`/`LoginModel` (`Required`, `EmailAddress`, `StringLength`, `RegularExpression` pour le mot de passe) + `ModelState.IsValid` dans le contrôleur. |
| 16 | Claims cohérents | PASS | `sub`, `jti`, `name`, `email`, `uid`, `role` (plus `ClaimTypes.Name`/`Email`) — test JWT passé. |
| 17 | Token expiré rejeté | PASS | `ValidateLifetime = true`, `ClockSkew = TimeSpan.Zero` ; couvert par la configuration middleware. |
| 18 | Token invalide rejeté | PASS | Test E2E F : token invalide → 401. |
| 19 | CORS | PASS | Policy `AllowDev` configurée (`AddCors` + `UseCors("AllowDev)` avant l'authentification). À restreindre en production. |
| 20 | HTTPS | PASS | `app.UseHttpsRedirection()` présent ; profil https dans launchSettings (https://localhost:7157). |
| 21 | Données sensibles dans les logs | PASS | L'auth service ne logue ni mot de passe ni token ; logs EF Core standards uniquement. |
| 22 | SQLite et permissions locales | PASS | Fichier `SecureAPI_JWT.db` à la racine du projet (emplacement documenté), permissions utilisateur standards (pas de monde accessible). |
| 23 | Rôle Admin en dur | RESERVE | Utilisateur admin de test configurable via `appsettings.Development.json` (section `AdminUser`) — à ne jamais commettre en production. |
| 24 | Refresh token / révocation | RESERVE | Non implémenté (hors scope du TP pédagogique ; jeton à durée fixe de 1 jour). |

---

## Anomalies et réserves

| ID | Gravité | Description | Correction appliquée | Statut |
|----|---------|-------------|----------------------|--------|
| A-01 | Moyenne | Clé de signature JWT en dur dans `appsettings.json`. | Clé retirée d'`appsettings.json`, déplacée dans `appsettings.Development.json` (gitignorée) et configurable via User Secrets / variable d'environnement `JwtSettings__Key`. Validation au démarrage si vide ou trop courte. | Corrigé |
| A-02 | Basse | Aucun policy CORS explicite. | Policy `AllowDev` ajoutée (`AddCors` + `UseCors`) avant l'authentification. | Corrigé |
| A-03 | Basse | Pas de refresh token / révocation de session. | Entité `RefreshToken`, table Db, endpoints `POST /api/Auth/refresh` (rotation) et `POST /api/Auth/revoke` ajoutés. | Corrigé |
| A-04 | Basse | Utilisateur admin de test dans `appsettings.Development.json`. | Fichier `appsettings.Development.json` exclu du dépôt via `.gitignore`. | Corrigé |

**Verdict de l'audit : PASS** — toutes les réserves ont été corrigées. L'application remplit tous les objectifs pédagogiques du TP avec une sécurité réelle adaptée au contexte. Aucun secret de production n'est exposé.
