# EXIGENCES — Atelier 8 Microservices ASP.NET Core

**Référence principale :** `Atelier 8 Introduction aux MicroServices avec ASP.NET Core.pdf` (52 pages)
**Contraintes complémentaires :** mission macOS M2, SQLite exclusif, microservices .NET 7 natifs, Docker utilisé uniquement pour RabbitMQ (`rabbitmq:3-management`, ports `5672`/`15672`), solution `MicroservicesTP`.
**Règle de lecture :** chaque exigence est marquée :
- **[PDF]** : explicitement présent dans le support ;
- **[MISSION]** : imposé par la mission de réalisation (addendum) ;
- **[DÉDUIT]** : nécessaire au fonctionnement mais non écrit dans le PDF ;
- **[OPTION]** : amélioration facultative, non exigée par le professeur.

---

## 1. Matrice de synthèse

| Élément du PDF | Exigence | Implémentation prévue | Vérification |
|---|---|---|---|
| ProductsAPI | Gestion des produits | API REST + SQLite `Products.db` | CRUD + persistance |
| OrdersAPI | Gestion des commandes | API REST + SQLite `Orders.db` | CRUD + persistance |
| ApiGateway | Routage HTTP | Ocelot 23.4.3 | Requêtes via `/apigateway/...` |
| Shared | Messages partagés | Class Library `Shared` | Compilation + référence |
| RabbitMQ | Messagerie asynchrone | Docker uniquement pour RabbitMQ (`rabbitmq:3-management`) | Connexion + management UI |
| MassTransit | Publication/consommation | Publisher + Consumer | Événement réel |
| ProductCreated | Événement produit | DTO `ProductCreated` | Publication + consommation |
| Consumer | Réception du message | `ProductCreatedConsumer` | Persistance `Orders.db` |
| Synchronisation | Copie locale du produit | Table `Products` d'OrdersAPI | Produit présent dans les deux bases |
| Idempotence | Pas de doublon | Clé = `ProductCreated.Id` | Rejeu du même message |

---

## 2. Exigences fonctionnelles

### 2.1 ProductsAPI [PDF] [MISSION]

| ID | Exigence | Priorité |
|---|---|---|
| F-PROD-01 | Créer un projet ASP.NET Core Web API nommé `ProductsAPI`. | Obligatoire |
| F-PROD-02 | Créer la classe `Product` avec `ProductId` (int), `Name` (string), `StockQte` (int), `Price` (double). | Obligatoire |
| F-PROD-03 | Créer `ProductsAPIContext` dans `Data/`. | Obligatoire |
| F-PROD-04 | Exposer `DbSet<Product> Products`. | Obligatoire |
| F-PROD-05 | Créer `ProductsController` avec la route `api/[controller]`. | Obligatoire |
| F-PROD-06 | Implémenter `GET /api/Products` (liste). | Obligatoire |
| F-PROD-07 | Implémenter `GET /api/Products/{id}`. | Obligatoire |
| F-PROD-08 | Implémenter `POST /api/Products`. | Obligatoire |
| F-PROD-09 | Implémenter `PUT /api/Products/{id}`. | Obligatoire |
| F-PROD-10 | Implémenter `DELETE /api/Products/{id}`. | **[MISSION]** (le PDF ne l'affiche pas) |
| F-PROD-11 | Publier `ProductCreated` après une sauvegarde réussie. | [PDF] |
| F-PROD-12 | Ne jamais publier si la sauvegarde échoue. | [MISSION] |
| F-PROD-13 | Retourner des codes HTTP cohérents (200, 201, 204, 400, 404, 500). | [DÉDUIT] |

### 2.2 OrdersAPI [PDF] [MISSION]

| ID | Exigence | Priorité |
|---|---|---|
| F-ORD-01 | Créer un projet ASP.NET Core Web API nommé `OrdersAPI`. | Obligatoire |
| F-ORD-02 | Créer la classe `Orders` avec `OrdersId`, `OrderDate`, `CustomerName`, `ProductId`. | Obligatoire |
| F-ORD-03 | Créer `OrdersAPIContext`. | Obligatoire |
| F-ORD-04 | Exposer `DbSet<Orders> Orders`. | Obligatoire |
| F-ORD-05 | Exposer `DbSet<Product> Products` (projection locale). | Obligatoire |
| F-ORD-06 | Créer `OrdersController` avec la route `api/[controller]`. | Obligatoire |
| F-ORD-07 | CRUD complet sur les commandes. | [PDF] + DELETE [MISSION] |
| F-ORD-08 | Le modèle local `Product` contient uniquement `Id` et `Name`. | [PDF] |
| F-ORD-09 | OrdersAPI ne doit jamais lire/écrire `Products.db`. | [MISSION] |
| F-ORD-10 | OrdersAPI doit fonctionner sans ProductsAPI pour son CRUD. | [MISSION] |

### 2.3 ApiGateway [PDF] [MISSION]

| ID | Exigence | Priorité |
|---|---|---|
| F-GW-01 | Créer `ApiGateway`. | Obligatoire |
| F-GW-02 | Installer Ocelot. | Obligatoire |
| F-GW-03 | Créer `Ocelot.json` à la racine du projet. | Obligatoire |
| F-GW-04 | Configurer les routes Products et Orders (collection + `{id}`). | Obligatoire |
| F-GW-05 | Configurer `DownstreamPathTemplate`, `DownstreamScheme`, `DownstreamHostAndPorts`, `UpstreamPathTemplate`, `UpstreamHttpMethod`. | Obligatoire |
| F-GW-06 | Enregistrer Ocelot dans `Program.cs` et ajouter le middleware. | Obligatoire |
| F-GW-07 | Router GET/POST/PUT/DELETE pour le CRUD complet. | [MISSION] |
| F-GW-08 | Les ports de `Ocelot.json` doivent correspondre aux ports réels. | [MISSION] |
| F-GW-09 | Tester le routage sans contourner la Gateway. | [MISSION] |

### 2.4 Shared et messaging [PDF] [MISSION]

| ID | Exigence | Priorité |
|---|---|---|
| F-MSG-01 | Créer une Class Library `Shared`. | Obligatoire |
| F-MSG-02 | Référencer `Shared` dans ProductsAPI et OrdersAPI. | Obligatoire |
| F-MSG-03 | Créer `ProductCreated` avec un identifiant stable et le nom. | Obligatoire |
| F-MSG-04 | Utiliser le même contrat dans les deux services. | Obligatoire |
| F-MSG-05 | Installer MassTransit et MassTransit.RabbitMQ. | Obligatoire |
| F-MSG-06 | Configurer RabbitMQ dans les deux services. | Obligatoire |
| F-MSG-07 | Injecter `IPublishEndpoint` dans `ProductsController`. | Obligatoire |
| F-MSG-08 | Créer `Consumer/ProductCreatedConsumer.cs`. | Obligatoire |
| F-MSG-09 | Implémenter `IConsumer<ProductCreated>`. | Obligatoire |
| F-MSG-10 | Enregistrer le consumer et la queue `event-listener`. | Obligatoire |
| F-MSG-11 | Persister la projection locale dans `Orders.db`. | Obligatoire |
| F-MSG-12 | Consumer idempotent : un même événement ne crée pas de doublon. | [MISSION] |
| F-MSG-13 | Prévoir une contrainte d'unicité sur l'identifiant métier. | [MISSION] |

### 2.5 Synchronisation [PDF]

| ID | Exigence | Priorité |
|---|---|---|
| F-SYNC-01 | Un produit créé dans ProductsAPI est publié sous forme de `ProductCreated`. | Obligatoire |
| F-SYNC-02 | OrdersAPI consomme l'événement et enregistre `Id` + `Name` dans sa table `Products`. | Obligatoire |
| F-SYNC-03 | Les données sont persistées après redémarrage. | [MISSION] |
| F-SYNC-04 | La synchronisation est prouvée par inspection des deux fichiers SQLite. | [MISSION] |

---

## 3. Exigences techniques

### 3.1 Environnement [MISSION]

| ID | Exigence |
|---|---|
| T-ENV-01 | macOS Apple Silicon M2, architecture `arm64`. |
| T-ENV-02 | .NET SDK cohérent avec les packages ; version réellement installée documentée. |
| T-ENV-03 | Aucun Docker/conteneur pour les microservices .NET ni pour les bases SQLite ; Docker autorisé **uniquement** pour RabbitMQ (validation utilisateur). |
| T-ENV-04 | Aucun SQL Server, PostgreSQL, base distante ou cloud. |
| T-ENV-05 | SQLite exclusivement, via `Microsoft.EntityFrameworkCore.Sqlite`. |
| T-ENV-06 | RabbitMQ exécuté localement via Docker (`rabbitmq:3-management`, conteneur `ecomm`, ports `5672`/`15672`). |
| T-ENV-07 | Erlang embarqué dans l'image `rabbitmq:3-management` (aucune installation Erlang hôte requise). |
| T-ENV-08 | Visual Studio 2022 vérifié ; s'il n'est pas exploitable, utiliser VS Code + SDK .NET. |
| T-ENV-09 | Swagger et/ou curl/Postman pour les tests. |

### 3.2 Bases de données [MISSION]

| ID | Exigence |
|---|---|
| T-DB-01 | `ProductsAPI` utilise `Products.db`, table `Products`. |
| T-DB-02 | `OrdersAPI` utilise `Orders.db`, tables `Orders` et `Products`. |
| T-DB-03 | Aucune base partagée, aucun DbContext partagé. |
| T-DB-04 | Aucune jointure SQL entre les deux bases. |
| T-DB-05 | Migrations indépendantes dans chaque projet. |
| T-DB-06 | Les fichiers `.db` ne doivent pas être versionnés. |
| T-DB-07 | Les données persistent après redémarrage. |
| T-DB-08 | Les deux fichiers peuvent être inspectés avec `sqlite3`. |

### 3.3 Configuration [MISSION]

| ID | Exigence |
|---|---|
| T-CFG-01 | Chaînes de connexion dans `appsettings.json` : `Data Source=Products.db` / `Data Source=Orders.db`. |
| T-CFG-02 | Configuration RabbitMQ : `localhost`, port `5672`, vhost `/`, identifiants locaux. |
| T-CFG-03 | Aucun mot de passe réel en dur dans le code source. |
| T-CFG-04 | Les secrets de production doivent passer par variables d'environnement ou user-secrets. |
| T-CFG-05 | Les ports réels sont documentés et cohérents avec Ocelot. |

### 3.4 Qualité et robustesse [MISSION]

| ID | Exigence |
|---|---|
| T-QUAL-01 | Validation des entrées par Data Annotations. |
| T-QUAL-02 | Gestion des 404, 400, 500. |
| T-QUAL-03 | Aucun secret dans le dépôt. |
| T-QUAL-04 | Consumer idempotent et protégé contre les doublons. |
| T-QUAL-05 | Gestion de l'indisponibilité RabbitMQ : l'application ne doit pas prétendre avoir publié si le broker est indisponible. |
| T-QUAL-06 | Aucune dépendance directe d'OrdersAPI vers ProductsAPI ou sa base. |
| T-QUAL-07 | Code lisible, pas de complexité disproportionnée. |
| T-QUAL-08 | Aucun package incompatible ou obsolète sans justification. |

---

## 4. Exigences de tests

| ID | Test | Critère de réussite |
|---|---|---|
| TEST-01 | Build de la solution | 0 erreur, 0 avertissement bloquant |
| TEST-02 | Migrations ProductsAPI | `Products.db` créé, table `Products` |
| TEST-03 | Migrations OrdersAPI | `Orders.db` créé, tables `Orders` et `Products` |
| TEST-04 | CRUD ProductsAPI | GET/POST/GET id/PUT/DELETE réussis |
| TEST-05 | CRUD OrdersAPI | GET/POST/GET id/PUT/DELETE réussis |
| TEST-06 | Swagger | Interface accessible sur les deux API |
| TEST-07 | Gateway Products | GET/POST/PUT/DELETE via `/apigateway/Products` |
| TEST-08 | Gateway Orders | GET/POST/PUT/DELETE via `/apigateway/Orders` |
| TEST-09 | RabbitMQ Docker | `docker exec ecomm rabbitmqctl status` OK, management UI accessible |
| TEST-10 | Connexions MassTransit | Les deux services connectés au broker |
| TEST-11 | Publication | Message `ProductCreated` visible dans l'exchange |
| TEST-12 | Consommation | `event-listener` reçoit et traite le message |
| TEST-13 | Synchronisation | Produit présent dans `Products.db` et `Orders.db` |
| TEST-14 | Idempotence | Rejeu du même message → une seule ligne locale |
| TEST-15 | Persistance | Données toujours présentes après redémarrage |
| TEST-16 | Indépendance | ProductsAPI fonctionne sans OrdersAPI et inversement |
| TEST-17 | Erreur de routage | Route inconnue → 404 |
| TEST-18 | Tests automatisés | `dotnet test` réussi |

---

## 5. Exigences de livraison

1. Solution `MicroservicesTP.sln`.
2. `ProductsAPI`, `OrdersAPI`, `ApiGateway`, `Shared`.
3. Deux bases SQLite indépendantes et leurs migrations.
4. Configuration MassTransit/RabbitMQ (conteneur `ecomm` sur port 5672).
5. Publisher et consumer.
6. Configuration Ocelot complète.
7. Tests automatisés et script E2E.
8. `README.md` avec commandes exactes.
9. Guides installation/démarrage/arrêt.
10. Rapport de conformité au PDF.
11. Rapport de validation AUTO-PASS avec preuves.
12. Documents d'audit, d'architecture, de plan et de tests.

---

## 6. AMÉLIORATIONS OPTIONNELLES

> Ces éléments ne sont **pas** exigés par le PDF ni par la mission. Ils ne doivent pas être présentés comme des exigences du professeur.

| ID | Amélioration | Bénéfice | Statut |
|---|---|---|---|
| OPT-01 | Pattern Outbox transactionnel | Garantit que la publication d'événement et la sauvegarde sont cohérentes | Non implémenté (hors périmètre TP) |
| OPT-02 | `ProductUpdated` / `ProductDeleted` | Maintient la projection locale à jour après PUT/DELETE | Non implémenté |
| OPT-03 | Authentification JWT sur la Gateway | Sécurisation de l'exposition | Non implémenté |
| OPT-04 | Health checks ASP.NET Core | Supervision des services | Non implémenté |
| OPT-05 | Retry/circuit breaker Ocelot | Résilience HTTP | Non implémenté |
| OPT-06 | Tests de charge | Validation de montée en charge | Non implémenté |
| OPT-07 | Conteneurisation des API .NET | Déploiement | Hors périmètre (Docker réservé uniquement à RabbitMQ) |
| OPT-08 | Journalisation structurée Serilog | Observabilité | Non implémenté |
| OPT-09 | Métriques OpenTelemetry | Traçabilité distribuée | Non implémenté |
| OPT-10 | Validation FluentValidation | Validation plus riche | Non implémenté |

---

## 7. Critères d'acceptation globaux

La réalisation est acceptée si :

1. la solution compile sans erreur ;
2. les deux bases SQLite sont créées indépendamment ;
3. les migrations sont appliquées sans erreur ;
4. les CRUD fonctionnent sur les deux API ;
5. Swagger est accessible ;
6. Ocelot route les deux API ;
7. RabbitMQ (Docker `rabbitmq:3-management`) est opérationnel ;
8. MassTransit publie et consomme réellement ;
9. un produit créé via l'API apparaît dans les deux bases ;
10. l'idempotence est démontrée ;
11. les données persistent après redémarrage ;
12. aucun accès direct à la base de l'autre service n'existe ;
13. les tests exécutés sont documentés avec preuves ;
14. les écarts par rapport au PDF sont documentés.
