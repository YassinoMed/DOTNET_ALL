# AUDIT-00 — ProductManagement / TP7 API CLEAN ARCHITECTURE

| Champ | Valeur |
|---|---|
| Date | 18 novembre 2026 |
| Périmètre | LOOP 00 uniquement — audit, analyse PDF, environnement, choix de base, plan |
| Dossier de travail | `/Users/mohamedyassine/Desktop/PFE/dsir/2emeMaster/DotNET/ProductManagment` |
| Référence fonctionnelle | `TP7 API CLEAN ARCHITECTURE.pdf` — 30 pages, lu intégralement |
| Statut LOOP 00 | **PASS_WITH_RESERVATIONS** |
| Projets créés pendant ce LOOP | **Aucun** (conforme à la consigne) |
| Prochaine action | Attendre la validation des décisions D1 à D4 avant LOOP 01 |

---

## 1. Résumé exécutif

L'audit confirme qu'un début de solution existe déjà dans le dossier, mais il est **incomplet et non compilable** :

- 3 projets seulement (`DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`), tous en `net7.0` ;
- aucun projet `ProductsAPI` ;
- aucune référence de projet et aucun package NuGet ;
- `IProductRepository.cs` contient en réalité une interface `IProductService` ;
- `ProductService.cs` déclare ses méthodes **en dehors de la classe** (erreur de compilation) ;
- des restes de projet F# (`DomainLayer.fsproj`, `MyPage.fs`, `AssemblyInfo.fs`) et des fichiers `Class1.cs` inutiles ;
- aucune migration, aucun `appsettings.json`, aucun `Program.cs`, aucun contrôleur.

L'environnement macOS est utilisable pour .NET 8, mais avec trois contraintes majeures :

1. le `dotnet` par défaut est **7.0.317** ; le SDK **8.0.425** existe dans `~/.dotnet` mais n'est pas dans le `PATH` ;
2. `dotnet-ef` global est en **7.0.20**, incompatible avec un projet EF Core 8 ;
3. **SQL Server LocalDB n'existe pas sur macOS** et Docker est explicitement interdit. Aucune instance SQL Server locale ou distante n'est disponible.

**Décision proposée :** cibler `net8.0`, utiliser **SQLite en local** pour exécuter réellement la migration et les tests, tout en conservant une configuration `UseSqlServer` documentée pour une exécution ultérieure sous Windows/LocalDB ou sur un SQL Server distant. Cette adaptation sera signalée comme réserve, sans prétendre à une conformité stricte au PDF sur ce point.

> **Conséquence :** le statut final du projet ne pourra pas être un « PASS strict PDF » si SQL Server LocalDB n'est pas utilisé. Il sera au mieux **PASS_WITH_RESERVATIONS** avec la réserve « base SQLite locale au lieu de LocalDB ».

---

## 2. Méthode et limites du LOOP 00

### 2.1 Ce qui a été fait

- Lecture des 30 pages du PDF (`pdftotext -layout`) ;
- lecture visuelle des pages 27, 28 et 29 (captures `appsettings.json`, `Program.cs`, références de projets) ;
- audit du système, des SDK, runtimes, outils EF, IDE, NuGet, ports et espace disque ;
- audit de tous les fichiers existants (`.cs`, `.csproj`, `.sln`, `.fs`, `.fsproj`) ;
- vérification de l'absence de SQL Server local et de Docker ;
- vérification de la disponibilité de SQLite ;
- consultation des versions NuGet disponibles ;
- rédaction du présent rapport.

### 2.2 Ce qui n'a pas été fait (volontairement)

- aucun projet, fichier `.cs`, `.csproj`, `.sln` ou package n'a été créé/modifié ;
- aucun `dotnet restore`, `dotnet build`, `dotnet ef` n'a été lancé sur la solution ;
- aucune base de données n'a été créée ;
- Docker n'a pas été utilisé ;
- les fichiers existants n'ont pas été supprimés ni déplacés.

---

## 3. Environnement détecté

### 3.1 Système

| Élément | Valeur | Preuve |
|---|---|---|
| OS | macOS 27.3 (build 26C5047e) | `sw_vers` |
| Architecture | `arm64` | `uname -m` |
| Processeur | Apple M2 | `sysctl -n machdep.cpu.brand_string` |
| Espace disque | 228 Gi, **17 Gi disponibles**, 92 % utilisés | `df -h .` |
| Dossier courant | `.../DotNET/ProductManagment` | `pwd` |

**Risque :** 17 Gi restants suffisent pour ce TP, mais il faudra surveiller les caches NuGet et nettoyer les `bin/obj` si nécessaire. Ne pas lancer Docker (interdit et gourmand en disque).

### 3.2 SDK et runtimes .NET

| Installation | SDK | Runtimes |
|---|---|---|
| `/usr/local/share/dotnet` (par défaut) | 6.0.425, 7.0.317 | ASP.NET Core 6.0.33, 7.0.20 ; .NET 6.0.33, 7.0.20 |
| `~/.dotnet` | **8.0.425**, 10.0.401 | ASP.NET Core **8.0.31**, 10.0.12 ; .NET **8.0.31**, 10.0.12 |

Commandes exécutées :

```bash
dotnet --info
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" --list-sdks
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" --list-runtimes
```

Constat :

- `dotnet --version` retourne **7.0.317** avec le `PATH` actuel ;
- `DOTNET_ROOT="$HOME/.dotnet" dotnet --version` retourne **10.0.401** (SDK le plus récent), pas 8.0.425 ;
- il n'y a **pas de `global.json`**.

**Correction prévue (LOOP 01) :**

1. créer un `global.json` avec `"version": "8.0.425"` et `"rollForward": "latestFeature"` ;
2. utiliser un script d'environnement ou une configuration de terminal VS Code :
   ```bash
   export DOTNET_ROOT="$HOME/.dotnet"
   export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
   ```
3. vérifier que `dotnet --version` retourne bien `8.0.425` avant toute création de projet.

> Le SDK 10.0.401 ne doit pas être utilisé pour ce TP : le projet cible `net8.0` et les packages seront épinglés en 8.0.x.

### 3.3 Outils Entity Framework

| Élément | Valeur | Preuve |
|---|---|---|
| `dotnet-ef` global | **7.0.20** | `dotnet-ef --version` |
| Emplacement | `~/.dotnet/tools/dotnet-ef` | `ls ~/.dotnet/tools` |
| Package `dotnet-ef` 8.x disponible | 8.0.31, 8.0.32, 8.0.33, 8.0.34 | API NuGet |
| Runtime .NET 8 disponible | 8.0.31 | `dotnet --list-runtimes` |

**Problème :** l'outil EF 7 ne doit pas être utilisé pour générer/appliquer des migrations EF Core 8.

**Correction prévue (LOOP 05) :** créer un manifeste d'outils local dans le dépôt et installer `dotnet-ef` **8.0.31** (version alignée sur le runtime 8.0.31), puis l'utiliser via `dotnet ef`. Le `dotnet-ef` global 7.0.20 restera intact.

### 3.4 IDE et extensions

| Élément | Valeur | Commentaire |
|---|---|---|
| Visual Studio Code | **1.142.0** (arm64) | Installé |
| Extension C# / C# Dev Kit | **aucune détectée** | `code --list-extensions` ne retourne aucune extension C# |
| Visual Studio for Mac | **17.6.14** | Installé mais **retiré depuis le 31 août 2024** |
| Rider | absent | — |

Référence Microsoft : Visual Studio for Mac a été retiré le 31 août 2024 ; Microsoft indique que **VS Code + C# Dev Kit** est l'alternative recommandée. La version 17.6.14 installée est la dernière ; le support .NET 8 y est au mieux « rudimentaire » et non officiel. Voir :
- <https://learn.microsoft.com/en-us/previous-versions/visualstudio/mac/supported-versions-net?view=vsmac-2022>
- <https://devblogs.microsoft.com/visualstudio/visual-studio-for-mac-retirement-announcement/>

**Recommandation :** utiliser la **CLI `dotnet`** pour toutes les opérations critiques (création, build, migrations, run, tests) et **VS Code** comme éditeur. Installer C# Dev Kit est optionnel pour l'édition, mais ne doit pas être une dépendance du build.

### 3.5 NuGet

| Élément | Valeur | Preuve |
|---|---|---|
| Source | `nuget.org` | `dotnet nuget list source` |
| Connectivité | HTTP **200** | `curl https://api.nuget.org/v3/index.json` |
| Dernière version stable EF Core | 11.0.0 | API NuGet |
| Dernière version 8.0.x EF Core | **8.0.34** | API NuGet |
| Version alignée sur le runtime installé | **8.0.31** | `Microsoft.NETCore.App 8.0.31` |
| Dernière version 6.x Swashbuckle | 6.9.0 | API NuGet |

**Recommandation :** épingler les packages EF Core en **8.0.31** pour rester aligné avec le runtime 8.0.31 installé. Utiliser une version 8.0.34 imposerait de disposer du runtime 8.0.34 ou de gérer un roll-forward ; ce n'est pas nécessaire pour le TP.

### 3.6 SQL Server / LocalDB

| Vérification | Résultat |
|---|---|
| `sqlcmd` | absent |
| `mssql-cli` | absent |
| Installation locale `/opt/mssql*` | aucune |
| Port `localhost:1433` | **connection refused** |
| Docker | installé mais **interdit par la consigne** ; daemon non utilisé |
| Instance SQL Server distante | aucune information, aucun identifiant, aucune variable de connexion |
| Variables d'environnement `SQL*` / `ConnectionStrings*` | aucune |

Référence Microsoft : LocalDB est un composant SQL Server Express pour Windows, installé via `SqlLocalDB.msi`, avec des fichiers sous `%LOCALAPPDATA%` et l'utilitaire `SqlLocalDB.exe`. Il n'existe pas d'implémentation LocalDB native pour macOS. Voir :
- <https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb>

**Conclusion :** sans Docker, sans VM Windows et sans instance distante, **SQL Server LocalDB est techniquement indisponible sur cette machine**. Le TP ne peut pas être exécuté de bout en bout avec LocalDB ici.

### 3.7 SQLite

| Élément | Valeur |
|---|---|
| Client `sqlite3` | **3.54.0** (2026-04-09) |
| Provider EF Core | `Microsoft.EntityFrameworkCore.Sqlite` 8.0.31 disponible |
| Support macOS arm64 | oui, natif .NET |
| Fichier de base prévu | `ProductManagement.db` (ignoré par Git) |

**Conclusion :** SQLite est la seule base relationnelle locale réellement exécutable dans cet environnement sans Docker et sans SQL Server distant. Elle permet de valider la migration, le seed et le CRUD. Elle ne remplace pas LocalDB d'un point de vue strictement conforme au PDF.

### 3.8 Ports et réseau

| Port | État | Usage |
|---|---|---|
| 1433 | libre (aucun listener) | SQL Server absent |
| 5000 | occupé par `ControlCenter` (AirPlay) | à éviter |
| 7000 | occupé par `ControlCenter` (AirPlay) | à éviter |
| 5080 | **libre** | port proposé pour l'API |
| 5081 | **libre** | secours |

**Recommandation :** configurer `applicationUrl` sur `http://localhost:5080` et Swagger sur `http://localhost:5080/swagger`.

### 3.9 État du dépôt Git

```text
fatal: votre branche actuelle 'main' ne contient encore aucun commit
A  .gitignore
AM ProductManagment.sln
?? ApplicationLayer/
?? DomainLayer/
?? InfrastructureLayer/
?? TP7 API CLEAN ARCHITECTURE.pdf
```

- aucun commit de référence ;
- les fichiers du début de solution sont non suivis ou partiellement indexés ;
- le PDF est non suivi.

**Recommandation :** avant LOOP 01, créer un **commit de sauvegarde** de l'état actuel (ou une archive `_baseline/`) pour ne pas perdre le travail existant. Ne rien supprimer sans validation. Les fichiers F# inutiles pourront être déplacés dans `_archive/` plutôt que supprimés.

---

## 4. Audit de l'existant

### 4.1 Projets et fichiers

| Élément | État | Verdict |
|---|---|---|
| `DomainLayer/DomainLayer.csproj` | `net7.0`, aucun package, aucune référence | À retargeter `net8.0` |
| `ApplicationLayer/ApplicationLayer.csproj` | `net7.0`, aucun package, aucune référence | À retargeter + référence Domain |
| `InfrastructureLayer/InfrastructureLayer.csproj` | `net7.0`, vide | À compléter + packages + références |
| `ProductsAPI` | absent | À créer |
| `ProductManagment.sln` | 3 projets, pas d'API | À reconstruire avec 4 projets |
| `DomainLayer/DomainLayer.fsproj` | ancien projet F# .NET Framework 4.7.2, non référencé par la solution | À archiver/supprimer après validation |
| `DomainLayer/MyPage.fs`, `AssemblyInfo.fs` | restes F# | À archiver |
| `Class1.cs` (3 projets) | fichiers vides inutiles | À supprimer |
| `ProductsAPI.Tests` | absent | À décider (D4) |

### 4.2 Code existant — défauts bloquants

| Fichier | Ligne(s) | Problème | Correction prévue |
|---|---|---|---|
| `DomainLayer/Entities/Products.cs` | 1-29 | Le fichier s'appelle `Products.cs` au lieu de `Product.cs` | Renommer en `Product.cs` |
| `DomainLayer/Entities/Products.cs` | 20-26 | `ValidateBusinessRules()` ne valide pas le nom | Ajouter la validation du nom (non vide, 2 à 100 caractères) |
| `ApplicationLayer/Interfaces/Repositories/IProductRepository.cs` | 6-13 | Déclare `IProductService` au lieu de `IProductRepository` | Réécrire l'interface repository |
| `ApplicationLayer/Interfaces/Repositories/IProductRepository.cs` | 2 | Mauvais `using` (`DTOs` au lieu de `DomainLayer.Entities`) | Corriger |
| `ApplicationLayer/Services/ProductService.cs` | 6-24 | La classe est fermée à la ligne 24 | Garder toutes les méthodes dans la classe |
| `ApplicationLayer/Services/ProductService.cs` | 25-88 | Méthodes déclarées au niveau namespace → **CS0116** | Réintégrer dans la classe |
| `ApplicationLayer/Services/ProductService.cs` | 1-2 | `using` manquants : `IProductRepository`, `IProductService`, `DomainLayer.Entities` | Ajouter |
| `ApplicationLayer/Services/ProductService.cs` | 6 | `IProductService` non importé / non implémenté correctement | Implémenter l'interface |
| `ApplicationLayer/Services/ProductService.cs` | 72 | `throw new Exception(...)` → HTTP 500 | Utiliser `KeyNotFoundException` → 404 |
| `ApplicationLayer/Interfaces/Services/IProductService.cs` | 1-14 | Correct, mais `using System;` inutile | Nettoyer |
| `ApplicationLayer/DTOs/*.cs` | — | Globalement conformes au PDF | Conserver, nettoyer les usings |
| Tous les `.csproj` | — | Aucun `ProjectReference` | Ajouter les 4 références |
| Tous les `.csproj` | — | Aucun package EF Core | Ajouter |
| `ProductManagment.sln` | 6-11 | 3 projets, pas d'API, format ancien | Reconstruire |
| `DomainLayer.fsproj` | 1-47 | Projet F# .NET Framework 4.7.2 non utilisé | Archiver |
| `Class1.cs` | — | Fichiers vides | Supprimer |

### 4.3 Ce qui est déjà correct

- L'entité `Product` a les bonnes propriétés et les attributs `[Required]`, `[StringLength]`, `[Column]`, `[Range]`.
- Les trois DTO sont globalement conformes au PDF.
- `IProductService` a les bonnes signatures.
- Le mapping manuel `Product → ProductDTO` est présent (mais mal placé).
- Les règles métier de base (prix > 0, stock ≥ 0) sont présentes.
- Le seed data du PDF n'est pas encore implémenté, mais les valeurs sont connues.
- Le `.gitignore` existe ; il faudra ajouter `*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`.

---

## 5. Matrice de conformité au PDF (30 pages)

| Pages | Contenu PDF | État actuel | Action LOOP |
|---|---|---|---|
| 1–9 | Théorie Clean Architecture, 4 couches | Compris | Aucune |
| 10–11 | `DomainLayer`, dossier `Entities`, `Product.cs` | Partiel : `Products.cs` existe, projet `net7.0` | LOOP 01 : renommer, retargeter, tester |
| 12–14 | DTO `ProductDTO`, `CreateProductDTO`, `UpdateProductDTO` | Présents et proches du PDF | LOOP 02 : nettoyer, valider |
| 15 | `IProductRepository` | Fichier erroné (contient `IProductService`) | LOOP 02 : réécrire |
| 16 | `IProductService` | Présent et correct | LOOP 02 : nettoyer |
| 17–19 | `ProductService` complet | Cassé (méthodes hors classe, usings manquants, exception générique) | LOOP 02 : réécrire |
| 20 | Projet `InfrastructureLayer`, packages EF Core | Projet vide, `net7.0`, aucun package | LOOP 03 : packages + refs |
| 21 | `ApplicationDbContext` + seed 3 produits | Absent | LOOP 03 : créer |
| 22–23 | `ProductRepository` CRUD | Absent | LOOP 03 : créer |
| 24 | Projet `ProductsAPI`, contrôleur vide | Absent | LOOP 04 : créer |
| 25–26 | `ProductController` GET/POST/PUT/DELETE | Absent | LOOP 04 : créer et corriger |
| 27 | `appsettings.json` + LocalDB | Absent | LOOP 04 : créer, adapter SQLite |
| 28 | `Program.cs` + DI | Absent | LOOP 04 : compléter (Swagger, Build, Run) |
| 29 | Références entre couches (capture) | Aucune référence | LOOP 01/03/04 : configurer |
| 30 | Migration `dbcreate` + `update-database` | Absent | LOOP 05 : migration SQLite + update |

**Note page 29 :** la capture montre une solution nommée `CleanArchitectureProduct` avec des projets `CleanArch.API`, `CleanArch.Application`, `CleanArch.Infrastructure`, `CleanArch.Domain`. C'est **incohérent** avec le texte du PDF (`ProductManagment`, `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer`, `ProductsAPI`). La capture sera ignorée pour le nommage ; seules les références et la présence d'un dossier `Migrations` sont retenues.

---

## 6. Erreurs et incohérences du PDF à corriger

### 6.1 Erreurs bloquantes

| Page | Problème | Correction |
|---|---|---|
| 19 | `ProductService` : méthodes `GetAllProductsAsync`, `GetByIdAsync`, `AddProductAsync`, `UpdateProductAsync`, `DeleteProductAsync` sont **hors de la classe** ; accolades mal placées | Réécrire la classe complète avec toutes les méthodes à l'intérieur |
| 19 | `using` manquants pour `IProductRepository`, `IProductService`, `Product` | Ajouter les trois usings |
| 19 | `throw new Exception("Product not found.")` | Remplacer par `KeyNotFoundException` + middleware 404 |
| 25 | `[HttpPut("{id}")]` mais signature `Put(UpdateProductDTO productDto)` sans `id` | Ajouter `int id` et vérifier `id == dto.Id` |
| 25–26 | `DELETE` retourne toujours `204`, même si le produit n'existe pas | Vérifier l'existence → `404` si absent |
| 24 | Contrôleur sans `using` ni `namespace` | Compléter |
| 28 | `Program.cs` réduit aux lignes DI ; pas de `builder`, `AddControllers`, Swagger, `Build`, `Run` | Compléter le fichier complet |
| 27 | Chaîne LocalDB sans `TrustServerCertificate=True` | Ajouter pour éviter l'erreur de certificat EF Core 8 |
| 20 | Packages `SqlServer` + `Tools` seulement | Ajouter `Design` (nécessaire pour les migrations CLI) |
| 30 | Commandes PMC `add-migration` / `update-database` | Utiliser les équivalents CLI `dotnet ef` (pas de PMC dans VS Code) |

### 6.2 Incohérences REST / qualité

| Page | Problème | Correction |
|---|---|---|
| 25 | `POST` retourne `Ok(product)` (200) | Retourner `CreatedAtAction` (201) |
| 25 | `GET` retourne `IEnumerable<ProductDTO>` brut | Retourner `ActionResult<IEnumerable<ProductDTO>>` |
| 19 | `AddProductAsync` retourne `ProductDTO?` alors que le repository retourne toujours un produit | Simplifier ou garantir une valeur non nulle |
| 19 | `DeleteProductAsync` ne vérifie pas l'existence | Vérifier et lever `KeyNotFoundException` |
| 25 | Pas de vérification de cohérence route/DTO pour `PUT` | Ajouter `id != dto.Id → 400` |
| 25 | Pas de `ProducesResponseType` | Ajouter pour Swagger (optionnel mais propre) |
| 24–26 | Pas de gestion globale des exceptions | Ajouter un middleware `ProblemDetails` |
| 21 | `HasData` sans configuration explicite de précision | Ajouter `HasPrecision(18,2)` / `HasMaxLength` |
| 22 | `Update` sur une entité lue en `AsNoTracking` | OK si `Update` est utilisé, mais à vérifier par test |
| 27 | LocalDB uniquement | Adapter macOS → SQLite (réserve) |
| — | Pas de projet de tests | Ajouter `ProductsAPI.Tests` (D4) |
| — | Pas de README / commandes de lancement | Ajouter `README.md` et `FINAL-REPORT.md` |

### 6.3 Incohérences de nommage

| Élément | PDF | Demande utilisateur | Décision proposée |
|---|---|---|---|
| Solution | `ProductManagment` (faute) | `ProductManagement` | D3 : renommer le `.sln` en `ProductManagement.sln` (dossier inchangé) |
| Entité | `Product.cs` | `Product.cs` | Renommer `Products.cs` |
| Projet API | `ProductsAPI` | `ProductsAPI` | Conserver |
| Projets de couches | `DomainLayer`, `ApplicationLayer`, `InfrastructureLayer` | idem | Conserver |
| Capture page 29 | `CleanArch.*` | — | Ignorer |

---

## 7. Décisions techniques proposées (à valider)

### D1 — Cible .NET 8 et SDK épinglé

- Cible : `net8.0` pour les 4 projets.
- `global.json` :
  ```json
  {
    "sdk": {
      "version": "8.0.425",
      "rollForward": "latestFeature"
    }
  }
  ```
- Utilisation de `DOTNET_ROOT="$HOME/.dotnet"` et `PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"`.
- **Justification :** SDK 8.0.425 et runtime 8.0.31 déjà présents ; évite d'installer quoi que ce soit et évite le SDK 10 par défaut.

**À valider :** OK pour épingler 8.0.425 ?

### D2 — SQLite local pour l'exécution, SQL Server documenté

- Provider local : **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite` 8.0.31).
- Chaîne locale : `Data Source=ProductManagement.db`.
- Le code conserve une branche `UseSqlServer` et une chaîne LocalDB documentée pour une exécution sous Windows.
- La migration sera générée pour **SQLite** et réellement appliquée avec `dotnet ef database update`.
- Seed : 3 produits du PDF.
- **Réserve explicite :** SQLite ≠ LocalDB. La conformité stricte au PDF n'est pas atteinte sur ce point. Aucune affirmation contraire ne sera faite.

**Alternative si vous fournissez un SQL Server distant :** renseigner `DatabaseProvider=SqlServer` et une chaîne de connexion valide ; dans ce cas, la migration devra être régénérée pour SQL Server (les migrations sont spécifiques au provider).

**À valider :** OK pour SQLite local + réserve documentée ?

### D3 — Nom de la solution

Deux options :

- **Option recommandée :** renommer le fichier `ProductManagment.sln` en `ProductManagement.sln` (orthographe correcte, conforme au prompt maître), en conservant le dossier existant `ProductManagment/` et les noms de projets du PDF.
- **Option stricte PDF :** conserver `ProductManagment.sln`.

**À valider :** Option recommandée ou option stricte PDF ?

### D4 — Projet de tests

Le prompt maître demande **exactement quatre projets** mais le LOOP 06 exige `dotnet test`. Ces deux exigences sont contradictoires.

- **Option recommandée :** conserver **4 projets de production** + ajouter **1 projet de test** `ProductsAPI.Tests` (xUnit + `WebApplicationFactory` + SQLite temporaire). Le projet de test n'est pas une couche applicative ; il permet d'exécuter réellement `dotnet test`.
- **Option stricte 4 projets :** pas de projet de test ; validation uniquement par script `curl`/Swagger, et `dotnet test` marqué N/A.

**À valider :** 4 projets + 1 projet de test, ou strictement 4 projets ?

### D5 — Versions de packages

| Package | Version proposée | Projet |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 8.0.31 | InfrastructureLayer |
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.0.31 | InfrastructureLayer (branche documentée) |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.31 | InfrastructureLayer |
| `Microsoft.EntityFrameworkCore.Tools` | 8.0.31 | InfrastructureLayer |
| `Swashbuckle.AspNetCore` | 6.9.0 | ProductsAPI |
| `Microsoft.AspNetCore.Mvc.Testing` | 8.0.31 | ProductsAPI.Tests (si D4 retenu) |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | ProductsAPI.Tests |
| `xunit` | 2.9.3 | ProductsAPI.Tests |
| `xunit.runner.visualstudio` | 2.8.2 | ProductsAPI.Tests |
| `coverlet.collector` | 6.0.2 | ProductsAPI.Tests |

**Justification :** alignement sur le runtime 8.0.31 ; évite les versions 9/10/11 qui ne ciblent pas `net8.0` ou imposeraient des runtimes absents.

### D6 — Comportement HTTP cible

| Endpoint | Succès | Erreurs |
|---|---|---|
| `GET /api/Product` | 200 + 3 produits seedés | — |
| `GET /api/Product/{id}` | 200 | 404 si absent |
| `POST /api/Product` | 201 + `Location` | 400 si modèle invalide |
| `PUT /api/Product/{id}` | 204 | 400 si `id != dto.Id` ou invalide ; 404 si absent |
| `DELETE /api/Product/{id}` | 204 | 404 si absent |
| Modèle invalide | — | 400 `ProblemDetails` |
| Exception métier | — | 400 `ProblemDetails` |
| Ressource absente | — | 404 `ProblemDetails` |

### D7 — Port et Swagger

- API : `http://localhost:5080`.
- Swagger UI : `http://localhost:5080/swagger`.
- Swagger activé en Development et pour la démonstration du TP.
- Pas de HTTPS imposé en local (évite les problèmes de certificat).

---

## 8. Architecture cible

### 8.1 Dépendances

```text
ProductsAPI ────────► ApplicationLayer
     │                      │
     └──────────────────────┼──────► DomainLayer
                            │
InfrastructureLayer ────────┴──────► ApplicationLayer
     │
     └──────────────────────────────► DomainLayer
```

- `DomainLayer` : **aucune dépendance** projet, aucun EF Core, aucun ASP.NET Core.
- `ApplicationLayer` → `DomainLayer`.
- `InfrastructureLayer` → `ApplicationLayer` + `DomainLayer`.
- `ProductsAPI` → `ApplicationLayer` + `InfrastructureLayer`.
- `ProductsAPI.Tests` → `ProductsAPI` (si D4).

### 8.2 Structure de fichiers prévue

```text
ProductManagment/
├── ProductManagement.sln                 (D3)
├── global.json                           (SDK 8.0.425)
├── AUDIT-00.md
├── README.md
├── FINAL-REPORT.md                       (à produire en LOOP 06)
├── DomainLayer/
│   ├── DomainLayer.csproj                (net8.0)
│   └── Entities/
│       └── Product.cs
├── ApplicationLayer/
│   ├── ApplicationLayer.csproj           (net8.0, ref DomainLayer)
│   ├── DTOs/
│   │   ├── ProductDTO.cs
│   │   ├── CreateProductDTO.cs
│   │   └── UpdateProductDTO.cs
│   ├── Interfaces/
│   │   ├── Repositories/IProductRepository.cs
│   │   └── Services/IProductService.cs
│   └── Services/
│       └── ProductService.cs
├── InfrastructureLayer/
│   ├── InfrastructureLayer.csproj        (net8.0, refs Application + Domain)
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   ├── DesignTimeDbContextFactory.cs (optionnel, si utile aux migrations)
│   │   └── Migrations/                   (généré en LOOP 05)
│   └── Repositories/
│       └── ProductRepository.cs
├── ProductsAPI/
│   ├── ProductsAPI.csproj                (net8.0, refs Application + Infrastructure)
│   ├── Controllers/
│   │   └── ProductController.cs
│   ├── Middleware/
│   │   └── ExceptionHandlingMiddleware.cs
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Program.cs
└── ProductsAPI.Tests/                    (D4)
    ├── ProductsAPI.Tests.csproj
    ├── ApiFactory.cs
    └── ProductEndpointsTests.cs
```

### 8.3 Fichiers à archiver/supprimer après validation

- `DomainLayer/DomainLayer.fsproj`
- `DomainLayer/MyPage.fs`
- `DomainLayer/AssemblyInfo.fs`
- `DomainLayer/Class1.cs`
- `ApplicationLayer/Class1.cs`
- `InfrastructureLayer/Class1.cs`
- Les dossiers `bin/` et `obj/` générés en `net7.0` (nettoyage avant retarget).

> Ces éléments ne sont pas nécessaires au TP. Ils seront déplacés dans `_archive/` (ou supprimés si vous confirmez) pour éviter toute confusion dans Visual Studio/VS Code.

---

## 9. Plan d'exécution des LOOP suivants

### LOOP 01 — Solution et DomainLayer

1. Créer un commit/une archive de sauvegarde de l'existant.
2. Créer `global.json` (8.0.425).
3. Reconstruire la solution avec les 4 projets `net8.0`.
4. Configurer les références de projets.
5. Archiver les restes F# et `Class1.cs`.
6. Renommer `Products.cs` → `Product.cs`, compléter la validation du nom.
7. Ajouter le projet de tests (D4) et les tests de `ValidateBusinessRules`.
8. `dotnet restore`, `dotnet build`, `dotnet test` (tests Domain).
9. Produire `LOOP-01.md`.

**Gate :** compilation Domain + tests règles métier OK.

### LOOP 02 — ApplicationLayer

1. Réécrire `IProductRepository` (bonne interface, bon namespace, `using DomainLayer.Entities`).
2. Conserver/compléter `IProductService`.
3. Réécrire `ProductService` complet (méthodes dans la classe, usings, mapping, validation, `KeyNotFoundException`).
4. Ajouter les tests Application (repository fake/mock).
5. `dotnet build`, `dotnet test`.
6. Produire `LOOP-02.md`.

**Gate :** Application compile, tests service OK.

### LOOP 03 — InfrastructureLayer

1. Ajouter les packages EF Core 8.0.31 + références.
2. Créer `ApplicationDbContext` avec `DbSet<Product>` et seed des 3 produits.
3. Configurer `HasPrecision(18,2)`, `HasMaxLength`, `HasIndex`.
4. Créer `ProductRepository` (`AsNoTracking` en lecture, `SaveChangesAsync` en écriture).
5. Créer la factory design-time si nécessaire.
6. `dotnet build`.
7. Produire `LOOP-03.md`.

**Gate :** Infrastructure compile, seed défini.

### LOOP 04 — ProductsAPI

1. Créer `ProductsAPI` (`net8.0`, refs Application + Infrastructure).
2. Ajouter `Swashbuckle.AspNetCore` 6.9.0.
3. Écrire `appsettings.json` (SQLite local + chaîne SQL Server documentée) et `appsettings.Development.json`.
4. Écrire `Program.cs` complet : DI, DbContext provider, repository, service, contrôleurs, Swagger, middleware, `app.Run()`.
5. Écrire `ProductController` avec les 5 endpoints et les codes HTTP cible.
6. Écrire le middleware d'exceptions `ProblemDetails`.
7. Configurer `launchSettings.json` sur `http://localhost:5080`.
8. `dotnet build`.
9. Produire `LOOP-04.md`.

**Gate :** API compile, Swagger configuré.

### LOOP 05 — Base de données et migrations

1. Créer un manifeste d'outils local et installer `dotnet-ef` **8.0.31**.
2. Vérifier `dotnet ef --version`.
3. Générer la migration SQLite `InitialCreate` :
   ```bash
   dotnet ef migrations add InitialCreate \
     --project InfrastructureLayer \
     --startup-project ProductsAPI \
     --output-dir Data/Migrations
   ```
4. Appliquer :
   ```bash
   dotnet ef database update \
     --project InfrastructureLayer \
     --startup-project ProductsAPI
   ```
5. Vérifier avec `sqlite3` :
   ```sql
   SELECT COUNT(*) FROM Products;          -- attendu : 3
   SELECT Id, Name, Price, Stock FROM Products ORDER BY Id;
   ```
6. Vérifier les 3 produits seedés.
7. Documenter la réserve SQLite vs LocalDB.
8. Produire `LOOP-05.md`.

**Gate :** base créée, 3 produits présents, migration appliquée.

### LOOP 06 — Tests et validation finale

1. `dotnet restore`
2. `dotnet build`
3. `dotnet test` (si D4 retenu)
4. Lancer l'API sur `http://localhost:5080`.
5. Vérifier `GET /swagger/index.html` (HTTP 200).
6. Exécuter les scénarios :
   - `GET /api/Product` → 200 + 3 produits ;
   - `GET /api/Product/1` → 200 + Laptop ;
   - `GET /api/Product/99999` → 404 ;
   - `POST /api/Product` valide → 201 ;
   - `POST /api/Product` prix ≤ 0 → 400 ;
   - `PUT /api/Product/{id}` → 204 ;
   - `PUT /api/Product/99999` → 404 ;
   - `DELETE /api/Product/{id}` → 204 ;
   - `DELETE /api/Product/99999` → 404 ;
   - vérifier la persistance après redémarrage.
7. Vérifier les logs, la DI, la sérialisation JSON et les migrations.
8. Produire `FINAL-REPORT.md` avec les preuves.
9. Afficher `✅ PROJET TERMINÉ ET FONCTIONNEL` **uniquement** si toutes les preuves sont disponibles.

---

## 10. Risques et mitigations

| Risque | Probabilité | Impact | Mitigation |
|---|---|---|---|
| SQL Server LocalDB indisponible sur macOS | certaine | élevé | SQLite local + réserve documentée ; SQL Server distant si fourni |
| SDK 7 utilisé par erreur | élevée | élevé | `global.json` 8.0.425 + `DOTNET_ROOT`/`PATH` vérifiés |
| `dotnet-ef` 7 utilisé avec EF 8 | élevée | élevé | outil local 8.0.31 via manifeste |
| Code existant cassé | certaine | élevé | réécriture contrôlée en LOOP 01/02, tests |
| VS for Mac inadapté à .NET 8 | moyenne | moyen | CLI + VS Code ; VS for Mac non requis |
| Extension C# Dev Kit absente | moyenne | faible | installer l'extension ou utiliser la CLI |
| Conflit de port 5000/7000 | certaine | faible | utiliser 5080 |
| Espace disque 17 Gi | moyenne | moyen | nettoyer `bin/obj`, pas de Docker, surveiller NuGet |
| Packages 9/10/11 attirés par NuGet | moyenne | moyen | versions épinglées 8.0.31 |
| Contradiction « 4 projets » vs `dotnet test` | certaine | faible | décision D4 explicite |
| Nom de solution PDF vs prompt | certaine | faible | décision D3 explicite |
| Migration SQLite non réutilisable pour SQL Server | certaine | moyen | documenter ; régénérer une migration SQL Server si besoin |
| Perte des fichiers existants | faible | élevé | commit/archive de sauvegarde avant LOOP 01 |

---

## 11. Preuves d'audit

### 11.1 Commandes exécutées

```bash
sw_vers
uname -m
sysctl -n machdep.cpu.brand_string
df -h .
dotnet --info
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" --list-sdks
DOTNET_ROOT="$HOME/.dotnet" "$HOME/.dotnet/dotnet" --list-runtimes
dotnet-ef --version
dotnet tool list -g
dotnet nuget list source
curl -s -o /dev/null -w "%{http_code}" https://api.nuget.org/v3/index.json
code --version
code --list-extensions
defaults read "/Applications/Visual Studio.app/Contents/Info.plist" CFBundleShortVersionString
command -v sqlcmd
command -v mssql-cli
nc -vz -G 2 127.0.0.1 1433
sqlite3 --version
lsof -nP -iTCP:5080 -sTCP:LISTEN
git status --short
grep -R -n -E "ProjectReference|PackageReference|TargetFramework" --include="*.csproj" .
```

### 11.2 Résultats marquants

```text
dotnet (PATH)                 → 7.0.317
~/.dotnet SDK                 → 8.0.425, 10.0.401
~/.dotnet runtime             → 8.0.31, 10.0.12
dotnet-ef                     → 7.0.20
NuGet                         → HTTP 200
VS Code                       → 1.142.0
Extensions C#                 → aucune
Visual Studio for Mac         → 17.6.14 (retiré)
SQL Server / sqlcmd           → absent
localhost:1433                → connection refused
Docker                        → non utilisé (interdit)
SQLite                        → 3.54.0
Port 5000/7000                → occupés par ControlCenter
Port 5080                     → libre
Projets existants             → 3, net7.0, aucune référence/package
ProductsAPI                   → absent
Migrations                    → absentes
Tests                         → absents
```

### 11.3 PDF

```text
Pages        : 30
Taille       : 2 013 942 octets
Création     : 21 octobre 2026
Extraction   : pdftotext -layout
Pages vues  : 27 (appsettings), 28 (Program.cs), 29 (références)
```

---

## 12. AUTO-PASS LOOP 00

| Critère | Résultat | Preuve |
|---|---|---|
| PDF lu intégralement | PASS | 30 pages extraites + pages 27–29 inspectées |
| Matrice de conformité PDF | PASS | Section 5 |
| Erreurs et incohérences PDF identifiées | PASS | Section 6 |
| Environnement macOS / SDK / runtimes audité | PASS | Section 3 |
| `dotnet-ef` audité | PASS | 7.0.20, à remplacer localement par 8.0.31 |
| IDE / extensions audités | PASS | VS Code 1.142.0, aucune extension C# ; VS Mac retiré |
| NuGet vérifié | PASS | HTTP 200 + versions |
| SQL Server / LocalDB vérifié | PASS | absent, port 1433 refusé |
| SQLite évalué | PASS | 3.54.0 disponible |
| Existant projet audité | PASS | 3 projets net7, aucun API, erreurs bloquantes |
| Aucun projet créé pendant LOOP 00 | PASS | Section 2.2 |
| Choix de base justifié | PASS_WITH_RESERVATIONS | SQLite proposé, LocalDB indisponible |
| Décisions D1–D4 à valider | EN ATTENTE | Section 7 |

### Statut : **PASS_WITH_RESERVATIONS**

**Réserves obligatoires :**

1. **Base de données :** SQLite local au lieu de SQL Server LocalDB, car LocalDB est indisponible sur macOS et Docker est interdit. La conformité stricte au PDF n'est pas atteinte sur ce point.
2. **IDE :** Visual Studio for Mac 17.6.14 est retiré et inadapté à .NET 8 ; utiliser la CLI + VS Code. C# Dev Kit n'est pas installé.
3. **Décisions à valider :** D2 (SQLite), D3 (nom de solution), D4 (projet de tests).
4. **Outillage :** installer `dotnet-ef` 8.0.31 en local et épingler le SDK 8.0.425 avant LOOP 01.

---

## 13. Décisions demandées avant LOOP 01

Merci de valider explicitement :

1. **D1 — SDK :** `net8.0` + `global.json` 8.0.425 + `DOTNET_ROOT=~/.dotnet` ?
2. **D2 — Base :** SQLite local pour exécuter réellement migration + CRUD, avec réserve documentée et branche SQL Server conservée ?
   - ou fournir une instance SQL Server distante (hôte, base, utilisateur, mot de passe) ;
   - ou accepter que `Update-Database` ne soit pas exécuté et que le TP reste partiel.
3. **D3 — Nom :** `ProductManagement.sln` (recommandé) ou `ProductManagment.sln` (strict PDF) ?
4. **D4 — Tests :** 4 projets de production + `ProductsAPI.Tests` (recommandé) ou strictement 4 projets sans `dotnet test` ?
5. **Nettoyage :** autorisation d'archiver les restes F# et les `Class1.cs` dans `_archive/` (aucune suppression définitive) ?

Après validation, je démarrerai **uniquement le LOOP 01** (solution + DomainLayer), puis je fournirai `LOOP-01.md` avec les preuves de compilation et de tests, avant de passer au LOOP 02.

---

## 14. Conclusion

L'audit est terminé et les preuves sont disponibles. Le projet peut être mené à bien sur cette machine avec **.NET 8 + SQLite local**, à condition d'accepter la réserve sur SQL Server LocalDB. Aucun projet n'a été créé ni modifié pendant ce LOOP 00, conformément à la consigne.

**Statut LOOP 00 : PASS_WITH_RESERVATIONS — en attente de validation des décisions D1 à D5.**
