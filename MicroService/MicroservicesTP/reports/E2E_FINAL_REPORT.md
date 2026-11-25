# E2E FINAL REPORT — MicroservicesTP

**Date d'exécution :** 18 novembre 2026, 13:10:16 CET
**Script :** `scripts/e2e.sh`
**Journal intégral :** `logs/e2e-final.log`
**Code retour :** **0**

---

## 1. Résultat global

```text
Checks PASS : 66
Checks FAIL : 0

Tous les contrôles ont réussi.
```

| Indicateur | Valeur |
|---|---:|
| Contrôles exécutés | **66** |
| Réussis | **66** |
| Échoués | **0** |
| Non exécutés dans le script | **0** |

---

## 2. Résultat par section

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

---

## 3. Couverture des 18 scénarios demandés

| # | Scénario | Statut | Preuve observée |
|---:|---|---|---|
| 1 | ProductsAPI GET | **PASS** | `GET /apigateway/Products/{id}` → 200 |
| 2 | ProductsAPI POST | **PASS** | `POST /apigateway/Products` → 201 |
| 3 | ProductsAPI PUT | **PASS** | 204 + stock 7 vérifié en base |
| 4 | ProductsAPI DELETE | **PASS** | 204, puis GET 404, ligne absente |
| 5 | OrdersAPI GET | **PASS** | `GET /apigateway/Orders/{id}` → 200 |
| 6 | OrdersAPI POST | **PASS** | `POST /apigateway/Orders` → 201 |
| 7 | OrdersAPI PUT | **PASS** | 204 + `CustomerName` mis à jour en base |
| 8 | OrdersAPI DELETE | **PASS** | 204, GET 404, `COUNT(*)=0` |
| 9 | CRUD via Gateway | **PASS** | sections 5 et 6, 23 contrôles |
| 10 | Validation des données | **PASS** | nom vide 400, prix négatif 400, commande sans produit 400 |
| 11 | Gestion 404 | **PASS** | produit 999999, commande supprimée, route inconnue |
| 12 | Publication RabbitMQ | **PASS** | exchange fanout + message consommé, file vide |
| 13 | Consommation RabbitMQ | **PASS** | `event-listener` : 1 consumer, projection créée |
| 14 | Synchronisation Products → Orders | **PASS** | produit 11 présent dans les deux bases |
| 15 | Idempotence | **PASS** | test xUnit broker réel, 2 publications, 1 ligne |
| 16 | Persistance après redémarrage | **PASS** | produit et projection toujours présents |
| 17 | Indépendance des services | **PASS** | chaque API répond sans l'autre ; `Data Source` isolés |
| 18 | Erreurs de disponibilité | **PARTIEL** | Gateway → 502 quand ProductsAPI est arrêté ; **coupure réelle RabbitMQ `NOT_TESTED`** |

**Scénario 18 — précision :** l'indisponibilité d'un service HTTP est testée et la Gateway renvoie bien `502`, sans faux succès. L'arrêt réel de RabbitMQ n'a **pas** été exécuté : le broker est partagé avec d'autres usages du poste et son interruption n'était pas maîtrisable sans impact externe. Le comportement `503` en cas d'échec de publication est couvert par un test unitaire avec `IPublishEndpoint` mocké, pas par une panne réelle.

---

## 4. Preuves marquantes du run final

```text
[PASS] POST /apigateway/Products (observé=201)
[PASS] Identifiant produit généré (observé=11)
[PASS] ProductCreated consommé : projection présente dans Orders.db.
[PASS] Exchange fanout ProductCreated présent (observé=fanout True)
[PASS] File event-listener avec exactement 1 consumer (observé=1)
[PASS] Connexions MassTransit actives vers RabbitMQ (observé=2, attendu>=2).
[PASS] Test xUnit d'idempotence exécuté sur RabbitMQ réel (2 publications).
[PASS] Une seule ligne locale après rejeu (observé=1)
[PASS] Nom mis à jour par le rejeu (sans doublon) (observé=E2E-Idem-20261118-131016-updated)
[PASS] Gateway ne renvoie pas de faux succès quand ProductsAPI est arrêté (HTTP 502).
[PASS] ProductsAPI configuré uniquement sur Products.db (observé=Data Source=Products.db)
[PASS] OrdersAPI configuré uniquement sur Orders.db (observé=Data Source=Orders.db)
```

---

## 5. Données créées par ce run

| Élément | Valeur |
|---|---|
| Produit de synchronisation | **11** — `E2E-Sync-20261118-131016` |
| Produit d'idempotence | **12** — `E2E-Idem-20261118-131016-updated` |
| Produit CRUD | **13** — créé puis supprimé via `DELETE` |

Projection `Orders.db` : `13` lignes (`1..13`). La projection `13` reste volontairement, car aucun événement `ProductDeleted` n'existe (limite documentée).

---

## 6. État final de l'environnement

| Élément | État |
|---|---|
| Ports 7066 / 7065 / 7162 | **libres** (services arrêtés par le script) |
| RabbitMQ 5672 / 15672 | **actifs** (non arrêtés) |
| `Products.db` | présent, 20 480 octets, 9 produits |
| `Orders.db` | présent, 24 576 octets, 13 projections, 0 commande |
| Intégrité SQLite | **ok** sur les deux bases (`mode=ro&immutable=1`) |
| Docker | non utilisé |

---

## 7. Limites du E2E

1. Aucun test de coupure réelle du broker (`NOT_TESTED`).
2. Aucun test de charge ou de concurrence soutenue.
3. Les CRUD passent par la Gateway ; les contrôleurs sont couverts séparément en xUnit.
4. La projection `Orders.db` ne suit pas les PUT/DELETE de ProductsAPI.
5. Le test d'idempotence dépend d'OrdersAPI déjà démarré et d'une projection initiale existante.

Ces limites sont déclarées, pas masquées.
