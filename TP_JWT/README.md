# SecureAPI_JWT — TP 9 : Sécuriser une API REST avec JWT

Projet ASP.NET Core Web API (.NET 7) sécurisé par authentification JWT Bearer, avec
ASP.NET Core Identity, Entity Framework Core et SQLite.

## Contexte et environnement

- OS : macOS Apple Silicon (M2), macOS 27.3
- SDK : .NET 7 (7.0.317) — **Ne pas migrer vers .NET 8/9/10**
- Langage : C#
- Framework : ASP.NET Core Web API
- Base de données : SQLite (fichier `SecureAPI_JWT.db` à la racine du projet)
- ORM : Entity Framework Core 7
- Authentification : ASP.NET Core Identity + JWT Bearer (HMAC SHA-256)
- Tests : xUnit
- Documentation API : Swagger / OpenAPI

## Pré-requis (macOS)

- .NET SDK 7.0.x installé (`dotnet --list-sdks` doit afficher `7.0.x`)
- SQLite (`sqlite3` disponible en CLI, optionnel)
- Git
- Terminal ou éditeur (VS Code / Rider / CLI .NET — Visual Studio 2022 complet n'est pas natif sur macOS)

## Installation

```bash
# Se placer dans le dossier du projet
cd SecureAPI_JWT

# Restaurer les packages NuGet
dotnet restore
```

## Base de données (migrations EF Core / SQLite)

Une migration initiale est déjà présente (`Migrations/InitialCreate.cs`). Pour l'appliquer :

```bash
dotnet ef database update
```

Cela crée le fichier `SecureAPI_JWT.db` à la racine du projet avec toutes les tables Identity
(`Users`, `Roles`, `UserRoles`, …). La base n'est **pas** supprimée ni recréée à chaque lancement.

Pour créer une nouvelle migration après un changement de modèle :

```bash
dotnet ef migrations add <NomMigration>
dotnet ef database update
```

## Lancement du projet

```bash
dotnet run
```

L'application écoute par défaut sur `http://localhost:5053` (profil `http`) et
`https://localhost:7157` (profil `https`, selon `launchSettings.json`).

Swagger UI est disponible en développement à l'adresse :
`http://localhost:5053/swagger`

## Tests automatisés (xUnit)

Les tests utilisent une base de données SQLite **temporaire et isolée** par exécution
(aucun impact sur `SecureAPI_JWT.db`).

```bash
cd ../SecureAPI_JWT.Tests   # ou depuis la racine selon l'arborescence
dotnet test
```

Résultat attendu : 19 tests, 0 échec, 0 ignoré.

## Endpoints

### Authentification (`api/Auth`)

| Méthode | Endpoint | Description | Authentification |
|---------|----------|-------------|------------------|
| POST | `/api/Auth/register` | Inscription d'un nouvel utilisateur | Non |
| POST | `/api/Auth/login` | Connexion et émission d'un JWT | Non |

### Endpoints sécurisés (`api/Secure`)

| Méthode | Endpoint | Description | Autorisation |
|---------|----------|-------------|--------------|
| GET | `/api/Secure/public` | Accès public | Aucun (anonyme) |
| GET | `/api/Secure/profile` | Profil de l'utilisateur connecté | JWT requis (`[Authorize]`) |
| GET | `/api/Secure/user` | Espace utilisateur | Rôle `User` |
| GET | `/api/Secure/admin` | Espace administrateur | Rôle `Admin` |
| GET | `/api/Secure/user-or-admin` | Espace utilisateur ou administrateur | Rôles `User,Admin` |

### Codes HTTP retournés

- `200` : succès
- `201` : ressource créée (register)
- `400` : données invalides / compte existant
- `401` : identifiants invalides ou token absent / invalide / expiré
- `403` : authentifié mais non autorisé (ex. utilisateur `User` sur endpoint `Admin`)

## Exemples d'appels

### 1. Inscription (`POST /api/Auth/register`)

Requête :
```http
POST /api/Auth/register HTTP/1.1
Content-Type: application/json

{
  "firstName": "Yassine",
  "lastName": "Moha",
  "username": "yassine",
  "email": "yassine@test.com",
  "password": "Yassine@123!"
}
```

Réponse (201) :
```json
{
  "message": "Inscription réussie.",
  "isAuthenticated": true,
  "username": "yassine",
  "email": "yassine@test.com",
  "roles": ["User"],
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresOn": "2026-11-26T..."
}
```

### 2. Connexion (`POST /api/Auth/login`)

Requête :
```http
POST /api/Auth/login HTTP/1.1
Content-Type: application/json

{
  "username": "yassine",
  "password": "Yassine@123!"
}
```

Réponse (200) :
```json
{
  "message": "Connexion réussie.",
  "isAuthenticated": true,
  "username": "yassine",
  "email": "yassine@test.com",
  "roles": ["User"],
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresOn": "2026-11-26T..."
}
```

Identifiants invalides → `401` avec `{"message":"Identifiants invalides.", ...}`.

### 3. Appel d'endpoint protégé avec Bearer Token

Une fois le JWT obtenu, l'ajouter à l'en-tête `Authorization` :

```http
GET /api/Secure/profile HTTP/1.1
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

Réponse (200) :
```json
{
  "message": "Profil utilisateur connecté.",
  "userId": "...",
  "username": "yassine",
  "email": "yassine@test.com",
  "roles": ["User"],
  "authenticated": true
}
```

Sans token → `401`. Avec token invalide → `401`. Utilisateur `User` sur `/api/Secure/admin` → `403`.

### 4. Depuis Swagger

1. Ouvrir `http://localhost:5053/swagger`.
2. Cliquer sur le bouton **Authorize** (verrou) en haut à droite.
3. Saisir : `Bearer <token>`.
4. Appeler les endpoints protégés.

## Rôles

- `User` : rôle par défaut attribué à tout nouvel utilisateur inscrit.
- `Admin` : rôle réservé aux administrateurs.

Un mécanisme de seed idempotent crée les rôles `User` et `Admin` au démarrage. Un
utilisateur admin de test peut être créé via la section `AdminUser` de
`appsettings.Development.json` (développement uniquement, à ne jamais commettre en production).

## Sécurité

- Clé de signature HMAC SHA-256 (clé de développement uniquement, à remplacer par un secret externe en production).
- Validation : signature, émetteur (issuer), audience, durée de vie.
- Aucun mot de passe ni hash retourné par l'API.
- Politique de mot de passe Identity activée (longueur ≥ 8, majuscule, minuscule, chiffre, caractère spécial).

## Structure du projet

```
SecureAPI_JWT/
├── Controllers/     AuthController.cs, SecureController.cs
├── Data/            ApplicationDbContext.cs
├── Helpers/         JwtSettings.cs
├── Migrations/      migrations EF Core
├── Models/          ApplicationUser, RegisterModel, LoginModel, AuthModel
├── Services/        IAuthService.cs, AuthService.cs
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
└── SecureAPI_JWT.csproj
```

## Notes macOS

Visual Studio 2022 complet n'est pas natif sur macOS ; le TP a été validé avec le SDK .NET 7 en CLI (terminal) et est compatible avec VS Code ou Rider.
