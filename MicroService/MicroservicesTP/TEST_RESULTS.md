# RÉSULTATS DE TEST — Atelier 8

**Date d'exécution :** 18 novembre 2026
**Machine :** Apple M2, macOS 27.3, .NET SDK 7.0.317
**Solution :** `MicroservicesTP.sln`

Tous les résultats ci-dessous proviennent de commandes réellement exécutées. Le **cycle final du 18 novembre 2026 (13:09–13:11 CET)** a réexécuté le build, les 47 cas xUnit et les 66 contrôles E2E ; les rapports détaillés sont dans `reports/`. Les journaux de la session précédente sont conservés dans `logs/archive/` et ne servent plus de preuve courante.

---

## 1. Synthèse

| Vérification | Résultat | Preuve |
|---|---|---|
| Build solution | **0 erreur, 0 avertissement** | `logs/build-final.log` |
| Tests unitaires | **46 réussis, 1 ignoré, 0 échec** (47) | `logs/tests-final.log` + `reports/TestResults/complete-tests.trx` |
| E2E HTTP + SQLite + RabbitMQ | **66 PASS, 0 FAIL** | `logs/e2e-final.log` |
| Idempotence broker réel | **1 réussi** (2 publications, 1 ligne) | `logs/e2e-idempotence.log` |
| Migration ProductsAPI | table `Products` créée | `sqlite3 ProductsAPI/Products.db ".schema Products"` |
| Migration OrdersAPI | tables `Orders` et `Products` créées | `sqlite3 OrdersAPI/Orders.db ".schema Orders"` |
| Persistance après redémarrage | vérifiée | section 7 de l'E2E |
| Indépendance des services | vérifiée | section 8 de l'E2E |

---

## 2. Build

```bash
cd MicroservicesTP
dotnet build MicroservicesTP.sln -v minimal
```

Extrait réel :

```text
La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)
```

Journal complet : `logs/build-final.log`.

---

## 3. Tests unitaires et d'intégration

### Commande

```bash
dotnet test MicroservicesTP.sln --no-build
```

### Résultat réel

```text
Réussi!  - échec : 0, réussite : 46, ignorée(s) : 1, total : 47, durée : 457 ms
```

Le test ignoré est `RabbitMqIdempotenceTests.Rejouer_le_meme_evenement_ne_cree_pas_de_doublon_dans_Orders_db`. Il nécessite un broker réel et les variables `RABBITMQ_E2E=1`, `ORDERS_DB_PATH`, `IDEMPOTENCE_PRODUCT_ID`, `IDEMPOTENCE_PRODUCT_NAME`. Il est exécuté par `scripts/e2e.sh` (voir §5). Il n'est jamais compté comme réussi sans exécution.

### Répartition

| Fichier de test | Cas | Contenu |
|---|---:|---|
| `ProductsControllerTests` | 8 | POST + publication après sauvegarde, 503 si publication échoue, GET liste/404, PUT 204/404, DELETE 204/404 |
| `OrdersControllerTests` | 6 | POST, GET liste, GET 404, PUT, DELETE, DELETE 404 |
| `ProductCreatedConsumerTests` | 6 | création, rejeu identique, mise à jour sans doublon, deux produits, ID invalide, nom vide |
| `ModelValidationTests` | 10 | Data Annotations Product/Orders (nom, stock, prix, longueurs, ProductId) |
| `SqlitePersistenceTests` | 1 | persistance réelle sur fichier, relecture après fermeture du contexte |
| `OcelotConfigurationTests` | 15 | 4 routes, ports/scheme, 10 combinaisons méthode/route |
| `RabbitMqIdempotenceTests` | 1 | **ignoré** hors E2E, réussi dans l'E2E |
| **Total** | **47** | **46 réussis + 1 ignoré** |

---

## 4. E2E — 66 contrôles

### Commande

```bash
./scripts/e2e.sh
```

Le script vérifie les prérequis, démarre les services, teste la topologie RabbitMQ, crée des produits via la Gateway, vérifie la synchronisation dans les deux bases, rejoue un événement sur le vrai broker, exécute le CRUD complet, redémarre les services et vérifie l'indépendance des microservices.

### Résultat réel

```text
Checks PASS : 66
Checks FAIL : 0

Tous les contrôles ont réussi.
Données de test conservées : produit 11 (E2E-Sync-20261118-131016), produit 12 (E2E-Idem-20261118-131016-updated).
```

### Détail par section

| Section | PASS | FAIL | Points vérifiés |
|---|---:|---:|---|
| 0. Prérequis | 7 | 0 | commandes, RabbitMQ AMQP, API de gestion, ports libres, bases et tables |
| 1. Démarrage | 4 | 0 | 3 services + Swagger/Gateway |
| 2. Topologie RabbitMQ | 3 | 0 | exchange fanout durable, 1 consumer, 2 connexions |
| 3. Synchronisation | 7 | 0 | POST 201, ID, `Products.db`, projection `Orders.db`, file vide, GET, nom |
| 4. Idempotence | 5 | 0 | POST, projection initiale, test xUnit broker réel, 1 ligne, nom mis à jour |
| 5. CRUD Products via Gateway | 13 | 0 | POST/GET/PUT/DELETE, 204/404, validations 400, route inconnue 404 |
| 6. CRUD Orders via Gateway | 10 | 0 | POST/GET/PUT/DELETE, 204/404, validation 400 |
| 7. Persistance | 8 | 0 | fichiers et données après arrêt/redémarrage |
| 8. Indépendance | 9 | 0 | Orders sans Products, 502 sans faux succès, Products sans Orders, isolation des `Data Source` |
| **Total** | **66** | **0** | |

Extraits marquants réellement observés :

- `ProductCreated consommé : projection présente dans Orders.db.`
- `File event-listener avec exactement 1 consumer (observé=1)`
- `Connexions MassTransit actives vers RabbitMQ (observé=2, attendu>=2)`
- `Gateway ne renvoie pas de faux succès quand ProductsAPI est arrêté (HTTP 502).`
- `ProductsAPI configuré uniquement sur Products.db (observé=Data Source=Products.db)`
- `OrdersAPI configuré uniquement sur Orders.db (observé=Data Source=Orders.db)`

Journal complet : `logs/e2e-final.log`.

---

## 5. Idempotence sur broker réel

Le test `RabbitMqIdempotenceTests` :

1. lit la projection initiale créée par le POST HTTP réel de l'E2E ;
2. démarre un bus MassTransit réel ;
3. publie `ProductCreated { Id = 12, Name = "E2E-Idem-...-updated" }` ;
4. attend la mise à jour de `Orders.db` ;
5. publie à nouveau le même message ;
6. vérifie qu'il n'existe **qu'une seule ligne** et que le nom est mis à jour.

Résultat réel :

```text
Réussi MicroservicesTP.Tests.RabbitMqIdempotenceTests.Rejouer_le_meme_evenement_ne_cree_pas_de_doublon_dans_Orders_db [8 s]
Réussi(s) : 1
```

Contrôles SQLite associés dans l'E2E :

```text
Une seule ligne locale après rejeu (observé=1)
Nom mis à jour par le rejeu (sans doublon) (observé=E2E-Idem-20261118-131016-updated)
```

Journal : `logs/e2e-idempotence.log`.

---

## 6. Preuves de persistance et d'isolation

### Schémas réellement créés

```sql
-- ProductsAPI/Products.db
CREATE TABLE "Products" (
    "ProductId" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "StockQte" INTEGER NOT NULL,
    "Price" REAL NOT NULL
);

-- OrdersAPI/Orders.db
CREATE TABLE "Orders" (
    "OrdersId" INTEGER NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY AUTOINCREMENT,
    "OrderDate" TEXT NOT NULL,
    "CustomerName" TEXT NULL,
    "ProductId" INTEGER NOT NULL
);
CREATE TABLE "Products" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY,
    "Name" TEXT NOT NULL
);
```

La table `Products` d'OrdersAPI a `Id` en clé primaire **sans AUTOINCREMENT** : l'identifiant vient du message, ce qui garantit l'unicité métier.

### Données après les exécutions

```text
Products.db (9 lignes) :
  1  Test manuel modifié
  2  E2E-Sync-20261118-124013
  3  E2E-Idem-20261118-124013
  5  E2E-Sync-20261118-124729
  6  E2E-Idem-20261118-124729
  8  E2E-Sync-20261118-124822
  9  E2E-Idem-20261118-124822
 11  E2E-Sync-20261118-131016
 12  E2E-Idem-20261118-131016

Orders.db — projection Products (13 lignes) :
  1..13, dont 11 = E2E-Sync-20261118-131016
             et 12 = E2E-Idem-20261118-131016-updated
             et 13 = E2E-Crud-20261118-131016 (produit supprimé, projection conservée)

Orders.db — table Orders : 0 ligne
  (la commande créée par l'E2E a été supprimée par le test DELETE)
```

Les produits `4`, `7`, `10` et `13` ont été supprimés par les tests DELETE de la Gateway ; leurs projections locales restent dans `Orders.db`, ce qui est le comportement attendu en l'absence d'événement `ProductDeleted` (limite documentée).

### Isolation vérifiée

- `ProductsAPI/appsettings.json` : `Data Source=Products.db`, aucun `Orders.db`.
- `OrdersAPI/appsettings.json` : `Data Source=Orders.db`, aucun `Products.db`.
- Les seules occurrences de l'autre base dans le code sont des **commentaires** expliquant l'isolation ; le contrôle E2E porte sur les chaînes `Data Source` réellement quotées.
- OrdersAPI a répondu `200` après arrêt de ProductsAPI, et inversement.

---

## 7. Ce qui n'a pas été testé

Pour rester honnête, les points suivants ne sont **pas** couverts par les preuves ci-dessus :

| Point | Statut | Commentaire |
|---|---|---|
| Coupure réelle de RabbitMQ pendant un POST | **NON EXÉCUTÉ** | Le chemin `503` est couvert par un test unitaire avec `IPublishEndpoint` mocké, pas par une coupure réelle du broker. |
| Livraisons concurrentes du même message | **NON EXÉCUTÉ en stress** | La clé primaire et la gestion de `DbUpdateException` protègent le cas, mais aucun test de charge concurrent n'a été lancé. |
| Charge / performance | **NON EXÉCUTÉ** | Hors périmètre du TP. |
| Authentification, TLS, JWT | **NON IMPLÉMENTÉ** | Amélioration optionnelle. |
| Page Overview RabbitMQ | **ÉCHEC ENVIRONNEMENTAL** | Bug RabbitMQ 4.4.0 + OTP 29 ; les autres endpoints de gestion fonctionnent. |
| Mise à jour de la projection après PUT/DELETE produit | **NON IMPLÉMENTÉ** | Pas d'événements `ProductUpdated`/`ProductDeleted` ; limite documentée. |
| Outbox transactionnel | **NON IMPLÉMENTÉ** | Amélioration optionnelle ; l'échec de publication renvoie `503`. |

---

## 8. Données de test

Les données E2E sont conservées volontairement comme preuve. Elles ne doivent pas être supprimées sans accord. La procédure de réinitialisation, destructive, est documentée dans `docs/GUIDE_DEMARRAGE.md` §8 avec un avertissement.

---

## 9. Index des preuves

| Fichier | Contenu |
|---|---|
| `logs/build-final.log` | build final, 0 erreur / 0 avertissement |
| `logs/tests-final.log` | 46 réussis, 1 ignoré, 0 échec |
| `logs/e2e-final.log` | 66 PASS / 0 FAIL, détail des sections |
| `reports/TestResults/complete-tests.trx` | résultats xUnit machine |
| `reports/BUILD_REPORT.md` | rapport de build |
| `reports/UNIT_TEST_REPORT.md` | rapport de tests |
| `reports/TEST_COVERAGE_AUDIT.md` | décompte et couverture |
| `reports/E2E_FINAL_REPORT.md` | rapport E2E |
| `logs/e2e-idempotence.log` | test broker réel réussi |
| `logs/environment.txt` | versions, ports, packages, système |
| `logs/ProductsAPI.log`, `logs/OrdersAPI.log`, `logs/ApiGateway.log` | journaux des services |
| `logs/e2e-start.log` | démarrages/redémarrages de l'E2E |
