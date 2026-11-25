# AUTO-PASS — ÉVALUATION HONNÊTE DE LA LIVRAISON

**Projet :** Atelier 8 — Microservices ASP.NET Core (`MicroservicesTP`)
**Date :** 18 novembre 2026
**Évaluateur :** l'agent réalisateur, à partir des journaux et du code réellement exécutés
**Nature :** auto-évaluation technique, **pas** une certification officielle

---

## 1. Méthode

Chaque critère ci-dessous est confronté à une preuve vérifiable :

- sortie de build ;
- résultat `dotnet test` ;
- journal E2E ;
- requête HTTP réelle ;
- inspection SQLite ;
- API RabbitMQ ;
- inspection du code.

Aucun critère n'est marqué PASS sur la seule base de l'intention. Un test ignoré n'est jamais compté comme réussi. Un défaut d'environnement n'est pas masqué.

**Journaux de référence :**

| Fichier | Contenu |
|---|---|
| `logs/build-final.log` | build final du 18 novembre 2026 (13:09 CET) |
| `logs/tests-final.log` | 47 cas, 46 réussis, 1 ignoré, 0 échec |
| `reports/TestResults/complete-tests.trx` | résultats xUnit machine |
| `logs/e2e-final.log` | 66 PASS, 0 FAIL |
| `logs/e2e-idempotence.log` | test broker réel |
| `logs/environment.txt` | versions et ports |

Rapports synthétiques : `reports/BUILD_REPORT.md`, `reports/UNIT_TEST_REPORT.md`, `reports/TEST_COVERAGE_AUDIT.md`, `reports/E2E_FINAL_REPORT.md`, `reports/AUTO_PASS_REPORT.md`. Les journaux de la session précédente sont conservés dans `logs/archive/`.

---

## 2. Verdict

> ## **PASS AVEC RÉSERVES DOCUMENTÉES**
>
> Tous les critères **obligatoires** de la mission sont satisfaits et prouvés.
> Deux réserves d'environnement sont documentées : la page **Overview** de RabbitMQ 4.4.0/OTP 29 et la fin de support de **.NET 7**.
> Trois limites fonctionnelles sont assumées et hors périmètre : pas d'**Outbox**, pas d'événements **ProductUpdated/ProductDeleted**, pas de test de **coupure réelle** du broker.

---

## 3. Scorecard des critères obligatoires

| # | Critère | Attendu | Observé | Statut | Preuve |
|---:|---|---|---|---|---|
| 1 | Build | 0 erreur, 0 avertissement | 0 / 0 | **PASS** | `logs/build-final.log` |
| 2 | Structure solution | 5 projets | Shared, ProductsAPI, OrdersAPI, ApiGateway, Tests | **PASS** | `.sln` |
| 3 | `Products.db` | table `Products` | `ProductId` PK AUTOINCREMENT, `Name`, `StockQte`, `Price` | **PASS** | `.schema` |
| 4 | `Orders.db` | tables `Orders`, `Products` | `Orders` + `Products(Id PK sans AUTOINCREMENT, Name)` | **PASS** | `.schema` |
| 5 | Migrations indépendantes | 2 historiques EF | 2 migrations `InitialCreate` distinctes | **PASS** | dossiers `Migrations/` |
| 6 | CRUD Products direct | GET/POST/PUT/DELETE | testé | **PASS** | tests + E2E |
| 7 | CRUD Orders direct | GET/POST/PUT/DELETE | testé | **PASS** | tests + E2E |
| 8 | CRUD Products Gateway | GET/POST/PUT/DELETE | 201/200/204/404 | **PASS** | E2E section 5 |
| 9 | CRUD Orders Gateway | GET/POST/PUT/DELETE | 201/200/204/404 | **PASS** | E2E section 6 |
| 10 | Validation | 400 sur entrées invalides | nom vide, prix négatif, ProductId 0 | **PASS** | E2E + tests |
| 11 | Ressource absente | 404 | produit/commande/route inconnus | **PASS** | E2E |
| 12 | Swagger | accessible sur les 2 API | HTTP 200 | **PASS** | E2E section 1 |
| 13 | Ocelot | 4 routes, méthodes CRUD | 4 routes, 15 tests de config | **PASS** | tests Ocelot |
| 14 | Broker RabbitMQ | opérationnel sur 5672 | ping OK, 4.4.0 | **PASS** | `environment.txt` |
| 15 | API de gestion | files/connexions/exchanges | 200 sur `/api/queues`, `/api/connections`, `/api/exchanges` | **PASS** | E2E section 0 |
| 16 | Page Overview | accessible | 500 / timeout (bug 4.4.0 + OTP 29) | **PARTIEL** | `ENVIRONMENT_AUDIT.md` |
| 17 | Connexions MassTransit | 2 | 2 observées | **PASS** | E2E section 2 |
| 18 | File `event-listener` | 1 consumer, 0 message | 1 consumer, 0 message | **PASS** | E2E section 2 |
| 19 | Exchange `ProductCreated` | fanout durable | `Shared.Messages:ProductCreated` fanout durable | **PASS** | E2E section 2 |
| 20 | Synchronisation | produit dans les 2 bases | produit 11 dans les deux | **PASS** | E2E section 3 |
| 21 | Publication après sauvegarde | ID généré publié | ID SQLite dans le message | **PASS** | test unitaire |
| 22 | Échec de publication | pas de faux succès | `503`, produit conservé | **PASS** | test unitaire mocké |
| 23 | Coupure réelle du broker | comportement vérifié | non exécuté | **NON EXÉCUTÉ** | — |
| 24 | Idempotence unitaire | pas de doublon | 6 tests consumer | **PASS** | `logs/tests-final.log` |
| 25 | Idempotence broker réel | rejeu réel, 1 ligne | 2 publications, 1 ligne | **PASS** | `e2e-idempotence.log` |
| 26 | Persistance | données après redémarrage | vérifiée | **PASS** | E2E section 7 |
| 27 | Indépendance | chaque API sans l'autre | Orders 200 sans Products, Products 200 sans Orders | **PASS** | E2E section 8 |
| 28 | Isolation des bases | aucun accès croisé | `Data Source` unique par service | **PASS** | E2E section 8 |
| 29 | Aucun Docker utilisé | 0 conteneur projet | broker natif, API natives | **PASS** | `ENVIRONMENT_AUDIT.md` |
| 30 | Aucune base externe | SQLite seulement | seul package `EntityFrameworkCore.Sqlite` | **PASS** | `.csproj` |
| 31 | Secrets | aucun secret réel | `guest/guest` local par défaut | **PASS** | config |
| 32 | Tests documentés | résultats réels | 46 + 1 skip + 66 E2E | **PASS** | `TEST_RESULTS.md` |
| 33 | Écarts documentés | liste justifiée | 20 écarts | **PASS** | `CONFORMITE_PDF.md` |
| 34 | Reproductibilité | scripts + docs | start/stop/check/e2e | **PASS** | `scripts/` |
| 35 | Version .NET | compatible SDK installé | 7.0.317 | **PASS** | `global.json` |
| 36 | Support .NET 7 | version supportée | **hors support depuis mai 2024** | **RÉSERVE** | `ENVIRONMENT_AUDIT.md` |

Légende : **PASS** = prouvé ; **PARTIEL** = fonctionne sauf un point d'environnement ; **NON EXÉCUTÉ** = aucune preuve, non compté comme réussite ; **RÉSERVE** = contrainte externe documentée.

---

## 4. Améliorations optionnelles (non exigées)

| Option | Statut | Pourquoi |
|---|---|---|
| Outbox transactionnel | **NON IMPLÉMENTÉ** | hors périmètre ; l'échec est signalé par `503` |
| `ProductUpdated` / `ProductDeleted` | **NON IMPLÉMENTÉ** | la projection ne suit pas les PUT/DELETE |
| JWT / authentification | **NON IMPLÉMENTÉ** | hors périmètre |
| Health checks | **NON IMPLÉMENTÉ** | hors périmètre |
| Retry / circuit breaker Ocelot | **NON IMPLÉMENTÉ** | hors périmètre |
| Tests de charge | **NON EXÉCUTÉ** | hors périmètre |
| Serilog / OpenTelemetry | **NON IMPLÉMENTÉ** | journalisation standard suffisante |
| FluentValidation | **NON IMPLÉMENTÉ** | Data Annotations suffisantes |

Ces éléments ne sont pas des critères d'acceptation du TP. Ils sont listés pour éviter toute ambiguïté.

---

## 5. Ce qui n'a pas été prouvé

| Sujet | Statut | Conséquence |
|---|---|---|
| Coupure réelle de RabbitMQ pendant un POST | NON EXÉCUTÉ | le `503` est prouvé par mock, pas par arrêt du broker |
| Livraisons concurrentes en stress | NON EXÉCUTÉ | la clé primaire protège, mais aucun test de charge |
| Page Overview RabbitMQ | ÉCHEC ENVIRONNEMENTAL | bug du plugin, sans impact applicatif |
| Mise à jour de la projection après PUT/DELETE | NON IMPLÉMENTÉ | limite fonctionnelle assumée |
| Authentification / TLS / production | NON IMPLÉMENTÉ | hors périmètre pédagogique |
| Comportement multi-instances | NON EXÉCUTÉ | hors périmètre |

---

## 6. Réserves explicitées

### Réserve 1 — Page Overview RabbitMQ

- **Constat :** `GET /api/overview` renvoie 500 ou ne répond pas ; le journal montre un `function_clause` dans `rabbit_mgmt_wm_nodes:all_nodes_raw/0`.
- **Cause :** incompatibilité RabbitMQ 4.4.0 avec Erlang/OTP 29.1.2.
- **Impact :** page d'accueil de l'UI de gestion ; aucune incidence sur AMQP, MassTransit, les files, la consommation ou l'application.
- **Contournement :** les scripts testent `/api/queues`. Les pages Queues/Connections/Exchanges restent utilisables.
- **Pourquoi non corrigé :** rétrograder Erlang ou changer de broker risquerait de casser un environnement validé, pour un endpoint non utilisé.

### Réserve 2 — .NET 7 hors support

- **Constat :** SDK 7.0.317, runtime 7.0.20, cible `net7.0`.
- **Cause :** support pédagogique et environnement imposé.
- **Impact :** pas de correctifs de sécurité Microsoft pour le runtime.
- **Recommandation :** migration vers .NET 8/9 LTS avant toute exposition réelle.

### Réserve 3 — Pas d'Outbox

- **Constat :** sauvegarde SQLite et publication RabbitMQ ne sont pas atomiques.
- **Impact :** en cas de panne entre les deux, le produit existe sans événement. L'API renvoie `503` et journalise, mais ne rejoue pas automatiquement.
- **Pourquoi accepté :** pattern hors périmètre du TP ; documenté comme amélioration.

---

## 7. Conditions de reproduction du verdict

```bash
cd MicroservicesTP

dotnet tool restore
./scripts/check-environment.sh
dotnet build MicroservicesTP.sln
dotnet test MicroservicesTP.sln
./scripts/e2e.sh
./scripts/validate-final.sh
```

Attendu :

```text
Build        : 0 erreur, 0 avertissement
Unit tests   : 46 réussis, 1 ignoré, 0 échec
E2E          : 66 PASS, 0 FAIL
Idempotence  : 1 réussi (broker réel)
```

Si l'un de ces résultats diffère, le verdict doit être réévalué : il n'est valable que pour l'environnement et les journaux datés du 18 novembre 2026.

---

## 8. Déclaration d'honnêteté

- Aucun résultat de test n'a été inventé, copié d'un exemple ou complété manuellement.
- Le test broker réel est explicitement **ignoré** dans `dotnet test` ; il n'est compté comme réussi que dans `e2e.sh`, où il a réellement tourné.
- Les 66 contrôles E2E sont ceux du script exécuté, pas une estimation.
- Le défaut de la page Overview est reporté tel quel, sans le masquer.
- Les données de test sont conservées et identifiées ; elles n'ont pas été nettoyées pour embellir le résultat.
- Les limites fonctionnelles (Outbox, événements de mise à jour, coupure réelle) sont déclarées comme non couvertes.

---

## 9. Conclusion

La livraison mérite le statut **PASS AVEC RÉSERVES DOCUMENTÉES** :

- **obligatoire :** tout est présent et prouvé ;
- **environnement :** le broker est fonctionnel, seule la page Overview du plugin de gestion est affectée ;
- **périmètre :** les améliorations non demandées ne sont pas présentées comme réalisées ;
- **honnêteté :** les points non testés sont explicitement marqués comme tels.

Un correcteur peut accepter la livraison en l'état, ou demander en complément :

1. un Outbox transactionnel ;
2. les événements `ProductUpdated`/`ProductDeleted` ;
3. un test de coupure réelle du broker ;
4. une migration .NET LTS.

Ces compléments sont des évolutions, pas des corrections de défauts cachés.
