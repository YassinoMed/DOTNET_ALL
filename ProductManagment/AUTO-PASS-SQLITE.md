# AUTO-PASS SQLite — clôture des partiels

| Champ | Valeur |
|---|---|
| Date | 18 novembre 2026 |
| Objectif | Passer les partiels en auto-pass / auto-validation / auto-correction avec **SQLite** comme base cible |
| Commande | `./scripts/auto-validate.sh` |
| Résultat | **9/9 portes PASS — 0 FAIL** |
| Statut périmètre SQLite | **PASS** |
| Statut PDF strict | **PASS_WITH_RESERVATIONS** (SQL Server LocalDB non exécuté) |

---

## 1. Méthode AUTO appliquée

Chaque partiel est traité par une boucle fermée :

```text
Définir la porte de validation
        ↓
Exécuter la preuve réelle
        ↓
PASS ──► clôturer le partiel
FAIL ──► corriger la cause
        ↓
Rejouer la même porte (3 tentatives max)
        ↓
FAIL persistant ──► documenter le blocage, ne pas masquer
```

Une porte n'est jamais validée par déclaration : elle exige une commande, une sortie et un code de retour.

---

## 2. Portes exécutées

| # | Porte | Preuve | Résultat |
|---|---|---|---|
| 1 | Restore | `dotnet restore ProductManagement.sln` | **PASS** |
| 2 | Build | 0 erreur, 0 avertissement | **PASS** |
| 3 | Tests | 29/29 réussis | **PASS** |
| 4 | Base neuve + migration | API démarre, migration `dbcreate` appliquée automatiquement | **PASS** |
| 5 | GET liste | `200` + 3 produits | **PASS** |
| 6 | Seed SQLite | `SELECT COUNT(*) FROM Products` = 3 | **PASS** |
| 7 | Historique migration | 1 ligne dans `__EFMigrationsHistory` | **PASS** |
| 8 | Smoke test HTTP | 22 PASS, 0 FAIL | **PASS** |
| 9 | Logs API | aucune occurrence `fail:` / `Unhandled exception` | **PASS** |

Sortie finale :

```text
==============================================
 RÉSULTAT AUTO-PASS : 9 PASS, 0 FAIL
==============================================
✅ AUTO-PASS SQLITE — toutes les portes sont vertes.
```

La porte 4 utilise une **base SQLite temporaire neuve** : elle prouve que la migration, le schéma et le seed sont recréés de zéro, sans dépendre de la base de développement existante.

---

## 3. Couverture de tests

Commande :

```bash
dotnet test ProductManagement.sln --no-build \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults
```

Résultat :

| Périmètre | Couverture lignes |
|---|---:|
| **Global** | **80,4 %** (373 / 464) |
| ApplicationLayer | 100,0 % |
| DomainLayer | 94,1 % |
| ProductsAPI | 81,2 % |
| InfrastructureLayer | 73,4 % |

Couverture branches : **75,5 %** (37 / 49).

Le partiel « rapport de couverture non généré » est donc **clôturé**.

---

## 4. Matrice de clôture des partiels

| Partiel initial | Décision | Preuve | Statut |
|---|---|---|---|
| SQL Server / LocalDB non exécuté | **SQLite est la cible locale officielle** du projet sur macOS ; le provider SQL Server reste supporté par configuration | base SQLite recréée, 3 produits, 22 checks HTTP ; script SQL Server dans `docs/` | **CLÔTURÉ pour le périmètre SQLite** — réserve PDF strict |
| Rapport de couverture absent | Ajout de `coverlet.collector` déjà présent + exécution réelle | 80,4 % lignes, 75,5 % branches | **CLÔTURÉ** |
| Visual Studio for Mac incompatible .NET 8 | Aucun correctif code possible : VS Mac 17.6 s'arrête à .NET 7 | build/tests via CLI, solution standard, VS Code disponible | **BLOQUÉ ENVIRONNEMENT** — documenté |
| Commit Git non effectué | Hors périmètre du TP, laissé à la décision de l'utilisateur | `git status` | **NON APPLICABLE** |

### Pourquoi SQL Server n'est plus un échec

SQLite a été explicitement choisi pour l'exécution locale :

- LocalDB est un composant Windows uniquement ;
- Docker est exclu ;
- aucune instance SQL Server distante n'est fournie ;
- le modèle EF, le seed, les règles métier et le CRUD sont indépendants du provider ;
- un script SQL Server équivalent est généré dans `docs/ProductManagement.sqlserver.sql`.

La réserve restante est donc **documentée et bornée** : elle concerne le moteur, pas le code applicatif.

---

## 5. Auto-corrections réalisées pendant le projet

| Problème détecté | Correction | Porte rejouée |
|---|---|---|
| `ProductService` avec méthodes hors classe | Classe réécrite | Build + 9 tests |
| `IProductRepository` contenant `IProductService` | Interface réécrite | Build |
| `DeleteProductAsync` sans vérification | `KeyNotFoundException` + 404 | Tests API |
| Tests d'intégration : `no such table: Products` | `ApiFactory.CreateHost` applique `Database.Migrate()` | 29/29 |
| Avertissement CS8981 sur `dbcreate` | Classe renommée `Dbcreate`, ID conservé | Build 0 avertissement |
| Ports 5000/7000 occupés | Port 5080 / 5082 | Smoke test |
| Couverture absente | Collecte XPlat + rapport | 80,4 % |

Aucune correction n'a été nécessaire pendant la dernière exécution : les 9 portes sont passées du premier coup.

---

## 6. Relancer la validation

```bash
cd ProductManagment
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

./scripts/auto-validate.sh
```

Le script :

- utilise une base SQLite temporaire ;
- démarre l'API sur le port 5082 ;
- exécute les 22 vérifications HTTP ;
- vérifie le seed et l'historique de migration ;
- arrête l'API et nettoie les fichiers temporaires ;
- retourne un code de sortie non nul si une porte échoue.

---

## 7. Conclusion

Avec **SQLite comme base cible**, tous les partiels applicatifs sont clôturés :

- build propre ;
- 29 tests réussis ;
- base neuve migrée et seedée ;
- CRUD complet validé ;
- Swagger opérationnel ;
- couverture mesurée ;
- logs propres.

Les seuls points hors périmètre restants sont l'exécution SQL Server/LocalDB (Windows) et la compatibilité Visual Studio for Mac (.NET 8 non supporté).

**Statut AUTO-PASS SQLite : PASS**
**Statut PDF strict : PASS_WITH_RESERVATIONS**
