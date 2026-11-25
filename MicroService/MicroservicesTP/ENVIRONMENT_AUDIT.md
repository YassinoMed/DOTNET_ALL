# AUDIT DE L'ENVIRONNEMENT — Atelier 8

**Date de collecte :** mercredi 18 novembre 2026, 12:49 CET
**Hôte :** MacBook Pro Apple Silicon M2
**Collecte :** `./scripts/check-environment.sh` et commandes read-only ; sortie brute dans `logs/environment.txt`.

---

## 1. Système

| Élément | Valeur observée |
|---|---|
| Produit | macOS **27.3** (build `26C5047e`) |
| Architecture | **arm64** |
| Processeur | **Apple M2** |
| Mémoire | **8 GB** |
| Disque racine | 228 GiB, **8,2 GiB libres (62 % utilisés)** |
| Shell des scripts | bash 3.2 (macOS) |

---

## 2. .NET

| Élément | Valeur observée |
|---|---|
| `dotnet --version` | **7.0.317** |
| SDK installés | 6.0.425 et **7.0.317** (`/usr/local/share/dotnet/sdk`) |
| Runtime ASP.NET Core | 6.0.33 et **7.0.20** |
| Runtime .NET | 6.0.33 et **7.0.20** |
| Outil EF local | **dotnet-ef 7.0.20** (`.config/dotnet-tools.json`) |
| Cible des projets | **net7.0** |
| `global.json` | `7.0.317`, `rollForward: latestPatch` |

**Justification :** le support et les packages choisis (EF Core 7, MassTransit 8.2.3, Ocelot 23.4.3) sont compatibles avec le SDK 7.0.317 réellement installé. Aucun package .NET 8/9 n'est référencé.

**Réserve :** .NET 7 est **hors support Microsoft depuis mai 2024**. Cette version est imposée par le support pédagogique et l'environnement fourni. Une mise en production exigerait une migration vers une version LTS.

---

## 3. Outils

| Outil | Version observée |
|---|---|
| SQLite CLI | **3.54.0** (2026-04-09) |
| curl | **8.14.1** (LibreSSL 3.3.6) |
| Homebrew | **7.0.13** |
| Python 3 | fourni avec macOS, utilisé pour lire le JSON |
| `dotnet-ef` | 7.0.20 (restauré localement) |

---

## 4. RabbitMQ / Erlang (natif, sans Docker)

| Élément | Valeur observée |
|---|---|
| RabbitMQ | **4.4.0** |
| Erlang/OTP | **29** (ERTS 17.1.1) |
| Installation | **Homebrew** (`/opt/homebrew/opt/rabbitmq`) |
| Démarrage | `brew services start rabbitmq` (LaunchAgent `sh.brew.rabbitmq`) |
| Processus | `beam.smp`, PID 21947 au moment de la collecte |
| AMQP | `127.0.0.1:5672` (écoute IPv4 localhost) |
| Management | `*:15672` (écoute IPv6, accessible via `localhost`) |
| Identifiants locaux | `guest` / `guest` (valides uniquement depuis localhost) |
| `rabbitmq-diagnostics -q ping` | **Ping succeeded** |

### Plugins explicitement activés

```text
[E*] rabbitmq_amqp1_0          4.4.0
[E*] rabbitmq_management       4.4.0
[e*] rabbitmq_management_agent 4.4.0
[E*] rabbitmq_mqtt             4.4.0
[E*] rabbitmq_stomp            4.4.0
[E*] rabbitmq_stream           4.4.0
[e*] rabbitmq_web_dispatch     4.4.0
```

### Topologie applicative observée

| Objet | Type | Durable | Détail |
|---|---|---|---|
| `Shared.Messages:ProductCreated` | fanout | oui | exchange du contrat |
| `event-listener` | fanout | oui | file de OrdersAPI |
| `event-listener` (queue) | — | — | 1 consumer pendant l'E2E, 0 message en attente |

Deux connexions MassTransit (`ProductsAPI` et `OrdersAPI`) ont été observées pendant l'E2E.

### Incident connu : `/api/overview` et page Overview

Avec **RabbitMQ 4.4.0 + Erlang/OTP 29.1.2**, l'endpoint `GET /api/overview` et la page « Overview » de l'interface de gestion échouent. Le journal RabbitMQ contient :

```text
[error] ... {function_clause,
  [{proplists,get_keys,[undefined,#{}], ...},
   {rabbit_mgmt_wm_nodes,all_nodes_raw,0, ...},
   {rabbit_mgmt_wm_nodes,all_nodes,1, ...},
   {rabbit_mgmt_wm_overview,web_contexts,1, ...},
   {rabbit_mgmt_wm_overview,to_json,2, ...},
   ...]}
[error] ... Ranch listener ... port 15672 ... request process ... exit
```

Conséquences observées :

| Endpoint | Résultat |
|---|---|
| `GET /api/queues` | **200** |
| `GET /api/connections` | **200** |
| `GET /api/exchanges` | **200** |
| `GET /api/overview` | **500** ou absence de réponse HTTP (`curl` code 000) |
| Page HTML `/` | chargée, mais son tableau de bord appelle `/api/overview` |

**Décision :** ne pas rétrograder Erlang ni casser un broker fonctionnel pour un endpoint non utilisé par l'application. Les scripts de vérification interrogent désormais `/api/queues` (`scripts/lib.sh`, fonction `rabbitmq_management_is_up`). L'application, MassTransit, l'E2E et les données ne sont pas affectés.

> Ce problème est un défaut de compatibilité du plugin de gestion, pas un défaut de la solution. Il est signalé ici pour qu'aucun correcteur ne conclue à tort à un broker hors service.

> **Mise à jour du 18/11/2026 :** l'incident n'est plus reproduit. `GET /api/overview` répond **HTTP 200** et la file `event-listener` expose 1 consumer / 0 message. Le tableau ci-dessus décrit l'état observé lors de l'audit initial et est conservé comme historique.

---

## 5. Docker et bases externes

### Docker

Docker Desktop **29.5.2** est installé et son démon est actif sur le poste. Des conteneurs **étrangers au projet** tournent :

```text
odoo18          | odoo:18              | Up
odoo18_pgadmin  | dpage/pgadmin4       | Up
odoo18_postgres | postgres:17          | Up (healthy)
```

**Aucun conteneur n'a été créé, démarré ou utilisé pour cet atelier.** Il n'existe notamment **aucun conteneur RabbitMQ** : le broker est un processus natif Homebrew. Les trois services .NET sont également lancés nativement par `dotnet run`.

La contrainte finale « aucun Docker / Docker Desktop / Compose pour la solution » est donc respectée, même si Docker est installé sur la machine pour d'autres usages.

Le fichier `docker/docker-compose.rabbitmq.yml` reproduit le chemin du PDF (conteneur `ecomm`, port 4001) mais **n'a pas été exécuté** : aucun conteneur n'a été créé pour cet atelier. Le défaut reste le broker natif 5672.

### Bases de données

Packages de fournisseur de base réellement référencés dans les `.csproj` :

```text
Microsoft.EntityFrameworkCore.Sqlite
```

Aucun package `SqlServer`, `Npgsql`, `MySql` ou équivalent. Chaînes de connexion de l'application :

```text
ProductsAPI : Data Source=Products.db
OrdersAPI   : Data Source=Orders.db
Tests       : Data Source=:memory: ou fichier temporaire
```

Un démon MySQL préexistant tourne sur le poste (`mysqld`, PID 1690) et des conteneurs PostgreSQL/Odoo sont actifs, mais **la solution ne les référence ni ne les contacte**. Toutes les preuves de persistance portent sur les deux fichiers SQLite du projet.

---

## 6. Ports

| Port | Usage | État à la fin des tests |
|---:|---|---|
| 7066 | ProductsAPI | libre (services arrêtés par l'E2E) |
| 7065 | OrdersAPI | libre (services arrêtés par l'E2E) |
| 7162 | ApiGateway | libre (services arrêtés par l'E2E) |
| 5672 | RabbitMQ AMQP | **écoute** (service Homebrew) |
| 15672 | RabbitMQ Management | **écoute** (service Homebrew) |

Les ports 7066/7065/7162 sont ceux du PDF. Le port Docker `4001` du support n'est pas utilisé : le broker natif écoute sur le port AMQP standard `5672`, ce qui correspond à la configuration finale.

---

## 7. Versions et compatibilité

| Package | Version | Compatibilité constatée |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 7.0.20 | build, migrations, runtime OK |
| `Microsoft.EntityFrameworkCore.Design` | 7.0.20 | `dotnet ef` OK |
| `MassTransit` | 8.2.3 | publish/consume réels OK |
| `MassTransit.RabbitMQ` | 8.2.3 | RabbitMQ 4.4.0 OK |
| `Ocelot` | 23.4.3 | routage HTTP OK |
| `Swashbuckle.AspNetCore` | 6.6.2 | Swagger OK |
| `xunit` | 2.9.2 | 46 tests réussis |
| `Moq` | 4.20.72 | mocks OK |
| `Xunit.SkippableFact` | 1.4.13 | test broker réel correctement ignoré hors E2E |

Aucun package obsolète ou incompatible n'est utilisé sans justification. `MassTransit.AspNetCore` (cité dans le PDF) n'existe plus pour MassTransit 8 : l'intégration ASP.NET Core est incluse dans `MassTransit`, décision documentée dans `docs/CONFORMITE_PDF.md`.

---

## 8. Sécurité de l'environnement

- RabbitMQ `guest/guest` : compte par défaut, **limitant les connexions à localhost**. Aucun secret de production n'est présent.
- Aucun fichier `.env`, certificat ou clé privée dans le projet.
- `*.db`, `*.db-shm`, `*.db-wal`, `logs/`, `*.pid` et `appsettings.*.local.json` sont exclus par `.gitignore`.
- Les API et la Gateway écoutent sur `localhost` uniquement dans les scripts de développement.
- Swagger est exposé en développement ; il devra être désactivé ou protégé en production.
- Aucune authentification/JWT : hors périmètre du TP (amélioration optionnelle).

---

## 9. Reproductibilité

Pour revérifier cet audit :

```bash
cd MicroservicesTP
dotnet tool restore
./scripts/check-environment.sh
```

La sortie brute de la collecte utilisée pour ce document est conservée dans `logs/environment.txt`.
