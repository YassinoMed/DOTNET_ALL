# PLAN D'IMPLÉMENTATION — MicroservicesTP

**Objectif :** livrer une solution ASP.NET Core 7 fonctionnelle sur macOS M2, avec API et bases SQLite natives, et Docker utilisé **exclusivement** pour RabbitMQ (`rabbitmq:3-management`), MassTransit et Ocelot.

**Principe :** exécution par lots, chaque lot est vérifié avant passage au suivant. Aucun lot n'est déclaré terminé sans preuve d'exécution.

---

## 0. Prérequis et vérifications initiales

| Prérequis | État initial | Action |
|---|---|---|
| macOS arm64 | Présent (macOS 27.3, M2, 8 Go) | Aucune |
| .NET SDK 7 | Présent (7.0.317) | Utiliser `net7.0` |
| Runtime ASP.NET Core 7 | Présent (7.0.20) | Aucune |
| dotnet-ef 7.0.20 | Présent dans `~/.dotnet/tools` | Ajouter un manifeste local |
| SQLite | Présent (3.54.0) | Aucune |
| Homebrew | Présent (7.0.13) | Disponible si besoin |
| Erlang | Embarqué dans `rabbitmq:3-management` | Fourni par l'image Docker RabbitMQ |
| RabbitMQ | À démarrer via Docker (`rabbitmq:3-management`) | Conteneur `ecomm` uniquement (`5672`/`15672`) |
| Docker | Installé (Desktop 29.5.2) | **Utilisé uniquement pour RabbitMQ** |
| Visual Studio 2022 | Installé (17.6.14) mais retiré du support Microsoft | Ne pas en dépendre |
| VS Code | Présent (1.142.0, arm64) | IDE de travail |
| Postman | Absent | Remplacer par Swagger + curl |
| Git | Dépôt absent | Ne pas initialiser sans demande |
| Ports 7065/7066/7162/5672/15672 | Libres | Utiliser ces ports |

**Critère de passage :** Conteneur Docker RabbitMQ `ecomm` démarré, `docker exec ecomm rabbitmqctl status` répond, management UI accessible sur `http://localhost:15672`.

---

## Lot 1 — Création de la solution et des projets

**Objectif :** créer une base de solution propre, sans code métier.

**Fichiers :**
- `MicroservicesTP/MicroservicesTP.sln`
- `MicroservicesTP/global.json`
- `MicroservicesTP/.config/dotnet-tools.json`
- `MicroservicesTP/Directory.Build.props`
- `ProductsAPI/ProductsAPI.csproj`
- `OrdersAPI/OrdersAPI.csproj`
- `ApiGateway/ApiGateway.csproj`
- `Shared/Shared.csproj`

**Dépendances :** SDK .NET 7.

**Modifications :**
- créer la solution ;
- créer deux Web API et une Web API Gateway ;
- créer une Class Library `Shared` ;
- ajouter les références `Shared` dans les deux API ;
- ajouter les packages avec versions épinglées ;
- créer `global.json` pour figer le SDK 7.0.317 ;
- créer le manifeste d'outils local avec `dotnet-ef` 7.0.20.

**Tests :**
```bash
dotnet restore MicroservicesTP.sln
dotnet build MicroservicesTP.sln
```

**Critères d'acceptation :**
- restauration NuGet réussie ;
- build sans erreur ;
- 4 projets présents ;
- aucune dépendance SQL Server ni conteneurisation des API .NET.

**Risques :**
- conflit de versions NuGet ;
- SDK 6 sélectionné par erreur → `global.json` obligatoire ;
- .NET 7 en fin de support → documenté.

---

## Lot 2 — ProductsAPI

**Objectif :** CRUD produits + SQLite + préparation du publisher.

**Fichiers :**
- `Models/Product.cs`
- `Data/ProductsAPIContext.cs`
- `Controllers/ProductsController.cs`
- `Program.cs`
- `appsettings.json`
- `Properties/launchSettings.json`

**Dépendances :** Lot 1, EF Core SQLite.

**Modifications :**
- modèle `Product` conforme au PDF + Data Annotations ;
- `DbSet<Product> Products` ;
- configuration `UseSqlite` avec résolution du chemin `Products.db` dans le content root ;
- contrôleur CRUD complet ;
- Swagger ;
- port HTTP 7066 ;
- injection de `IPublishEndpoint` préparée pour le lot 7.

**Tests :**
- build ;
- migration ;
- Swagger ;
- CRUD via curl.

**Critères d'acceptation :**
- `Products.db` créé ;
- table `Products` avec les bonnes colonnes ;
- GET/POST/GET id/PUT/DELETE fonctionnels ;
- persistance après redémarrage.

**Risques :**
- chemin SQLite relatif au répertoire courant → résolution explicite ;
- doublon de clé primaire lors des tests → IDs générés.

---

## Lot 3 — OrdersAPI

**Objectif :** CRUD commandes + projection locale + SQLite.

**Fichiers :**
- `Models/Orders.cs`
- `Models/Product.cs`
- `Data/OrdersAPIContext.cs`
- `Controllers/OrdersController.cs`
- `Program.cs`
- `appsettings.json`
- `Properties/launchSettings.json`

**Dépendances :** Lot 1, EF Core SQLite.

**Modifications :**
- modèle `Orders` conforme au PDF ;
- modèle local `Product { Id, Name }` ;
- `DbSet<Orders> Orders` et `DbSet<Product> Products` ;
- `Product.Id` en `ValueGeneratedNever()` pour accepter l'ID source ;
- contrôleur CRUD complet ;
- port HTTP 7065 ;
- Swagger.

**Tests :**
- build ;
- migration ;
- CRUD via curl ;
- inspection `sqlite3 Orders.db ".schema"`.

**Critères d'acceptation :**
- `Orders.db` créé ;
- tables `Orders` et `Products` ;
- CRUD commandes fonctionnel sans ProductsAPI ;
- aucune référence à `Products.db`.

**Risques :**
- confusion entre `Orders` (classe) et `Order` (concept) → respecter le nom du PDF ;
- projection locale vide tant que le consumer n'est pas actif → normal.

---

## Lot 4 — API Gateway Ocelot

**Objectif :** exposer les deux microservices via un point d'entrée unique.

**Fichiers :**
- `ApiGateway/Ocelot.json`
- `ApiGateway/Program.cs`
- `ApiGateway/appsettings.json`
- `ApiGateway/Properties/launchSettings.json`

**Dépendances :** Lots 2 et 3 démarrés.

**Modifications :**
- installer Ocelot 23.4.3 ;
- créer `Ocelot.json` avec 4 routes (collection + `{id}`) ;
- scheme `http`, ports 7066/7065 ;
- méthodes GET/POST/PUT/DELETE ;
- `AddJsonFile("Ocelot.json")`, `AddOcelot()`, `await app.UseOcelot()` ;
- port Gateway 7162 ;
- copie de `Ocelot.json` dans le répertoire de sortie.

**Tests :**
```bash
curl http://localhost:7162/apigateway/Products
curl http://localhost:7162/apigateway/Orders
curl http://localhost:7162/apigateway/Products/1
```

**Critères d'acceptation :**
- les deux API répondent via la Gateway ;
- les routes `{id}` fonctionnent ;
- une route inconnue retourne 404 ;
- aucun contournement direct dans les tests de routage.

**Risques :**
- Ocelot 23 API différente de 18 → utiliser `await UseOcelot()` ;
- ports non alignés → vérifier `launchSettings.json` ;
- JSON non copié à l'exécution → `CopyToOutputDirectory`.

---

## Lot 5 — Shared et contrat ProductCreated

**Objectif :** définir le contrat unique de message.

**Fichiers :**
- `Shared/Messages/ProductCreated.cs`
- `Shared/Shared.csproj`

**Dépendances :** Lot 1.

**Modifications :**
- classe `ProductCreated { int Id; string Name; }` ;
- namespace `Shared.Messages` ;
- référence dans les deux API ;
- aucune dépendance EF Core dans `Shared`.

**Tests :**
- build des deux API ;
- vérification de la référence unique.

**Critères d'acceptation :**
- un seul fichier de contrat ;
- les deux services compilent avec le même type ;
- aucune duplication manuelle.

**Risques :**
- namespace différent du PDF → documenté ;
- ajout de champs non nécessaires → éviter.

---

## Lot 6 — RabbitMQ via Docker (`rabbitmq:3-management`)

**Objectif :** broker opérationnel dans un conteneur dédié (sans conteneuriser les API ni SQLite).

**Dépendances :** Docker Desktop démarré.

**Commandes :**
```bash
docker run -d --hostname my-rabbit --name ecomm -p 15672:15672 -p 5672:5672 rabbitmq:3-management
docker exec ecomm rabbitmqctl status
```

**Vérifications :**
- port 5672 en écoute ;
- port 15672 en écoute ;
- `http://localhost:15672` accessible ;
- compte local configuré (note : sur Docker, `guest`/`guest` est restreint à loopback par défaut ; un utilisateur local dédié ou loopback ajusté est configuré) ;
- version RabbitMQ/Erlang notée.

**Critères d'acceptation :**
- conteneur `ecomm` (`rabbitmq:3-management`) démarré ;
- management UI accessible ;
- aucun conteneur pour `ProductsAPI`, `OrdersAPI`, `ApiGateway` ou SQLite.

**Risques :**
- daemon Docker arrêté → démarrer Docker Desktop ;
- `guest`/`guest` bloqué depuis l'hôte via NAT Docker → créer un utilisateur dédié ou configurer les droits locaux ;
- port occupé → identifier le processus.

---

## Lot 7 — Publication depuis ProductsAPI

**Objectif :** publier `ProductCreated` après sauvegarde.

**Fichiers :**
- `ProductsAPI/Program.cs`
- `ProductsAPI/Controllers/ProductsController.cs`
- `ProductsAPI/appsettings.json`

**Dépendances :** Lots 2, 5, 6.

**Modifications :**
- `AddMassTransit` + `UsingRabbitMq` ;
- lecture de la configuration `RabbitMq` ;
- injection de `IPublishEndpoint` ;
- publication dans `PostProduct` après `SaveChangesAsync` ;
- gestion explicite de l'échec de publication ;
- aucun identifiant en dur dans le code.

**Tests :**
- créer un produit ;
- vérifier l'exchange et la queue dans RabbitMQ ;
- vérifier que le message contient `Id` et `Name`.

**Critères d'acceptation :**
- message publié après succès SQL ;
- aucun message si la sauvegarde échoue ;
- connexion MassTransit visible dans RabbitMQ.

**Risques :**
- broker arrêté → l'API doit signaler l'échec ;
- double écriture SQL/publication → limite documentée, Outbox optionnel.

---

## Lot 8 — Consommation dans OrdersAPI

**Objectif :** consommer `ProductCreated` et maintenir la projection locale.

**Fichiers :**
- `OrdersAPI/Consumer/ProductCreatedConsumer.cs`
- `OrdersAPI/Program.cs`
- `OrdersAPI/appsettings.json`

**Dépendances :** Lots 3, 5, 6, 7.

**Modifications :**
- `ProductCreatedConsumer : IConsumer<ProductCreated>` ;
- validation du message ;
- recherche par `Id` ;
- insertion ou mise à jour idempotente ;
- enregistrement du consumer ;
- queue `event-listener` ;
- configuration RabbitMQ.

**Tests :**
- créer un produit ;
- vérifier la ligne dans `Orders.db` ;
- rejouer le message ;
- vérifier l'absence de doublon.

**Critères d'acceptation :**
- message consommé ;
- produit local persisté ;
- idempotence démontrée ;
- queue `event-listener` visible.

**Risques :**
- message invalide → erreur contrôlée ;
- concurrence → clé primaire unique ;
- consumer non enregistré → aucune consommation.

---

## Lot 9 — Migrations et inspection des bases

**Objectif :** créer et vérifier les deux bases indépendantes.

**Commandes :**
```bash
dotnet tool restore
dotnet ef migrations add InitialCreate --project ProductsAPI --startup-project ProductsAPI
dotnet ef database update --project ProductsAPI --startup-project ProductsAPI
dotnet ef migrations add InitialCreate --project OrdersAPI --startup-project OrdersAPI
dotnet ef database update --project OrdersAPI --startup-project OrdersAPI
```

**Vérifications :**
```bash
sqlite3 ProductsAPI/Products.db ".tables"
sqlite3 ProductsAPI/Products.db ".schema Products"
sqlite3 OrdersAPI/Orders.db ".tables"
sqlite3 OrdersAPI/Orders.db ".schema Orders"
sqlite3 OrdersAPI/Orders.db ".schema Products"
```

**Critères d'acceptation :**
- deux fichiers distincts ;
- migrations présentes dans chaque projet ;
- colonnes conformes ;
- aucune table de l'autre service.

**Risques :**
- migration appliquée au mauvais projet → `--project` et `--startup-project` explicites ;
- base créée au mauvais endroit → résolution du chemin dans le content root.

---

## Lot 10 — Tests d'intégration et validation globale

**Objectif :** prouver le fonctionnement complet.

**Fichiers :**
- `Tests/MicroservicesTP.Tests.csproj`
- `Tests/*.cs`
- `scripts/e2e.sh`
- `TEST_RESULTS.md`
- `RAPPORT_FINAL.md`

**Tests :**
1. `dotnet test` (unitaires/intégration locale) ;
2. script E2E avec vrais processus et vrai RabbitMQ ;
3. CRUD via Gateway ;
4. synchronisation des deux bases ;
5. rejeu RabbitMQ et idempotence ;
6. redémarrage et persistance ;
7. indépendance des services ;
8. inspection SQLite ;
9. inspection management RabbitMQ.

**Critères d'acceptation :**
- tous les tests exécutés et documentés ;
- preuves horodatées ;
- aucun PASS artificiel ;
- AUTO-PASS uniquement si toutes les conditions sont remplies.

**Risques :**
- tests dépendants d'un broker actif → documenter la dépendance ;
- ports occupés → script de nettoyage ;
- données résiduelles → utiliser des identifiants uniques, ne pas supprimer les bases.

---

## 11. Ordre d'exécution recommandé

```text
Phase 0  Audit environnement
   ↓
Lot 1    Solution + projets
   ↓
Lot 2    ProductsAPI (sans messaging)
   ↓
Lot 3    OrdersAPI (sans consumer)
   ↓
Lot 5    Shared
   ↓
Lot 4    Gateway (API HTTP)
   ↓
Lot 6    RabbitMQ (Docker uniquement)
   ↓
Lot 7    Publisher
   ↓
Lot 8    Consumer
   ↓
Lot 9    Migrations finales + inspection
   ↓
Lot 10   Tests E2E + rapport
```

---

## 12. Definition of Done

- [ ] Build solution sans erreur.
- [ ] Les 4 projets compilent.
- [ ] `Products.db` et `Orders.db` existent et sont indépendants.
- [ ] Migrations présentes et appliquées.
- [ ] CRUD complet sur les deux API.
- [ ] Swagger accessible.
- [ ] Gateway opérationnelle.
- [ ] RabbitMQ (`rabbitmq:3-management`) opérationnel.
- [ ] MassTransit connecté.
- [ ] `ProductCreated` publié et consommé.
- [ ] Produit présent dans les deux bases.
- [ ] Idempotence démontrée.
- [ ] Persistance après redémarrage démontrée.
- [ ] Tests automatisés exécutés.
- [ ] Documentation complète.
- [ ] Rapport AUTO-PASS honnête.
