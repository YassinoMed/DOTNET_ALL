# BUILD REPORT — MicroservicesTP

**Date :** 18 novembre 2026, 13:09:53 CET
**Solution :** `MicroservicesTP.sln`
**Journal intégral :** `logs/build-final.log`

---

## 1. Environnement d'exécution

| Élément | Valeur |
|---|---|
| SDK .NET | **7.0.317** (`/usr/local/share/dotnet/sdk/7.0.317/`) |
| Runtime hôte | **7.0.20** |
| RID | **osx-arm64** |
| OS | macOS 27.3 (build `26C5047e`) |
| Architecture | Apple M2 arm64 |
| Configuration | **Debug** |
| `global.json` | `7.0.317`, `rollForward: latestPatch` |

---

## 2. Commandes exécutées

```bash
dotnet restore MicroservicesTP.sln
dotnet build MicroservicesTP.sln --no-restore -v minimal
```

### Restauration

```text
Tous les projets sont à jour pour la restauration.
RESTORE_EXIT=0
```

### Build

```text
MSBuild version 17.6.11+5eb99e6e9 for .NET
  Shared -> .../Shared/bin/Debug/net7.0/Shared.dll
  ApiGateway -> .../ApiGateway/bin/Debug/net7.0/ApiGateway.dll
  ProductsAPI -> .../ProductsAPI/bin/Debug/net7.0/ProductsAPI.dll
  OrdersAPI -> .../OrdersAPI/bin/Debug/net7.0/OrdersAPI.dll
  MicroservicesTP.Tests -> .../Tests/bin/Debug/net7.0/MicroservicesTP.Tests.dll

La génération a réussi.
    0 Avertissement(s)
    0 Erreur(s)

Temps écoulé 00:00:00.59
BUILD_EXIT=0
```

---

## 3. Résultat

| Indicateur | Valeur |
|---|---|
| Projets compilés | **5 / 5** |
| Erreurs | **0** |
| Avertissements de compilation | **0** |
| Code retour | **0** |
| Durée | **0,59 s** |
| **Verdict** | **PASS** |

---

## 4. Distinction avertissements de compilation / environnement

- **Avertissements de compilation :** aucun.
- **Avertissements d'environnement (hors build) :**
  - .NET 7 est hors support Microsoft depuis mai 2024 ;
  - RabbitMQ 4.4.0 + Erlang/OTP 29 expose un bug sur `/api/overview` ;
  - ces points n'affectent ni la compilation, ni l'exécution applicative.

Aucun `NoWarn` n'a été ajouté pour masquer un avertissement : `Directory.Build.props` ne contient qu'une suppression historique de `CS1591` (commentaires XML manquants), sans rapport avec le build courant.
