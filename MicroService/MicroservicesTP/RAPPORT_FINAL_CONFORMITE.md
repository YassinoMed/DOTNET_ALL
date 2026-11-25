# RAPPORT FINAL DE CONFORMITÉ — Atelier 8 Microservices

**Projet :** `MicroservicesTP` — ASP.NET Core 7, EF Core, MassTransit/RabbitMQ, Ocelot, SQLite
**Date :** 18 novembre 2026
**Solution :** `MicroservicesTP.sln`
**Cycle :** audit → build → tests → E2E → audit final → documentation → auto-pass
**Verdict :** **PASS_WITH_RESERVATIONS** (voir `reports/AUTO_PASS_REPORT.md`)

Légende des statuts utilisés dans ce rapport :

| Statut | Signification |
|---|---|
| **RÉALISÉ** | implémenté dans le code livré |
| **VÉRIFIÉ** | exécuté et prouvé pendant le cycle final (18/11/2026, 13:09–13:11 CET) |
| **PARTIEL** | fonctionne, mais un point annexe n'est pas couvert |
| **NON TESTÉ** | aucune preuve d'exécution ; non compté comme réussi |
| **NON IMPLÉMENTÉ** | absent du périmètre actuel |
| **FUTUR** | recommandation, pas une fonctionnalité existante |

---

## 1. Introduction

Ce rapport clôt la livraison de l'atelier 8. Il décrit ce qui a été construit, ce qui a été réellement vérifié, et ce qui reste hors périmètre. Il ne remplace pas les preuves : chaque affirmation renvoie à un journal ou à un rapport daté.

Le cycle final a réexécuté, dans cet ordre :

1. audit en lecture seule et baseline (`FINAL_AUDIT_BASELINE.md`) ;
2. build complet (`logs/build-final.log`) ;
3. suite xUnit complète (`logs/tests-final.log`, `reports/TestResults/complete-tests.trx`) ;
4. E2E 66 contrôles (`logs/e2e-final.log`) ;
5. contrôle d'intégrité SQLite en mode immuable ;
6. documentation et validation finale (`scripts/validate-final.sh`).

Aucun code applicatif n'a été modifié pendant ce cycle : l'audit n'a confirmé aucun défaut P0/P1 justifiant une correction. Aucune base n'a été supprimée, migrée ou réinitialisée.

---

## 2. Objectifs pédagogiques

| Objectif du support | Statut | Preuve |
|---|---|---|
| Deux microservices ASP.NET Core indépendants | **RÉALISÉ / VÉRIFIÉ** | 5 projets, deux API sur ports distincts, E2E section 8 |
| Base SQLite par service | **RÉALISÉ / VÉRIFIÉ** | `Products.db` et `Orders.db`, chaînes `Data Source` distinctes |
| Communication asynchrone par broker | **RÉALISÉ / VÉRIFIÉ** | MassTransit 8.2.3 + RabbitMQ natif, E2E sections 2–4 |
| Contrat partagé | **RÉALISÉ / VÉRIFIÉ** | `Shared.Messages.ProductCreated`, assembly commun |
| API Gateway | **RÉALISÉ / VÉRIFIÉ** | Ocelot 23.4.3, 4 routes, E2E sections 5–8 |
| CRUD complet | **RÉALISÉ / VÉRIFIÉ** | tests contrôleurs + E2E |
| Tests automatisés | **RÉALISÉ / VÉRIFIÉ** | 47 cas xUnit, 66 contrôles E2E |
| Documentation reproductible | **RÉALISÉ / VÉRIFIÉ** | `README.md`, `docs/`, scripts, ce rapport |

Aucun objectif obligatoire du support n'est déclaré non réalisé.

---

## 3. Architecture

```text
Client HTTP
    │
    ▼
ApiGateway (Ocelot 23.4.3, :7162)
    │  /apigateway/Products/*        /apigateway/Orders/*
    ▼                                ▼
ProductsAPI (:7066)              OrdersAPI (:7065)
    │  EF Core 7.0.20                │  EF Core 7.0.20
    ▼                                ▼
Products.db                      Orders.db
    │                                ▲
    │ Publish ProductCreated         │ Consume ProductCreated
    └──────────► RabbitMQ 4.4.0 ◄────┘
                 exchange fanout
                 file event-listener
```

| Composant | Rôle | Statut |
|---|---|---|
| `Shared` | contrat `ProductCreated` | **RÉALISÉ** |
| `ProductsAPI` | CRUD produits, persistance, publication | **RÉALISÉ / VÉRIFIÉ** |
| `OrdersAPI` | CRUD commandes, consumer, projection locale | **RÉALISÉ / VÉRIFIÉ** |
| `ApiGateway` | routage Ocelot | **RÉALISÉ / VÉRIFIÉ** |
| `Tests` | 47 cas xUnit | **RÉALISÉ / VÉRIFIÉ** |
| RabbitMQ | broker natif Homebrew | **VÉRIFIÉ** (4.4.0, OTP 29.1.2) |

---

## 4. ProductsAPI

| Exigence | Statut | Détail / preuve |
|---|---|---|
| CRUD GET/POST/PUT/DELETE | **VÉRIFIÉ** | `ProductsControllerTests` (8 cas) + E2E section 5 (13 contrôles) |
| Validation des entrées | **VÉRIFIÉ** | Data Annotations ; nom vide et prix négatif → 400 en E2E |
| Identifiant inexistant | **VÉRIFIÉ** | GET/PUT/DELETE → 404 |
| Persistance SQLite | **VÉRIFIÉ** | produit 11 relu après redémarrage (E2E section 7) |
| Publication `ProductCreated` | **VÉRIFIÉ** | exchange fanout, message consommé, file vide |
| Sauvegarde avant publication | **VÉRIFIÉ** | test unitaire : l'ID SQLite généré est celui publié |
| Échec de publication → 503 | **VÉRIFIÉ** | test unitaire avec `IPublishEndpoint` mocké |
| Produit conservé après échec de publication | **VÉRIFIÉ** | test unitaire dédié |
| Pas de publication après échec de sauvegarde | **VÉRIFIÉ** | test unitaire dédié |
| Pas de fuite d'internes dans les erreurs | **VÉRIFIÉ** | `ProblemDetails`, pas de stack trace exposée |

Limite assumée : la sauvegarde et la publication ne sont pas atomiques (pas d'Outbox). Voir § 8 et § 16.

---

## 5. OrdersAPI

| Exigence | Statut | Détail / preuve |
|---|---|---|
| CRUD GET/POST/PUT/DELETE | **VÉRIFIÉ** | `OrdersControllerTests` (6 cas) + E2E section 6 (10 contrôles) |
| Base indépendante | **VÉRIFIÉ** | `Data Source=Orders.db` uniquement ; aucune référence à `Products.db` |
| Consumer `ProductCreatedConsumer` | **VÉRIFIÉ** | file `event-listener`, 1 consumer observé |
| File durable | **VÉRIFIÉ** | file et exchange durables (E2E section 2) |
| Projection locale `Products(Id, Name)` | **VÉRIFIÉ** | 13 lignes finales, dont produit 11 |
| Validation des événements | **VÉRIFIÉ** | `Id <= 0`, nom vide, nom > 100 → exception, message en file d'erreur |
| Idempotence | **VÉRIFIÉ** | upsert par clé primaire + test broker réel (2 publications, 1 ligne) |
| Gestion des doublons | **VÉRIFIÉ** | 6 tests consumer, dont rejeu et mise à jour |
| Messages concurrents | **PARTIEL** | `DbUpdateException` gérée ; aucun test de charge concurrente |
| Exceptions de consommation | **VÉRIFIÉ** | échec → file d'erreur MassTransit, pas de perte silencieuse |
| Aucun accès direct à ProductsAPI/Products.db | **VÉRIFIÉ** | inspection code + configuration |

---

## 6. ApiGateway

| Exigence | Statut | Détail / preuve |
|---|---|---|
| Enregistrement Ocelot | **VÉRIFIÉ** | `AddOcelot` + `Ocelot.json` chargé explicitement |
| Middleware Ocelot | **VÉRIFIÉ** | `await app.UseOcelot()` (API asynchrone moderne) |
| Quatre routes CRUD | **VÉRIFIÉ** | 2 routes Products + 2 routes Orders, 5 méthodes |
| Méthodes HTTP correctes | **VÉRIFIÉ** | GET/POST sur collection, GET/PUT/DELETE sur `{id}` |
| Correspondance des identifiants | **VÉRIFIÉ** | `{id}` transmis tel quel ; E2E 404 sur id inconnu |
| Code 400 | **VÉRIFIÉ** | validation relayée depuis les API |
| Code 404 | **VÉRIFIÉ** | produit/commande/route inconnus |
| Code 502 | **VÉRIFIÉ** | ProductsAPI arrêté → Gateway 502, pas de faux succès |
| Absence de routes incorrectes | **VÉRIFIÉ** | `OcelotConfigurationTests` (15 cas) |
| Service arrêté | **VÉRIFIÉ** | E2E section 8 |

Limite : pas de retry/circuit breaker Ocelot (hors périmètre).

---

## 7. Shared

| Point | Statut |
|---|---|
| Contrat `ProductCreated` unique | **RÉALISÉ** |
| Même assembly référencé par les deux API | **VÉRIFIÉ** (`Shared.csproj` référencé par `ProductsAPI` et `OrdersAPI`) |
| Propriétés `Id`, `Name` non dupliquées | **VÉRIFIÉ** |
| Aucune modification de contrat pendant le cycle final | **VÉRIFIÉ** |

Le contrat n'a pas été renommé ni étendu : les deux services partagent exactement le même type.

---

## 8. RabbitMQ et MassTransit

| Point | Statut | Preuve |
|---|---|---|
| Broker natif Homebrew (pas de Docker) | **VÉRIFIÉ** | PID `beam.smp`, `rabbitmq-diagnostics ping` |
| Version RabbitMQ | **VÉRIFIÉ** | 4.4.0, Erlang/OTP 29.1.2 |
| MassTransit | **VÉRIFIÉ** | 8.2.3 dans les deux API |
| Exchange `ProductCreated` | **VÉRIFIÉ** | fanout durable `Shared.Messages:ProductCreated` |
| File `event-listener` | **VÉRIFIÉ** | durable, 1 consumer |
| Connexions MassTransit | **VÉRIFIÉ** | 2 connexions observées |
| Publication après persistance | **VÉRIFIÉ** | test unitaire + E2E |
| Gestion broker indisponible | **PARTIEL** | 503 testé par mock ; coupure réelle `NOT_TESTED` |
| Page `/api/overview` | **PARTIEL** | bug RabbitMQ 4.4.0 + OTP 29 ; `/api/queues` fonctionne |
| Outbox transactionnel | **NON IMPLÉMENTÉ** | limite documentée, évolution prioritaire |
| Retry consumer explicite | **NON IMPLÉMENTÉ** | échec → file d'erreur MassTransit |
| Surveillance DLQ | **NON IMPLÉMENTÉ** | `event-listener_error` non monitorée |
| Identifiants `guest/guest` | **VÉRIFIÉ local** | limités à `localhost`, jamais présentés comme production |

---

## 9. Synchronisation asynchrone

| Point | Statut | Preuve |
|---|---|---|
| Produit créé via Gateway | **VÉRIFIÉ** | POST 201, produit 11 |
| Produit persisté dans Products.db | **VÉRIFIÉ** | ligne 11 présente |
| Événement publié sur le broker | **VÉRIFIÉ** | exchange fanout, file consommée |
| Projection créée dans Orders.db | **VÉRIFIÉ** | ligne 11 présente, nom identique |
| File vidée après consommation | **VÉRIFIÉ** | 0 message en attente |
| Persistance après redémarrage | **VÉRIFIÉ** | produit et projection toujours présents |
| Synchronisation des mises à jour | **NON IMPLÉMENTÉ** | aucun événement `ProductUpdated` |
| Synchronisation des suppressions | **NON IMPLÉMENTÉ** | aucun événement `ProductDeleted` |

Le flux nominal `ProductCreated` est prouvé de bout en bout. Les flux de mise à jour et de suppression ne font pas partie du contrat actuel.

---

## 10. Idempotence

| Niveau | Statut | Preuve |
|---|---|---|
| Consumer : rejeu du même événement | **VÉRIFIÉ** | 6 tests unitaires + test broker réel |
| Consumer : clé primaire métier | **VÉRIFIÉ** | `Products.Id` sans AUTOINCREMENT dans Orders.db |
| Consumer : upsert | **VÉRIFIÉ** | recherche puis insertion/mise à jour |
| Consumer : course concurrente | **PARTIEL** | `DbUpdateException` gérée, pas de test de stress |
| Broker réel : 2 publications → 1 ligne | **VÉRIFIÉ** | E2E section 4, nom mis à jour sans doublon |
| HTTP : clé d'idempotence | **NON IMPLÉMENTÉ** | un POST rejoué après 503 crée un second produit |

Le test broker réel est le 47e cas xUnit. Il est `SKIP` dans `dotnet test`, puis exécuté avec succès par `scripts/e2e.sh` (`RABBITMQ_E2E=1`). Il n'existe pas de 48e test.

---

## 11. Isolation des bases

| Contrôle | Statut | Preuve |
|---|---|---|
| `ProductsAPI` → `Products.db` uniquement | **VÉRIFIÉ** | `appsettings.json` + E2E section 8 |
| `OrdersAPI` → `Orders.db` uniquement | **VÉRIFIÉ** | `appsettings.json` + E2E section 8 |
| Aucune référence croisée dans le code | **VÉRIFIÉ** | grep du code et des configurations |
| Migrations indépendantes | **VÉRIFIÉ** | deux historiques EF distincts |
| Intégrité après E2E | **VÉRIFIÉ** | `PRAGMA integrity_check = ok` sur les deux bases |
| Suppression/réinitialisation | **AUCUNE** | aucune commande destructive exécutée |

État final : `Products.db` = 9 produits ; `Orders.db` = 13 projections, 0 commande. Les projections des produits supprimés (`4`, `7`, `10`, `13`) restent volontairement, faute d'événement de suppression.

---

## 12. Sécurité

| Point | Statut | Commentaire |
|---|---|---|
| Identifiants RabbitMQ | **LIMITE ASSUMÉE** | `guest/guest` local, limité à `localhost` |
| Chaînes SQLite | **VÉRIFIÉ** | chemins relatifs locaux, aucun secret |
| Secrets en clair dans le dépôt | **VÉRIFIÉ** | aucun secret de production |
| Journalisation des exceptions | **VÉRIFIÉ** | pas de données sensibles dans les réponses HTTP |
| Timeouts HTTP | **PARTIEL** | timeouts par défaut ASP.NET Core ; pas de politique explicite |
| Configuration CORS | **NON IMPLÉMENTÉ** | aucune politique CORS déclarée |
| Exposition réseau | **LIMITE ASSUMÉE** | services sur `localhost` en développement |
| Authentification / autorisation | **NON IMPLÉMENTÉ** | hors périmètre pédagogique |
| TLS | **NON IMPLÉMENTÉ** | hors périmètre pédagogique |
| Swagger | **LIMITE ASSUMÉE** | exposé dans tous les environnements |
| Validation des entrées | **VÉRIFIÉ** | Data Annotations côté API |
| Requêtes SQL | **VÉRIFIÉ** | EF Core paramétré, pas de SQL concaténé |

Aucun secret réel n'est présent. `guest/guest` ne doit jamais être utilisé en production. Aucune authentification n'est revendiquée.

---

## 13. Tests

| Indicateur | Valeur réelle |
|---|---:|
| Cas xUnit découverts | **47** |
| Cas exécutés | **46** |
| Réussis | **46** |
| Échoués | **0** |
| Ignorés | **1** (broker réel) |
| Méthodes de test | 33 (dont `[Theory]`) |
| Contrôles E2E | **66** |
| Contrôles E2E échoués | **0** |
| Exécution broker réel dans l'E2E | **1** (même cas, pas un 48e) |

Répartition : `ModelValidationTests` 10, `OcelotConfigurationTests` 15, `OrdersControllerTests` 6, `ProductCreatedConsumerTests` 6, `ProductsControllerTests` 8, `SqlitePersistenceTests` 1, `RabbitMqIdempotenceTests` 1.

Preuves : `logs/tests-final.log`, `reports/TestResults/complete-tests.trx`, `logs/e2e-final.log`, `reports/TEST_COVERAGE_AUDIT.md`.

Aucun test n'a été ajouté pour gonfler le décompte. Aucun échec n'a été transformé en skip.

---

## 14. Résultats E2E

| Section | PASS | FAIL |
|---|---:|---:|
| 0. Prérequis | 7 | 0 |
| 1. Démarrage des trois services | 4 | 0 |
| 2. Topologie RabbitMQ | 3 | 0 |
| 3. Synchronisation ProductsAPI → OrdersAPI | 7 | 0 |
| 4. Idempotence sur le vrai broker | 5 | 0 |
| 5. CRUD Products via la Gateway | 13 | 0 |
| 6. CRUD Orders via la Gateway | 10 | 0 |
| 7. Persistance après redémarrage | 8 | 0 |
| 8. Indépendance des microservices et des bases | 9 | 0 |
| **Total** | **66** | **0** |

Les 18 scénarios demandés sont couverts : 17 **PASS**, 1 **PARTIEL** (scénario 18 : indisponibilité HTTP testée avec 502 ; coupure réelle de RabbitMQ `NOT_TESTED`). Détail : `reports/E2E_FINAL_REPORT.md`.

---

## 15. Conformité au PDF

| Aspect | Statut |
|---|---|
| Architecture deux API + Gateway + contrat partagé | **CONFORME** |
| SQLite séparées | **CONFORME** |
| CRUD complet | **CONFORME** |
| Publication/consommation RabbitMQ | **CONFORME** |
| Ocelot | **CONFORME** (API asynchrone moderne au lieu de `UseOcelot().Wait()`) |
| Tests | **CONFORME** |
| Documentation | **CONFORME** |
| Écarts assumés | **20 écarts justifiés** dans `docs/CONFORMITE_PDF.md` |

Le PDF n'est pas déclaré « couvert intégralement » : les écarts (Swagger, gestion d'erreurs moderne, absence d'Outbox, absence d'événements de mise à jour) sont listés et justifiés. Aucune fonctionnalité non implémentée n'est présentée comme existante.

---

## 16. Limites connues

| # | Limite | Impact | Statut |
|---:|---|---|---|
| L1 | Pas d'Outbox transactionnel | Produit persisté sans événement si crash entre commit et publish | **NON IMPLÉMENTÉ** |
| L2 | Pas d'événements `ProductUpdated` / `ProductDeleted` | La projection Orders.db diverge après PUT/DELETE produit | **NON IMPLÉMENTÉ** |
| L3 | .NET 7 hors support depuis mai 2024 | Pas de correctifs de sécurité runtime | **CONTRAINTE ENVIRONNEMENT** |
| L4 | Pas d'authentification / TLS / CORS | Inadapté à une exposition publique | **NON IMPLÉMENTÉ** |
| L5 | Coupure réelle de RabbitMQ non testée | Le 503 est prouvé par mock uniquement | **NON TESTÉ** |
| L6 | Pas de clé d'idempotence HTTP | Un POST rejoué après 503 crée un doublon | **NON IMPLÉMENTÉ** |
| L7 | Page `/api/overview` RabbitMQ en 500 | Bug 4.4.0 + OTP 29, sans impact applicatif | **CONTRAINTE ENVIRONNEMENT** |
| L8 | Pas de health checks | Supervision limitée | **NON IMPLÉMENTÉ** |
| L9 | Pas de test de charge/concurrence soutenue | Comportement sous stress inconnu | **NON TESTÉ** |
| L10 | Swagger exposé partout | Surface d'information en production | **LIMITE ASSUMÉE** |

Ces limites sont explicites et ne sont pas comptées comme des réussites.

---

## 17. Améliorations futures

Par ordre de priorité recommandé (détail : `docs/ARCHITECTURE_EVOLUTION.md`) :

1. **Retry consumer + alerte DLQ** — faible effort, fort gain de robustesse.
2. **Health checks SQLite/RabbitMQ** — supervision de base.
3. **Outbox transactionnel** — atomicité persistance/publication.
4. **`ProductUpdated` / `ProductDeleted`** — cohérence de la projection.
5. **Idempotency-Key HTTP** — protection contre les doublons après 503.
6. **OpenTelemetry + Serilog** — corrélation et observabilité.
7. **Migration .NET 8/9 LTS** — support et sécurité.
8. **Authentification JWT / TLS** — avant toute exposition réelle.

Aucune de ces améliorations n'est implémentée dans la livraison actuelle.

---

## 18. Installation

Prérequis macOS :

```bash
brew install --cask dotnet-sdk
brew install rabbitmq
brew services start rabbitmq
```

Le projet cible **.NET SDK 7.0.317** (`global.json`, `rollForward: latestPatch`). Vérification :

```bash
cd MicroservicesTP
dotnet --version          # 7.0.317
dotnet tool restore
./scripts/check-environment.sh
```

Détail complet : `docs/INSTALLATION_MACOS.md`. Aucune étape Docker n'est requise ni autorisée.

---

## 19. Démarrage

```bash
cd MicroservicesTP

# Démarrer RabbitMQ si nécessaire (natif, sans Docker)
brew services start rabbitmq

# Démarrer les trois services
./scripts/start-all.sh

# Vérifier
curl http://localhost:7162/apigateway/Products
curl http://localhost:7162/apigateway/Orders

# Tests
dotnet test MicroservicesTP.sln
./scripts/e2e.sh

# Arrêt (RabbitMQ reste actif)
./scripts/stop-all.sh

# Validation finale en lecture seule
./scripts/validate-final.sh
```

| Service | URL |
|---|---|
| ProductsAPI | `http://localhost:7066` (`/swagger`) |
| OrdersAPI | `http://localhost:7065` (`/swagger`) |
| ApiGateway | `http://localhost:7162/apigateway/...` |
| RabbitMQ Management | `http://localhost:15672` |

---

## 20. Conclusion

La livraison est **conforme au périmètre pédagogique de l'atelier 8** :

- architecture microservices complète et fonctionnelle ;
- deux bases SQLite réellement isolées ;
- synchronisation asynchrone prouvée sur un broker réel ;
- idempotence du consumer prouvée par test broker réel ;
- build sans erreur ni avertissement ;
- 46 tests xUnit réussis, 1 ignoré justifié, 0 échec ;
- 66 contrôles E2E réussis, 0 échec ;
- bases intègres après exécution ;
- documentation et preuves présentes et cohérentes.

Le verdict est **PASS_WITH_RESERVATIONS** : tous les critères obligatoires sont réussis, mais des limites de production (Outbox, événements de mise à jour, .NET 7 hors support, absence d'authentification, coupure réelle du broker non testée) restent documentées et hors périmètre.

Aucune donnée n'a été détruite. Aucun résultat n'a été inventé. Aucune fonctionnalité future n'est présentée comme existante.
