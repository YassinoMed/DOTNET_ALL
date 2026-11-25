# CONFORMITÉ AU SUPPORT PDF — Atelier 8

**Support :** `Atelier 8 Introduction aux Microservices avec ASP.NET Core.pdf` (52 pages)
**Solution :** `MicroservicesTP`
**Date :** 18 novembre 2026 (mise à jour : artefacts Postman et Docker optionnel)

Ce document confronte chaque exigence du support et de la mission aux choix réellement implémentés. Les statuts utilisés sont :

- **CONFORME** : exigence satisfaite et vérifiée ;
- **CONFORME AVEC ÉCART** : fonctionnalité satisfaite, mise en œuvre différente et justifiée ;
- **PARTIEL** : satisfait sur le périmètre applicatif, limite d'environnement documentée ;
- **NON APPLICABLE** : exigence liée à un choix abandonné (Docker, Visual Studio) ;
- **NON IMPLÉMENTÉ (option)** : amélioration hors périmètre, explicitement facultative.

---

## 1. Synthèse

| Domaine | Statut global |
|---|---|
| ProductsAPI | **CONFORME** |
| OrdersAPI | **CONFORME** |
| ApiGateway Ocelot | **CONFORME** |
| Shared et MassTransit | **CONFORME** |
| Synchronisation et idempotence | **CONFORME** |
| SQLite et isolation des bases | **CONFORME** |
| Environnement macOS / .NET 7 | **CONFORME** |
| Broker RabbitMQ | **CONFORME AVEC ÉCART** (natif au lieu de Docker) |
| Interface de gestion RabbitMQ | **CONFORME** (Overview HTTP 200 vérifié le 18/11/2026) |
| Tests et preuves | **CONFORME** |
| Améliorations optionnelles | **NON IMPLÉMENTÉ (option)** |

Aucune fonctionnalité pédagogique du support n'a été supprimée.

---

## 2. Exigences fonctionnelles

### 2.1 ProductsAPI

| ID | Exigence | Mise en œuvre | Preuve | Statut |
|---|---|---|---|---|
| F-PROD-01 | Projet `ProductsAPI` | `ProductsAPI/ProductsAPI.csproj` | build | CONFORME |
| F-PROD-02 | `Product` : `ProductId`, `Name`, `StockQte`, `Price` | `Models/Product.cs` | code + tests | CONFORME |
| F-PROD-03 | `ProductsAPIContext` dans `Data/` | `Data/ProductsAPIContext.cs` | build | CONFORME |
| F-PROD-04 | `DbSet<Product> Products` | `DbSet<Product> Products` | migration | CONFORME |
| F-PROD-05 | `ProductsController`, route `api/[controller]` | `Controllers/ProductsController.cs` | Swagger | CONFORME |
| F-PROD-06 | `GET /api/Products` | implémenté | E2E + tests | CONFORME |
| F-PROD-07 | `GET /api/Products/{id}` | implémenté | E2E + tests | CONFORME |
| F-PROD-08 | `POST /api/Products` | implémenté | E2E + tests | CONFORME |
| F-PROD-09 | `PUT /api/Products/{id}` | implémenté | E2E + tests | CONFORME |
| F-PROD-10 | `DELETE /api/Products/{id}` | ajouté (absent du PDF) | E2E + tests | CONFORME |
| F-PROD-11 | Publier `ProductCreated` après sauvegarde | `Publish` après `SaveChangesAsync` | test unitaire | CONFORME |
| F-PROD-12 | Ne pas publier si la sauvegarde échoue | retour `500` avant publication | inspection du code | CONFORME |
| F-PROD-13 | Codes HTTP cohérents | 200/201/204/400/404/500/503 | E2E | CONFORME |

### 2.2 OrdersAPI

| ID | Exigence | Mise en œuvre | Preuve | Statut |
|---|---|---|---|---|
| F-ORD-01 | Projet `OrdersAPI` | `OrdersAPI/OrdersAPI.csproj` | build | CONFORME |
| F-ORD-02 | `Orders` : `OrdersId`, `OrderDate`, `CustomerName`, `ProductId` | `Models/Orders.cs` | migration | CONFORME |
| F-ORD-03 | `OrdersAPIContext` | `Data/OrdersAPIContext.cs` | build | CONFORME |
| F-ORD-04 | `DbSet<Orders> Orders` | présent | migration | CONFORME |
| F-ORD-05 | `DbSet<Product> Products` | projection locale | migration | CONFORME |
| F-ORD-06 | `OrdersController`, route `api/[controller]` | présent | Swagger | CONFORME |
| F-ORD-07 | CRUD complet | GET/POST/PUT/DELETE | E2E + tests | CONFORME |
| F-ORD-08 | `Product` local = `Id`, `Name` | `Models/Product.cs` | code | CONFORME |
| F-ORD-09 | Ne jamais lire/écrire `Products.db` | `Data Source=Orders.db` uniquement | E2E + config | CONFORME |
| F-ORD-10 | Fonctionner sans ProductsAPI | arrêt de ProductsAPI testé | E2E section 8 | CONFORME |

### 2.3 ApiGateway

| ID | Exigence | Mise en œuvre | Preuve | Statut |
|---|---|---|---|---|
| F-GW-01 | Projet `ApiGateway` | présent | build | CONFORME |
| F-GW-02 | Ocelot installé | 23.4.3 | `csproj` | CONFORME |
| F-GW-03 | `Ocelot.json` à la racine | présent | build | CONFORME |
| F-GW-04 | Routes Products et Orders | 4 routes | tests Ocelot | CONFORME |
| F-GW-05 | Propriétés Ocelot obligatoires | toutes présentes | tests Ocelot | CONFORME |
| F-GW-06 | Enregistrement + middleware | `AddOcelot` + `await UseOcelot()` | runtime | CONFORME |
| F-GW-07 | GET/POST/PUT/DELETE | CRUD complet | E2E | CONFORME |
| F-GW-08 | Ports cohérents | 7066 / 7065 | tests Ocelot | CONFORME |
| F-GW-09 | Tester sans contourner la Gateway | parcours E2E via 7162 | `logs/e2e-final.log` | CONFORME |

### 2.4 Shared et messaging

| ID | Exigence | Mise en œuvre | Preuve | Statut |
|---|---|---|---|---|
| F-MSG-01 | Class Library `Shared` | `Shared/Shared.csproj` | build | CONFORME |
| F-MSG-02 | Référencée par les deux API | `ProjectReference` | build | CONFORME |
| F-MSG-03 | `ProductCreated` avec ID stable + nom | `Shared/Messages/ProductCreated.cs` | code | CONFORME |
| F-MSG-04 | Même contrat des deux côtés | un seul assembly | runtime | CONFORME |
| F-MSG-05 | MassTransit + RabbitMQ | 8.2.3 | `csproj` | CONFORME |
| F-MSG-06 | RabbitMQ configuré | `RabbitMq` dans `appsettings.json` | runtime | CONFORME |
| F-MSG-07 | `IPublishEndpoint` injecté | `ProductsController` | code + tests | CONFORME |
| F-MSG-08 | `Consumer/ProductCreatedConsumer.cs` | présent | build | CONFORME |
| F-MSG-09 | `IConsumer<ProductCreated>` | implémenté | tests | CONFORME |
| F-MSG-10 | Consumer + file `event-listener` | `ReceiveEndpoint` | E2E topologie | CONFORME |
| F-MSG-11 | Projection dans `Orders.db` | table `Products` | E2E | CONFORME |
| F-MSG-12 | Consumer idempotent | upsert par `Id` | tests + broker réel | CONFORME |
| F-MSG-13 | Contrainte d'unicité | clé primaire `Id` | schéma SQLite | CONFORME |

### 2.5 Synchronisation

| ID | Exigence | Mise en œuvre | Preuve | Statut |
|---|---|---|---|---|
| F-SYNC-01 | Publication de `ProductCreated` | après sauvegarde | tests | CONFORME |
| F-SYNC-02 | Consommation et copie `Id` + `Name` | consumer | E2E | CONFORME |
| F-SYNC-03 | Persistance après redémarrage | SQLite | E2E section 7 | CONFORME |
| F-SYNC-04 | Preuve par inspection des deux fichiers | `sqlite3` | `TEST_RESULTS.md` | CONFORME |

---

## 3. Exigences techniques

### 3.1 Environnement

| ID | Exigence | Réalisation | Statut |
|---|---|---|---|
| T-ENV-01 | macOS Apple Silicon M2 arm64 | macOS 27.3, Apple M2, arm64 | CONFORME |
| T-ENV-02 | SDK cohérent et documenté | SDK 7.0.317, runtime 7.0.20 | CONFORME |
| T-ENV-03 | Pas de Docker pour les API/SQLite | API natives, SQLite natif | CONFORME |
| T-ENV-04 | Aucun SQL Server/PostgreSQL/cloud | SQLite uniquement | CONFORME |
| T-ENV-05 | SQLite exclusif | EF Core Sqlite 7.0.20 | CONFORME |
| T-ENV-06 | RabbitMQ local via Docker `rabbitmq:3-management` | **RabbitMQ 4.4.0 natif Homebrew** ; chemin Docker optionnel dans `docker/` | **CONFORME AVEC ÉCART** |
| T-ENV-07 | Erlang embarqué, aucune installation hôte | **Erlang/OTP 29 installé via Homebrew** | **CONFORME AVEC ÉCART** |
| T-ENV-08 | Visual Studio 2022 ou VS Code + SDK | VS Code + CLI .NET | CONFORME |
| T-ENV-09 | Swagger et/ou curl/Postman | Swagger + curl + collection Postman (`postman/`) | CONFORME |

**Justification des écarts T-ENV-06/07 :** la contrainte finale de la mission impose **aucun Docker**. Le broker est donc installé et exécuté nativement via Homebrew. L'interface applicative reste strictement identique : AMQP `localhost:5672`, vhost `/`, management `15672`. Un broker Docker `rabbitmq:3-management` sur le même port fonctionnerait sans modifier le code ; seul l'hébergement change.

### 3.2 Bases de données

| ID | Exigence | Réalisation | Statut |
|---|---|---|---|
| T-DB-01 | `Products.db`, table `Products` | créé par migration | CONFORME |
| T-DB-02 | `Orders.db`, tables `Orders` + `Products` | créé par migration | CONFORME |
| T-DB-03 | Aucune base/DbContext partagé | contextes distincts | CONFORME |
| T-DB-04 | Aucune jointure inter-bases | aucune jointure | CONFORME |
| T-DB-05 | Migrations indépendantes | deux historiques EF | CONFORME |
| T-DB-06 | `.db` non versionnés | `.gitignore` | CONFORME |
| T-DB-07 | Persistance après redémarrage | E2E section 7 | CONFORME |
| T-DB-08 | Inspection `sqlite3` | procédure documentée | CONFORME |

### 3.3 Configuration

| ID | Exigence | Réalisation | Statut |
|---|---|---|---|
| T-CFG-01 | Chaînes `Data Source=...` | `Products.db` / `Orders.db` | CONFORME |
| T-CFG-02 | RabbitMQ `localhost:5672`, vhost `/` | configuration MassTransit | CONFORME |
| T-CFG-03 | Aucun mot de passe réel en dur | `guest/guest` = compte local par défaut | CONFORME |
| T-CFG-04 | Secrets par variables/user-secrets | surcharge de configuration possible | CONFORME |
| T-CFG-05 | Ports documentés et cohérents | 7066/7065/7162 | CONFORME |

### 3.4 Qualité et robustesse

| ID | Exigence | Réalisation | Statut |
|---|---|---|---|
| T-QUAL-01 | Validation par Data Annotations | modèles + `[ApiController]` | CONFORME |
| T-QUAL-02 | Gestion 400/404/500 | `ProblemDetails` | CONFORME |
| T-QUAL-03 | Aucun secret dans le dépôt | aucun secret réel | CONFORME |
| T-QUAL-04 | Consumer idempotent | upsert + clé primaire | CONFORME |
| T-QUAL-05 | Pas de faux succès si RabbitMQ indisponible | `503` + produit conservé | CONFORME (test mocké, coupure réelle non testée) |
| T-QUAL-06 | Aucune dépendance à l'autre service | isolation vérifiée | CONFORME |
| T-QUAL-07 | Code lisible | structure standard ASP.NET Core | CONFORME |
| T-QUAL-08 | Packages justifiés | versions compatibles documentées | CONFORME |

---

## 4. Exigences de test

| ID | Test | Résultat réel | Statut |
|---|---|---|---|
| TEST-01 | Build 0 erreur / 0 avertissement | 0 / 0 | CONFORME |
| TEST-02 | Migration ProductsAPI | table `Products` | CONFORME |
| TEST-03 | Migration OrdersAPI | tables `Orders` + `Products` | CONFORME |
| TEST-04 | CRUD ProductsAPI | direct + Gateway | CONFORME |
| TEST-05 | CRUD OrdersAPI | direct + Gateway | CONFORME |
| TEST-06 | Swagger | 200 sur les deux API | CONFORME |
| TEST-07 | Gateway Products | GET/POST/PUT/DELETE | CONFORME |
| TEST-08 | Gateway Orders | GET/POST/PUT/DELETE | CONFORME |
| TEST-09 | RabbitMQ Docker + management | broker natif OK ; management HTTP 200 | **CONFORME** |
| TEST-10 | Connexions MassTransit | 2 observées | CONFORME |
| TEST-11 | Publication visible | exchange + consommation | CONFORME |
| TEST-12 | Consommation `event-listener` | 1 consumer, 0 message en attente | CONFORME |
| TEST-13 | Synchronisation deux bases | produit 11 dans les deux bases | CONFORME |
| TEST-14 | Idempotence | 1 seule ligne après rejeu | CONFORME |
| TEST-15 | Persistance | après arrêt/redémarrage | CONFORME |
| TEST-16 | Indépendance | chaque API sans l'autre | CONFORME |
| TEST-17 | Route inconnue | 404 | CONFORME |
| TEST-18 | `dotnet test` | 46 réussis, 1 ignoré, 0 échec | CONFORME |
| TEST-19 | Propagation + contrats JSON | `verify-propagation.sh` : 43 réussis, 1 avertissement, 0 échec | **CONFORME AVEC RÉSERVE** |

**TEST-09 :** le PDF utilise `docker exec ecomm rabbitmqctl status`. Sans Docker, l'équivalent natif est `rabbitmq-diagnostics -q ping` et `rabbitmqctl version` : **Ping succeeded**, version **4.4.0**. Le 18/11/2026, `/api/overview` et `/api/queues/%2F/event-listener` répondent **HTTP 200** (1 consumer, 0 message en attente). La réserve historique « Overview 500 » des rapports figés correspond à un état antérieur ; le chemin Docker optionnel est fourni dans `docker/` mais n'a pas été exécuté.

**TEST-19 :** `scripts/verify-propagation.sh` s'exécute en lecture seule et vérifie, au-delà des codes HTTP, les champs métier des réponses JSON. Produit de référence : **17 — `Gateway-Test-Product`**, présent à l'identique dans `Products.db` et dans la projection `Orders.db`, avec `stockQte=15` et `price=199.9`. Les réponses directes et Gateway sont comparées. La topologie est confirmée : exchange fanout `Shared.Messages:ProductCreated` → exchange `event-listener` → file `event-listener` → 1 consumer. Résultat : **43 réussis / 1 avertissement / 0 échec**. L'avertissement porte sur la commande `8` (`TEST-TERMINAL-ORDERS`), qui référence `productId=18` absent de la projection : le PDF ne définit aucune contrainte FK sur `Orders.ProductId`, et aucune donnée n'a été modifiée. Détail : [`../reports/PROPAGATION_VERIFICATION.md`](../reports/PROPAGATION_VERIFICATION.md) et [`../logs/verify-propagation.log`](../logs/verify-propagation.log).

---

## 5. Écarts par rapport au PDF

| # | Page(s) | PDF | Réalisation | Justification |
|---:|---|---|---|---|
| 1 | global | SQL Server | **SQLite** | contrainte de la mission, deux bases indépendantes |
| 2 | 31–38 | Docker `rabbitmq:3-management` | **RabbitMQ natif Homebrew** ; compose optionnel `docker/docker-compose.rabbitmq.yml` | contrainte finale « aucun Docker » ; même AMQP 5672/UI 15672 ; chemin Docker fourni mais non exécuté |
| 3 | 31–38 | Port Docker `4001` | **5672** par défaut ; **4001** via le compose optionnel | port AMQP standard pour le broker natif ; le compose reproduit le port du PDF |
| 4 | 24 | `DownstreamScheme: https` | **`http`** | ports du PDF conservés, pas de certificat local |
| 5 | 24 | Routes `{id}` sans PUT/DELETE, POST sur `{id}` | **GET/PUT/DELETE sur `{id}`, POST sur collection** | CRUD REST complet exigé |
| 6 | 15, 21 | DELETE absent de Swagger | **DELETE ajouté** | CRUD complet |
| 7 | 13 | `app.UseOcelot().Wait()` | **`await app.UseOcelot()`** | API actuelle Ocelot 23.4.3 |
| 8 | 22 | Ocelot 18.0.0 | **23.4.3** | compatibilité .NET 7 |
| 9 | 40 | `MassTransit.AspNetCore` | **`MassTransit` + `MassTransit.RabbitMQ` 8.2.3** | package inexistant pour MassTransit 8 |
| 10 | 42 | `ProductController` dans le texte | **`ProductsController`** | nom réel du projet |
| 11 | 44 | DbSet `Product` singulier | **table `Products`** | nommage imposé par la mission |
| 12 | 39 | Namespace `Shared.Models` | **`Shared.Messages`** | structure imposée, contrat identique |
| 13 | 47 | `//Id = context.Message.Id` commenté | **`Id` métier utilisé + upsert** | idempotence réelle |
| 14 | 16 | `OrderDate { get; }` | **`{ get; set; }`** | sinon la date JSON n'est jamais affectée |
| 15 | 14 | Écran « Projet par défaut : OrdersAPI » | **options `--project`/`--startup-project` explicites** | évite de migrer la mauvaise base |
| 16 | global | Solution `MicroservicesApp` | **`MicroservicesTP`** | nom imposé |
| 17 | 51–52 | Un seul produit synchronisé à l'écran | **synchronisation expliquée et testée après démarrage du consumer** | le consumer n'était pas actif lors des créations antérieures |
| 18 | global | Visual Studio 2022 for Mac | **VS Code + CLI** | Visual Studio for Mac est retiré du support |
| 19 | 15, 21 | Pas de validation visible | **Data Annotations + 400** | robustesse exigée |
| 20 | 43 | Publication sans gestion d'échec | **`503` si publication échoue** | ne jamais feindre un succès asynchrone |

---

## 6. Éléments du PDF non applicables

| Élément | Raison |
|---|---|
| Docker Desktop, conteneur `ecomm`, `docker exec` | contrainte finale : aucun Docker ; chemin Compose fourni dans `docker/`, non exécuté |
| Port `4001` | remplacé par 5672 pour le broker natif ; disponible via le Compose optionnel |
| SQL Server, chaînes `Server=...` | SQLite exclusif |
| Visual Studio 2022 for Mac | retiré du support Microsoft ; VS Code + SDK |
| Package Manager Console | remplacée par `dotnet ef` (outil local) |

---

## 7. Améliorations optionnelles

| Option | Statut | Commentaire |
|---|---|---|
| Outbox transactionnel | NON IMPLÉMENTÉ | échec de publication signalé par `503`, non rejouable automatiquement |
| `ProductUpdated` / `ProductDeleted` | NON IMPLÉMENTÉ | la projection ne suit pas les PUT/DELETE |
| JWT sur la Gateway | NON IMPLÉMENTÉ | hors périmètre |
| Health checks ASP.NET Core | NON IMPLÉMENTÉ | hors périmètre |
| Retry/circuit breaker Ocelot | NON IMPLÉMENTÉ | hors périmètre |
| Tests de charge | NON EXÉCUTÉ | hors périmètre |
| Serilog / OpenTelemetry | NON IMPLÉMENTÉ | journalisation ASP.NET Core standard |
| FluentValidation | NON IMPLÉMENTÉ | Data Annotations suffisantes pour le TP |

Ces éléments ne sont **pas** des exigences du professeur et ne doivent pas être présentés comme tels.

L'analyse détaillée de ces évolutions (Outbox, retry, DLQ, idempotence HTTP, observabilité) se trouve dans [`ARCHITECTURE_EVOLUTION.md`](ARCHITECTURE_EVOLUTION.md).

---

## 8. Critères d'acceptation globaux

| # | Critère | Statut | Preuve |
|---:|---|---|---|
| 1 | Compilation sans erreur | **OUI** | `logs/build-final.log` |
| 2 | Deux bases SQLite indépendantes | **OUI** | schémas SQLite |
| 3 | Migrations appliquées | **OUI** | historiques EF |
| 4 | CRUD sur les deux API | **OUI** | E2E sections 5–6 |
| 5 | Swagger accessible | **OUI** | 200 sur 7066/7065 |
| 6 | Ocelot route les deux API | **OUI** | E2E |
| 7 | RabbitMQ opérationnel | **OUI** (natif) | ping + topologie |
| 8 | MassTransit publie et consomme | **OUI** | exchange + consumer + projection |
| 9 | Produit présent dans les deux bases | **OUI** | produit 11 |
| 10 | Idempotence démontrée | **OUI** | test broker réel |
| 11 | Persistance après redémarrage | **OUI** | E2E section 7 |
| 12 | Aucun accès direct à l'autre base | **OUI** | config + grep E2E |
| 13 | Tests documentés avec preuves | **OUI** | `TEST_RESULTS.md` |
| 14 | Écarts documentés | **OUI** | ce document |

---

## 9. Artefacts ajoutés pour fermer les écarts d'outillage

| Artefact | Chemin | Ce qu'il couvre | Statut |
|---|---|---|---|
| Collection Postman | `postman/MicroservicesTP.postman_collection.json` | Test p. 51-52 et parcours Gateway p. 27-29 | **fourni, JSON validé** ; dossiers 00-02 exécutés avec Newman (11 requêtes, 23 assertions, 0 échec), dossier mutation non exécuté |
| Environnement Postman | `postman/MicroservicesTP.postman_environment.json` | URLs locales et compte RabbitMQ | **fourni, JSON validé** |
| Mode d'emploi Postman | `postman/README.md` | Import, mutation contrôlée, vérification SQLite | **fourni** |
| Compose RabbitMQ 4001 | `docker/docker-compose.rabbitmq.yml` | Docker Desktop, conteneur `ecomm`, port 4001 (p. 35, 41, 51) | **fourni, non exécuté** (contrainte « aucun Docker ») |
| Mode d'emploi Docker | `docker/README.md` | Surcharge `RabbitMq__Port=4001` | **fourni** |
| Vérification de propagation | `scripts/verify-propagation.sh` + `reports/PROPAGATION_VERIFICATION.md` | Propagation Products → Orders et contrats JSON métier | **exécuté, 43 réussis / 1 avertissement / 0 échec** |

### 9.1 Clôture des points partiels

| Point | Statut après complément |
|---|---|
| Postman absent | **COMBLÉ** : collection + environnement fournis ; dossiers 00-02 exécutés avec Newman (11 requêtes, 23 assertions, 0 échec) |
| Docker / port 4001 | **CHEMIN OPTIONNEL FOURNI** : compose `ecomm` 4001:5672, non exécuté ; le chemin natif 5672 reste le défaut testé |
| Overview RabbitMQ | **COMBLÉ** : `/api/overview` et la file `event-listener` répondent HTTP 200 le 18/11/2026 |
| Ocelot 18.0.0 / `.Wait()` | **ÉCART ASSUMÉ** : Ocelot 23.4.3 + `await` ; fonctionnalité identique, API non régressée |
| `MassTransit.AspNetCore` | **ÉCART ASSUMÉ** : packages `MassTransit` + `MassTransit.RabbitMQ` 8.2.3 |
| DbSet `Product` singulier | **NORMALISÉ** : `DbSet<Product> Products` + table `Products` |
| Assistant Visual Studio / Package Manager Console | **NON APPLICABLE** : équivalent CLI `dotnet ef` |
| HTTPS | **CHOIX CONFORME** : le PDF autorise HTTP ou HTTPS (p. 25) |
| Propagation Products → Orders « à confirmer » | **CONFIRMÉE** : produit 17 identique dans les deux bases, topologie fanout → exchange → file → consumer, `43/1/0` |
| Vérification métier des JSON « à renforcer » | **RENFORCÉE** : `productId`/`name`/`stockQte`/`price` et `ordersId`/`customerName`/`productId`/`orderDate` comparés aux bases et entre direct/Gateway |

Les écarts de version et d'API restent volontaires : revenir à Ocelot 18, à `MassTransit.AspNetCore` ou à `UseOcelot().Wait()` serait une régression technique sans gain fonctionnel.

---

## 10. Conclusion

La solution est **conforme au support sur le plan fonctionnel et pédagogique**. Les écarts sont de trois natures :

1. **Contraintes de la mission** : SQLite au lieu de SQL Server, aucun Docker, nommage `MicroservicesTP`/`Products`/`Shared.Messages`, CRUD complet, idempotence.
2. **Corrections nécessaires** : `OrderDate` settable, `Id` métier du consumer, Ocelot asynchrone, routes complètes, validation, gestion d'échec de publication.
3. **Environnement** : RabbitMQ natif 4.4.0 avec Erlang/OTP 29, .NET 7 hors support. La réserve Overview n'est plus reproduite ; les rapports figés conservent l'état antérieur.

Les artefacts Postman et Docker optionnel ferment les écarts d'outillage. La vérification de propagation confirme le chaînon Products → Orders et les contrats JSON métier. Les écarts de version restent assumés et documentés. Aucune de ces décisions ne supprime une fonctionnalité attendue. Le détail des preuves se trouve dans `TEST_RESULTS.md`, `reports/PROPAGATION_VERIFICATION.md` et `docs/AUTO_PASS.md`.
