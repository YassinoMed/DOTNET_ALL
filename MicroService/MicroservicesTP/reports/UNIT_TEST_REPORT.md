# UNIT TEST REPORT — MicroservicesTP

**Date :** 18 novembre 2026, 13:09:59 CET
**Projet de test :** `Tests/MicroservicesTP.Tests.csproj` (net7.0)
**Journal :** `logs/tests-final.log`
**Résultats TRX :** `reports/TestResults/complete-tests.trx`

---

## 1. Commande

```bash
dotnet test MicroservicesTP.sln --no-build \
  --logger "trx;LogFileName=complete-tests.trx" \
  --results-directory reports/TestResults
```

Code retour : **0**.

---

## 2. Résultats globaux

| Indicateur | Valeur |
|---|---:|
| Total de cas découverts | **47** |
| Cas exécutés | **46** |
| Réussis | **46** |
| Échoués | **0** |
| Erreurs | **0** |
| Ignorés (non exécutés) | **1** |
| Durée rapportée par xUnit | **406 ms** |
| **Verdict** | **PASS** |

Extrait réel :

```text
Réussi! - échec : 0, réussite : 46, ignorée(s) : 1, total : 47, durée : 406 ms
```

---

## 3. Résultat par classe

| Classe de test | Cas | Réussis | Ignorés | Échecs |
|---|---:|---:|---:|---:|
| `ModelValidationTests` | 10 | 10 | 0 | 0 |
| `OcelotConfigurationTests` | 15 | 15 | 0 | 0 |
| `OrdersControllerTests` | 6 | 6 | 0 | 0 |
| `ProductCreatedConsumerTests` | 6 | 6 | 0 | 0 |
| `ProductsControllerTests` | 8 | 8 | 0 | 0 |
| `SqlitePersistenceTests` | 1 | 1 | 0 | 0 |
| `RabbitMqIdempotenceTests` | 1 | 0 | **1** | 0 |
| **Total** | **47** | **46** | **1** | **0** |

Le projet de test est unique ; il référence `ProductsAPI`, `OrdersAPI` et `Shared`. Aucun autre projet ne contient de tests.

---

## 4. Catégories

| Catégorie | Cas | Statut dans cette exécution |
|---|---:|---|
| Tests unitaires purs (contrôleurs, validation, consumer, persistance) | 31 | 31 réussis |
| Tests de configuration Ocelot (`Trait` absent, classe dédiée) | 15 | 15 réussis |
| Test d'intégration broker réel (`Trait("Category","RabbitMq")`) | 1 | **ignoré** (préconditions absentes) |

Aucun test n'est marqué `[Trait]` en dehors du test broker réel.

---

## 5. Test ignoré — justification exacte

**Test :** `RabbitMqIdempotenceTests.Rejouer_le_meme_evenement_ne_cree_pas_de_doublon_dans_Orders_db`

Il est ignoré par `Skip.IfNot(RABBITMQ_E2E == "1", ...)` lors d'un `dotnet test` ordinaire. Il exige :

1. `RABBITMQ_E2E=1` ;
2. `ORDERS_DB_PATH` pointant vers `Orders.db` ;
3. `IDEMPOTENCE_PRODUCT_ID` et `IDEMPOTENCE_PRODUCT_NAME` correspondant à une projection déjà créée ;
4. OrdersAPI démarré et abonné à `event-listener`.

Ce test **n'est pas** compté comme réussi dans ce rapport. Il est exécuté séparément par `scripts/e2e.sh`, avec succès :

```text
Réussi MicroservicesTP.Tests.RabbitMqIdempotenceTests.Rejouer_le_meme_evenement_ne_cree_pas_de_doublon_dans_Orders_db [8 s]
Réussi(s) : 1
```

Journal : `logs/e2e-idempotence.log`.

---

## 6. Absence de double comptage

- Les **47 cas xUnit** incluent le test broker réel (1 cas, ignoré ici).
- L'E2E **ne rajoute pas** un 48e test xUnit : il réexécute ce même cas avec les préconditions réelles.
- Les **66 contrôles E2E** sont des vérifications Bash distinctes (HTTP, SQLite, RabbitMQ, redémarrage) ; un seul de ces contrôles déclenche l'exécution du test xUnit broker réel.

Détail complet : `reports/TEST_COVERAGE_AUDIT.md`.

---

## 7. Preuves

| Preuve | Chemin |
|---|---|
| Journal console complet | `logs/tests-final.log` |
| Résultats TRX | `reports/TestResults/complete-tests.trx` |
| Journal du test broker réel (E2E) | `logs/e2e-idempotence.log` |
