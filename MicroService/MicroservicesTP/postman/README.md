# Collection Postman — Atelier 8

Cette collection reproduit les tests du support :

- **p. 27-29** : appels des deux microservices via la Gateway Ocelot ;
- **p. 51-52** : POST d'un produit dans ProductsAPI, puis vérification de la communication RabbitMQ.

Fichiers :

| Fichier | Rôle |
|---|---|
| `MicroservicesTP.postman_collection.json` | 4 dossiers, 14 requêtes |
| `MicroservicesTP.postman_environment.json` | URLs locales et compte RabbitMQ `guest` |

## 1. Import

1. Ouvrir Postman → **Import**.
2. Importer les deux fichiers JSON.
3. Sélectionner l'environnement **MicroservicesTP — Local** en haut à droite.

## 2. Dossiers en lecture seule

Les dossiers **00**, **01** et **02** ne modifient aucune donnée :

- **00 — Santé** : Gateway, Swagger des deux API, Overview RabbitMQ, file `event-listener` ;
- **01 — Gateway Ocelot** : liste des produits, produit par identifiant, liste des commandes ;
- **02 — APIs directes** : mêmes lectures sur 7066 et 7065.

Ils peuvent être exécutés avec **Run folder** sans précaution particulière.

## 3. Dossier de mutation contrôlée

Le dossier **03 — Communication RabbitMQ** contient un `POST /api/Products` qui **ajoute réellement un produit** dans `Products.db`. Il déclenche `ProductCreated`, consommé par OrdersAPI.

Ne l'exécuter que sur une base de TP dont les données peuvent évoluer. Le produit créé porte un nom `Postman-<timestamp>`.

La collection ne contient **aucun DELETE** : supprimer le produit dans ProductsAPI ne supprimerait pas sa projection dans OrdersAPI, car le PDF ne définit pas d'événement `ProductDeleted`.

## 4. Vérifier la projection dans Orders.db

Après le POST, la requête suivante est en **lecture seule** et ne modifie pas la base :

```bash
sqlite3 "file:OrdersAPI/Orders.db?mode=ro&immutable=1" \
  "SELECT Id, Name FROM Products ORDER BY Id DESC LIMIT 5;"
```

Le produit `Postman-...` doit apparaître avec le même `Id` que dans `Products.db` :

```bash
sqlite3 "file:ProductsAPI/Products.db?mode=ro&immutable=1" \
  "SELECT ProductId, Name FROM Products ORDER BY ProductId DESC LIMIT 5;"
```

La file `event-listener` doit avoir **1 consumer** et **0 message en attente** :

```bash
curl -s -u guest:guest \
  http://localhost:15672/api/queues/%2F/event-listener
```

## 5. Prérequis

```bash
cd MicroservicesTP
./scripts/start-all.sh
```

Les URLs par défaut sont :

| Variable | Valeur |
|---|---|
| `gatewayUrl` | `http://localhost:7162` |
| `productsUrl` | `http://localhost:7066` |
| `ordersUrl` | `http://localhost:7065` |
| `rabbitmqUrl` | `http://localhost:15672` |

## 6. Équivalence sans Postman

La collection n'est pas nécessaire au fonctionnement du TP. Les mêmes vérifications sont automatisées par :

```bash
./scripts/e2e.sh
```

Le script `e2e.sh` couvre le POST, la synchronisation, l'idempotence et la persistance sans intervention manuelle.

## 7. Vérification effectuée

Le 18/11/2026, les dossiers 00 à 02 ont été exécutés avec **Newman** (CLI Postman) sur les services actifs :

| Dossier | Requêtes | Assertions | Échecs |
|---|---:|---:|---:|
| 00 — Santé | 5 | 11 | 0 |
| 01 — Gateway Ocelot | 3 | 6 | 0 |
| 02 — APIs directes | 3 | 6 | 0 |
| **Total** | **11** | **23** | **0** |

Journal : `../logs/postman-readonly.log`.

Le dossier 03 n'a pas été exécuté : il ajoute volontairement un produit. Le parcours équivalent est couvert par `scripts/e2e.sh`.
