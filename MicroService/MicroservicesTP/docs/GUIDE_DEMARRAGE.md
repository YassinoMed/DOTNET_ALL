# GUIDE DE DÉMARRAGE ET D'UTILISATION

Ce guide décrit l'utilisation quotidienne de la solution `MicroservicesTP` après l'installation décrite dans `INSTALLATION_MACOS.md`.

---

## 1. Vérifier l'environnement

```bash
cd MicroservicesTP
dotnet tool restore
./scripts/check-environment.sh
```

Le script est en lecture seule. Il affiche le système, .NET, RabbitMQ, les ports, les bases et les outils.

---

## 2. Démarrer

```bash
./scripts/start-all.sh
```

Le script démarre RabbitMQ si nécessaire, applique les migrations manquantes, compile, puis lance :

| Service | URL | Swagger |
|---|---|---|
| ProductsAPI | `http://localhost:7066` | `http://localhost:7066/swagger` |
| OrdersAPI | `http://localhost:7065` | `http://localhost:7065/swagger` |
| ApiGateway | `http://localhost:7162` | pas de Swagger (Ocelot) |
| RabbitMQ Management | `http://localhost:15672` | `guest` / `guest` |

Pour suivre les journaux :

```bash
tail -f logs/ProductsAPI.log
tail -f logs/OrdersAPI.log
tail -f logs/ApiGateway.log
```

---

## 3. Arrêter

```bash
./scripts/stop-all.sh
```

Les trois services sont arrêtés. **RabbitMQ reste actif.** Pour l'arrêter explicitement :

```bash
brew services stop rabbitmq
```

---

## 4. Tester avec Swagger

1. Ouvrir `http://localhost:7066/swagger`.
2. Déplier `POST /api/Products`, cliquer sur **Try it out**.
3. Corps exemple :

```json
{
  "name": "Clavier",
  "stockQte": 10,
  "price": 49.90
}
```

4. Exécuter : réponse `201` avec `productId`.
5. Vérifier la synchronisation :

```bash
sqlite3 -header -column OrdersAPI/Orders.db \
  "SELECT Id, Name FROM Products ORDER BY Id;"
```

Le produit doit apparaître dans `Orders.db` après quelques secondes.

Répéter l'opération sur `http://localhost:7065/swagger` pour les commandes.

---

## 5. Tester avec curl

### 5.1 Via la Gateway (parcours nominal)

```bash
# Liste des produits
curl -s http://localhost:7162/apigateway/Products

# Création
curl -s -X POST http://localhost:7162/apigateway/Products \
  -H 'Content-Type: application/json' \
  -d '{"name":"Clavier","stockQte":10,"price":49.90}'

# Lecture (remplacer 1 par l'identifiant retourné)
curl -s http://localhost:7162/apigateway/Products/1

# Mise à jour
curl -s -o /dev/null -w '%{http_code}\n' \
  -X PUT http://localhost:7162/apigateway/Products/1 \
  -H 'Content-Type: application/json' \
  -d '{"productId":1,"name":"Clavier modifié","stockQte":7,"price":59.90}'

# Suppression
curl -s -o /dev/null -w '%{http_code}\n' \
  -X DELETE http://localhost:7162/apigateway/Products/1
```

Réponses attendues : `200`, `201`, `204`, `404`.

### 5.2 Commandes via la Gateway

```bash
# Création
curl -s -X POST http://localhost:7162/apigateway/Orders \
  -H 'Content-Type: application/json' \
  -d '{"orderDate":"2026-11-18T10:00:00Z","customerName":"Alice","productId":2}'

# Lecture
curl -s http://localhost:7162/apigateway/Orders/1

# Mise à jour
curl -s -o /dev/null -w '%{http_code}\n' \
  -X PUT http://localhost:7162/apigateway/Orders/1 \
  -H 'Content-Type: application/json' \
  -d '{"ordersId":1,"orderDate":"2026-11-18T11:00:00Z","customerName":"Alice Martin","productId":2}'

# Suppression
curl -s -o /dev/null -w '%{http_code}\n' \
  -X DELETE http://localhost:7162/apigateway/Orders/1
```

### 5.3 Vérifier les erreurs

```bash
# 400 : nom vide
curl -s -o /dev/null -w '%{http_code}\n' \
  -X POST http://localhost:7162/apigateway/Products \
  -H 'Content-Type: application/json' \
  -d '{"name":"","stockQte":1,"price":1}'

# 404 : produit inexistant
curl -s -o /dev/null -w '%{http_code}\n' \
  http://localhost:7162/apigateway/Products/999999

# 404 : route inconnue
curl -s -o /dev/null -w '%{http_code}\n' \
  http://localhost:7162/apigateway/RouteInconnue
```

### 5.4 Accès direct (hors Gateway)

```bash
curl -s http://localhost:7066/api/Products
curl -s http://localhost:7065/api/Orders
```

L'utilisation directe sert au diagnostic ; le parcours de démonstration passe par la Gateway.

---

## 6. Inspecter les bases SQLite

```bash
# Schémas
sqlite3 ProductsAPI/Products.db ".schema Products"
sqlite3 OrdersAPI/Orders.db ".schema Orders"
sqlite3 OrdersAPI/Orders.db ".schema Products"

# Données
sqlite3 -header -column ProductsAPI/Products.db \
  "SELECT ProductId, Name, StockQte, Price FROM Products ORDER BY ProductId;"

sqlite3 -header -column OrdersAPI/Orders.db \
  "SELECT Id, Name FROM Products ORDER BY Id;"

sqlite3 -header -column OrdersAPI/Orders.db \
  "SELECT OrdersId, OrderDate, CustomerName, ProductId FROM Orders ORDER BY OrdersId;"
```

Rappels :

- `Products.db` ne contient **que** la table `Products` et l'historique EF.
- `Orders.db` contient `Orders`, la projection `Products` et l'historique EF.
- La projection `Products` d'OrdersAPI ne suit **pas** les PUT/DELETE de ProductsAPI : il n'existe pas d'événement `ProductUpdated`/`ProductDeleted`.
- `OrderDate` est stockée en texte ; le suffixe `Z` peut disparaître à la relecture (limite SQLite/EF Core).

---

## 7. Inspecter RabbitMQ

```bash
# État des files (consumer, messages)
curl -s -u guest:guest http://localhost:15672/api/queues \
  | python3 -m json.tool

# Connexions
curl -s -u guest:guest http://localhost:15672/api/connections \
  | python3 -m json.tool

# Exchanges
curl -s -u guest:guest http://localhost:15672/api/exchanges \
  | python3 -m json.tool
```

Attendu pendant le fonctionnement :

- exchange `Shared.Messages:ProductCreated`, type `fanout`, durable ;
- file `event-listener`, 1 consumer ;
- 2 connexions MassTransit (ProductsAPI et OrdersAPI) ;
- 0 message en attente après traitement.

> **Ne pas utiliser `/api/overview`** : bug RabbitMQ 4.4.0 + Erlang/OTP 29. Utiliser `/api/queues`. Voir `../ENVIRONMENT_AUDIT.md`.

Interface web : `http://localhost:15672` (`guest` / `guest`). La page **Overview** peut échouer ; les pages **Queues**, **Connections** et **Exchanges** fonctionnent.

---

## 8. Réinitialiser les données de test (destructif)

> **AVERTISSEMENT :** cette section supprime **toutes** les lignes des deux bases. Ne l'exécuter qu'avec l'accord du responsable du poste et après avoir sauvegardé ce qui doit l'être. Les données E2E sont conservées volontairement comme preuve.

```bash
# Arrêter les services avant toute écriture manuelle
./scripts/stop-all.sh

# ProductsAPI
sqlite3 ProductsAPI/Products.db \
  "DELETE FROM Products; DELETE FROM sqlite_sequence WHERE name='Products';"

# OrdersAPI
sqlite3 OrdersAPI/Orders.db \
  "DELETE FROM Orders; DELETE FROM Products; DELETE FROM sqlite_sequence WHERE name IN ('Orders');"
```

Ne pas supprimer les fichiers `.db` eux-mêmes : les migrations et l'historique EF doivent rester cohérents.

---

## 9. Lancer les tests

### 9.1 Tests unitaires

```bash
dotnet test MicroservicesTP.sln
```

Résultat attendu : **46 réussis, 1 ignoré, 0 échec**. Le test ignoré nécessite un broker réel.

### 9.2 E2E complet

```bash
./scripts/e2e.sh
```

Le script :

1. vérifie les prérequis et la liberté des ports ;
2. démarre RabbitMQ si nécessaire et les trois services ;
3. vérifie la topologie RabbitMQ ;
4. crée des produits via la Gateway et vérifie les deux bases ;
5. rejoue un événement sur le vrai broker ;
6. exécute le CRUD complet ;
7. redémarre les services et vérifie la persistance ;
8. vérifie l'indépendance des services ;
9. arrête les services à la fin.

Résultat attendu : **66 PASS, 0 FAIL**.

Journaux : `logs/e2e-final.log`, `logs/e2e-idempotence.log`.

### 9.3 Test d'idempotence seul

```bash
RABBITMQ_E2E=1 \
ORDERS_DB_PATH="$PWD/OrdersAPI/Orders.db" \
IDEMPOTENCE_PRODUCT_ID="<id>" \
IDEMPOTENCE_PRODUCT_NAME="<nom initial du produit>" \
dotnet test Tests/MicroservicesTP.Tests.csproj \
  --filter 'Category=RabbitMq' --logger 'console;verbosity=normal'
```

Ce test suppose qu'OrdersAPI tourne et que la projection du produit existe déjà avec son **nom initial** (sans suffixe `-updated`). Il est plus simple de passer par `./scripts/e2e.sh`, qui prépare ces conditions avec un produit neuf.

### 9.5 Validation finale (lecture seule)

```bash
./scripts/validate-final.sh
```

Le script vérifie la solution, le SDK, RabbitMQ, les deux bases (`integrity_check`), les rapports et les résultats de test. Il ne supprime rien, ne migre rien, ne redémarre rien et ne tue aucun processus. Code retour `0` si tous les contrôles obligatoires passent.

### 9.4 Filtrer par classe

```bash
dotnet test --filter FullyQualifiedName~ProductsControllerTests
dotnet test --filter FullyQualifiedName~ProductCreatedConsumerTests
dotnet test --filter FullyQualifiedName~OcelotConfigurationTests
```

---

## 10. Dépannage rapide

| Symptôme | Solution |
|---|---|
| Port occupé | `./scripts/stop-all.sh`, puis `lsof -nP -iTCP:7066 -sTCP:LISTEN` |
| Swagger 404 | lancer avec `./scripts/start-all.sh` (environnement Development) |
| `dotnet ef` absent | `dotnet tool restore` |
| Consumer = 0 | attendre 10–30 s, vérifier RabbitMQ et `logs/OrdersAPI.log` |
| `/api/overview` échoue | utiliser `/api/queues` ; bug d'environnement connu |
| RabbitMQ ne redémarre pas | `brew services stop rabbitmq`, attendre 40 s, `brew services start rabbitmq` |
| Base absente | appliquer les migrations avec `--project` et `--startup-project` |
| `database is locked` | ne pas écrire avec `sqlite3` pendant que le service travaille |

Guide d'installation complet : `INSTALLATION_MACOS.md`.
