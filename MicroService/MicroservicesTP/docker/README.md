# Chemin Docker du PDF — optionnel et non exécuté

Le support installe RabbitMQ dans un conteneur Docker (`rabbitmq:3-management`), expose AMQP sur le port **4001** et nomme le conteneur **`ecomm`** (PDF p. 35, 41 et 51).

Ce fichier fournit ce chemin à l'identique pour un poste équipé de Docker. Il n'a **pas été exécuté** sur ce poste : la livraison utilise RabbitMQ natif Homebrew sur le port standard **5672**, conformément à la contrainte « aucun Docker ».

## 1. Démarrer le broker Docker

```bash
docker compose -f docker/docker-compose.rabbitmq.yml up -d
```

Vérifier le conteneur et le broker, comme dans le PDF :

```bash
docker ps --filter name=ecomm
docker exec ecomm rabbitmqctl status
```

Interface de gestion : <http://localhost:15672> — `guest` / `guest`.

## 2. Pointer les API sur le port 4001

Les fichiers `appsettings.json` restent sur **5672** pour le broker natif. La surcharge se fait par variable d'environnement, sans modifier le code :

```bash
RabbitMq__Port=4001 dotnet run --project ProductsAPI
RabbitMq__Port=4001 dotnet run --project OrdersAPI
```

La Gateway ne dépend pas de RabbitMQ :

```bash
dotnet run --project ApiGateway
```

## 3. Arrêter le broker Docker

```bash
docker compose -f docker/docker-compose.rabbitmq.yml down
```

## 4. Ports

| Chemin | AMQP | Management | Conteneur |
|---|---:|---:|---|
| Docker (PDF) | **4001** | 15672 | `ecomm` |
| Natif (livraison) | **5672** | 15672 | aucun |

Ne pas lancer les deux chemins sur les mêmes ports de management en même temps. Les deux brokers peuvent coexister (4001 et 5672), mais chaque instance d'API doit pointer vers un seul broker via `RabbitMq__Port`.

## 5. Statut

| Élément | Statut |
|---|---|
| Fichier Compose fourni | **OUI** |
| Exécution Docker sur ce poste | **NON** (Docker absent / contrainte de la mission) |
| Chemin natif 5672 testé | **OUI** (E2E 66 PASS) |
| Chemin Docker 4001 validé à l'exécution | **NON** — non revendiqué |
