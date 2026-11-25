# VÉRIFICATION DE PROPAGATION ET CONTRATS JSON

**Date :** 18 novembre 2026
**Script :** `scripts/verify-propagation.sh` (lecture seule)
**Journal :** `logs/verify-propagation.log`
**Code retour :** **0**
**Résultat :** **43 réussis / 1 avertissement / 0 échec** — **PASS_WITH_RESERVATIONS**

---

## 1. Périmètre

Le script ne crée, ne modifie et ne supprime aucune donnée. Il ne démarre et n'arrête aucun service. Il :

- lit `Products.db` et `Orders.db` en mode `ro&immutable=1` ;
- interroge les API en `GET` (direct et Gateway) ;
- interroge RabbitMQ Management en lecture ;
- vérifie les champs métier et les valeurs, pas seulement les codes HTTP.

Commande :

```bash
./scripts/verify-propagation.sh
```

---

## 2. Propagation confirmée

Produit sélectionné : **17 — `Gateway-Test-Product`**.

| Contrôle | Résultat |
|---|---|
| Présent dans `Products.db` | **PASS** |
| Présent dans `Orders.db` (table `Products`) | **PASS** |
| Même nom dans les deux bases | **PASS** |
| Une seule projection pour cet identifiant | **PASS** |
| `GET` direct et `GET` Gateway renvoient le même JSON | **PASS** |
| `productId`, `name`, `stockQte`, `price` conformes à la base | **PASS** |

Valeurs observées : `productId=17`, `name=Gateway-Test-Product`, `stockQte=15`, `price=199.9`.

Cette vérification complète les preuves historiques :

- E2E du 18/11/2026 13:10 : produit **11** créé via la Gateway et projection vérifiée dans `Orders.db` ;
- test broker réel : produit **12**, deux publications, une seule projection après rejeu.

---

## 3. Contrats JSON métier renforcés

### Produit

| Champ | Vérification |
|---|---|
| `productId` | entier, égal à l'identifiant demandé |
| `name` | chaîne, égale à la valeur en base |
| `stockQte` | entier, égal à la valeur en base |
| `price` | numérique, égal à la valeur en base |

### Commande

Pour chaque commande renvoyée :

| Champ | Vérification |
|---|---|
| `ordersId` | présent |
| `customerName` | chaîne non vide |
| `productId` | présent ; doit exister dans la projection locale `Orders.db.Products` |
| `orderDate` | chaîne non vide |

Les réponses directes et Gateway sont comparées : mêmes produits, mêmes commandes.

---

## 4. Topologie RabbitMQ

| Élément | Résultat |
|---|---|
| `Overview` | HTTP 200, `product_name=RabbitMQ` |
| Exchange `Shared.Messages:ProductCreated` | **fanout** |
| Exchange `event-listener` | présent |
| File `event-listener` | 1 consumer, 0 message en attente |
| Binding `ProductCreated` → exchange `event-listener` | présent |
| Binding exchange `event-listener` → file `event-listener` | présent |

La topologie MassTransit réelle est donc : **exchange fanout → exchange `event-listener` → file `event-listener` → consumer**.

---

## 5. Réserve restante

**Commande 8 — `TEST-TERMINAL-ORDERS`** référence `productId=18`, absent de la projection `Orders.db.Products` (identifiants présents : 1 à 17).

- La commande n'a **pas** été supprimée : elle appartient aux tests externes et aucune donnée métier n'a été modifiée.
- Le PDF ne définit **aucune contrainte de clé étrangère** entre `Orders.ProductId` et la projection `Products`.
- Cette réserve concerne la validation des commandes, **pas** la propagation ProductsAPI → OrdersAPI, qui est confirmée.
- Le script la signale en **WARN** et n'échoue pas : le verdict reste `PASS_WITH_RESERVATIONS`.

Une évolution possible (hors PDF) serait de refuser une commande dont le `productId` n'existe pas dans la projection locale. Elle n'a pas été appliquée pour ne pas modifier le comportement attendu par les tests existants.

---

## 6. Conclusion

Les deux réserves du loop E2E sont traitées :

1. **Propagation RabbitMQ** : confirmée par la présence du produit 17 dans les deux bases, la topologie complète et les preuves E2E du produit 11.
2. **Vérification métier des JSON** : renforcée champ par champ sur les produits et les commandes, avec comparaison directe/Gateway.

Seule reste la commande orpheline 8, documentée et volontairement conservée.
