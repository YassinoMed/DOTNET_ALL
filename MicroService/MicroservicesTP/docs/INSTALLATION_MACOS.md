# INSTALLATION — macOS Apple Silicon

Guide d'installation reproductible pour l'Atelier 8 sur un Mac Apple Silicon (M1/M2/M3), sans Docker.

**Environnement validé :** macOS 27.3, Apple M2 arm64, .NET SDK 7.0.317, RabbitMQ 4.4.0, Erlang/OTP 29.

---

## 1. Prérequis

| Composant | Version validée | Vérification |
|---|---|---|
| macOS | 27.3 | `sw_vers` |
| Architecture | arm64 | `uname -m` |
| Homebrew | 7.0.13 | `brew --version` |
| .NET SDK | 7.0.317 | `dotnet --list-sdks` |
| SQLite | 3.54.0 | `sqlite3 --version` |
| curl | 8.14.1 | `curl --version` |
| Python 3 | fourni avec macOS | `python3 --version` |

> **Important :** la solution cible `net7.0`. Un SDK .NET 8/9 seul ne suffit pas : `global.json` demande la branche 7.0.3xx avec `rollForward: latestPatch`.

---

## 2. Installer Homebrew (si absent)

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

Sur Apple Silicon, Homebrew s'installe dans `/opt/homebrew`. Ajouter au profil si nécessaire :

```bash
echo 'eval "$(/opt/homebrew/bin/brew shellenv)"' >> ~/.zprofile
eval "$(/opt/homebrew/bin/brew shellenv)"
```

---

## 3. Installer .NET 7

### 3.1 Vérifier l'existant

```bash
dotnet --list-sdks
```

La sortie doit contenir une version `7.0.3xx`, par exemple :

```text
7.0.317 [/usr/local/share/dotnet/sdk]
```

### 3.2 Si le SDK 7 est absent

Deux options.

**Option A — installateur officiel (recommandé)**

Télécharger le SDK .NET 7.0.317 pour macOS arm64 depuis la page officielle :

<https://dotnet.microsoft.com/download/dotnet/7.0>

Installer le paquet `.pkg`, puis vérifier :

```bash
dotnet --list-sdks
dotnet --list-runtimes | grep AspNetCore
```

**Option B — script `dotnet-install`**

```bash
mkdir -p ~/.dotnet
curl -sSL https://dot.net/v1/dotnet-install.sh \
  | bash -s -- --version 7.0.317 --install-dir "$HOME/.dotnet"
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

> **Réserve :** .NET 7 est hors support depuis mai 2024. Cette version est imposée par le support pédagogique. En production, migrer vers une version LTS.

---

## 4. Installer RabbitMQ natif

### 4.1 Installation

```bash
brew install rabbitmq
```

Homebrew installe RabbitMQ **4.4.0** et Erlang **29.1.2** comme dépendance. Aucun Docker n'est utilisé.

### 4.2 Démarrage

```bash
brew services start rabbitmq
```

Attendre le démarrage complet (peut prendre 30–40 s après un redémarrage) :

```bash
export PATH="/opt/homebrew/opt/rabbitmq/sbin:$PATH"

for i in $(seq 1 60); do
  rabbitmq-diagnostics -q ping && break
  sleep 2
done

rabbitmqctl version
```

Résultat attendu :

```text
Ping succeeded
4.4.0
```

### 4.3 Activer l'interface de gestion

```bash
rabbitmq-plugins enable rabbitmq_management
```

Vérifier les ports :

```bash
lsof -nP -iTCP:5672 -sTCP:LISTEN
lsof -nP -iTCP:15672 -sTCP:LISTEN
```

- AMQP : `127.0.0.1:5672`
- Management : `*:15672` → <http://localhost:15672>
- Identifiants par défaut : `guest` / `guest` (localhost uniquement)

### 4.4 Vérification de l'API de gestion

```bash
curl -s -o /dev/null -w '%{http_code}\n' -u guest:guest \
  http://localhost:15672/api/queues
```

Attendu : `200`.

> **Ne pas utiliser `/api/overview` comme test de santé.** Avec RabbitMQ 4.4.0 + Erlang/OTP 29, cet endpoint plante (`rabbit_mgmt_wm_nodes:all_nodes_raw/0`) et peut renvoyer `500` ou ne jamais répondre. Les endpoints `/api/queues`, `/api/connections` et `/api/exchanges` fonctionnent. Détails : `../ENVIRONMENT_AUDIT.md`.

### 4.5 Arrêt / redémarrage

```bash
brew services stop rabbitmq
brew services start rabbitmq
```

En cas d'erreur `launchctl bootstrap ... exit 5` après un `restart`, arrêter complètement, attendre la fin de l'arrêt (jusqu'à 40 s), puis démarrer :

```bash
brew services stop rabbitmq
sleep 40
brew services start rabbitmq
```

---

## 5. Préparer la solution

```bash
cd MicroservicesTP

# Restaurer l'outil EF local (dotnet-ef 7.0.20)
dotnet tool restore

# Vérifier l'environnement (lecture seule)
./scripts/check-environment.sh
```

### 5.1 Compiler

```bash
dotnet build MicroservicesTP.sln
```

Attendu : `0 Avertissement(s)`, `0 Erreur(s)`.

### 5.2 Appliquer les migrations

Les bases sont normalement déjà présentes. Pour les recréer sur une machine neuve :

```bash
dotnet ef database update --project ProductsAPI --startup-project ProductsAPI
dotnet ef database update --project OrdersAPI   --startup-project OrdersAPI
```

Vérifier :

```bash
sqlite3 ProductsAPI/Products.db ".tables"
sqlite3 ProductsAPI/Products.db ".schema Products"

sqlite3 OrdersAPI/Orders.db ".tables"
sqlite3 OrdersAPI/Orders.db ".schema Orders"
sqlite3 OrdersAPI/Orders.db ".schema Products"
```

Attendu :

```text
ProductsAPI/Products.db : Products   __EFMigrationsHistory
OrdersAPI/Orders.db     : Orders     Products   __EFMigrationsHistory
```

> Toujours préciser `--project` et `--startup-project`. Le PDF montre un écran où le projet par défaut est `OrdersAPI` alors que la commande concerne `ProductsAPI` : sans ces options, la mauvaise base peut être migrée.

### 5.3 Démarrer

```bash
./scripts/start-all.sh
```

Le script :

1. démarre RabbitMQ s'il n'est pas joignable ;
2. applique les migrations si un fichier `.db` manque ;
3. compile la solution ;
4. lance ProductsAPI, OrdersAPI et ApiGateway ;
5. attend que Swagger et la Gateway répondent.

### 5.4 Vérifier

```bash
curl -s -o /dev/null -w 'ProductsAPI %{http_code}\n' http://localhost:7066/swagger/index.html
curl -s -o /dev/null -w 'OrdersAPI   %{http_code}\n' http://localhost:7065/swagger/index.html
curl -s -o /dev/null -w 'Gateway     %{http_code}\n' http://localhost:7162/apigateway/Products
```

Attendu : `200` partout.

### 5.5 Arrêter

```bash
./scripts/stop-all.sh
```

RabbitMQ reste démarré. Pour l'arrêter : `brew services stop rabbitmq`.

---

## 6. Lancer les tests

```bash
# Tests unitaires (le test broker réel est ignoré)
dotnet test MicroservicesTP.sln

# E2E complet : démarre et arrête les services, utilise le vrai broker
./scripts/e2e.sh
```

L'E2E exige que les ports 7065, 7066 et 7162 soient libres. Il arrête les services à la fin.

---

## 7. Dépannage

| Symptôme | Cause probable | Solution |
|---|---|---|
| `dotnet ef` introuvable | outil local non restauré | `dotnet tool restore` |
| SDK 7 introuvable | `global.json` pointe 7.0.317 | installer un SDK 7.0.3xx ou adapter `global.json` |
| `Address already in use` | service déjà lancé | `./scripts/stop-all.sh`, puis vérifier `lsof -nP -iTCP:7066 -sTCP:LISTEN` |
| RabbitMQ ne démarre pas | arrêt précédent encore en cours | `brew services stop rabbitmq`, attendre 40 s, redémarrer |
| `launchctl bootstrap ... exit 5` | LaunchAgent en état transitoire | arrêter, attendre, redémarrer ; sinon lancer `rabbitmq-server` au premier plan pour voir l'erreur |
| MassTransit ne se connecte pas | broker arrêté ou démarré après les API | `rabbitmq-diagnostics -q ping`, puis redémarrer les API |
| Consumer `event-listener` = 0 | connexion asynchrone en cours | attendre 10–30 s ; l'E2E attend jusqu'à 60 s |
| `/api/overview` répond 500 ou bloque | bug RabbitMQ 4.4.0 + OTP 29 | utiliser `/api/queues` ; ne pas conclure que le broker est mort |
| Swagger renvoie 404 | environnement Production | lancer via `./scripts/start-all.sh` (Development) |
| Base SQLite introuvable | migrations non appliquées | `dotnet ef database update` avec les deux options |
| `database is locked` | écriture concurrente avec `sqlite3` | ne pas écrire dans la base pendant que le service travaille ; relancer la requête |
| Date sans suffixe `Z` | comportement SQLite/EF Core | limite connue, sans impact fonctionnel |

---

## 8. Ce qui n'est pas installé

- **Aucun Docker, Docker Desktop ou Compose** n'est requis ni utilisé par la solution.
- **Aucun SQL Server, PostgreSQL, MySQL ou base distante** n'est utilisé par l'application. Les seules bases sont les deux fichiers SQLite du projet.
- **Aucun Visual Studio** n'est requis : VS Code + CLI .NET suffisent.

---

## 9. Références

- README : `../README.md`
- Guide de démarrage : `GUIDE_DEMARRAGE.md`
- Audit environnement : `../ENVIRONMENT_AUDIT.md`
- Résultats de test : `../TEST_RESULTS.md`
- Conformité PDF : `CONFORMITE_PDF.md`
