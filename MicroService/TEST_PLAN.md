# PLAN DE TESTS — MicroservicesTP

**Principe :** un test n'est déclaré réussi que si la commande a réellement été exécutée et si sa sortie observée est conservée. Aucun résultat n'est déduit d'une simple lecture du code.

**Environnement :** macOS arm64, .NET 7.0.317, SQLite 3.54, RabbitMQ Docker (`rabbitmq:3-management`, ports 5672/15672), Ocelot 23.4.3.

---

## 1. Préconditions

| Précondition | Vérification |
|---|---|
| Solution compile | `dotnet build MicroservicesTP.sln` |
| Outil EF disponible | `dotnet tool restore && dotnet ef --version` |
| RabbitMQ démarré | `docker exec ecomm rabbitmqctl status` |
| Management plugin actif | `curl http://localhost:15672` |
| Ports libres | `lsof -iTCP:7065 -iTCP:7066 -iTCP:7162` |
| Bases accessibles | `sqlite3 ProductsAPI/Products.db` et `OrdersAPI/Orders.db` |

---

## 2. Tests unitaires et d'intégration locale (`dotnet test`)

| ID | Test | Dépendance broker | Preuve |
|---|---|---|---|
| UT-01 | Création d'un produit dans `ProductsAPIContext` | Non | Test xUnit |
| UT-02 | GET all / GET id / PUT / DELETE ProductsController | Non | Test xUnit |
| UT-03 | `ProductCreated` publié après POST réussi | Non (mock `IPublishEndpoint`) | Test xUnit |
| UT-04 | Aucune publication si la sauvegarde échoue | Non | Test xUnit |
| UT-05 | CRUD OrdersController | Non | Test xUnit |
| UT-06 | Consumer : premier message → insertion | Non | Test xUnit |
| UT-07 | Consumer : même message rejoué → aucune duplication | Non | Test xUnit |
| UT-08 | Consumer : nouveau nom → mise à jour | Non | Test xUnit |
| UT-09 | Persistance fichier SQLite après dispose/réouverture | Non | Test xUnit |
| UT-10 | Validation des modèles (Name vide, prix négatif) | Non | Test xUnit |
| UT-11 | Configuration Ocelot : 4 routes, méthodes CRUD | Non | Test xUnit |

> **Limite explicite :** UT-03 utilise un mock. Il ne prouve **pas** la publication réelle vers RabbitMQ. Cette preuve est apportée par les tests E2E.

---

## 3. Tests fonctionnels HTTP directs

### Test A — ProductsAPI

| Étape | Commande | Attendu |
|---|---|---|
| A1 | `curl -s http://localhost:7066/swagger/index.html` | HTTP 200 |
| A2 | `curl -s http://localhost:7066/api/Products` | HTTP 200 + tableau JSON |
| A3 | `curl -X POST .../api/Products -H 'Content-Type: application/json' -d '{"name":"...","stockQte":10,"price":99.5}'` | HTTP 201 + `productId` |
| A4 | `curl .../api/Products/{id}` | HTTP 200 + produit |
| A5 | `curl -X PUT .../api/Products/{id} -d '{...}'` | HTTP 204 |
| A6 | `curl -X DELETE .../api/Products/{id}` | HTTP 204 |
| A7 | `sqlite3 Products.db "SELECT ..."` | ligne conforme |

### Test B — OrdersAPI

| Étape | Commande | Attendu |
|---|---|---|
| B1 | `curl -s http://localhost:7065/swagger/index.html` | HTTP 200 |
| B2 | `curl -s http://localhost:7065/api/Orders` | HTTP 200 |
| B3 | `curl -X POST .../api/Orders ...` | HTTP 201 |
| B4 | `curl .../api/Orders/{id}` | HTTP 200 |
| B5 | `curl -X PUT .../api/Orders/{id}` | HTTP 204 |
| B6 | `curl -X DELETE .../api/Orders/{id}` | HTTP 204 |
| B7 | `sqlite3 Orders.db "SELECT ..."` | ligne conforme |

---

## 4. Tests API Gateway

| ID | Requête | Attendu |
|---|---|---|
| GW-01 | `GET http://localhost:7162/apigateway/Products` | 200 + produits |
| GW-02 | `GET http://localhost:7162/apigateway/Products/{id}` | 200 |
| GW-03 | `POST http://localhost:7162/apigateway/Products` | 201 |
| GW-04 | `PUT http://localhost:7162/apigateway/Products/{id}` | 204 |
| GW-05 | `DELETE http://localhost:7162/apigateway/Products/{id}` | 204 |
| GW-06 | `GET http://localhost:7162/apigateway/Orders` | 200 |
| GW-07 | `GET http://localhost:7162/apigateway/Orders/{id}` | 200 |
| GW-08 | `POST http://localhost:7162/apigateway/Orders` | 201 |
| GW-09 | `PUT/DELETE /apigateway/Orders/{id}` | 204 |
| GW-10 | `GET http://localhost:7162/apigateway/Unknown` | 404 |
| GW-11 | `GET /apigateway/Products/999999` | 404 |

---

## 5. Tests RabbitMQ

| ID | Vérification | Commande | Attendu |
|---|---|---|---|
| RMQ-01 | Conteneur RabbitMQ actif | `docker ps --filter name=ecomm` | `ecomm` Up (`5672`, `15672`) |
| RMQ-02 | Nœud opérationnel | `docker exec ecomm rabbitmqctl status` | `Status of node rabbit@my-rabbit` |
| RMQ-03 | Ports | `lsof -iTCP:5672 -iTCP:15672` | écoute |
| RMQ-04 | Management UI | `curl -u guest:guest http://localhost:15672/api/overview` | JSON |
| RMQ-05 | Connexions des deux services | `/api/connections` | 2 connexions |
| RMQ-06 | Exchange `ProductCreated` | `/api/exchanges` | exchange présent |
| RMQ-07 | Queue `event-listener` | `/api/queues` | queue présente |
| RMQ-08 | Message publié | `/api/exchanges/.../publish` ou logs | message accepté |
| RMQ-09 | Message consommé | queue `messages` | traité, pas bloqué |
| RMQ-10 | Rejeu | publish manuel du même payload | aucune duplication SQL |

---

## 6. Test de synchronisation de bout en bout

| Étape | Action | Preuve attendue |
|---|---|---|
| E2E-01 | Démarrer RabbitMQ | `rabbitmqctl status` OK |
| E2E-02 | Démarrer OrdersAPI (consumer) | logs `event-listener` |
| E2E-03 | Démarrer ProductsAPI | logs MassTransit connecté |
| E2E-04 | Démarrer ApiGateway | port 7162 |
| E2E-05 | POST `/apigateway/Products` | 201 + productId |
| E2E-06 | Lire `Products.db` | produit présent |
| E2E-07 | Attendre la consommation | ligne dans `Orders.db` |
| E2E-08 | Lire `Orders.db` | `Id` et `Name` identiques |
| E2E-09 | Rejouer le même message | une seule ligne locale |
| E2E-10 | Redémarrer les trois services | données toujours présentes |
| E2E-11 | Arrêter ProductsAPI, GET Orders | OrdersAPI fonctionne |
| E2E-12 | Arrêter OrdersAPI, GET Products | ProductsAPI fonctionne |
| E2E-13 | Vérifier l'absence d'accès croisé | aucun `Products.db` dans OrdersAPI |

---

## 7. Test d'idempotence

**Procédure :**

1. Créer un produit `P` via `POST /api/Products`.
2. Attendre la persistance dans `Orders.db`.
3. Re-publier le même `ProductCreated` dans RabbitMQ (management API ou `rabbitmqadmin`).
4. Attendre le traitement.
5. Exécuter :
   ```sql
   SELECT COUNT(*) FROM Products WHERE Id = <P.Id>;
   ```
6. Résultat attendu : `1`.
7. Modifier le nom dans le message rejoué et vérifier que la ligne est mise à jour sans duplication.

**Preuve :** sortie SQL + logs du consumer.

---

## 8. Test de persistance

1. Noter les IDs créés.
2. Arrêter les trois API.
3. Vérifier que les fichiers `.db` existent.
4. Redémarrer.
5. `GET` les mêmes IDs via la Gateway.
6. Vérifier les lignes SQL.

**Attendu :** données identiques après redémarrage.

---

## 9. Test d'indépendance des bases

| Vérification | Commande | Attendu |
|---|---|---|
| ProductsAPI ne référence pas Orders.db | `grep -R "Orders.db" ProductsAPI` | aucun résultat |
| OrdersAPI ne référence pas Products.db | `grep -R "Products.db" OrdersAPI` | aucun résultat |
| Aucune chaîne de connexion partagée | inspection `appsettings.json` | deux chaînes distinctes |
| Aucune jointure inter-bases | inspection code | aucune |
| OrdersAPI démarre sans ProductsAPI | arrêt ProductsAPI puis GET Orders | 200 |
| ProductsAPI démarre sans OrdersAPI | arrêt OrdersAPI puis GET Products | 200 |

---

## 10. Tests de robustesse

| ID | Scénario | Attendu |
|---|---|---|
| ROB-01 | POST produit avec `name` vide | 400 ValidationProblemDetails |
| ROB-02 | POST produit avec `price` négatif | 400 |
| ROB-03 | GET produit inexistant | 404 |
| ROB-04 | PUT produit inexistant | 404 |
| ROB-05 | DELETE produit inexistant | 404 |
| ROB-06 | RabbitMQ arrêté puis POST produit | erreur explicite, pas de faux succès |
| ROB-07 | Message invalide (Id <= 0) | erreur contrôlée / queue d'erreur |
| ROB-08 | Route Gateway inconnue | 404 |
| ROB-09 | Requête avec JSON malformé | 400 |

---

## 11. Matrice de traçabilité

| Exigence | Tests |
|---|---|
| F-PROD-01..13 | A1–A7, UT-01..04, E2E |
| F-ORD-01..10 | B1–B7, UT-05, E2E |
| F-GW-01..09 | GW-01..11, UT-11 |
| F-MSG-01..13 | RMQ-01..10, UT-06..08, E2E |
| F-SYNC-01..04 | E2E-05..13 |
| T-DB-01..08 | A7, B7, section 9 |
| T-QUAL-01..08 | ROB-01..09 |
| Tests automatisés | `dotnet test` |

---

## 12. Statuts autorisés

- **PASS** : exécuté, résultat conforme, preuve conservée.
- **PASS_WITH_RESERVATIONS** : exécuté, conforme, mais limitation documentée.
- **FAIL** : exécuté, résultat non conforme.
- **NOT_EXECUTED** : non exécuté (dépendance absente, décision explicite).
- **BLOCKED** : dépendance indisponible.

Le rapport final ne pourra être `AUTO-PASS` que si tous les tests obligatoires sont PASS et si aucune dépendance critique n'est NOT_EXECUTED.
