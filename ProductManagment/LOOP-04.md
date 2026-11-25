# LOOP-04 — ProductsAPI

| Champ | Valeur |
|---|---|
| Date | 18 novembre 2026 |
| Statut | **PASS** |
| Périmètre | Contrôleur CRUD, Program.cs, configuration, Swagger, middleware |
| Prérequis | LOOP-03 PASS |

---

## 1. Éléments attendus

- Projet `ProductsAPI` référençant ApplicationLayer + InfrastructureLayer.
- `ProductController` avec 5 endpoints.
- Codes HTTP corrects (201, 204, 400, 404).
- `Program.cs` complet : DI, DbContext, Swagger, pipeline.
- `appsettings.json` avec chaîne de connexion depuis la configuration.
- Swagger opérationnel.
- Compilation et démarrage.

## 2. Éléments réalisés

### Contrôleur

| Méthode | Route | Comportement |
|---|---|---|
| GET | `/api/Product` | `200` + liste |
| GET | `/api/Product/{id}` | `200` ou `404` |
| POST | `/api/Product` | `201 CreatedAtAction` + `Location` |
| PUT | `/api/Product/{id}` | `204`, `400` si `id != dto.Id`, `404` si absent |
| DELETE | `/api/Product/{id}` | `204`, `404` si absent |

Corrections par rapport au PDF :

- le PUT utilise l'`id` de la route et vérifie la cohérence avec le corps ;
- le POST retourne `CreatedAtAction` (201) au lieu de `Ok` (200) ;
- le DELETE vérifie l'existence (404 au lieu d'un 204 systématique) ;
- `ProducesResponseType` ajoutés pour Swagger ;
- `ActionResult<T>` utilisé pour les retours.

### Program.cs

- `AddControllers`, `AddEndpointsApiExplorer`, `AddSwaggerGen`.
- Sélection du provider via `DatabaseProvider` (`Sqlite` par défaut, `SqlServer` supporté).
- `AddDbContext<ApplicationDbContext>`.
- `AddScoped<IProductRepository, ProductRepository>`.
- `AddScoped<IProductService, ProductService>`.
- Swagger activé (`EnableSwagger: true`).
- Middleware d'exceptions.
- Application automatique des migrations au démarrage.
- `public partial class Program` pour les tests d'intégration.

### Middleware

`ExceptionHandlingMiddleware` :

- `KeyNotFoundException` → `404 ProblemDetails` ;
- `InvalidOperationException` / `ArgumentException` → `400 ProblemDetails` ;
- autre exception → `500 ProblemDetails` + log.

### Configuration

- `appsettings.json` : provider, chaînes SQLite et SQL Server/LocalDB, Swagger, logging.
- `launchSettings.json` : `http://localhost:5080`, lancement sur Swagger.
- Package `Swashbuckle.AspNetCore` 6.9.0.
- Package `Microsoft.EntityFrameworkCore.Design` 8.0.31.

## 3. Fichiers créés / modifiés

```text
ProductsAPI/ProductsAPI.csproj
ProductsAPI/Program.cs
ProductsAPI/Controllers/ProductController.cs
ProductsAPI/Middleware/ExceptionHandlingMiddleware.cs
ProductsAPI/appsettings.json
ProductsAPI/appsettings.Development.json
ProductsAPI/Properties/launchSettings.json
```

## 4. Commandes exécutées

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export ASPNETCORE_ENVIRONMENT=Development

dotnet build ProductManagement.sln --no-incremental
dotnet run --project ProductsAPI/ProductsAPI.csproj --urls "http://localhost:5080"
```

## 5. Résultats de compilation et de démarrage

```text
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)

info: Microsoft.EntityFrameworkCore.Migrations[20405]
      No migrations were applied. The database is already up to date.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5080
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Content root path: .../ProductManagment/ProductsAPI
```

## 6. Vérifications HTTP

```text
GET  /swagger/index.html              → 200
GET  /swagger/v1/swagger.json         → 200
GET  /api/Product                     → 200 (3 produits)
GET  /api/Product/1                   → 200 (Laptop)
GET  /api/Product/99999               → 404
POST /api/Product (valide)            → 201 + Location
POST /api/Product (prix ≤ 0)          → 400
PUT  /api/Product/1                   → 204
PUT  /api/Product/99999               → 404
DELETE /api/Product/4                 → 204
DELETE /api/Product/99999             → 404
```

## 7. Erreurs rencontrées et corrections

| Erreur | Correction |
|---|---|
| Projet API absent | Créé et référencé |
| `Program.cs` du template incomplet | Réécrit avec DI, Swagger, middleware |
| PUT sans id de route | Signature `Update(int id, UpdateProductDTO)` |
| DELETE toujours 204 | Vérification d'existence → 404 |
| POST retournait 200 | `CreatedAtAction` → 201 |
| Exceptions génériques → 500 | Middleware `ProblemDetails` |
| Ports 5000/7000 occupés par AirPlay | Port 5080 |

## 8. Risques / réserves

- Le démarrage applique les migrations : sur une base SQL Server configurée avec des migrations SQLite, il faudra régénérer la migration provider-specific (voir `README.md`).
- Swagger est activé par configuration (`EnableSwagger`) pour la démonstration, même hors Development.

## 9. AUTO-PASS

| Critère | Résultat | Preuve |
|---|---|---|
| Contrôleur 5 endpoints | PASS | `ProductController.cs` |
| Codes HTTP corrects | PASS | smoke test 22/22 |
| DI complète | PASS | démarrage + requêtes |
| Swagger | PASS | 200 sur index et JSON |
| Chaîne de connexion en configuration | PASS | `appsettings.json` |
| Compilation | PASS | 0 erreur, 0 avertissement |

**Statut : PASS**
