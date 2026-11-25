# FINAL AUDIT BASELINE — Atelier 8 Microservices

**Date et heure :** mercredi 18 novembre 2026, 13:08 CET
**Projet :** `/Users/mohamedyassine/Desktop/PFE/dsir/2emeMaster/DotNET/MicroService/MicroservicesTP`
**Solution :** `MicroservicesTP.sln`
**Objet :** état initial figé avant le cycle final BUILD → TESTS → E2E → AUTO-PASS.

---

## 1. Contrôle de session

| Élément | Constat |
|---|---|
| Processus OpenCode | **1 seul** : PID 12155, cwd `/Users/mohamedyassine/Desktop/PFE/dsir/2emeMaster/DotNET/MicroService` |
| Autres agents (Claude, Codex, Cursor) | aucun |
| Conflit de session | **aucun** |
| Dépôt Git | **aucun** (`fatal: not a git repository`) |
| Commits / push | impossibles et non effectués |

Conclusion : la session est unique et autorisée à écrire. Aucune autre instance ne travaille sur le projet.

---

## 2. Processus et ports

| Port | Usage | État initial | PID |
|---:|---|---|---|
| 7066 | ProductsAPI | **libre** | — |
| 7065 | OrdersAPI | **libre** | — |
| 7162 | ApiGateway | **libre** | — |
| 5672 | RabbitMQ AMQP | occupé (natif Homebrew) | 21947 (`beam.smp`) |
| 15672 | RabbitMQ Management | occupé (natif Homebrew) | 21947 |

- `rabbitmq-diagnostics -q ping` : **Ping succeeded**
- `rabbitmqctl version` : **4.4.0**
- `GET /api/queues` : **HTTP 200**
- Aucun Docker n'a été utilisé pour ce projet.

---

## 3. Environnement

| Élément | Valeur observée |
|---|---|
| macOS | 27.3 (build `26C5047e`) |
| Architecture | arm64, Apple M2 |
| .NET SDK | **7.0.317** (`global.json`, `rollForward: latestPatch`) |
| Runtime ASP.NET Core | 7.0.20 |
| EF Core / dotnet-ef | **7.0.20** (outil local) |
| MassTransit / RabbitMQ | **8.2.3** |
| Ocelot | **23.4.3** |
| Swashbuckle | 6.6.2 |
| xUnit / Moq | 2.9.2 / 4.20.72 |
| RabbitMQ / Erlang | **4.4.0 / OTP 29.1.2** |
| SQLite CLI | 3.54.0 |

---

## 4. Inventaire

- **80 fichiers** hors `bin/`, `obj/` et `.DS_Store`.
- **5 projets** : `Shared`, `ProductsAPI`, `OrdersAPI`, `ApiGateway`, `Tests`.
- Dossiers présents : `docs/`, `logs/`, `scripts/`, `Tests/Infrastructure/`, migrations EF des deux API.
- Aucun répertoire `reports/` avant ce cycle : il sera créé pour les rapports finaux.
- Documentation existante : `README.md`, `ENVIRONMENT_AUDIT.md`, `TEST_RESULTS.md`, `RAPPORT_FINAL.md`, `docs/*.md`.

### Projets et références

| Projet | Références |
|---|---|
| `Shared` | aucune (contrat `ProductCreated`) |
| `ProductsAPI` | `Shared`, EF Core Sqlite 7.0.20, MassTransit 8.2.3, Swashbuckle 6.6.2 |
| `OrdersAPI` | `Shared`, EF Core Sqlite 7.0.20, MassTransit 8.2.3, Swashbuckle 6.6.2 |
| `ApiGateway` | Ocelot 23.4.3 |
| `Tests` | ProductsAPI, OrdersAPI, Shared, xUnit, Moq, SkippableFact |

---

## 5. État SQLite (lecture seule)

### Intégrité

| Base | Taille | `PRAGMA integrity_check` | `PRAGMA foreign_key_check` |
|---|---:|---|---|
| `ProductsAPI/Products.db` | 20 480 octets | **ok** | aucune violation |
| `OrdersAPI/Orders.db` | 24 576 octets | **ok** | aucune violation |

### Contenu

`Products.db` — table `Products` (7 lignes) :

```text
1  Test manuel modifié              9   29.99
2  E2E-Sync-20261118-124013        12  123.45
3  E2E-Idem-20261118-124013         3    9.99
5  E2E-Sync-20261118-124729        12  123.45
6  E2E-Idem-20261118-124729         3    9.99
8  E2E-Sync-20261118-124822        12  123.45
9  E2E-Idem-20261118-124822         3    9.99
```

`Orders.db` — projection `Products` (10 lignes, `1..10`), table `Orders` : **0 ligne**.

### Migrations

| Base | Migration appliquée |
|---|---|
| Products.db | `20261118113239_InitialCreate` |
| Orders.db | `20261118113248_InitialCreate` |

Aucune migration destructive, aucun `EnsureDeleted`, aucun `database drop` n'a été exécuté.

---

## 6. Résultats historiques (avant ce cycle)

| Vérification | Résultat historique | Journal |
|---|---|---|
| Build | 0 erreur / 0 avertissement | `logs/build.log` |
| Tests unitaires | 46 réussis, 1 ignoré, 0 échec (47) | `logs/unit-tests.log` |
| E2E | 66 PASS / 0 FAIL | `logs/e2e-run.log` |
| Idempotence broker réel | 1 réussi (2 publications, 1 ligne) | `logs/e2e-idempotence.log` |

**Ces résultats ne sont pas utilisés comme preuve de la version actuelle.** Ils servent uniquement de référence de non-régression ; le cycle final réexécute build, tests et E2E.

---

## 7. Modifications locales préexistantes

- Aucun dépôt Git : la comparaison automatique avec un état versionné est impossible.
- Les corrections de la session précédente ont porté sur `scripts/e2e.sh` (attente de la topologie MassTransit, `CHECK_REGEX`, contrôle d'isolation par chaînes `Data Source`), `scripts/lib.sh` (sonde `/api/queues`) et la documentation.
- Aucun fichier de configuration, migration ou base n'a été modifié pendant cet audit.
- Aucune donnée métier n'a été supprimée.

---

## 8. Risques et limites déjà identifiés

| # | Risque | Niveau | Traitement |
|---:|---|---|---|
| R1 | .NET 7 hors support Microsoft depuis mai 2024 | Environnement | Documenté ; migration LTS recommandée |
| R2 | `/api/overview` RabbitMQ 4.4.0 + OTP 29 en échec | Environnement | Sonde remplacée par `/api/queues` ; documenté |
| R3 | Absence de pattern Outbox | Architecture | Documenté ; `503` explicite en cas d'échec de publication |
| R4 | Projection non mise à jour après PUT/DELETE produit | Fonctionnel | Limite assumée ; pas d'événements `ProductUpdated`/`ProductDeleted` |
| R5 | Coupure réelle du broker non testée | Test | À marquer `NOT_TESTED` |
| R6 | Pas d'authentification / TLS / CORS explicite | Sécurité | Hors périmètre du TP ; `guest/guest` limité à localhost |
| R7 | Swagger exposé dans tous les environnements | Sécurité | Choix pédagogique documenté ; à restreindre en production |
| R8 | Pas de clé d'idempotence HTTP sur POST | Robustesse | Limite documentée ; sécurité assurée côté consumer |

---

## 9. Décision de poursuite

Tous les prérequis sont réunis :

- session unique ;
- ports applicatifs libres ;
- broker natif opérationnel ;
- bases intègres et présentes ;
- scripts relus, aucune opération destructive.

Le cycle final peut démarrer : **BUILD → TESTS → E2E → AUDIT → DOCUMENTATION → AUTO-PASS**.
