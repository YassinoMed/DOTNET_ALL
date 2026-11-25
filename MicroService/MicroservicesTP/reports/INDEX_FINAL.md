# INDEX FINAL DES LIVRABLES — MicroservicesTP

**Date :** 18 novembre 2026
**Solution :** `MicroservicesTP.sln`
**Périmètre :** livraison de l'atelier 8 après cycle final build → tests → E2E → audit → documentation → auto-pass.

Tous les chemins sont relatifs à la racine `MicroservicesTP/`.

---

## 1. Solution et projets

| Chemin | Description | Statut | Preuve |
|---|---|---|---|
| `MicroservicesTP.sln` | solution 5 projets | **présent, compilé** | `logs/build-final.log` |
| `global.json` | SDK 7.0.317, `rollForward: latestPatch` | **présent** | `dotnet --version` |
| `Directory.Build.props` | propriétés communes | **présent** | build 0 avertissement |
| `Shared/Shared.csproj` | contrat `ProductCreated` | **compilé** | build |
| `ProductsAPI/ProductsAPI.csproj` | API produits, EF Core, MassTransit | **compilé** | build |
| `OrdersAPI/OrdersAPI.csproj` | API commandes, consumer | **compilé** | build |
| `ApiGateway/ApiGateway.csproj` | Gateway Ocelot 23.4.3 | **compilé** | build |
| `Tests/MicroservicesTP.Tests.csproj` | 47 cas xUnit | **compilé, exécuté** | TRX |

---

## 2. Bases de données

| Chemin | Description | Statut | Preuve |
|---|---|---|---|
| `ProductsAPI/Products.db` | base produits, 9 lignes | **intègre** | `integrity_check = ok` |
| `OrdersAPI/Orders.db` | base commandes, 13 projections, 0 commande | **intègre** | `integrity_check = ok` |
| `ProductsAPI/Migrations/` | migration `InitialCreate` | **présente** | historique EF |
| `OrdersAPI/Migrations/` | migration `InitialCreate` | **présente** | historique EF |

Aucune base n'a été supprimée, réinitialisée ou migrée pendant le cycle final. Les projections `4`, `7`, `10`, `13` correspondent à des produits supprimés via l'API et restent volontairement (pas d'événement `ProductDeleted`).

---

## 3. Scripts

| Chemin | Description | Statut | Preuve |
|---|---|---|---|
| `scripts/check-environment.sh` | contrôle lecture seule de l'environnement | **présent** | exécutable |
| `scripts/start-all.sh` | démarrage RabbitMQ si besoin + 3 services | **présent** | utilisé par l'E2E |
| `scripts/stop-all.sh` | arrêt des services, RabbitMQ conservé | **présent** | utilisé par l'E2E |
| `scripts/lib.sh` | helpers ports, services, RabbitMQ | **présent** | sourcé par l'E2E |
| `scripts/e2e.sh` | 66 contrôles E2E non destructifs | **exécuté, 66 PASS** | `logs/e2e-final.log` |
| `scripts/validate-final.sh` | validation finale lecture seule | **exécuté, PASS** | `logs/validate-final.log` |
| `scripts/verify-propagation.sh` | propagation Products → Orders + contrats JSON, lecture seule | **exécuté, 43 PASS / 1 WARN / 0 FAIL** | `logs/verify-propagation.log` |

---

## 4. Code et tests

| Chemin | Description | Statut | Preuve |
|---|---|---|---|
| `ProductsAPI/Controllers/ProductsController.cs` | CRUD + publication | **vérifié** | tests + E2E |
| `OrdersAPI/Controllers/OrdersController.cs` | CRUD commandes | **vérifié** | tests + E2E |
| `OrdersAPI/Consumer/ProductCreatedConsumer.cs` | consumer idempotent | **vérifié** | tests + broker réel |
| `ApiGateway/Ocelot.json` | 4 routes CRUD | **vérifié** | 15 tests de configuration |
| `ApiGateway/Program.cs` | `await app.UseOcelot()` | **vérifié** | E2E Gateway |
| `Tests/ProductsControllerTests.cs` | 8 cas CRUD/publication | **46 réussis au total** | TRX |
| `Tests/OrdersControllerTests.cs` | 6 cas CRUD | **réussis** | TRX |
| `Tests/ProductCreatedConsumerTests.cs` | 6 cas consumer/idempotence | **réussis** | TRX |
| `Tests/ModelValidationTests.cs` | 10 cas validation | **réussis** | TRX |
| `Tests/OcelotConfigurationTests.cs` | 15 cas configuration | **réussis** | TRX |
| `Tests/SqlitePersistenceTests.cs` | 1 cas persistance | **réussi** | TRX |
| `Tests/RabbitMqIdempotenceTests.cs` | 1 cas broker réel | **réussi dans l'E2E, skip en suite** | `logs/e2e-final.log` |

---

## 5. Rapports finaux

| Chemin | Description | Statut | Preuve associée |
|---|---|---|---|
| `FINAL_AUDIT_BASELINE.md` | baseline initiale du 18/11/2026 13:08 | **présent** | état avant run final |
| `reports/BUILD_REPORT.md` | build final, SDK, RID, 0/0 | **présent** | `logs/build-final.log` |
| `reports/UNIT_TEST_REPORT.md` | 47 cas, 46 réussis, 1 ignoré | **présent** | `logs/tests-final.log`, TRX |
| `reports/TEST_COVERAGE_AUDIT.md` | décompte sans double comptage | **présent** | TRX + E2E |
| `reports/E2E_FINAL_REPORT.md` | 66 contrôles, 18 scénarios | **présent** | `logs/e2e-final.log` |
| `reports/PROPAGATION_VERIFICATION.md` | propagation confirmée + contrats JSON renforcés | **présent** | `logs/verify-propagation.log` |
| `docs/ARCHITECTURE_EVOLUTION.md` | Outbox, retry, DLQ, idempotence HTTP | **présent** | recommandations, non implémentées |
| `RAPPORT_FINAL_CONFORMITE.md` | rapport de livraison en 20 sections | **présent** | tous les rapports |
| `reports/AUTO_PASS_REPORT.md` | verdict final | **présent** | critères et preuves |
| `reports/INDEX_FINAL.md` | cet index | **présent** | inventaire réel |
| `reports/TestResults/complete-tests.trx` | résultats xUnit machine | **présent** | 47 cas |

---

## 6. Journaux du cycle final (preuves courantes)

| Chemin | Contenu | Statut |
|---|---|---|
| `logs/build-final.log` | restore + build, exit 0, 0 erreur / 0 avertissement | **preuve finale** |
| `logs/tests-final.log` | 47 cas, 46 réussis, 1 ignoré, 0 échec | **preuve finale** |
| `logs/e2e-final.log` | 66 PASS / 0 FAIL, données finales | **preuve finale** |
| `logs/e2e-idempotence.log` | test broker réel réussi | **preuve finale** |
| `logs/e2e-start.log` | démarrage des services pendant l'E2E final | **preuve finale** |
| `logs/validate-final.log` | validation finale lecture seule | **preuve finale** |
| `logs/verify-propagation.log` | propagation SQLite + contrats JSON + topologie RabbitMQ | **43 PASS / 1 WARN / 0 FAIL** |
| `logs/environment.txt` | versions, ports, état initial | **contexte** |

---

## 7. Journaux historiques (conservés, non utilisés comme preuve courante)

| Chemin | Origine |
|---|---|
| `logs/build.log` | session précédente (12:49) |
| `logs/unit-tests.log` | session précédente (12:49) |
| `logs/e2e-run.log` | session précédente (12:48) |
| `logs/archive/build-20261118-1248.log` | copie archivée |
| `logs/archive/unit-tests-20261118-1248.log` | copie archivée |
| `logs/archive/e2e-run-20261118-1248.log` | copie archivée |
| `logs/archive/e2e-start-20261118-1248.log` | copie archivée |
| `logs/archive/e2e-idempotence-20261118-1248.log` | copie archivée |

Aucun journal historique n'a été supprimé. Les journaux finaux portent le suffixe `-final` ou sont datés du cycle 13:09–13:11.

---

## 8. Documentation

| Chemin | Rôle | Statut |
|---|---|---|
| `README.md` | point d'entrée, commandes, résultats, limites | **harmonisé** |
| `TEST_RESULTS.md` | synthèse des exécutions et données | **harmonisé** |
| `RAPPORT_FINAL.md` | première synthèse de réalisation | **harmonisé** |
| `ENVIRONMENT_AUDIT.md` | audit d'environnement | **présent** |
| `docs/INSTALLATION_MACOS.md` | installation native Homebrew | **présent** |
| `docs/GUIDE_DEMARRAGE.md` | démarrage, exemples, dépannage, §9.5 validateur | **harmonisé** |
| `docs/CONFORMITE_PDF.md` | conformité au support, 20 écarts | **harmonisé** |
| `docs/AUTO_PASS.md` | évaluation critère par critère | **harmonisé** |
| `docs/ARCHITECTURE_EVOLUTION.md` | évolutions recommandées | **créé** |

---

## 9. Synthèse de vérification

| Indicateur | Valeur | Source |
|---|---:|---|
| Build | 0 erreur / 0 avertissement, exit 0 | `logs/build-final.log` |
| Cas xUnit | 47 (46 réussis, 1 ignoré, 0 échec) | `reports/TestResults/complete-tests.trx` |
| Contrôles E2E | 66 PASS / 0 FAIL | `logs/e2e-final.log` |
| Test broker réel | 1 réussi (2 publications, 1 ligne) | `logs/e2e-final.log` |
| Produits | 9 | `Products.db` (lecture immuable) |
| Projections Orders | 13 | `Orders.db` (lecture immuable) |
| Commandes | 0 | `Orders.db` |
| Intégrité SQLite | ok / ok | `PRAGMA integrity_check` |
| Validation finale | PASS, 56 réussis / 4 avertissements / 0 échec | `logs/validate-final.log` |

---

## 10. Notes de lecture

1. Les preuves courantes sont celles du **18 novembre 2026, 13:09–13:11 CET**, complétées par la validation finale.
2. Les anciens journaux restent consultables dans `logs/` et `logs/archive/` ; ils ne valident pas la version actuelle.
3. Le test broker réel est unique : compté une fois dans les 47 cas xUnit, il est ignoré en suite complète puis exécuté par l'E2E. Il n'existe pas de 48e test.
4. Aucune fonctionnalité future (Outbox, `ProductUpdated`, authentification) n'est listée comme réalisée.

---

## 11. Compléments d'outillage (18/11/2026, après le snapshot)

Ces fichiers ne modifient ni le code applicatif, ni les bases, ni les résultats figés ci-dessus.

| Chemin | Description | Statut | Vérification |
|---|---|---|---|
| `postman/MicroservicesTP.postman_collection.json` | 14 requêtes, 4 dossiers | **fourni** | JSON valide ; dossiers 00-02 vérifiés en HTTP 200 via curl |
| `postman/MicroservicesTP.postman_environment.json` | URLs locales et compte RabbitMQ | **fourni** | JSON valide |
| `postman/README.md` | import, mutation contrôlée, vérification SQLite | **fourni** | — |
| `docker/docker-compose.rabbitmq.yml` | chemin Docker du PDF : `ecomm`, 4001:5672 | **fourni, non exécuté** | YAML valide |
| `docker/README.md` | surcharge `RabbitMq__Port=4001` | **fourni** | — |
| `logs/postman-readonly.log` | exécution Newman des dossiers 00-02 | **11 requêtes, 23 assertions, 0 échec** | `postman/README.md` §7 |
| `scripts/verify-propagation.sh` | vérification lecture seule propagation + contrats JSON | **exécuté** | `logs/verify-propagation.log` |
| `reports/PROPAGATION_VERIFICATION.md` | rapport propagation, JSON métier, topologie, réserve | **présent** | `logs/verify-propagation.log` |
| `logs/verify-propagation.log` | 43 réussis, 1 avertissement, 0 échec | **PASS_WITH_RESERVATIONS, exit 0** | ce journal |

Le POST du dossier 03 n'a pas été exécuté afin de ne pas ajouter de données métier. Le parcours équivalent est couvert par `scripts/e2e.sh`.
