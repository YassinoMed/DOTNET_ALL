# ProductManagement — TP7 Clean Architecture

API REST de gestion de produits construite avec **ASP.NET Core 8**, **Entity Framework Core 8** et une **Clean Architecture** en quatre couches.

> Ce projet est la réalisation du TP `TP7 API CLEAN ARCHITECTURE.pdf`.
> Sur macOS, SQL Server LocalDB n'existe pas et Docker n'est pas utilisé : la base locale d'exécution est **SQLite**. Une configuration SQL Server/LocalDB et un script SQL Server généré par EF Core sont fournis pour Windows. Voir [Base de données](#base-de-données).

---

## Architecture

```text
ProductsAPI ──────────► ApplicationLayer
     │                        │
     └────────────────────────┼──────► DomainLayer
                              │
InfrastructureLayer ──────────┴──────► ApplicationLayer
     │
     └───────────────────────────────► DomainLayer
```

| Projet | Rôle | Dépendances |
|---|---|---|
| `DomainLayer` | Entité `Product` + règles métier | aucune |
| `ApplicationLayer` | DTO, interfaces, `ProductService` | DomainLayer |
| `InfrastructureLayer` | `ApplicationDbContext`, seed, `ProductRepository` | ApplicationLayer + DomainLayer |
| `ProductsAPI` | Contrôleur CRUD, DI, Swagger, middleware | ApplicationLayer + InfrastructureLayer |
| `ProductsAPI.Tests` | Tests unitaires et d'intégration (xUnit) | ProductsAPI |

---

## Prérequis

### macOS (environnement actuel)

- .NET SDK **8.0.425** dans `~/.dotnet` (runtime 8.0.31)
- `sqlite3` (fourni avec macOS)
- Aucun Docker, aucun SQL Server

Le `dotnet` système de cette machine est en 7.0.317 ; il faut donc exporter l'environnement avant chaque commande :

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
```

`global.json` épingle le SDK 8.0.425.

### Windows (PDF d'origine)

- .NET 8 SDK
- SQL Server Express LocalDB (`(localdb)\MSSQLLocalDB`)
- Visual Studio 2022 (ou CLI `dotnet ef`)

---

## Démarrage rapide (macOS)

```bash
cd ProductManagment

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

dotnet tool restore          # installe dotnet-ef 8.0.31 (outil local)
dotnet build
dotnet test

# Migrations + base SQLite (facultatif : l'API applique aussi les migrations au démarrage)
dotnet ef database update --project InfrastructureLayer --startup-project ProductsAPI

dotnet run --project ProductsAPI/ProductsAPI.csproj --urls http://localhost:5080
```

Ou simplement :

```bash
./scripts/dev.sh
```

- Swagger UI : <http://localhost:5080/swagger>
- OpenAPI JSON : <http://localhost:5080/swagger/v1/swagger.json>
- Base SQLite : `ProductsAPI/ProductManagement.db`

### Smoke test complet

API démarrée, dans un autre terminal :

```bash
./scripts/smoke-test.sh
```

Le script vérifie Swagger, les 3 produits seedés, GET par id, 404, POST 201/400, PUT 204/404, DELETE 204/404 et la persistance.

---

## Endpoints

| Méthode | Route | Succès | Erreurs |
|---|---|---|---|
| GET | `/api/Product` | `200` + liste | — |
| GET | `/api/Product/{id}` | `200` | `404` si absent |
| POST | `/api/Product` | `201` + `Location` | `400` données invalides |
| PUT | `/api/Product/{id}` | `204` | `400` id incohérent/invalide, `404` si absent |
| DELETE | `/api/Product/{id}` | `204` | `404` si absent |

Exemple de corps POST/PUT :

```json
{
  "name": "Mechanical Keyboard",
  "description": "RGB keyboard",
  "price": 149.99,
  "stock": 12
}
```

Pour PUT, ajoutez `"id"` dans le corps (il doit correspondre à l'id de la route).

### Données initiales

| Id | Name | Description | Price | Stock |
|---|---|---|---|---|
| 1 | Laptop | High-end gaming laptop | 1500.00 | 10 |
| 2 | Smartphone | Latest model smartphone | 800.00 | 25 |
| 3 | Wireless Headphones | Noise cancelling headphones | 200.00 | 40 |

---

## Base de données

### Configuration

`ProductsAPI/appsettings.json` :

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "Sqlite": "Data Source=ProductManagement.db",
    "SqlServer": "Server=(localdb)\\MSSQLLocalDB;Database=ProductManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

Le fournisseur est choisi par `DatabaseProvider` (`Sqlite` ou `SqlServer`). On peut aussi le surcharger par variable d'environnement :

```bash
export DatabaseProvider=SqlServer
export ConnectionStrings__SqlServer="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
```

### Migration locale (SQLite)

La migration `dbcreate` est dans `InfrastructureLayer/Data/Migrations` et a été appliquée :

```bash
dotnet ef database update --project InfrastructureLayer --startup-project ProductsAPI
```

Vérification :

```bash
sqlite3 ProductsAPI/ProductManagement.db \
  "SELECT Id, Name, Price, Stock FROM Products ORDER BY Id;"
```

### SQL Server / LocalDB (Windows)

Le script `docs/ProductManagement.sqlserver.sql` a été généré par EF Core pour SQL Server (table `Products`, index, seed des 3 produits). Il peut être exécuté avec `sqlcmd` ou SSMS.

Les migrations EF étant spécifiques au fournisseur, pour utiliser SQL Server via `dotnet ef` :

```bash
# 1. Sauvegarder/supprimer la migration SQLite locale (ou travailler sur une copie)
# 2. Générer la migration SQL Server
export DatabaseProvider=SqlServer
dotnet ef migrations add SqlServerInitial \
  --project InfrastructureLayer --startup-project ProductsAPI \
  --output-dir Data/MigrationsSqlServer

dotnet ef database update \
  --project InfrastructureLayer --startup-project ProductsAPI
```

> **Réserve importante :** l'exécution locale de ce TP utilise SQLite, pas LocalDB, car LocalDB est un composant Windows et Docker est exclu. Le code, le modèle EF, le seed et le CRUD sont identiques ; seul le fournisseur de base diffère. La conformité stricte au PDF sur ce point n'est donc pas revendiquée.

---

## Tests

```bash
dotnet test
```

Résultat attendu : **29 tests réussis** (0 échec).

| Suite | Couverture |
|---|---|
| `Domain/ProductTests` | prix positif/nul/négatif, stock négatif, nom invalide, propriétés |
| `Application/ProductServiceTests` | CRUD, mapping, produit inexistant (`KeyNotFoundException`) |
| `Api/ProductEndpointsTests` | 3 produits seedés, GET/POST/PUT/DELETE, 400/404, Swagger, persistance |

Les tests d'intégration utilisent une base SQLite temporaire isolée par test.

### Couverture

```bash
dotnet test ProductManagement.sln --no-build \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults
```

Dernière mesure : **80,4 % des lignes** (373/464) et **75,5 % des branches** (37/49).

| Couche | Couverture lignes |
|---|---:|
| ApplicationLayer | 100,0 % |
| DomainLayer | 94,1 % |
| ProductsAPI | 81,2 % |
| InfrastructureLayer | 73,4 % |

---

## Validation automatique (AUTO-PASS SQLite)

Un seul script rejoue toutes les portes de validation sur une base SQLite neuve :

```bash
./scripts/auto-validate.sh
```

| # | Porte |
|---|---|
| 1 | `dotnet restore` |
| 2 | Build sans erreur ni avertissement |
| 3 | 29 tests |
| 4 | Démarrage API + migration automatique sur base neuve |
| 5 | `GET /api/Product` → 200 avec 3 produits |
| 6 | `SELECT COUNT(*) FROM Products` = 3 |
| 7 | Migration `dbcreate` enregistrée |
| 8 | Smoke test HTTP (22 vérifications) |
| 9 | Logs API sans erreur |

Dernier résultat : **9 PASS, 0 FAIL**. Le détail de la méthode, les auto-corrections et la matrice de clôture des partiels sont dans [`AUTO-PASS-SQLITE.md`](AUTO-PASS-SQLITE.md).

---

## Visual Studio for Mac

Visual Studio for Mac a été **retiré par Microsoft le 31 août 2024**. La version installée (17.6.14) ne supporte pas officiellement .NET 8. Le fichier `ProductManagement.sln` est un format standard Visual Studio et peut être ouvert, mais la compilation fiable de ce projet se fait avec la **CLI `dotnet`** ou **Visual Studio Code**. Le projet n'utilise volontairement aucune dépendance propre à un IDE.

---

## Structure

```text
ProductManagment/
├── ProductManagement.sln
├── global.json
├── README.md
├── FINAL-REPORT.md
├── AUTO-PASS-SQLITE.md
├── AUDIT-00.md
├── LOOP-01.md … LOOP-06.md
├── DomainLayer/
│   ├── DomainLayer.csproj
│   └── Entities/Product.cs
├── ApplicationLayer/
│   ├── DTOs/{ProductDTO,CreateProductDTO,UpdateProductDTO}.cs
│   ├── Interfaces/Repositories/IProductRepository.cs
│   ├── Interfaces/Services/IProductService.cs
│   └── Services/ProductService.cs
├── InfrastructureLayer/
│   ├── Data/ApplicationDbContext.cs
│   ├── Data/Migrations/…_dbcreate.cs
│   └── Repositories/ProductRepository.cs
├── ProductsAPI/
│   ├── Controllers/ProductController.cs
│   ├── Middleware/ExceptionHandlingMiddleware.cs
│   ├── Properties/launchSettings.json
│   ├── appsettings.json
│   └── Program.cs
├── ProductsAPI.Tests/
│   ├── ApiFactory.cs
│   ├── Api/ProductEndpointsTests.cs
│   ├── Application/ProductServiceTests.cs
│   └── Domain/ProductTests.cs
├── docs/ProductManagement.sqlserver.sql
└── scripts/{dev.sh,smoke-test.sh,auto-validate.sh}
```
