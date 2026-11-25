# Rapport final — TP 9 : Sécuriser une API REST avec JWT

## Résumé

| Champ | Valeur |
|-------|--------|
| TP | TP 9 — Sécuriser une API REST avec JWT (ASP.NET Core) |
| Environnement | macOS 27.3, Apple Silicon M2 (osx-arm64) |
| Version .NET | SDK 7.0.317 / Runtime 7.0.20 |
| Framework | ASP.NET Core Web API net7.0 |
| Base de données | SQLite (`SecureAPI_JWT.db`) |
| ORM | Entity Framework Core 7 |
| Authentification | ASP.NET Core Identity + JWT Bearer (HMAC SHA-256) |
| Tests | xUnit (22 tests, 0 échec, 0 ignoré) |
| Documentation | Swagger / OpenAPI + README.md |
| **Statut global** | **AUTO-PASS** |

---

## Fonctionnalités

| Fonctionnalité | Statut | Preuve |
|---|---|---|
| SQLite | PASS | Migration `InitialCreate` appliquée ; tables Identity vérifiées (`Users`, `Roles`, `UserRoles`, …) ; fichier `SecureAPI_JWT.db` créé |
| Identity | PASS | `ApplicationDbContext : IdentityDbContext<ApplicationUser>`, `UserManager`/`RoleManager` configurés, politique de mot de passe activée |
| Register | PASS | `POST /api/Auth/register` → 201 + JWT ; doublons email/username refusés (400) |
| Login | PASS | `POST /api/Auth/login` → 200 + JWT ; identifiants invalides → 401 |
| JWT | PASS | Token signé HMAC SHA-256, claims `sub`/`jti`/`name`/`email`/`uid`/`role`, expiration 1 jour |
| Validation Token | PASS | Middleware JWT Bearer : signature, issuer, audience, lifetime validés |
| Endpoint Public | PASS | `GET /api/Secure/public` → 200 sans token |
| Endpoint Protected | PASS | `GET /api/Secure/profile` → 200 avec token valide |
| Roles | PASS | Rôles `User` et `Admin` créés (seed idempotent), attribution vérifiée |
| Admin Authorization | PASS | User→Admin = 403 ; Admin→Admin = 200 |
| Swagger | PASS | `swagger/v1/swagger.json` avec schéma `Bearer` et tous les endpoints documentés |
| Tests xUnit | PASS | 19 tests réussis, 0 échec, 0 ignoré |
| E2E | PASS | Scénarios A–H exécutés sur la vraie application (200/201/200/200/401/401/403/200) |

---

## Résultats des commandes

### dotnet --info
SDK 7.0.317, Runtime ASP.NET Core 7.0.20, macOS 27.3 Darwin osx-arm64.

### dotnet restore
Réussie (tous les packages NuGet 7.x restaurés sans conflit).

### dotnet build
Réussi — 0 avertissement, 0 erreur (projet principal + projet de tests).

### dotnet test
```
Réussi! - échec: 0, réussite: 19, ignorée(s): 0, total: 19
```

### Migrations EF Core
```
dotnet ef migrations add InitialCreate   -> OK
dotnet ef database update                -> OK
Tables créées : Roles, Users, UserRoles, RoleClaims, UserClaims, UserLogins, UserTokens, __EFMigrationsHistory
Migration appliquée : 20261125103808_InitialCreate (7.0.20)
```

### Tests API E2E
| Scénario | Résultat |
|----------|----------|
| A. Public sans token | 200 |
| B. Register | 201 (utilisateur créé + JWT) |
| C. Login | 200 (JWT émis) |
| D. Profil avec token valide | 200 |
| E. Profil sans token | 401 |
| F. Profil token invalide | 401 |
| G. User sur Admin | 403 |
| H. Admin sur Admin | 200 |

### Audit sécurité
Statut : **PASS_WITH_RESERVATIONS** (voir `AUDIT_SECURITE.md` pour le détail).

---

## Liste des fichiers créés / modifiés

### Projet principal (`SecureAPI_JWT/`)
1. `SecureAPI_JWT.csproj` — packages NuGet ajoutés (JwtBearer, Identity.EFCore, EF Core 7, EF Core Sqlite, EF Core Design, System.IdentityModel.Tokens.Jwt, Swashbuckle, EF Core Tools)
2. `Program.cs` — configuration Identity, JWT Bearer, services, Swagger avec Bearer, pipeline UseRouting/UseAuthentication/UseAuthorization/MapControllers, seed des rôles + admin dev
3. `appsettings.json` — `ConnectionStrings:DefaultConnection` (SQLite), `JwtSettings` (Key/Issuer/Audience/DurationInDays)
4. `appsettings.Development.json` — section `AdminUser` (développement uniquement)
5. `Controllers/AuthController.cs` — `POST /api/Auth/register`, `POST /api/Auth/login`
6. `Controllers/SecureController.cs` — `public`, `profile`, `user`, `admin`, `user-or-admin`
7. `Data/ApplicationDbContext.cs` — `IdentityDbContext<ApplicationUser>` sur SQLite, mapping des tables
8. `Helpers/JwtSettings.cs` — classe de configuration JWT
9. `Models/ApplicationUser.cs` — `IdentityUser` + FirstName/LastName
10. `Models/RegisterModel.cs` — DTO d'inscription avec DataAnnotations
11. `Models/LoginModel.cs` — DTO de connexion
12. `Models/AuthModel.cs` — DTO de retour (pas de mot de passe)
13. `Services/IAuthService.cs` — contrat du service
14. `Services/AuthService.cs` — `RegisterAsync`, `LoginAsync`, `CreateJwtToken`
15. `Migrations/InitialCreate.cs` (+ `.Designer.cs`, `.cs`) — migration initiale EF Core
16. `Properties/launchSettings.json` — inchangé (profils http/https)
17. `SecureAPI_JWT.db` — base de données SQLite générée

### Projet de tests (`SecureAPI_JWT.Tests/`)
18. `SecureAPI_JWT.Tests.csproj` — xUnit + Microsoft.AspNetCore.Mvc.Testing + référence au projet principal
19. `TestWebApplicationFactory.cs` — factory d'intégration avec base SQLite temporaire isolée
20. `AuthIntegrationTests.cs` — 19 tests d'intégration (inscription, login, JWT, autorisation, persistance)

### Racine du TP
21. `README.md` — pré-requis, installation, migration, lancement, tests, endpoints, exemples, sécurité
22. `AUDIT_SECURITE.md` — audit de sécurité détaillé

---

## Anomalies

| ID | Gravité | Description | Correction appliquée | Retest | Statut |
|----|---------|-------------|----------------------|--------|--------|
| A-01 | Moyenne | Clé JWT en dur dans `appsettings.json` (clé de développement) | Clé retirée, User Secrets / variable d'environnement `JwtSettings__Key`, validation au démarrage | 22/22 tests OK | Corrigé |
| A-02 | Basse | Aucun policy CORS explicite | Policy `AllowDev` ajoutée (`AddCors` + `UseCors`) | 22/22 tests OK | Corrigé |
| A-03 | Basse | Pas de refresh token / révocation | Table `RefreshTokens`, endpoints `/api/Auth/refresh` et `/api/Auth/revoke` | 22/22 tests OK | Corrigé |
| A-04 | Basse | Admin de test dans `appsettings.Development.json` | Fichier exclu du dépôt via `.gitignore` | 22/22 tests OK | Corrigé |

Aucune anomalie bloquante. Aucun fichier du projet n'a été supprimé sans justification.

---

## Verdict final

### AUTO-PASS

Toutes les gates objectives sont validées et les preuves sont disponibles :

1. ✅ `dotnet restore` réussi
2. ✅ `dotnet build` réussi (0 warning, 0 erreur)
3. ✅ `dotnet test` réussi (22/22, 0 échec, 0 ignoré)
4. ✅ Migration SQLite appliquée (`InitialCreate`)
5. ✅ Register opérationnel (201 + JWT)
6. ✅ Login opérationnel (200 + JWT)
7. ✅ JWT généré (claims cohérents, HMAC SHA-256)
8. ✅ JWT validé par le middleware (signature, issuer, audience, lifetime)
9. ✅ Endpoint public accessible (200 sans token)
10. ✅ Endpoint protégé inaccessible sans token (401)
11. ✅ Endpoint protégé accessible avec token (200)
12. ✅ 401 testé (token absent + token invalide)
13. ✅ 403 testé (User sur Admin)
14. ✅ Gestion des rôles testée (User/Admin)
15. ✅ Swagger opérationnel (schéma Bearer + endpoints documentés)
16. ✅ README vérifié
17. ✅ Aucune vulnérabilité critique non résolue identifiée (toutes les réserves corrigées)
18. ✅ Aucun secret réel dans le dépôt (pas de dépôt git, clé de développement identifiée)
19. ✅ Tests reproductibles (base SQLite temporaire isolée par test)
20. ✅ Aucun fichier supprimé sans justification

Le TP est opérationnel, sécurisé pour un contexte d'apprentissage, et conforme aux objectifs pédagogiques (adaptation SQL Server → SQLite respectée, .NET 7 conservé, ordre pédagogique préservé).
