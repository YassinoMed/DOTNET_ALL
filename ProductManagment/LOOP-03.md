# LOOP-03 — InfrastructureLayer

| Champ | Valeur |
|---|---|
| Date | 18 novembre 2026 |
| Statut | **PASS** |
| Périmètre | EF Core, `ApplicationDbContext`, seed, `ProductRepository` |
| Prérequis | LOOP-02 PASS |

---

## 1. Éléments attendus

- Packages EF Core 8 alignés sur le runtime 8.0.31.
- Références ApplicationLayer + DomainLayer.
- `ApplicationDbContext` avec `DbSet<Product>`.
- Configuration du modèle (longueurs, précision decimale, index).
- Seed des 3 produits du PDF.
- `ProductRepository` CRUD avec `AsNoTracking` en lecture et `SaveChangesAsync` en écriture.
- Compilation sans erreur.

## 2. Éléments réalisés

### Packages (InfrastructureLayer)

| Package | Version |
|---|---|
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.31 |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.31 |
| Microsoft.EntityFrameworkCore.Design | 8.0.31 |
| Microsoft.EntityFrameworkCore.Tools | 8.0.31 |

### `ApplicationDbContext`

- `DbSet<Product> Products`.
- Table `Products`.
- `Name` requis, max 100.
- `Description` max 500.
- `Price` precision (18,2).
- `Stock` requis.
- Index sur `Name`.
- `HasData` avec les 3 produits :

| Id | Name | Description | Price | Stock |
|---|---|---|---|---|
| 1 | Laptop | High-end gaming laptop | 1500.00 | 10 |
| 2 | Smartphone | Latest model smartphone | 800.00 | 25 |
| 3 | Wireless Headphones | Noise cancelling headphones | 200.00 | 40 |

### `ProductRepository`

- `GetAllAsync` : `AsNoTracking`, tri par Id.
- `GetByIdAsync` : `AsNoTracking`, `FirstOrDefaultAsync`.
- `AddAsync` : `Add` + `SaveChangesAsync`, retourne l'entité avec son Id.
- `UpdateAsync` : `Update` + `SaveChangesAsync`.
- `DeleteAsync` : recherche par Id, suppression si trouvée, `SaveChangesAsync`.

## 3. Fichiers créés / modifiés

```text
InfrastructureLayer/InfrastructureLayer.csproj
InfrastructureLayer/Data/ApplicationDbContext.cs
InfrastructureLayer/Repositories/ProductRepository.cs
```

## 4. Commandes exécutées

```bash
dotnet add InfrastructureLayer/InfrastructureLayer.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.31
dotnet add InfrastructureLayer/InfrastructureLayer.csproj package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.31
dotnet add InfrastructureLayer/InfrastructureLayer.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.31
dotnet add InfrastructureLayer/InfrastructureLayer.csproj package Microsoft.EntityFrameworkCore.Tools --version 8.0.31

dotnet build ProductManagement.sln --no-incremental
```

## 5. Résultats de compilation

```text
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)
```

## 6. Vérifications du modèle et du seed

Le seed a été vérifié après application de la migration (LOOP-05) :

```text
Id         Name                  Description          Price  Stock
--  -------------------  ---------------------------  -----  -----
 1  Laptop               High-end gaming laptop        1500     10
 2  Smartphone           Latest model smartphone        800     25
 3  Wireless Headphones  Noise cancelling headphones    200     40
```

## 7. Erreurs rencontrées et corrections

| Erreur | Correction |
|---|---|
| Projet Infrastructure vide, sans package | Packages et références ajoutés |
| Aucun DbContext | `ApplicationDbContext` créé |
| Aucun repository | `ProductRepository` créé |
| Précision décimale non configurée | `HasPrecision(18, 2)` |
| Risque de doublons de seed | `HasData` avec Id explicites 1–3 |

## 8. Risques / réserves

- SQLite stocke `decimal` comme `TEXT` ; les opérations du TP (CRUD simple) ne font pas de tri/agrégation sur `Price`.
- Le provider SQL Server est configuré dans le code mais non exécuté localement (LocalDB indisponible sur macOS).

## 9. AUTO-PASS

| Critère | Résultat | Preuve |
|---|---|---|
| Packages EF Core présents | PASS | csproj |
| Références correctes | PASS | csproj |
| DbContext + DbSet | PASS | `ApplicationDbContext.cs` |
| Seed 3 produits | PASS | migration + requête SQLite |
| Repository CRUD | PASS | `ProductRepository.cs` |
| AsNoTracking / SaveChangesAsync | PASS | code + tests d'intégration |
| Compilation | PASS | 0 erreur |

**Statut : PASS**
