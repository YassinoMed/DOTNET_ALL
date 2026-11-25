# LOOP-05 — Base de données et migrations

| Champ | Valeur |
|---|---|
| Date | 18 novembre 2026 |
| Statut | **PASS_WITH_RESERVATIONS** |
| Périmètre | Outil EF, migration, création de la base, vérification du seed |
| Prérequis | LOOP-04 PASS |
| Réserve principale | SQLite local au lieu de SQL Server LocalDB |

---

## 1. Éléments attendus

- Outil `dotnet-ef` compatible EF Core 8.
- Migration nommée `dbcreate` (nom demandé par le TP).
- Application réelle de la migration.
- Vérification de la table `Products` et des 3 produits.
- Aucune base préexistante écrasée sans sauvegarde.

## 2. Outil EF

Le `dotnet-ef` global était en **7.0.20**, incompatible avec EF Core 8. Un outil local a été installé :

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.0.31
dotnet ef --version
```

Résultat :

```text
Entity Framework Core .NET Command-line Tools
8.0.31
```

Le manifeste `.config/dotnet-tools.json` est versionné ; `dotnet tool restore` suffit sur une nouvelle machine.

## 3. Migrations

### Migration SQLite (appliquée localement)

```bash
dotnet ef migrations add dbcreate \
  --project InfrastructureLayer \
  --startup-project ProductsAPI \
  --output-dir Data/Migrations
```

Fichiers générés :

```text
InfrastructureLayer/Data/Migrations/20261118084043_Dbcreate.cs
InfrastructureLayer/Data/Migrations/20261118084043_Dbcreate.Designer.cs
InfrastructureLayer/Data/Migrations/ApplicationDbContextModelSnapshot.cs
```

La classe a été renommée `Dbcreate` (PascalCase) pour supprimer l'avertissement CS8981, **sans changer l'identifiant de migration** `20261118084043_dbcreate`.

### Application

```bash
dotnet ef database update \
  --project InfrastructureLayer \
  --startup-project ProductsAPI
```

Résultat :

```text
Build succeeded.
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20261118084043_dbcreate'.
Applying migration '20261118084043_dbcreate'.
Done.
```

Base créée : `ProductsAPI/ProductManagement.db` (le content root de l'API est `ProductsAPI/`).

## 4. Vérification de la base

```bash
sqlite3 ProductsAPI/ProductManagement.db ".tables"
sqlite3 ProductsAPI/ProductManagement.db ".schema Products"
sqlite3 -header -column ProductsAPI/ProductManagement.db \
  "SELECT Id, Name, Description, Price, Stock FROM Products ORDER BY Id;"
sqlite3 ProductsAPI/ProductManagement.db "SELECT COUNT(*) FROM Products;"
```

Résultats :

```text
Products     __EFMigrationsHistory

CREATE TABLE "Products" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Price" decimal(18,2) NOT NULL,
    "Stock" INTEGER NOT NULL
);
CREATE INDEX "IX_Products_Name" ON "Products" ("Name");

Id         Name                  Description          Price  Stock
--  -------------------  ---------------------------  -----  -----
 1  Laptop               High-end gaming laptop        1500     10
 2  Smartphone           Latest model smartphone        800     25
 3  Wireless Headphones  Noise cancelling headphones    200     40

3
```

Migration enregistrée :

```text
MigrationId               ProductVersion
20261118084043_dbcreate   8.0.31
```

## 5. Script SQL Server (artefact pour Windows)

Un script SQL Server a été généré à partir du modèle EF :

```bash
export DatabaseProvider=SqlServer
dotnet ef migrations add SqlServerInitial \
  --project InfrastructureLayer --startup-project ProductsAPI \
  --output-dir Data/MigrationsSqlServer

dotnet ef migrations script \
  --project InfrastructureLayer --startup-project ProductsAPI \
  --output docs/ProductManagement.sqlserver.sql

rm -rf InfrastructureLayer/Data/MigrationsSqlServer
```

Le script `docs/ProductManagement.sqlserver.sql` contient :

- création de `__EFMigrationsHistory` ;
- table `Products` avec `Id int IDENTITY`, `Name nvarchar(100)`, `Description nvarchar(500)`, `Price decimal(18,2)`, `Stock int` ;
- index `IX_Products_Name` ;
- insertion des 3 produits.

> Ce script n'a **pas** été exécuté : aucune instance SQL Server n'est disponible sur cette machine et Docker est exclu. Il sert de preuve de conformité du modèle SQL Server et de point de départ pour Windows/LocalDB.

## 6. Commandes exécutées

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.0.31
dotnet ef migrations add dbcreate --project InfrastructureLayer --startup-project ProductsAPI --output-dir Data/Migrations
dotnet ef database update --project InfrastructureLayer --startup-project ProductsAPI
dotnet ef migrations list --project InfrastructureLayer --startup-project ProductsAPI --no-build

sqlite3 ProductsAPI/ProductManagement.db ".tables"
sqlite3 ProductsAPI/ProductManagement.db ".schema Products"
sqlite3 -header -column ProductsAPI/ProductManagement.db "SELECT Id, Name, Description, Price, Stock FROM Products ORDER BY Id;"
sqlite3 ProductsAPI/ProductManagement.db "SELECT COUNT(*) FROM Products;"
```

## 7. Erreurs rencontrées et corrections

| Erreur | Correction |
|---|---|
| `dotnet-ef` global 7.0.20 | Outil local 8.0.31 via manifeste |
| LocalDB indisponible sur macOS | Provider SQLite local + script SQL Server |
| Avertissement CS8981 sur `dbcreate` | Classe renommée `Dbcreate`, ID conservé |
| Risque de double migration SQLite/SQL Server | Migration SQL Server générée dans un dossier temporaire puis supprimée ; script conservé |

## 8. Réserves

1. **SQLite ≠ SQL Server LocalDB.** La conformité stricte au PDF sur le moteur de base n'est pas atteinte.
2. Les migrations EF sont spécifiques au provider : pour SQL Server, il faut régénérer la migration (procédure documentée dans `README.md`).
3. Le script SQL Server n'a pas été exécuté, faute d'instance.

## 9. AUTO-PASS

| Critère | Résultat | Preuve |
|---|---|---|
| Outil EF 8 | PASS | `dotnet ef --version` = 8.0.31 |
| Migration `dbcreate` créée | PASS | fichier + `migrations list` |
| Migration appliquée | PASS | log « Applying migration » + historique |
| Table `Products` | PASS | `.schema Products` |
| 3 produits seedés | PASS | `SELECT` + count = 3 |
| Aucune base écrasée | PASS | base créée après sauvegarde initiale |
| Provider identique au PDF | **RÉSERVE** | SQLite local, script SQL Server fourni |

**Statut : PASS_WITH_RESERVATIONS** — réserve unique : SQLite local au lieu de SQL Server LocalDB.
