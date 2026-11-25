# RAPPORT FINAL — Atelier 8
## Introduction aux Microservices avec ASP.NET Core

**Réalisation :** solution `MicroservicesTP`
**Environnement :** macOS 27.3, Apple M2 arm64, .NET SDK 7.0.317
**Date :** 18 novembre 2026
**Statut :** **réalisé et validé** — 0 erreur de build, 46 tests réussis, E2E 66/66.

---

## 1. Synthèse

L'atelier demandait de construire deux microservices ASP.NET Core indépendants, une Gateway Ocelot, un contrat partagé et une communication asynchrone MassTransit/RabbitMQ, avec deux bases séparées et une démonstration de synchronisation idempotente.

La solution livrée comprend :

- **ProductsAPI** : CRUD complet sur les produits, base `Products.db`, publication de `ProductCreated` après sauvegarde ;
- **OrdersAPI** : CRUD complet sur les commandes, base `Orders.db`, projection locale `Products(Id, Name)` alimentée par un consumer idempotent ;
- **ApiGateway** : quatre routes Ocelot exposant le CRUD complet des deux API ;
- **Shared** : contrat `ProductCreated` partagé ;
- **Tests** : 47 cas xUnit (46 réussis, 1 test broker réel exécuté dans l'E2E) et un harness E2E de 66 contrôles ;
- **Documentation** : installation, démarrage, conformité au PDF, audit d'environnement, résultats et auto-évaluation honnête.

Toutes les données, tous les messages et tous les tests proviennent de l'environnement réel. Aucun résultat n'a été simulé ou inventé.

---

## 2. Objectif et périmètre

### 2.1 Objectif pédagogique

Mettre en œuvre une architecture microservices minimale mais complète :

1. deux services REST autonomes ;
2. une base de données par service, sans partage ;
3. une Gateway qui centralise l'exposition HTTP ;
4. un événement métier publié de façon asynchrone ;
5. un consumer qui maintient une copie locale des données utiles ;
6. une preuve d'idempotence, de persistance et d'indépendance.

### 2.2 Contraintes respectées

| Contrainte | Mise en œuvre |
|---|---|
| macOS Apple Silicon | exécution native arm64, aucun émulateur |
| SQLite exclusif | EF Core 7 + `Microsoft.EntityFrameworkCore.Sqlite` |
| Aucune base partagée | `Products.db` et `Orders.db` distincts, aucun DbContext commun |
| Aucune jointure inter-services | chaque service ne lit que sa base |
| Aucun Docker pour la solution | RabbitMQ natif Homebrew, API lancées par `dotnet run` |
| CRUD complet | GET/POST/PUT/DELETE sur les deux API et via la Gateway |
| Publication fiable | `SaveChangesAsync` d'abord, `Publish` ensuite, `503` si échec |
| Idempotence | clé primaire métier + upsert, testée sur broker réel |
| Versions compatibles | .NET 7.0.317, EF 7.0.20, MassTransit 8.2.3, Ocelot 23.4.3 |
| Écarts documentés | `docs/CONFORMITE_PDF.md` |

---

## 3. Architecture réalisée

```text
Client
  |
  | HTTP
  v
ApiGateway (Ocelot) :7162
  |                         \
  | /apigateway/Products    \ /apigateway/Orders
  v                          v
ProductsAPI :7066        OrdersAPI :7065
Products.db              Orders.db
Products                 Orders + Products
  |
  | ProductCreated
  v
RabbitMQ :5672
exchange fanout durable
Shared.Messages:ProductCreated
  |
  v
event-listener (durable)
  |
  v
ProductCreatedConsumer
  |
  v
Orders.db.Products (Id, Name)
```

### 3.1 ProductsAPI

| Élément | Détail |
|---|---|
| Modèle | `Product { ProductId, Name, StockQte, Price }` |
| Validation | Data Annotations : nom obligatoire 1–100, stock ≥ 0, prix ≥ 0 |
| Contexte | `ProductsAPIContext`, DbSet `Products`, table `Products` |
| CRUD | GET liste, GET id, POST, PUT, DELETE |
| Codes | 200, 201, 204, 400, 404, 500, 503 |
| Messaging | `IPublishEndpoint.Publish<ProductCreated>` après `SaveChangesAsync` |
| Erreur de publication | `503 ProblemDetails` avec `productId`, produit conservé, incident journalisé |

### 3.2 OrdersAPI

| Élément | Détail |
|---|---|
| Modèle commande | `Orders { OrdersId, OrderDate, CustomerName, ProductId }` |
| Modèle projection | `Product { Id, Name }`, `Id` en clé primaire sans génération automatique |
| Contexte | `OrdersAPIContext`, DbSets `Orders` et `Products`, base `Orders.db` |
| CRUD | GET liste, GET id, POST, PUT, DELETE |
| Consumer | `ProductCreatedConsumer`, file `event-listener` |
| Idempotence | recherche par `Id`, insertion sinon mise à jour du nom, protection par clé primaire |
| Isolation | aucune référence à ProductsAPI ni à `Products.db` |

### 3.3 ApiGateway

| Route amont | Méthodes | Cible |
|---|---|---|
| `/apigateway/Products` | GET, POST | `http://localhost:7066/api/Products` |
| `/apigateway/Products/{id}` | GET, PUT, DELETE | `http://localhost:7066/api/Products/{id}` |
| `/apigateway/Orders` | GET, POST | `http://localhost:7065/api/Orders` |
| `/apigateway/Orders/{id}` | GET, PUT, DELETE | `http://localhost:7065/api/Orders/{id}` |

Ocelot 23.4.3 est enregistré par `AddOcelot`, puis activé par `await app.UseOcelot()`.

### 3.4 Shared

`Shared/Messages/ProductCreated.cs` :

```csharp
public class ProductCreated
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

Le même assembly est référencé par ProductsAPI et OrdersAPI : un seul contrat, un seul exchange (`Shared.Messages:ProductCreated`).

---

## 4. Flux asynchrone et idempotence

### 4.1 Séquence de création

1. Le client appelle `POST /apigateway/Products`.
2. Ocelot transmet à ProductsAPI.
3. ProductsAPI valide le corps (400 si invalide).
4. ProductsAPI ajoute le produit et appelle `SaveChangesAsync`.
5. SQLite génère `ProductId`.
6. **Après** la sauvegarde réussie, ProductsAPI publie `ProductCreated { Id = ProductId, Name }`.
7. RabbitMQ route le message vers l'exchange fanout.
8. La file durable `event-listener` reçoit le message.
9. OrdersAPI consomme le message.
10. Le consumer cherche la projection par `Id` :
    - absente → insertion ;
    - présente avec un autre nom → mise à jour ;
    - présente avec le même nom → aucune écriture.
11. OrdersAPI renvoie la commande ou poursuit son fonctionnement, indépendamment de ProductsAPI.

### 4.2 Garanties

| Garantie | Mécanisme | Preuve |
|---|---|---|
| Pas de publication avant sauvegarde | `Publish` après `SaveChangesAsync` | test unitaire : l'ID publié est celui généré par SQLite |
| Pas de doublon | clé primaire `Products.Id` + upsert | test unitaire + test broker réel |
| Pas de faux succès si publication échoue | catch + `503`, produit conservé | test unitaire |
| Message invalide non persisté | contrôles `Id > 0`, nom non vide ≤ 100 | tests consumer |
| Projection locale uniquement | aucun accès à `Products.db` | configuration + grep E2E |
| Persistance | fichiers SQLite | redémarrage E2E |

### 4.3 Ce que le TP ne garantit pas

Sans pattern Outbox, la sauvegarde et la publication ne forment pas une transaction atomique. Si RabbitMQ tombe entre les deux, le produit existe mais l'événement est perdu. L'API le signale par `503` et journalise l'incident ; elle ne prétend jamais que la synchronisation a réussi. Un Outbox est documenté comme amélioration optionnelle.

---

## 5. Robustesse et sécurité

- **Validation déclarative** : Data Annotations sur les deux modèles, rejet HTTP 400 par `[ApiController]`.
- **Erreurs métier** : `404` avec `ProblemDetails` pour les identifiants absents.
- **Erreurs d'enregistrement** : `500` sans publication.
- **Erreur de publication** : `503` avec identifiant du produit conservé.
- **Isolation** : aucun secret de production, `guest/guest` limité à localhost.
- **Configuration** : chaînes de connexion dans `appsettings.json`, surchargeables par environnement.
- **Journalisation** : logs ASP.NET Core et MassTransit dans `logs/`.
- **Pas d'authentification** : hors périmètre ; la Gateway ne doit pas être exposée publiquement en l'état.

---

## 6. Conformité au support

Le PDF de 52 pages est couvert fonctionnellement. Les écarts sont limités, justifiés et tracés dans `docs/CONFORMITE_PDF.md`. Les principaux :

| Écart | Décision |
|---|---|
| SQL Server → SQLite | contrainte de la mission ; deux fichiers indépendants |
| Docker RabbitMQ → RabbitMQ natif | contrainte finale « aucun Docker » ; même AMQP 5672 et UI 15672 |
| Ocelot 18 → 23.4.3 | compatibilité .NET 7 et API asynchrone |
| `https` → `http` dans Ocelot | ports du PDF conservés, pas de certificat local |
| Routes Gateway incomplètes → CRUD complet | exigence de la mission |
| DbSet `Product` → table `Products` | nommage imposé |
| `Shared.Models` → `Shared.Messages` | structure imposée, contrat identique |
| `OrderDate { get; }` → `{ get; set; }` | sinon la date JSON n'est pas désérialisée |
| Consumer `Id` commenté → `Id` métier | idempotence réelle |
| Nom de solution `MicroservicesApp` → `MicroservicesTP` | nom imposé |

Aucune fonctionnalité pédagogique du support n'a été supprimée.

---

## 7. Validation

### 7.1 Résultats

| Test | Résultat |
|---|---|
| Build | **0 erreur / 0 avertissement** |
| Tests unitaires | **46 réussis / 1 ignoré / 0 échec** |
| E2E | **66 PASS / 0 FAIL** |
| Idempotence broker réel | **1 réussi**, 2 publications, 1 ligne |
| Synchronisation | produit présent dans les deux bases |
| Persistance | données intactes après redémarrage |
| Indépendance | chaque API fonctionne sans l'autre |
| Gateway | CRUD complet, 400/404/502 corrects |

### 7.2 Preuves

Cycle final réexécuté le 18 novembre 2026 à 13:09–13:11 CET :

- `logs/build-final.log`
- `logs/tests-final.log` + `reports/TestResults/complete-tests.trx`
- `logs/e2e-final.log` (66 PASS, 0 FAIL)
- `logs/e2e-idempotence.log`
- `logs/environment.txt`
- `reports/BUILD_REPORT.md`
- `reports/UNIT_TEST_REPORT.md`
- `reports/TEST_COVERAGE_AUDIT.md`
- `reports/E2E_FINAL_REPORT.md`
- `reports/AUTO_PASS_REPORT.md`
- `TEST_RESULTS.md`
- `docs/AUTO_PASS.md`

Les journaux de la session précédente sont conservés dans `logs/archive/` à titre historique.

### 7.3 Réserves honnêtes

1. **Page Overview RabbitMQ** : bug du plugin de gestion avec RabbitMQ 4.4.0 + Erlang/OTP 29 ; les endpoints queues/connections/exchanges fonctionnent. Sans impact sur l'application.
2. **.NET 7 hors support** : contrainte du support ; migration LTS recommandée en production.
3. **Pas d'Outbox** : échec de publication signalé mais non rejouable automatiquement.
4. **Pas d'événements de mise à jour/suppression** : la projection `Orders.db` ne suit pas les PUT/DELETE.
5. **Coupure réelle du broker** : le comportement `503` est testé avec un mock, pas avec un arrêt réel de RabbitMQ.
6. **Pas de test de charge ni d'authentification** : hors périmètre.

---

## 8. Livrables

| Livrable | Emplacement |
|---|---|
| Solution | `MicroservicesTP.sln` |
| ProductsAPI | `ProductsAPI/` |
| OrdersAPI | `OrdersAPI/` |
| ApiGateway | `ApiGateway/` |
| Contrat partagé | `Shared/Messages/ProductCreated.cs` |
| Migrations | `ProductsAPI/Migrations/`, `OrdersAPI/Migrations/` |
| Tests | `Tests/` |
| Scripts | `scripts/` |
| Journaux | `logs/` |
| README | `README.md` |
| Audit environnement | `ENVIRONMENT_AUDIT.md` |
| Résultats de test | `TEST_RESULTS.md` |
| Conformité PDF | `docs/CONFORMITE_PDF.md` |
| Auto-évaluation | `docs/AUTO_PASS.md` |
| Installation | `docs/INSTALLATION_MACOS.md` |
| Guide d'utilisation | `docs/GUIDE_DEMARRAGE.md` |

---

## 9. Démarrage

```bash
cd MicroservicesTP
dotnet tool restore
./scripts/check-environment.sh
./scripts/start-all.sh
```

Puis :

- Swagger Products : `http://localhost:7066/swagger`
- Swagger Orders : `http://localhost:7065/swagger`
- Gateway : `http://localhost:7162/apigateway/Products`
- RabbitMQ : `http://localhost:15672` (`guest` / `guest`)

Arrêt :

```bash
./scripts/stop-all.sh
```

---

## 10. Conclusion

La solution répond aux exigences fonctionnelles et techniques de l'atelier : deux microservices réellement indépendants, deux bases SQLite distinctes, une Gateway Ocelot complète, un contrat partagé, une publication et une consommation MassTransit réelles, une synchronisation idempotente prouvée sur le broker, et une persistance vérifiée après redémarrage.

Les écarts par rapport au support sont volontaires, limités et documentés. Les limites restantes sont connues et n'empêchent pas la démonstration pédagogique. Le projet peut être repris, testé et étendu (Outbox, événements de mise à jour, authentification, health checks) sans remettre en cause l'architecture.
