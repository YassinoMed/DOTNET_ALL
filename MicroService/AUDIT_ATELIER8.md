# AUDIT DOCUMENTAIRE — Atelier 8
## Introduction aux Microservices avec ASP.NET Core

**Document source :** `Atelier 8 Introduction aux MicroServices avec ASP.NET Core.pdf`
**Auteur du support :** Malek Zribi
**Nombre de pages :** 52
**Format :** 960 × 540 pt, WPS Presentation, PDF 1.7
**Méthode d'analyse :** extraction texte (`pdftotext -layout`) + rendu image 130 dpi de chaque page + lecture des captures de code, des diagrammes et des consoles.
**Date de l'audit :** 18 novembre 2026

---

## 1. Synthèse exécutive

Le support est un atelier guidé de bout en bout. Il construit une solution de démonstration composée de :

- `ProductsAPI` : microservice REST de gestion des produits ;
- `OrdersAPI` : microservice REST de gestion des commandes, qui reçoit une copie locale des produits ;
- `ApiGateway` : point d'entrée HTTP basé sur Ocelot ;
- `Shared` : bibliothèque de contrats de messages ;
- RabbitMQ : broker de messages asynchrone ;
- MassTransit : abstraction .NET au-dessus de RabbitMQ.

Le fil pédagogique est le suivant :

1. créer deux API REST indépendantes ;
2. les exposer derrière une API Gateway ;
3. constater que `OrdersAPI` ne possède pas les informations produit ;
4. publier un événement `ProductCreated` depuis `ProductsAPI` ;
5. consommer cet événement dans `OrdersAPI` avec MassTransit ;
6. persister une projection locale du produit dans la base d'`OrdersAPI` ;
7. vérifier la synchronisation via l'API Gateway, RabbitMQ et les tables SQL.

Le support est globalement exploitable, mais il contient plusieurs **incohérences historiques** (ports Docker, nom de package MassTransit, projet par défaut d'une migration, absence de DELETE dans Swagger, identifiant du produit commenté dans le consumer). Ces points sont détaillés en section 4 et corrigés/documents dans la réalisation.

---

## 2. Audit page par page (52 pages)

| Page | Titre / sujet | Contenu observé (texte, captures, code) | Éléments techniques à comprendre | Statut |
|---:|---|---|---|---|
| 1 | Page de titre | « Atelier 8 : Introduction aux Microservices avec ASP.NET Core », auteur Malek Zribi. | Cadrage général, aucun code. | Analysé |
| 2 | Que sont les microservices ? | Définition : découpage en composants plus petits et indépendants ; opposition au monolithe ; modules qui coopèrent. | Microservice, indépendance, modularité, monolithe. | Analysé |
| 3 | Avantages des microservices | Développement distribué plus rapide, haute évolutivité, résilience, déploiement facile, maintenance facilitée. | Scalabilité, résilience, déploiement indépendant. | Analysé |
| 4 | Architecture monolithique | Diagramme : un client, une application contenant `Products API` et `Orders API`, une base unique. | Base partagée, couplage fort, point unique de défaillance. | Analysé |
| 5 | Architecture de microservices | Diagramme : client → MicroService 1/2/n, chacun avec sa base `DB 1`, `DB 2`, `DB n`, déployés sur des serveurs distincts. | Base par service, indépendance de déploiement. | Analysé |
| 6 | API Gateway Ocelot | Définition de la passerelle middleware ; Ocelot open source .NET, supporté par Microsoft ; version .NET Core 3.1+. | Rôle de l'API Gateway, Ocelot, masquage des microservices. | Analysé |
| 7 | L'API Gateway | Diagramme : client → `API GATEWAY` → microservices → bases séparées. | Point d'entrée unique, routage. | Analysé |
| 8 | Fonctionnalités de la passerelle | Virtualisation des API, couche de sécurité, réduction de la complexité (JWT, etc.). | Sécurité centralisée, abstraction des versions. | Analysé |
| 9 | Création de Microservices ASP.NET Core | Solution `MicroservicesApp` avec 3 projets : `ProductsAPI`, `OrdersAPI`, `ApiGateway`. Capture de l'Explorateur de solutions. | Nom de solution du support ; 3 projets Web API. | Analysé |
| 10 | Microservice 1 — ProductsAPI | Classe `Product` : `ProductId` (int), `Name` (string), `StockQte` (int), `Price` (double). `Name` initialisé à `String.Empty`. | Modèle produit exact du support. | Analysé |
| 11 | ProductsAPI — contrôleur | Ajout d'un contrôleur API généré avec Entity Framework : « Contrôleur d'API avec actions, utilisant Entity Framework ». | Scaffolding, contrôleur API, EF. | Analysé |
| 12 | ProductsAPI — contexte | Boîte de dialogue : modèle `Product (ProductsAPI)`, contexte `ProductsAPIContext (ProductsAPI.Data)`, contrôleur `ProductsController`. | Séparation Modèle / Data / Controllers. | Analysé |
| 13 | ProductsAPI — code du contrôleur | Capture partielle (`ProductsController.cs`, lignes 1–29) : `[Route("api/[controller]")]`, `[ApiController]`, `ControllerBase`, `_context`, constructeur, `GetProduct()` retournant `_context.Product.ToListAsync()`. | Route `/api/Products`, DbSet singulier `Product`, GET initial. | Analysé |
| 14 | ProductsAPI — migration | Texte : lancer une migration avant de tester, choisir `ProductsAPI` comme projet par défaut. Capture 1 : projet par défaut `OrdersAPI`, commande `add-Migration dbcreate`. Capture 2 : projet `ProductsAPI`, commande `update-database`. | Migration EF, projet de démarrage. **Incohérence : le premier écran sélectionne OrdersAPI alors que le texte impose ProductsAPI.** | Analysé — ambiguïté |
| 15 | Test ProductsAPI | Swagger `https://localhost:7066/swagger/index.html`, endpoints `GET /api/Products`, `POST /api/Products`, `GET /api/Products/{id}`, `PUT /api/Products/{id}`. Aucun DELETE visible. | Port 7066, Swagger, CRUD partiel (sans DELETE). | Analysé |
| 16 | Microservice 2 — OrdersAPI | Classe `Orders` : `OrdersId` (int), `OrderDate` (DateTime), `CustomerName` (string nullable), `ProductId` (int). | Modèle commande exact. | Analysé |
| 17 | OrdersAPI — contrôleur | Ajout d'un contrôleur API avec Entity Framework. | Scaffolding. | Analysé |
| 18 | OrdersAPI — contexte | Modèle `Orders (OrdersAPI)`, contexte `OrdersAPIContext (OrdersAPI.Data)`, contrôleur `OrdersController`. | Séparation Modèle / Data / Controllers. | Analysé |
| 19 | OrdersAPI — code du contrôleur | Capture partielle : `[Route("api/[controller]")]`, `_context`, constructeur, `GetOrders()` retournant `_context.Orders.ToListAsync()`. | Route `/api/Orders`, DbSet `Orders`. | Analysé |
| 20 | OrdersAPI — migration | Projet par défaut `OrdersAPI`, commandes `add-Migration dbcreate` puis `update-database`. | Migration indépendante. | Analysé |
| 21 | Test OrdersAPI | Swagger `https://localhost:7065/swagger/index.html`, endpoints `GET/POST /api/Orders`, `GET/PUT /api/Orders/{id}`. Aucun DELETE visible. | Port 7065, CRUD partiel. | Analysé |
| 22 | Création de l'API Gateway | 3e projet Web API `ApiGateway` ; installer Ocelot 18.0.0 via NuGet. | Ocelot, version du support. | Analysé |
| 23 | Le fichier Ocelot.json | Diapositive de transition : instructions pour créer `Ocelot.json` à la racine, sans code affiché (le code est page 24). | Fichier de configuration Ocelot. | Analysé |
| 24 | Ocelot.json — code | 4 routes : `/api/Products` → `/apigateway/Products` (GET, PUT, POST) ; `/api/Orders` → `/apigateway/Orders` (GET, PUT, POST) ; `/api/Products/{id}` → `/apigateway/products/{id}` (Get, Post) ; `/api/Orders/{id}` → `/apigateway/Orders/{id}` (Get, Post). Downstream `https`, hôtes `localhost:7066` et `localhost:7065`. | DownstreamPathTemplate, Scheme, HostAndPorts, UpstreamPathTemplate, UpstreamHttpMethod. **Incohérences : POST sur `/{id}`, pas de DELETE, pas de PUT sur `/{id}`.** | Analysé — ambiguïté |
| 25 | Ocelot.json — explication | Définition de `Routes`, `DownstreamPathTemplate`, `DownstreamSchem` (sic), `DownstreamHostAndPorts`, `UpStreamPathTemplate`, `UpstreamHttpMethod`. | Vocabulaire Ocelot. | Analysé |
| 26 | Intégration du pipeline Ocelot | `Program.cs` : `using Ocelot.DependencyInjection; using Ocelot.Middleware;`, `builder.Configuration.AddJsonFile("Ocelot.json");`, `builder.Services.AddOcelot();`, `app.UseOcelot().Wait();`. | Enregistrement DI + middleware. `.Wait()` est l'API historique. | Analysé |
| 27 | Test Gateway — Products | Postman `GET https://localhost:7162/apigateway/Products` → 200, produits `prod1` (50, 1000) et `prod2` (40, 1520). | Port Gateway 7162, routage Products. | Analysé |
| 28 | Test Gateway — Orders | `GET https://localhost:7162/apigateway/Orders` → 200, commandes avec `ordersId`, `orderDate`, `customerName`, `productId`. | Routage Orders. | Analysé |
| 29 | Test Gateway — Products/{id} | `GET https://localhost:7162/apigateway/Products/1` → 200, produit `prod1`. | Routage avec paramètre. | Analysé |
| 30 | Section RabbitMQ | Diapositive de section : « Communication de données asynchrones entre microservices à l'aide de RabbitMQ — Message Broker avec MassTransit ». | Annonce du module messaging. | Analysé |
| 31 | RabbitMQ — principe | RabbitMQ diffuse des messages de façon asynchrone ; `OrdersAPI` ne possède pas les informations produit ; après création d'un produit, enregistrer les informations nécessaires dans une table produit locale d'`OrdersAPI`. | Asynchronisme, projection locale, découplage. | Analysé |
| 32 | RabbitMQ — architecture | Diagramme : client → API Gateway → ProductsAPI/OrdersAPI ; chaque API a sa base ; RabbitMQ est le broker asynchrone entre les microservices. | Broker, bases séparées, communication événementielle. | Analysé |
| 33 | RabbitMQ — Fanout | Diagramme Producer → Fanout Exchange → 3 queues → 3 consumers. | Exchange fanout, queues, consumers. | Analysé |
| 34 | MassTransit | MassTransit enveloppe RabbitMQ, Azure Service Bus, SQS, ActiveMQ ; configuration simplifiée des messages .NET. | Abstraction du broker. | Analysé |
| 35 | Installer RabbitMQ (Docker) | Docker Desktop ; `docker run -d --hostname my-rabbit --name ecomm -p 15672:15672 -p 5672:5672 rabbitmq:3-management`. Annotation : « Changer ce 1er numéro de port vers 4001 ». | Image `rabbitmq:3-management`, ports 15672/5672. **Incohérence : l'annotation pointe 15672 alors que MassTransit utilisera 4001 comme port AMQP.** | Analysé — ambiguïté |
| 36 | Docker Desktop | Capture du conteneur `ecomm`, image `rabbitmq:3-management`, statut Running, ports 15672 et 5672. | Vérification du conteneur. | Analysé |
| 37 | Interface RabbitMQ | `http://localhost:15672`, login `guest` / `guest`. | Management UI, identifiants par défaut. | Analysé |
| 38 | RabbitMQ — files | Management UI, onglet Queues, version RabbitMQ 3.11.2, Erlang 25.1.2, aucune file. | Vérification des queues. | Analysé |
| 39 | Bibliothèque Shared | Créer une Class Library `Shared` avec les DTO ; référence dans les deux microservices. Code : `namespace Shared.Models`, `class ProductCreated { public int Id; public string Name = String.Empty; }`. | Contrat partagé, référence de projet. | Analysé |
| 40 | Packages NuGet | Installer dans les deux microservices : `MassTransit`, `MassTransit.AspNetCore`, `MassTransit.RabbitMQ`. | Packages du support ; `MassTransit.AspNetCore` est obsolète en v8. | Analysé — obsolescence |
| 41 | Émetteur — Program.cs | `AddMassTransit`, `UsingRabbitMq`, `cfg.Host(new Uri("rabbitmq://localhost:4001"), h => { h.Username("guest"); h.Password("guest"); })`. | Hôte RabbitMQ, port 4001, identifiants. | Analysé |
| 42 | Émetteur — injection | `ProductsController` : champs `_context` et `_publishEndpoint` (`IPublishEndpoint`), constructeur avec les deux dépendances. Le texte dit « ProductController » (singulier), le code montre `ProductsController`. | Injection de dépendance MassTransit. | Analysé — ambiguïté mineure |
| 43 | Émetteur — publication | `PostProduct` : `_context.Product.Add(product); await _context.SaveChangesAsync(); await _publishEndpoint.Publish<ProductCreated>(new ProductCreated { Id = product.ProductId, Name = product.Name }); return CreatedAtAction(...)`. | Publication après sauvegarde ; message `ProductCreated`. | Analysé |
| 44 | Table Products dans OrdersAPI | Classe locale `Product { int Id; string Name; }` ; `OrdersAPIContext` avec `DbSet<OrdersAPI.Orders> Orders` et `DbSet<Product> Products`. | Projection locale, deux DbSet. | Analysé |
| 45 | Migration OrdersAPI — Products | `Add-migration addproduct`, `update-database`, projet par défaut `OrdersAPI`, message « Multiple startup projects set. Using project 'OrdersAPI' ». | Migration indépendante de la table locale. | Analysé |
| 46 | Récepteur — principe | `OrdersAPI` est récepteur ; `MassTransit.IConsumer<T>` ; créer le dossier `Consumer` et `ProductCreatedConsumer.cs`. | Consumer, dossier, convention de nom. | Analysé |
| 47 | Récepteur — code consumer | `class ProductCreatedConsumer : IConsumer<ProductCreated>` ; injection `OrdersAPIContext` ; `Consume` crée un `Product` avec `Name = context.Message.Name` ; `//Id = context.Message.Id,` est **commenté** ; `Add` + `SaveChangesAsync`. | Consumer de base ; **pas d'idempotence** dans le code du support. | Analysé — faiblesse |
| 48 | Récepteur — explication | Le consumer doit hériter de `IConsumer` ; `Consume` s'exécute à chaque message ; stockage en base ; enregistrer MassTransit dans `Program.cs`. | Cycle de consommation. | Analysé |
| 49 | Récepteur — Program.cs | `AddMassTransit(x => { x.AddConsumer<ProductCreatedConsumer>(); x.UsingRabbitMq((context,cfg) => { cfg.Host(...4001...); cfg.ReceiveEndpoint("event-listener", e => { e.ConfigureConsumer<ProductCreatedConsumer>(context); }); }); });`. | Queue `event-listener`, consumer enregistré. | Analysé |
| 50 | Récepteur — explication | File `event-listener` ; exchange Fanout ; `ProductCreatedConsumer` écoute chaque message. | Nom de queue, fanout. | Analysé |
| 51 | Test de communication | Lancer les microservices et Docker ; POST Postman `{"name":"prod12","stockQte":660,"price":1500}` → réponse `productId: 12` ; le produit doit apparaître dans la table produit d'OrdersAPI. | Test de bout en bout. | Analysé |
| 52 | Résultat final | Deux captures SQL Server : `dbo.Products` (OrdersAPI) avec `Id`, `Name` et une ligne `prod12` ; `dbo.Product` (ProductsAPI) avec `ProductId`, `Name`, `StockQte`, `Price` et plusieurs produits. | Preuve de synchronisation de la projection locale. | Analysé |

---

## 3. Code extrait des captures

### 3.1 Modèle `Product` (ProductsAPI, page 10)

```csharp
namespace ProductsAPI
{
    public class Product
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = String.Empty;
        public int StockQte { get; set; }
        public double Price { get; set; }
    }
}
```

### 3.2 Modèle `Orders` (OrdersAPI, page 16)

```csharp
namespace OrdersAPI
{
    public class Orders
    {
        public int OrdersId { get; set; }
        public DateTime OrderDate { get; set; }
        public string? CustomerName { get; set; }
        public int ProductId { get; set; }
    }
}
```

### 3.3 Contrôleur `ProductsController` (page 13, extrait)

```csharp
[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly ProductsAPIContext _context;

    public ProductsController(ProductsAPIContext context)
    {
        _context = context;
    }

    // GET: api/Products
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProduct()
    {
        return await _context.Product.ToListAsync();
    }
}
```

### 3.4 Contrôleur `OrdersController` (page 19, extrait)

```csharp
[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly OrdersAPIContext _context;

    public OrdersController(OrdersAPIContext context)
    {
        _context = context;
    }

    // GET: api/Orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Orders>>> GetOrders()
    {
        return await _context.Orders.ToListAsync();
    }
}
```

### 3.5 Contrat `ProductCreated` (page 39)

```csharp
namespace Shared.Models
{
    public class ProductCreated
    {
        public int Id { get; set; }
        public string Name { get; set; } = String.Empty;
    }
}
```

### 3.6 Publication dans `ProductsController` (page 43)

```csharp
[HttpPost]
public async Task<ActionResult<Product>> PostProduct(Product product)
{
    _context.Product.Add(product);
    await _context.SaveChangesAsync();

    await _publishEndpoint.Publish<ProductCreated>(new ProductCreated
    {
        Id = product.ProductId,
        Name = product.Name
    });

    return CreatedAtAction("GetProduct", new { id = product.ProductId }, product);
}
```

### 3.7 Consumer (page 47)

```csharp
using MassTransit;
using OrdersAPI.Data;
using Shared.Models;

namespace OrdersAPI.Consumer
{
    public class ProductCreatedConsumer : IConsumer<ProductCreated>
    {
        private readonly OrdersAPIContext _OrdersAPIContext;

        public ProductCreatedConsumer(OrdersAPIContext ordersAPIContext)
        {
            _OrdersAPIContext = ordersAPIContext;
        }

        public async Task Consume(ConsumeContext<ProductCreated> context)
        {
            var newProduct = new Product
            {
                //Id = context.Message.Id,
                Name = context.Message.Name
            };

            _OrdersAPIContext.Add(newProduct);
            await _OrdersAPIContext.SaveChangesAsync();
        }
    }
}
```

### 3.8 Configuration MassTransit du consumer (page 49)

```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(new Uri("rabbitmq://localhost:4001"), h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("event-listener", e =>
        {
            e.ConfigureConsumer<ProductCreatedConsumer>(context);
        });
    });
});
```

### 3.9 Ocelot.json (page 24)

```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/Products",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 7066 }],
      "UpstreamPathTemplate": "/apigateway/Products",
      "UpstreamHttpMethod": ["GET", "PUT", "POST"]
    },
    {
      "DownstreamPathTemplate": "/api/Orders",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 7065 }],
      "UpstreamPathTemplate": "/apigateway/Orders",
      "UpstreamHttpMethod": ["GET", "PUT", "POST"]
    },
    {
      "DownstreamPathTemplate": "/api/Products/{id}",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 7066 }],
      "UpstreamPathTemplate": "/apigateway/products/{id}",
      "UpstreamHttpMethod": ["Get", "Post"]
    },
    {
      "DownstreamPathTemplate": "/api/Orders/{id}",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 7065 }],
      "UpstreamPathTemplate": "/apigateway/Orders/{id}",
      "UpstreamHttpMethod": ["Get", "Post"]
    }
  ]
}
```

---

## 4. Anomalies, ambiguïtés et limites du support

| # | Page(s) | Constat | Impact | Décision de réalisation |
|---:|---|---|---|---|
| A1 | 14 | Le premier écran de migration montre `Projet par défaut : OrdersAPI` alors que le texte impose `ProductsAPI`. | Risque de migrer la mauvaise base. | Migration exécutée explicitement avec `--project ProductsAPI --startup-project ProductsAPI`. |
| A2 | 24 | Les routes `/{id}` acceptent `POST` mais pas `PUT`/`DELETE` ; les routes collection n'ont pas `DELETE`. | CRUD incomplet via Gateway. | Routes complétées : GET/POST/PUT/DELETE sur collection, GET/PUT/DELETE sur `{id}`. |
| A3 | 24 | `DownstreamScheme` vaut `https` avec les ports 7066/7065. | Dépend de la confiance du certificat de développement. | Scheme `http` en local, mêmes numéros de ports ; documenté. |
| A4 | 26 | `app.UseOcelot().Wait()` est l'API historique. | Blocage potentiel / API obsolète. | `await app.UseOcelot();` avec Ocelot 23.4.3. |
| A5 | 35 | L'annotation « changer ce 1er numéro de port vers 4001 » pointe vers le port hôte `5672` de `-p 5672:5672`, alors que l'écran Docker Desktop page 36 montre `5672:5672` actif. | Divergence de port dans le support. | Conformément à la validation utilisateur (`utiliser docker juste rabbitMQ`), RabbitMQ tourne seul dans Docker (`rabbitmq:3-management`) sur le port standard `5672` (et `15672` UI). |
| A6 | 40 | `MassTransit.AspNetCore` n'existe plus pour MassTransit 8 ; l'intégration ASP.NET Core est dans `MassTransit`. | Package obsolète / incompatible. | `MassTransit` + `MassTransit.RabbitMQ` 8.2.3. |
| A7 | 41, 49 | Le port 4001 est un port Docker personnalisé. | Non aligné avec le conteneur standard `5672:5672` de la page 36. | Port AMQP standard `5672` retenu après confirmation utilisateur. |
| A8 | 42 | Le texte parle de `ProductController`, le code de `ProductsController`. | Confusion de nom. | `ProductsController` conservé (nom réel du projet). |
| A9 | 47 | `//Id = context.Message.Id` est commenté ; l'identité locale est auto-générée. | Un message rejoué crée un doublon ; pas d'idempotence. | `Id` source conservé comme clé primaire + vérification d'existence. |
| A10 | 44, 52 | ProductsAPI utilise un DbSet `Product` singulier ; OrdersAPI utilise `Products` pluriel. | Noms de tables différents. | Table `Products` dans les deux bases, conformément à l'addendum SQLite ; documenté. |
| A11 | 15, 21 | Swagger ne montre pas DELETE. | CRUD partiel dans le support. | DELETE ajouté aux deux contrôleurs et à Ocelot. |
| A12 | 39 | Namespace `Shared.Models` ; la structure cible demandée place le fichier dans `Shared/Messages`. | Divergence d'organisation. | `Shared/Messages/ProductCreated.cs`, namespace `Shared.Messages` ; contrat identique. |
| A13 | 51–52 | Le dernier écran ne montre qu'un seul produit synchronisé, alors que ProductsAPI en contient plusieurs. | Peut faire croire à une synchronisation partielle. | Explication : le consumer n'était pas actif lors des créations antérieures ; le test E2E crée un produit après démarrage. |
| A14 | 22 | Ocelot 18.0.0 est ancien. | Compatibilité .NET 7 à vérifier. | Ocelot 23.4.3, cible `net7.0`, dernière version compatible. |
| A15 | global | SQL Server dans les captures, aucune configuration SQLite. | Contrainte de la nouvelle mission. | SQLite + EF Core 7.0.20, deux fichiers indépendants natifs. |
| A16 | global | Solution nommée `MicroservicesApp` dans le support. | Nom différent de la cible. | Solution `MicroservicesTP`. |
| A17 | 16, 28 | `Orders.OrderDate` est déclaré `{ get; }` (sans `set;`) page 16, expliquant `"0001-01-01T00:00:00"` dans la capture Postman page 28. | La désérialisation JSON ne peut pas affecter la date de commande. | `{ get; set; }` ajouté sur `OrderDate`. |

---

## 5. Exigences implicites déduites du support

Le support ne formule pas explicitement ces points, mais ils sont nécessaires au fonctionnement :

1. `ProductsAPIContext` doit exposer un `DbSet<Product>` et être enregistré dans le conteneur DI.
2. `OrdersAPIContext` doit exposer `Orders` et `Products`, et être enregistré dans le conteneur DI.
3. Les migrations doivent être générées et appliquées avant le premier appel HTTP.
4. Les ports réels des API doivent correspondre à ceux d'`Ocelot.json`.
5. Les deux microservices doivent référencer `Shared` et utiliser le même contrat.
6. Le consumer doit être enregistré dans MassTransit **et** rattaché à `event-listener`.
7. Le message doit être publié après la sauvegarde effective du produit.
8. RabbitMQ doit être démarré avant les microservices, sinon MassTransit ne peut pas se connecter.
9. Les identifiants RabbitMQ `guest/guest` ne sont valides que depuis localhost par défaut.
10. La table locale `Products` d'OrdersAPI ne doit contenir que les informations utiles (`Id`, `Name`).

---

## 6. Éléments illisibles ou manquants

- Aucune page n'est totalement illisible. Les captures de code des pages 13, 19, 42, 43, 47 et 49 sont **partielles** (le bas de l'écran est coupé) ; les méthodes non visibles (PUT, DELETE, détails du scaffolding) ne peuvent pas être reconstituées à partir du PDF seul.
- La page 23 ne contient pas le code JSON (il est page 24).
- Les pages 15 et 21 ne montrent pas DELETE dans Swagger.
- Le PDF ne montre pas :
  - la configuration `UseSqlite` ni les chaînes de connexion (puisqu'il utilise SQL Server) ;
  - les fichiers de migration générés ;
  - la configuration `appsettings.json` des API ;
  - les commandes `dotnet ef` (il utilise la Package Manager Console) ;
  - la gestion des erreurs, la validation, l'idempotence, les secrets.
- Le PDF ne précise pas si `CustomerName` doit être obligatoire ; il est nullable dans le code.
- Le PDF ne précise pas le comportement attendu en cas d'échec de publication RabbitMQ après sauvegarde.

---

## 7. Conclusion de l'audit

L'atelier est **compréhensible et reproductible** sur le plan pédagogique. Les briques fonctionnelles sont claires :

- deux API REST avec EF Core ;
- une Gateway Ocelot ;
- un contrat partagé ;
- un publisher MassTransit ;
- un consumer MassTransit idempotent ;
- deux bases indépendantes ;
- une vérification de bout en bout.

Les corrections nécessaires sont limitées et documentées : SQLite au lieu de SQL Server, Docker utilisé exclusivement pour le broker RabbitMQ (`rabbitmq:3-management` sur port 5672/15672) conformément à la validation utilisateur, microservices ASP.NET Core natifs sur macOS, Ocelot/MassTransit aux versions compatibles .NET 7, CRUD complet, correction `OrderDate { get; set; }`, idempotence du consumer, et sécurisation de la configuration. Aucune fonctionnalité pédagogique du support n'est supprimée.
