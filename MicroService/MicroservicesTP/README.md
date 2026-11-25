# Atelier 8 — Microservices ASP.NET Core

**Solution :** `MicroservicesTP.sln`
**Statut :** validé — build 0 erreur / 0 avertissement, 46 tests unitaires réussis, test d'idempotence sur broker réel réussi, E2E **66 PASS / 0 FAIL**, collection Postman vérifiée avec Newman (11 requêtes, 23 assertions).

Ce projet implémente l'atelier « Introduction aux Microservices avec ASP.NET Core » sur macOS Apple Silicon, avec deux API REST indépendantes, une Gateway Ocelot, SQLite et RabbitMQ natif (sans Docker).

---

## 1. Architecture

```text
                        curl / Swagger
                              |
                              v
                    ApiGateway (Ocelot) :7162
                    /apigateway/Products[/{id}]
                    /apigateway/Orders[/{id}]
                       /                    \
                      v                      v
          ProductsAPI :7066          OrdersAPI :7065
          Products.db                Orders.db
          table Products             tables Orders + Products
                      \                      ^
                       \                     |
             ProductCreated (MassTransit)   |
                       \                     |
                        v                    |
              RabbitMQ :5672                 |
              exchange fanout                |
              Shared.Messages:ProductCreated |
                        |                    |
                        v                    |
                 file event-listener --------+
                 ProductCreatedConsumer (idempotent)
```

- **ProductsAPI** possède exclusivement `Products.db`.
- **OrdersAPI** possède exclusivement `Orders.db` ; sa table `Products` est une projection locale alimentée par l'événement.
- Aucune jointure, aucun DbContext et aucun accès direct ne traversent les deux bases.
- La publication intervient **après** `SaveChangesAsync` ; un échec de publication renvoie `503` sans prétendre que l'événement a été publié.

---

## 2. Pile technique

| Composant | Version | Remarque |
|---|---|---|
| .NET SDK | **7.0.317** | `global.json` avec `rollForward: latestPatch` |
| Runtime ASP.NET Core | **7.0.20** | installé sur la machine |
| EF Core + SQLite | **7.0.20** | `Microsoft.EntityFrameworkCore.Sqlite` |
| MassTransit | **8.2.3** | + `MassTransit.RabbitMQ` 8.2.3 |
| Ocelot | **23.4.3** | `await app.UseOcelot()` |
| Swashbuckle | **6.6.2** | Swagger sur les deux API |
| RabbitMQ | **4.4.0** | natif Homebrew, Erlang/OTP 29.1.2 |
| xUnit / Moq | 2.9.2 / 4.20.72 | 47 tests |

> **Avertissement :** .NET 7 est hors support Microsoft depuis mai 2024. La version est imposée par le support et le SDK installé. Voir `ENVIRONMENT_AUDIT.md`.

---

## 3. Arborescence

```text
MicroservicesTP/
├── MicroservicesTP.sln
├── global.json
├── Directory.Build.props
├── .config/dotnet-tools.json        # dotnet-ef 7.0.20 (outil local)
├── Shared/
│   └── Messages/ProductCreated.cs   # contrat partagé
├── ProductsAPI/
│   ├── Models/Product.cs
│   ├── Data/ProductsAPIContext.cs
│   ├── Controllers/ProductsController.cs
│   ├── Migrations/
│   └── Products.db                  # généré, non versionné
├── OrdersAPI/
│   ├── Models/Orders.cs
│   ├── Models/Product.cs            # projection locale Id + Name
│   ├── Data/OrdersAPIContext.cs
│   ├── Consumer/ProductCreatedConsumer.cs
│   ├── Controllers/OrdersController.cs
│   ├── Migrations/
│   └── Orders.db                    # généré, non versionné
├── ApiGateway/
│   ├── Ocelot.json
│   └── Program.cs
├── Tests/                           # 47 tests xUnit
├── scripts/
│   ├── lib.sh
│   ├── start-all.sh
│   ├── stop-all.sh
│   ├── check-environment.sh
│   └── e2e.sh
├── docs/
│   ├── INSTALLATION_MACOS.md
│   ├── GUIDE_DEMARRAGE.md
│   ├── CONFORMITE_PDF.md
│   └── AUTO_PASS.md
├── postman/                         # collection Postman (PDF p. 51)
├── docker/                          # chemin Docker optionnel, non exécuté
├── logs/                            # journaux des exécutions réelles
├── README.md
├── ENVIRONMENT_AUDIT.md
├── TEST_RESULTS.md
└── RAPPORT_FINAL.md
```

Les documents d'analyse du support (audit des 52 pages, exigences, architecture, plan, plan de test) se trouvent dans le dossier parent `../`.

---

## 4. Démarrage rapide

### Prérequis

- macOS Apple Silicon avec Homebrew ;
- .NET SDK 7.0.x (`dotnet --list-sdks`) ;
- RabbitMQ installé nativement (`brew install rabbitmq`) ;
- `sqlite3`, `curl`, `python3` (fournis avec macOS).

Installation détaillée : [`docs/INSTALLATION_MACOS.md`](docs/INSTALLATION_MACOS.md).

### Commandes

```bash
cd MicroservicesTP

# 1. Restaurer l'outil EF local
dotnet tool restore

# 2. Vérifier l'environnement (lecture seule)
./scripts/check-environment.sh

# 3. Démarrer RabbitMQ si besoin, appliquer les migrations si besoin,
#    compiler et lancer les trois services
./scripts/start-all.sh

# 4. Arrêter les trois services (RabbitMQ reste actif)
./scripts/stop-all.sh

# 5. Validation finale lecture seule (bases, rapports, broker, ports)
./scripts/validate-final.sh
```

Le validateur final ne supprime rien, ne migre rien, ne redémarre rien et ne tue aucun processus.

### URL

| Service | URL | Documentation |
|---|---|---|
| ProductsAPI | `http://localhost:7066` | `http://localhost:7066/swagger` |
| OrdersAPI | `http://localhost:7065` | `http://localhost:7065/swagger` |
| ApiGateway | `http://localhost:7162` | routes `/apigateway/...` |
| RabbitMQ Management | `http://localhost:15672` | `guest` / `guest` (localhost) |

> La page « Overview » a été vérifiée **HTTP 200** le 18/11/2026, ainsi que `/api/queues/%2F/event-listener` (1 consumer, 0 message). Si le bug RabbitMQ 4.4.0 + Erlang/OTP 29 réapparaît, les pages Queues/Connections/Exchanges restent utilisables ; voir `ENVIRONMENT_AUDIT.md`.

### Tester avec Postman (option)

Importer `postman/MicroservicesTP.postman_collection.json` et `postman/MicroservicesTP.postman_environment.json`, puis sélectionner l'environnement **MicroservicesTP — Local**. Les dossiers 00 à 02 sont en lecture seule ; le dossier 03 ajoute un produit de test et vérifie la consommation RabbitMQ. Détails : [`postman/README.md`](postman/README.md).

### Broker Docker du PDF (option)

Le chemin Docker du support (conteneur `ecomm`, port 4001) est fourni dans `docker/docker-compose.rabbitmq.yml` mais **n'a pas été exécuté**. Le défaut reste le broker natif 5672. Détails : [`docker/README.md`](docker/README.md).

---

## 5. Endpoints

### Accès direct

| Méthode | ProductsAPI | OrdersAPI |
|---|---|---|
| GET | `/api/Products` | `/api/Orders` |
| GET | `/api/Products/{id}` | `/api/Orders/{id}` |
| POST | `/api/Products` | `/api/Orders` |
| PUT | `/api/Products/{id}` | `/api/Orders/{id}` |
| DELETE | `/api/Products/{id}` | `/api/Orders/{id}` |

### Via la Gateway Ocelot

| Méthode | Route |
|---|---|
| GET / POST | `/apigateway/Products` |
| GET / PUT / DELETE | `/apigateway/Products/{id}` |
| GET / POST | `/apigateway/Orders` |
| GET / PUT / DELETE | `/apigateway/Orders/{id}` |

Exemples `curl` : [`docs/GUIDE_DEMARRAGE.md`](docs/GUIDE_DEMARRAGE.md).

---

## 6. Tests et preuves

| Vérification | Commande | Résultat réel |
|---|---|---|
| Build | `dotnet build MicroservicesTP.sln` | **0 erreur, 0 avertissement** |
| Tests unitaires | `dotnet test MicroservicesTP.sln` | **46 réussis, 1 ignoré, 0 échec** |
| E2E complet | `./scripts/e2e.sh` | **66 PASS, 0 FAIL** |
| Idempotence broker réel | incluse dans `e2e.sh` | **1 test réussi**, 2 publications, 1 seule ligne |
| Synchronisation | incluse dans `e2e.sh` | produit présent dans `Products.db` et `Orders.db` |
| Propagation + contrats JSON | `./scripts/verify-propagation.sh` | **43 réussis, 1 avertissement, 0 échec** |
| Persistance | incluse dans `e2e.sh` | données intactes après redémarrage |

Le test xUnit utilisant un vrai broker est marqué `SKIP` lors d'un simple `dotnet test` : il est exécuté par `scripts/e2e.sh` avec `RABBITMQ_E2E=1`. Aucun résultat n'est simulé.

Rapports détaillés :

- [`reports/BUILD_REPORT.md`](reports/BUILD_REPORT.md) — build final, SDK, RID, code retour ;
- [`reports/UNIT_TEST_REPORT.md`](reports/UNIT_TEST_REPORT.md) — 47 cas, résultats réels et test ignoré ;
- [`reports/TEST_COVERAGE_AUDIT.md`](reports/TEST_COVERAGE_AUDIT.md) — décompte sans double comptage ;
- [`reports/E2E_FINAL_REPORT.md`](reports/E2E_FINAL_REPORT.md) — 66 contrôles et scénarios ;
- [`reports/PROPAGATION_VERIFICATION.md`](reports/PROPAGATION_VERIFICATION.md) — propagation Products → Orders et contrats JSON ;
- [`reports/INDEX_FINAL.md`](reports/INDEX_FINAL.md) — index des livrables et preuves ;
- [`reports/AUTO_PASS_REPORT.md`](reports/AUTO_PASS_REPORT.md) — verdict final ;
- [`TEST_RESULTS.md`](TEST_RESULTS.md) — synthèse des commandes et données observées ;
- [`docs/AUTO_PASS.md`](docs/AUTO_PASS.md) — évaluation critère par critère ;
- [`docs/CONFORMITE_PDF.md`](docs/CONFORMITE_PDF.md) — correspondance avec le PDF et écarts justifiés ;
- [`docs/ARCHITECTURE_EVOLUTION.md`](docs/ARCHITECTURE_EVOLUTION.md) — Outbox, retry, DLQ, idempotence HTTP ;
- [`RAPPORT_FINAL_CONFORMITE.md`](RAPPORT_FINAL_CONFORMITE.md) — rapport final de livraison ;
- [`RAPPORT_FINAL.md`](RAPPORT_FINAL.md) — première synthèse de réalisation.

Journaux du cycle final : `logs/build-final.log`, `logs/tests-final.log`, `logs/e2e-final.log`, `logs/e2e-idempotence.log`, `logs/environment.txt`. Les journaux de la session précédente sont conservés dans `logs/archive/`.

---

## 7. Choix et écarts principaux

| Sujet | PDF | Réalisation | Justification |
|---|---|---|---|
| Base de données | SQL Server | **SQLite** (`Products.db`, `Orders.db`) | Contrainte de la mission, aucune base externe |
| Broker | Docker `rabbitmq:3-management` | **RabbitMQ 4.4.0 natif Homebrew** | Contrainte finale : aucun Docker ; même AMQP 5672 / UI 15672 |
| Ocelot | 18.0.0, `UseOcelot().Wait()` | **23.4.3**, `await app.UseOcelot()` | Version compatible .NET 7, API asynchrone actuelle |
| Scheme Ocelot | `https` | **`http`** | Ports du PDF conservés, pas de certificat de développement |
| Routes Gateway | PUT/DELETE absents | **CRUD complet** | Exigence de la mission |
| Table ProductsAPI | DbSet `Product` | **table `Products`** | Contrainte de nommage de la mission |
| Contrat | `Shared.Models` | **`Shared.Messages`** | Structure cible demandée, contrat identique |
| Consumer | `Id` commenté | **`Id` métier + upsert idempotent** | Garantie anti-doublon |
| `OrderDate` | `{ get; }` | **`{ get; set; }`** | Sinon la date JSON n'est jamais désérialisée |
| Postman | p. 51 | **Collection + environnement fournis** | Test reproductible sans modifier le code |
| Docker / port 4001 | p. 35, 41 | **Compose optionnel fourni, non exécuté** | Contrainte « aucun Docker » ; natif 5672 reste le défaut |

Liste exhaustive : [`docs/CONFORMITE_PDF.md`](docs/CONFORMITE_PDF.md).

---

## 8. Limites connues

1. **Pas de pattern Outbox** : si RabbitMQ tombe entre `SaveChanges` et `Publish`, le produit est enregistré mais l'événement est perdu ; l'API renvoie `503` et l'incident est journalisé. Un Outbox est une amélioration hors périmètre.
2. **Pas d'événements `ProductUpdated` / `ProductDeleted`** : la projection `Orders.db` ne suit pas les PUT/DELETE de ProductsAPI. Le CRUD reste correct dans chaque service.
3. **Page Overview RabbitMQ** : vérifiée HTTP 200 le 18/11/2026. La réserve historique (bug RabbitMQ 4.4.0 + OTP 29) est conservée dans les rapports figés ; les autres endpoints de gestion fonctionnent.
4. **Date SQLite** : `OrderDate` est stockée comme texte ; le suffixe `Z` n'est pas restitué après lecture. Comportement SQLite/EF Core documenté.
5. **.NET 7 hors support** : contrainte du support ; une migration vers .NET 8/9 serait nécessaire en production.
6. **Pas d'authentification, de health checks, de retries Ocelot** : améliorations optionnelles non demandées.
7. **Pas de clé d'idempotence HTTP** : un client qui répète un `POST` après un `503` peut créer un second produit. Le `503` signifie « produit enregistré, événement non publié » ; il ne doit pas être traité comme « rien n'a été fait ». Analyse dans `docs/ARCHITECTURE_EVOLUTION.md`.
8. **Pas de contrainte FK sur `Orders.ProductId`** : le PDF ne définit aucune relation en base entre une commande et la projection locale `Products`. Une commande peut donc référencer un `productId` absent de la projection si le produit n'a jamais été consommé (ou a été créé avant l'abonnement). Le script `scripts/verify-propagation.sh` signale ce cas en avertissement, sans le corriger. Détail : [`reports/PROPAGATION_VERIFICATION.md`](reports/PROPAGATION_VERIFICATION.md).

---

## 9. Données de test conservées

> **Snapshot des rapports :** 18/11/2026, 13:11. L'état courant des bases peut différer si des tests manuels ont été exécutés depuis. Les compléments d'outillage (Postman, Docker) n'ont effectué aucune écriture.

Les exécutions E2E ont laissé des lignes volontairement conservées comme preuve :

- `Products.db` : produits `1, 2, 3, 5, 6, 8, 9, 11, 12` (9 lignes) ;
- `Orders.db` : projections `1` à `13`, dont les produits synchronisés `11` (`E2E-Sync-20261118-131016`) et `12` (`E2E-Idem-20261118-131016-updated`) ; la projection `13` correspond au produit CRUD supprimé via l'API (aucun événement `ProductDeleted`).

**État observé le 18/11/2026 après le loop E2E :** `Products.db` = **13 produits** (dernier : `17 — Gateway-Test-Product`), `Orders.db` = **17 projections** et **3 commandes**. Les identifiants `14` à `17` proviennent des tests du loop et de la vérification de propagation. La commande `8` référence `productId=18`, absent de la projection : c'est la réserve documentée au §8.8, aucune donnée n'a été supprimée ni réinitialisée.

Ne supprimer ces lignes qu'avec l'accord du responsable du poste. La procédure de réinitialisation est documentée dans `docs/GUIDE_DEMARRAGE.md` (§8) avec un avertissement explicite.
