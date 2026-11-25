# LOOP-06 — Tests et validation finale

| Champ | Valeur |
|---|---|
| Date | 18 novembre 2026 |
| Statut | **PASS_WITH_RESERVATIONS** |
| Périmètre | Restore, build, tests, API, Swagger, CRUD complet, persistance |
| Prérequis | LOOP-05 PASS_WITH_RESERVATIONS |
| Réserve principale | SQLite local au lieu de SQL Server LocalDB |

---

## 1. Éléments attendus

- `dotnet restore`, `dotnet build`, `dotnet test` exécutés.
- API démarrée.
- Swagger accessible.
- Tous les scénarios HTTP obligatoires validés.
- Persistance vérifiée.
- Logs sans erreur.

## 2. Commandes exécutées

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export ASPNETCORE_ENVIRONMENT=Development

dotnet restore ProductManagement.sln
dotnet build ProductManagement.sln --no-incremental
dotnet test ProductManagement.sln --no-build

dotnet run --project ProductsAPI/ProductsAPI.csproj --urls "http://localhost:5080"
./scripts/smoke-test.sh
```

## 3. Résultats de compilation

```text
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)
```

## 4. Résultats des tests automatisés

```text
Réussi!  - échec : 0, réussite : 29, ignorée(s) : 0, total : 29, durée : 1 s
```

Répartition :

| Suite | Nombre | Résultat |
|---|---:|---|
| Domain — règles métier | 8 | 8/8 |
| Application — service CRUD | 9 | 9/9 |
| API — intégration HTTP + Swagger | 12 | 12/12 |
| **Total** | **29** | **29/29** |

Les tests d'intégration utilisent une base SQLite temporaire isolée par test et appliquent la migration réelle.

## 5. Scénarios HTTP exécutés

Résultat du script `scripts/smoke-test.sh` :

```text
=== 1. Swagger ===
PASS  GET /swagger/index.html (obtenu: 200)
PASS  GET /swagger/v1/swagger.json (obtenu: 200)
PASS  swagger.json contient /api/Product
=== 2. GET tous les produits ===
PASS  GET /api/Product (obtenu: 200)
PASS  GET /api/Product retourne 3 produits (obtenu: 3)
=== 3. GET par id ===
PASS  GET /api/Product/1 (obtenu: 200)
PASS  Le produit 1 est Laptop (obtenu: Laptop)
PASS  GET /api/Product/99999 (obtenu: 404)
=== 4. POST produit valide ===
PASS  POST /api/Product valide (obtenu: 201)
PASS  POST retourne un identifiant (obtenu: 4)
=== 5. POST produit invalide ===
PASS  POST /api/Product prix négatif (obtenu: 400)
PASS  POST /api/Product prix nul (obtenu: 400)
=== 6. PUT produit existant ===
PASS  PUT /api/Product/1 (obtenu: 204)
PASS  Persistance du PUT (nom) (obtenu: Laptop Pro)
=== 7. PUT produit inexistant ===
PASS  PUT /api/Product/99999 (obtenu: 404)
=== 8. DELETE produit temporaire ===
PASS  DELETE /api/Product/4 (obtenu: 204)
PASS  GET produit supprimé (obtenu: 404)
=== 9. DELETE produit inexistant ===
PASS  DELETE /api/Product/99999 (obtenu: 404)
=== 10. Restauration de l'état initial ===
PASS  PUT /api/Product/1 (restauration) (obtenu: 204)
PASS  GET /api/Product final (obtenu: 200)
PASS  Nombre final de produits (obtenu: 3)
PASS  Produit 1 restauré (obtenu: Laptop)

Résultat : 22 PASS, 0 FAIL
```

## 6. Persistance et état final

Après les tests, `GET /api/Product` retourne exactement les 3 produits seedés :

```json
[
  { "id": 1, "name": "Laptop", "description": "High-end gaming laptop", "price": 1500, "stock": 10 },
  { "id": 2, "name": "Smartphone", "description": "Latest model smartphone", "price": 800, "stock": 25 },
  { "id": 3, "name": "Wireless Headphones", "description": "Noise cancelling headphones", "price": 200, "stock": 40 }
]
```

Le produit temporaire créé par le smoke test (id 4) a été supprimé et le produit 1 a été restauré à ses valeurs initiales.

## 7. Logs

Extrait des logs de l'API :

```text
GET    /api/Product                → 200
GET    /api/Product/1              → 200
GET    /api/Product/99999          → 404
POST   /api/Product                → 201
POST   /api/Product (invalide)     → 400
PUT    /api/Product/1              → 204
PUT    /api/Product/99999          → 404
DELETE /api/Product/4              → 204
DELETE /api/Product/99999          → 404
```

Recherche d'erreurs dans les logs : aucune occurrence de `fail:`, `Unhandled` ou `Exception`.

## 8. Vérifications complémentaires

| Vérification | Résultat |
|---|---|
| Sérialisation JSON camelCase | PASS (`id`, `name`, `price`, `stock`) |
| DI (repository, service, DbContext) | PASS (API démarre et répond) |
| Migration appliquée au démarrage | PASS (« database is already up to date ») |
| Swagger JSON contient les routes | PASS |
| Middleware 404/400 | PASS |
| Persistance après PUT/DELETE | PASS |

## 9. Erreurs rencontrées et corrections

| Erreur | Correction |
|---|---|
| Tests d'intégration : `no such table: Products` | `WebApplicationFactory` capture l'hôte à `Build()` ; ajout de `CreateHost` avec `Database.Migrate()` dans `ApiFactory` |
| Avertissement CS8981 sur la migration | Classe renommée `Dbcreate`, ID conservé |
| Ports 5000/7000 occupés | Port 5080 |

## 10. Réserves

1. **Base :** SQLite local au lieu de SQL Server LocalDB (LocalDB Windows-only, Docker exclu). Le script SQL Server `docs/ProductManagement.sqlserver.sql` est fourni mais n'a pas été exécuté.
2. **IDE :** Visual Studio for Mac 17.6 ne supporte pas .NET 8 ; validation faite via CLI et compatible VS Code.
3. **Migration :** spécifique au provider ; régénération nécessaire pour SQL Server.

## 11. AUTO-PASS

| Critère | Résultat | Preuve |
|---|---|---|
| Restore | PASS | commande exécutée |
| Build | PASS | 0 erreur, 0 avertissement |
| Tests | PASS | 29/29 |
| API démarre | PASS | port 5080 |
| Swagger | PASS | index + JSON 200 |
| GET liste + 3 produits | PASS | smoke test |
| GET par id + 404 | PASS | smoke test |
| POST 201 + 400 | PASS | smoke test |
| PUT 204 + 404 | PASS | smoke test |
| DELETE 204 + 404 | PASS | smoke test |
| Persistance | PASS | vérifiée |
| Logs propres | PASS | aucune erreur |
| Base identique au PDF | **RÉSERVE** | SQLite local |

**Statut : PASS_WITH_RESERVATIONS** — toutes les fonctionnalités sont vérifiées ; seule la réserve de fournisseur de base subsiste.
