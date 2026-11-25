# LOOP-01 — Solution et DomainLayer

| Champ | Valeur |
|---|---|
| Date | 18 novembre 2026 |
| Statut | **PASS** |
| Périmètre | Solution, projets, références, entité `Product` |
| Prérequis | LOOP-00 validé (`AUDIT-00.md`) — GO AUTO |

---

## 1. Éléments attendus

- Solution `ProductManagement.sln` avec les 4 projets de production.
- Projet de tests `ProductsAPI.Tests` (décision D4).
- Cibles `net8.0` + `global.json` épinglé sur 8.0.425.
- Références conformes à la Clean Architecture.
- Entité `DomainLayer.Entities.Product` avec règles métier.
- Aucune dépendance EF Core / ASP.NET Core dans DomainLayer.
- Archivage des restes F# et fichiers inutiles.

## 2. Éléments réalisés

- `global.json` créé : SDK `8.0.425`, `rollForward: latestFeature`.
- Solution `ProductManagement.sln` recréée (l'ancienne `ProductManagment.sln` archivée).
- Projets créés :
  - `DomainLayer` (classlib, net8.0)
  - `ApplicationLayer` (classlib, net8.0)
  - `InfrastructureLayer` (classlib, net8.0)
  - `ProductsAPI` (webapi, net8.0, controllers, sans HTTPS)
  - `ProductsAPI.Tests` (xunit, net8.0)
- Références :
  - ApplicationLayer → DomainLayer
  - InfrastructureLayer → ApplicationLayer + DomainLayer
  - ProductsAPI → ApplicationLayer + InfrastructureLayer
  - ProductsAPI.Tests → ProductsAPI
- Entité renommée `Products.cs` → `Product.cs`, complétée :
  - `Id`, `Name` (2–100), `Description` (≤ 500), `Price` decimal(18,2), `Stock` ≥ 0 ;
  - `ValidateBusinessRules()` vérifie nom, description, prix > 0, stock ≥ 0.
- Fichiers F# (`DomainLayer.fsproj`, `MyPage.fs`, `AssemblyInfo.fs`) et `Class1.cs` archivés dans `_archive/legacy/`.
- Sauvegarde de l'état initial : `_archive/baseline-src-20261118.tar.gz`.
- `.gitignore` complété (`*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`, `_archive/`).

## 3. Fichiers créés / modifiés

```text
global.json
ProductManagement.sln
DomainLayer/DomainLayer.csproj
DomainLayer/Entities/Product.cs
ApplicationLayer/ApplicationLayer.csproj
InfrastructureLayer/InfrastructureLayer.csproj
ProductsAPI/ProductsAPI.csproj
ProductsAPI.Tests/ProductsAPI.Tests.csproj
.gitignore
_archive/baseline-src-20261118.tar.gz
_archive/legacy/…
```

## 4. Commandes exécutées

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

dotnet new sln -n ProductManagement
dotnet new classlib -n DomainLayer -f net8.0 -o DomainLayer --force
dotnet new classlib -n ApplicationLayer -f net8.0 -o ApplicationLayer --force
dotnet new classlib -n InfrastructureLayer -f net8.0 -o InfrastructureLayer --force
dotnet new webapi -n ProductsAPI -f net8.0 -o ProductsAPI --use-controllers --no-https
dotnet new xunit -n ProductsAPI.Tests -f net8.0 -o ProductsAPI.Tests

dotnet add ApplicationLayer/ApplicationLayer.csproj reference DomainLayer/DomainLayer.csproj
dotnet add InfrastructureLayer/InfrastructureLayer.csproj reference ApplicationLayer/ApplicationLayer.csproj
dotnet add InfrastructureLayer/InfrastructureLayer.csproj reference DomainLayer/DomainLayer.csproj
dotnet add ProductsAPI/ProductsAPI.csproj reference ApplicationLayer/ApplicationLayer.csproj
dotnet add ProductsAPI/ProductsAPI.csproj reference InfrastructureLayer/InfrastructureLayer.csproj
dotnet add ProductsAPI.Tests/ProductsAPI.Tests.csproj reference ProductsAPI/ProductsAPI.csproj

dotnet build ProductManagement.sln --no-incremental
dotnet test ProductManagement.sln --no-build
```

## 5. Résultats de compilation

```text
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)
```

## 6. Tests exécutés

`ProductsAPI.Tests/Domain/ProductTests.cs` — **8 tests réussis** :

- prix valide → aucune exception ;
- prix nul → `InvalidOperationException` ;
- prix négatif → `InvalidOperationException` ;
- stock négatif → `InvalidOperationException` ;
- nom vide / espace / trop court → `InvalidOperationException` (3 cas) ;
- lecture/écriture des propriétés.

## 7. Erreurs rencontrées et corrections

| Erreur | Correction |
|---|---|
| Ancien projet `DomainLayer.fsproj` incohérent | Archivé, projet C# recréé |
| `Products.cs` au lieu de `Product.cs` | Renommé |
| `ValidateBusinessRules` ne validait pas le nom | Validation ajoutée |
| SDK 7 utilisé par défaut | `global.json` + `DOTNET_ROOT`/`PATH` |
| Ancienne solution à 3 projets | Solution reconstruite avec 5 projets |

## 8. Risques / réserves

- Visual Studio for Mac 17.6 ne supporte pas .NET 8 ; utiliser la CLI ou VS Code (voir `README.md`).
- Les restes F# sont archivés, pas supprimés définitivement.

## 9. AUTO-PASS

| Critère | Résultat | Preuve |
|---|---|---|
| 4 projets de production + tests | PASS | `dotnet sln list` |
| Références correctes | PASS | csproj |
| Domain sans EF/ASP.NET | PASS | aucun package dans `DomainLayer.csproj` |
| Compilation | PASS | 0 erreur, 0 avertissement |
| Tests Domain | PASS | 8/8 |
| Archivage de l'existant | PASS | `_archive/` |

**Statut : PASS**
