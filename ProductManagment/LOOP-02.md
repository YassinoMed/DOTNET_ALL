# LOOP-02 — ApplicationLayer

| Champ | Valeur |
|---|---|
| Date | 18 novembre 2026 |
| Statut | **PASS** |
| Périmètre | DTO, interfaces Repository/Service, `ProductService` |
| Prérequis | LOOP-01 PASS |

---

## 1. Éléments attendus

- `ProductDTO`, `CreateProductDTO`, `UpdateProductDTO`.
- `IProductRepository` correctement nommée et typée.
- `IProductService` avec les 5 opérations CRUD.
- `ProductService` complet, asynchrone, avec mapping manuel.
- Validation métier et gestion d'un produit inexistant sans HTTP 500.
- Aucune dépendance EF Core / ASP.NET Core.
- Tests Application.

## 2. Éléments réalisés

- DTO nettoyés :
  - `ProductDTO` : Id, Name, Description, Price, Stock ;
  - `CreateProductDTO` : `[Required]`, `[StringLength(100, MinimumLength = 2)]`, description ≤ 500, prix > 0, stock ≥ 0 ;
  - `UpdateProductDTO` : mêmes règles + `Id` ≥ 1.
- `IProductRepository` réécrite :
  ```csharp
  Task<IEnumerable<Product>> GetAllAsync();
  Task<Product?> GetByIdAsync(int id);
  Task<Product> AddAsync(Product product);
  Task UpdateAsync(Product product);
  Task DeleteAsync(int id);
  ```
  (l'ancien fichier déclarait par erreur `IProductService`).
- `IProductService` conservée avec les signatures du PDF.
- `ProductService` réécrit :
  - toutes les méthodes sont **dans la classe** (l'ancien code les déclarait au niveau namespace → CS0116) ;
  - usings corrects (`ApplicationLayer.Interfaces.Repositories`, `ApplicationLayer.Interfaces.Services`, `DomainLayer.Entities`) ;
  - mapping manuel `Product → ProductDTO` ;
  - `AddProductAsync` valide puis persiste ;
  - `UpdateProductAsync` lève `KeyNotFoundException` si absent ;
  - `DeleteProductAsync` vérifie l'existence avant suppression ;
  - aucune exception générique `Exception("Product not found.")`.
- Pas d'AutoMapper (conforme au TP).

## 3. Fichiers créés / modifiés

```text
ApplicationLayer/DTOs/ProductDTO.cs
ApplicationLayer/DTOs/CreateProductDTO.cs
ApplicationLayer/DTOs/UpdateProductDTO.cs
ApplicationLayer/Interfaces/Repositories/IProductRepository.cs
ApplicationLayer/Interfaces/Services/IProductService.cs
ApplicationLayer/Services/ProductService.cs
ProductsAPI.Tests/Application/ProductServiceTests.cs
```

## 4. Commandes exécutées

```bash
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

`ProductsAPI.Tests/Application/ProductServiceTests.cs` — **9 tests réussis** :

- récupération de tous les produits + mapping ;
- récupération par id existant ;
- récupération par id inconnu → `null` ;
- ajout valide → persisté + DTO ;
- ajout avec prix invalide → exception, aucune persistance ;
- modification d'un produit existant ;
- modification d'un id inconnu → `KeyNotFoundException` ;
- suppression d'un produit existant ;
- suppression d'un id inconnu → `KeyNotFoundException`.

## 7. Erreurs rencontrées et corrections

| Erreur | Correction |
|---|---|
| `IProductRepository.cs` contenait `IProductService` | Interface réécrite |
| Méthodes hors de la classe dans `ProductService` | Classe réécrite complète |
| Usings manquants | Ajoutés |
| `throw new Exception(...)` | `KeyNotFoundException` |
| `DeleteProductAsync` ne vérifiait pas l'existence | Vérification + 404 via middleware |
| `AddProductAsync` pouvait retourner `null` sans raison | Retour systématique du produit créé |

## 8. Risques / réserves

- `AddProductAsync` conserve la signature nullable du PDF (`ProductDTO?`) ; le contrôleur utilise `created!` après un ajout réussi.
- La validation domaine utilise `InvalidOperationException`, convertie en 400 par le middleware.

## 9. AUTO-PASS

| Critère | Résultat | Preuve |
|---|---|---|
| DTO conformes | PASS | fichiers DTO |
| Interfaces correctes | PASS | `IProductRepository`, `IProductService` |
| Service complet et compilable | PASS | build 0 erreur |
| Produit inexistant sans 500 | PASS | tests + middleware 404 |
| Tests Application | PASS | 9/9 |
| Pas d'EF Core dans Application | PASS | aucune référence package |

**Statut : PASS**
