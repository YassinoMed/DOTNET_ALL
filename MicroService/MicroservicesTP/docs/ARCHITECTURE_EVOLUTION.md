# ARCHITECTURE — ÉVOLUTIONS RECOMMANDÉES

**Projet :** `MicroservicesTP` — Atelier 8
**Date :** 18 novembre 2026
**Statut :** document de recommandation. **Aucune de ces évolutions n'est implémentée** dans la livraison actuelle, sauf mention contraire explicite.

---

## 1. Point de départ : publication après commit, sans atomicité

Le flux actuel de `ProductsAPI` est :

```text
1. _context.Products.Add(product)
2. await _context.SaveChangesAsync()      // COMMIT SQLite
3. await _publishEndpoint.Publish(...)    // RabbitMQ
4. return 201 Created
```

Entre les étapes 2 et 3, un crash du processus, une coupure réseau ou un broker indisponible laisse un **produit persisté sans événement publié**. L'API renvoie alors `503` et journalise l'incident, mais aucun mécanisme ne rejoue automatiquement la publication.

### Scénario de panne

```text
SaveChangesAsync()  ── réussite ──►  Products.db contient le produit
        │
        X  crash / broker down
        │
Publish()           ── jamais exécuté ──►  Orders.db ne reçoit rien
```

Conséquence : `Products.db` et la projection `Orders.db` divergent jusqu'à une intervention manuelle.

### Risque associé côté client HTTP

Si le client reçoit `503` et répète son `POST`, un **deuxième produit** est créé (identifiant différent), car il n'existe aucune clé d'idempotence HTTP. Le `503` signifie « produit enregistré mais événement non publié », pas « rien n'a été fait ». Ce comportement est documenté dans `docs/AUTO_PASS.md` et `reports/TEST_COVERAGE_AUDIT.md`.

---

## 2. Outbox transactionnel (recommandation principale)

### Principe

Écrire le produit **et** l'événement à publier dans la **même transaction SQLite** :

```sql
BEGIN;
INSERT INTO Products (...) VALUES (...);
INSERT INTO OutboxMessages (Id, Type, Payload, OccurredAt, PublishedAt)
VALUES (..., 'ProductCreated', '{...}', ..., NULL);
COMMIT;
```

Un processus séparé (worker hébergé) lit les lignes `PublishedAt IS NULL`, publie vers RabbitMQ, puis marque la ligne comme publiée.

### Avantages

- atomicité entre persistance et intention de publier ;
- reprise après crash ;
- pas de perte silencieuse d'événement.

### Coûts et risques

| Élément | Impact |
|---|---|
| Migration EF | nouvelle table `OutboxMessages` (non destructive) |
| Worker | `BackgroundService` + `IBus` |
| Idempotence | le consumer doit rester idempotent (déjà le cas) |
| Tests | nouveaux tests de reprise et de double publication |
| Complexité | modérée à élevée |

### Décision pour ce TP

**Non implémenté.** Le pattern Outbox n'est pas exigé par le support, et son ajout aurait imposé une migration et un worker supplémentaires, hors du périmètre pédagogique. Il est recommandé comme première évolution avant toute mise en production.

---

## 3. Publication différée et retry

### 3.1 Retry de publication immédiat

Envelopper `Publish` dans une politique de retry bornée (par exemple 3 tentatives avec backoff exponentiel) absorbe les incidents transitoires du broker.

- **Avantage :** simple, sans migration.
- **Limite :** ne protège pas d'un crash du processus entre le commit et la publication ; ne remplace pas l'Outbox.

### 3.2 Retry côté consumer

MassTransit peut configurer `UseMessageRetry` sur l'endpoint `event-listener` :

```csharp
endpoint.UseMessageRetry(retry => retry.Exponential(
    retryLimit: 5,
    minInterval: TimeSpan.FromMilliseconds(200),
    maxInterval: TimeSpan.FromSeconds(10),
    intervalDelta: TimeSpan.FromSeconds(2)));
```

- **Avantage :** absorbe les verrous SQLite transitoires (`database is locked`) et les erreurs réseau.
- **Limite :** un message métier invalide ne doit pas être réessayé indéfiniment ; il doit finir en file d'erreur.
- **État actuel :** aucune politique de retry explicite n'est configurée. Les erreurs de consommation font échouer le message, qui part dans la file d'erreur MassTransit.

---

## 4. Dead-letter queue (DLQ)

MassTransit crée une file `<endpoint>_error` lorsque la consommation échoue définitivement. Le consumer actuel lève `InvalidOperationException` pour les messages invalides (`Id <= 0`, nom vide, nom > 100 caractères) : ces messages sont donc dirigés vers `event-listener_error`.

### Manque actuel

- aucun monitoring de `event-listener_error` ;
- aucune procédure de reprise ou de purge documentée ;
- aucun test ne vérifie le contenu de cette file.

### Recommandation

1. Surveiller la profondeur de `event-listener_error`.
2. Ajouter une alerte si elle augmente.
3. Documenter une procédure de requeue après correction.
4. Ne jamais purger automatiquement sans analyse.

---

## 5. Idempotence

### 5.1 Déjà en place

- clé primaire métier `Products.Id` dans `Orders.db` ;
- upsert : recherche par `Id`, insertion ou mise à jour ;
- gestion de `DbUpdateException` en cas de course entre deux livraisons ;
- test réel : deux publications, une seule ligne.

### 5.2 Idempotence HTTP (non implémentée)

Pour éviter qu'un client qui reçoit `503` crée un doublon en réessayant :

| Option | Description | Coût |
|---|---|---|
| En-tête `Idempotency-Key` | table `IdempotencyKeys(Key, ProductId, CreatedAt)` + contrainte unique | migration + tests |
| Clé métier fournie par le client | `POST` avec un identifiant client unique | change le contrat API |
| Réponse `201` malgré l'échec de publication | **rejetée** : masquerait un échec asynchrone | inacceptable |

**Décision :** aucune de ces options n'est implémentée. La limite est documentée ; le comportement actuel (`503` explicite) est préféré à un faux succès.

---

## 6. Observabilité

### État actuel

- journaux ASP.NET Core et MassTransit dans `logs/` ;
- journalisation des échecs de sauvegarde et de publication ;
- aucun identifiant de corrélation entre `Products.db`, le message et `Orders.db`.

### Recommandations

1. **Corrélation** : propager `ProductId` et un `CorrelationId` dans les en-têtes MassTransit.
2. **Métriques** : compteurs de publications réussies/échouées, profondeur de `event-listener`, latence de projection.
3. **Health checks** : `/health` pour SQLite et RabbitMQ.
4. **Traces** : OpenTelemetry (ASP.NET Core + MassTransit + SQLite).
5. **Logs structurés** : Serilog avec champs `ProductId`, `MessageId`, `CorrelationId`.
6. **Tableau de bord** : suivi de l'écart entre `Products.db` et `Orders.db`.

---

## 7. Événements de mise à jour et de suppression

Actuellement, seuls `ProductCreated` est publié. Un `PUT` ou `DELETE` sur `ProductsAPI` ne met pas à jour la projection `Orders.db`.

### Recommandation

- publier `ProductUpdated { Id, Name }` après un `PUT` réussi ;
- publier `ProductDeleted { Id }` après un `DELETE` réussi ;
- consumer idempotent : mise à jour ou suppression par `Id` ;
- attention aux commandes existantes qui référencent un produit supprimé : définir une règle métier (refus, conservation de l'historique, etc.).

Cette évolution est hors périmètre du TP.

---

## 8. Feuille de route suggérée

| Priorité | Évolution | Effort | Risque | Prérequis |
|---:|---|---|---|---|
| 1 | Retry consumer + alerte DLQ | faible | faible | aucun |
| 2 | Health checks SQLite/RabbitMQ | faible | faible | aucun |
| 3 | Outbox transactionnel | moyen | moyen | migration non destructive |
| 4 | `ProductUpdated` / `ProductDeleted` | moyen | moyen | règles métier |
| 5 | Idempotency-Key HTTP | moyen | moyen | migration |
| 6 | OpenTelemetry + Serilog | moyen | faible | aucun |
| 7 | Authentification JWT / TLS | élevé | élevé | infrastructure |

---

## 9. Ce que ce document ne prétend pas

- Aucune de ces évolutions n'est présentée comme réalisée.
- Le TP reste conforme à son périmètre pédagogique sans elles.
- Les limites sont assumées et vérifiables dans `reports/AUTO_PASS_REPORT.md` et `RAPPORT_FINAL_CONFORMITE.md`.
