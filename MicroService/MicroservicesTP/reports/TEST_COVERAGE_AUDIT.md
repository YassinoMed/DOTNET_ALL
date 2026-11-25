# TEST COVERAGE AUDIT — MicroservicesTP

**Date :** 18 novembre 2026
**Objet :** clarifier sans ambiguïté le décompte des tests et la couverture réelle.

---

## 1. Décompte exact

| Catégorie | Nombre | Nature |
|---|---:|---|
| Cas xUnit découverts | **47** | `dotnet test` |
| Cas xUnit exécutés lors de la suite complète | **46** | 46 réussis |
| Cas xUnit ignorés lors de la suite complète | **1** | test broker réel |
| Cas xUnit en échec | **0** | — |
| Contrôles E2E Bash | **66** | `scripts/e2e.sh` |
| Contrôles E2E en échec | **0** | — |
| Exécution du test broker réel dans l'E2E | **1** | même cas xUnit, préconditions réelles |

---

## 2. Le test broker réel est-il inclus dans les 47 ?

**Oui.** Le cas
`RabbitMqIdempotenceTests.Rejouer_le_meme_evenement_ne_cree_pas_de_doublon_dans_Orders_db`
est le 47e cas xUnit. Il est ignoré (`NotExecuted`) dans la suite complète, puis exécuté par l'E2E avec `RABBITMQ_E2E=1`.

Il n'existe donc **pas** de « 48e test ». Le total réel de cas xUnit distincts reste **47**. Le compter une seconde fois comme test supplémentaire serait un double comptage.

---

## 3. Pourquoi 47 cas pour 33 méthodes ?

Certains tests sont des `[Theory]` avec `[InlineData]` :

| Classe | Méthodes | Cas |
|---|---:|---:|
| `ModelValidationTests` | 9 (8 `[Fact]` + 1 `[Theory]` à 2 données) | 10 |
| `OcelotConfigurationTests` | 3 (1 `[Fact]` + 2 `[Theory]` à 14 données) | 15 |
| `OrdersControllerTests` | 6 `[Fact]` | 6 |
| `ProductCreatedConsumerTests` | 6 `[Fact]` | 6 |
| `ProductsControllerTests` | 8 `[Fact]` | 8 |
| `SqlitePersistenceTests` | 1 `[Fact]` | 1 |
| `RabbitMqIdempotenceTests` | 1 `[SkippableFact]` | 1 |
| **Total** | **33 méthodes** | **47 cas** |

---

## 4. Couverture par domaine

| Domaine | Couvert par | Statut |
|---|---|---|
| CRUD ProductsAPI | `ProductsControllerTests` (8) + E2E section 5 | **COUVERT** |
| CRUD OrdersAPI | `OrdersControllerTests` (6) + E2E section 6 | **COUVERT** |
| Validation Products | `ModelValidationTests` + E2E 400 | **COUVERT** |
| Validation Orders | `ModelValidationTests` + E2E 400 | **COUVERT** |
| Consumer `ProductCreated` | `ProductCreatedConsumerTests` (6) | **COUVERT** |
| Idempotence unitaire | `ProductCreatedConsumerTests` (rejeu, mise à jour, doublons) | **COUVERT** |
| Idempotence broker réel | `RabbitMqIdempotenceTests` exécuté par l'E2E | **COUVERT** |
| Publication après sauvegarde | `ProductsControllerTests` (ID généré publié) | **COUVERT** |
| Échec de publication → 503 | `ProductsControllerTests` (mock `IPublishEndpoint`) | **COUVERT** |
| Échec de sauvegarde → pas de publication | `ProductsControllerTests` | **COUVERT** |
| Configuration Ocelot | `OcelotConfigurationTests` (15 cas) | **COUVERT** |
| Persistance SQLite | `SqlitePersistenceTests` + E2E section 7 | **COUVERT** |
| Synchronisation inter-services | E2E section 3 | **COUVERT** |
| Indépendance des services | E2E section 8 | **COUVERT** |
| Gateway 400 / 404 / 502 | E2E sections 5, 6, 8 | **COUVERT** |
| Routing Gateway direct | E2E sections 5–8 (via `http://localhost:7162`) | **COUVERT** |
| Coupure réelle de RabbitMQ | — | **NON TESTÉ** |
| Consommation concurrente en stress | — | **NON TESTÉ** |
| Clé d'idempotence HTTP | — | **NON IMPLÉMENTÉ** |
| Authentification / TLS | — | **NON IMPLÉMENTÉ** |
| Charge / performance | — | **NON TESTÉ** |

---

## 5. Précision sur les appels directs

Les scénarios CRUD E2E passent par la Gateway Ocelot (`:7162`), qui relaie vers `:7066` et `:7065`. Les contrôleurs sont également testés directement en xUnit avec des contextes SQLite en mémoire. Il n'existe pas de scénario E2E qui appelle `:7066` ou `:7065` pour le CRUD : ce n'est pas une lacune fonctionnelle (la Gateway est le point d'entrée imposé), mais le rapport ne doit pas prétendre que les deux chemins ont été testés en E2E.

---

## 6. Tests non exécutés — déclaration explicite

| Scénario | Statut | Raison |
|---|---|---|
| Arrêt réel de RabbitMQ pendant un POST | `NOT_TESTED` | Le broker est partagé avec d'autres usages du poste ; l'arrêt n'a pas été jugé maîtrisable sans risque. Le chemin `503` est couvert par un test mocké. |
| Charge concurrente sur le consumer | `NOT_TESTED` | Hors périmètre ; la clé primaire protège fonctionnellement. |
| POST rejoué par un client après 503 | `NOT_TESTED` | Aucune clé d'idempotence HTTP n'existe ; limite documentée. |
| Panne réseau / timeout RabbitMQ | `NOT_TESTED` | Hors périmètre du TP. |

Aucun de ces points n'est présenté comme réussi.

---

## 7. Verdict de couverture

- Tous les tests **obligatoires du TP** sont couverts et réussis.
- Le test broker réel est réellement exécuté, mais une seule fois, dans l'E2E.
- Les scénarios de production non exigés sont explicitement marqués `NOT_TESTED` ou `NON IMPLÉMENTÉ`.
- Aucun test n'a été ajouté artificiellement pour gonfler le décompte.
