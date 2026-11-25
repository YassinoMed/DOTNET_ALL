# LOOP 00 — AUDIT INITIAL DE L'ENVIRONNEMENT

Date d'audit : 2026-11-25  
Répertoire du projet : `/Users/mohamedyassine/Desktop/PFE/dsir/2emeMaster/DotNET/TP10`  
Support pédagogique détecté : `Atelier 10 BLAZOR Part1.pdf` (52 pages, Cours réalisé par Malek Zribi)

---

## 1. OS et Architecture matérielle

- **Système d'exploitation** : macOS (`ProductVersion: 27.3`, `BuildVersion: 26C5047e`)
- **Noyau** : Darwin `27.3.0` (`RELEASE_ARM64_T8112`)
- **Architecture CPU** : Apple Silicon `arm64` (`osx-arm64`)
- **Shell** : `zsh`

---

## 2. Versions .NET installées

Exécutable : `/usr/local/share/dotnet/dotnet`

### SDK installés
- `7.0.317` (`/usr/local/share/dotnet/sdk/7.0.317/`) — SDK actif par défaut
- `6.0.425` (`/usr/local/share/dotnet/sdk/6.0.425/`)

### Runtimes installés
- `Microsoft.AspNetCore.App 7.0.20`
- `Microsoft.NETCore.App 7.0.20`
- `Microsoft.AspNetCore.App 6.0.33`
- `Microsoft.NETCore.App 6.0.33`

### Outils globaux .NET (`~/.dotnet/tools`)
- `dotnet-ef` `7.0.20` (présent dans `~/.dotnet/tools/dotnet-ef`, non ajouté au `$PATH` par défaut)
- `dotnet-aspnet-codegenerator` `7.0.12`

### Workloads .NET
- Aucun workload optionnel installé (`dotnet workload list` vide). Les templates Blazor WebAssembly standard (`blazorwasm`, `blazorwasm-empty`) fonctionnent sans workload AOT pour le mode JIT WebAssembly standard.

---

## 3. Templates .NET vérifiés

Les modèles requis pour l'Atelier 10 sont disponibles sur le SDK 7.0.317 :
- `sln` (`Solution`)
- `classlib` (`Bibliothèque de classes`)
- `webapi` (`API web ASP.NET Core`)
- `blazorwasm` (`Application WebAssembly Blazor`)
- `xunit` (`xUnit Test Project`)

---

## 4. IDE détectés et contrainte Visual Studio 2022

- **Visual Studio 2022 (Windows)** : **Non disponible nativement sur macOS**.
- **Visual Studio for Mac** : Version `17.6.14.413` détectée dans `/Applications/Visual Studio.app` (IDE distinct de Visual Studio 2022 Windows et retiré du support Microsoft).
- **VS Code (`code`) / JetBrains Rider (`rider`)** : Non détectés dans `/Applications` ni dans le `$PATH`.
- **Navigateurs détectés** : Google Chrome (`/Applications/Google Chrome.app`), Safari (`/Applications/Safari.app`).
- **Stratégie retenue** : Développement, compilation, tests et exécution réalisés directement via le CLI `.NET` (`dotnet` 7.0.317) et `zsh` sur macOS, sans bloquer le TP.

---

## 5. SQLite et packages NuGet disponibles

### SQLite système
- Binaire : `/usr/bin/sqlite3`
- Version : `3.54.0` (64-bit)

### Cache NuGet local (`~/.nuget/packages`)
Packages compatibles `.NET 7.0` déjà présents localement :
- `Microsoft.EntityFrameworkCore.Sqlite` `7.0.20`
- `Microsoft.EntityFrameworkCore.Design` `7.0.20`
- `Microsoft.EntityFrameworkCore.Tools` `7.0.20`
- `Microsoft.AspNetCore.Mvc.Testing` `7.0.20`
- `Swashbuckle.AspNetCore` `6.5.0`
- `xunit` `2.4.2` / `2.9.3`
- `xunit.runner.visualstudio` `2.4.5` / `2.8.2`
- `Microsoft.NET.Test.Sdk` `17.5.0` / `17.12.0`
- `bunit` / `bunit.web` `1.35.3` / `1.40.0`

Note : `Microsoft.Extensions.Http` `7.0.0` (mentionné page 27 du PDF pour `AddHttpClient<TClient, TImplementation>`) sera restauré via NuGet (`https://api.nuget.org/v3/index.json`).

---

## 6. Certificat HTTPS et Ports disponibles

### Certificat développeur HTTPS
- Certificat présent : `61842FD7AE3D34E9FDC9AFF678D5A4DD4D5BE97A` (`CN=localhost`, valide jusqu'au `2027-09-30`), non vérifié comme approuvé système (`--trust`).

### Ports déjà occupés sur la machine
- `5000`, `7000` : `ControlCenter` (AirPlay Receiver macOS) — **à éviter absolument**
- `5053`, `5198` : processus locaux déjà en écoute — **à éviter**
- `3306`, `6379`, `5672`, `11434`, `8025` : services locaux divers

### Ports libres retenus pour le TP
- **API (`EmployeeManagement.Api`)** : `http://localhost:5280` (et profil HTTPS `https://localhost:7280`)
- **Blazor WASM (`EmployeeManagement.Web`)** : `http://localhost:5281` (et profil HTTPS `https://localhost:7281`)
- Vérification `lsof` : ports `5280`, `5281`, `7280`, `7281` libres.

---

## 7. Analyse de compatibilité, contraintes et risques

| Élément | Constat | Risque | Mesure d'atténuation |
|---|---|---|---|
| .NET 7.0 (EoL) | SDK `7.0.317` et runtime `7.0.20` installés | Fin de support officielle (warnings éventuels NuGet/SDK) | Conserver explicitement `net7.0` (`global.json` sur `7.0.317`) comme demandé, sans migration silencieuse |
| Visual Studio 2022 | Absent sur macOS | Impossibilité d'utiliser les assistants graphiques VS 2022 | Utiliser `dotnet` CLI, documenter l'absence de VS 2022 |
| Ports 5000/7000 | Occupés par macOS ControlCenter | Échec de démarrage Kestrel si ports par défaut utilisés | Configurer explicitement `5280`/`7280` (API) et `5281`/`7281` (Web) |
| Certificat HTTPS local | Peut bloquer `fetch()` WASM si non approuvé dans le navigateur | Erreur TLS/CORS en navigateur | Configurer HTTP `5280`/`5281` comme profil principal de démonstration locale + CORS explicite |
| Divergence mineure PDF vs Cahier des charges | Slide 17 du PDF écrit `DateOfBrith` et omet `DepartmentId` explicite | Incompatibilité avec les exigences du TP | Respecter `DateOfBirth` et inclure `DepartmentId` + navigation `Department` (Phase 2 & 8) |

---

## 8. Plan d'exécution synthétique

1. **Phase 1 — Architecture** : Créer `EmployeeManagement/EmployeeManagement.sln` avec `EmployeeManagement.Models`, `EmployeeManagement.Api`, `EmployeeManagement.Web`, `tests/EmployeeManagement.Tests`, et `documentation/`.
2. **Phase 2 — Modèles** : Implémenter `Employee`, `Department`, `Gender` avec validations `DataAnnotations` et relation FK `DepartmentId` / `Department`.
3. **Phase 3 — SQLite** : Configurer `AppDbContext` + `DbInitializer` idempotent sur `employee_management.db` (5 départements : IT, HR, Finance, Marketing, Sales + employés cohérents).
4. **Phase 4 — REST API** : Implémenter Repositories + `EmployeesController` (CRUD complet) + `DepartmentsController` (GET all / GET id) + gestion d'erreurs propre + CORS.
5. **Phase 5 à 12 — Blazor WASM** : Implémenter `IEmployeeService`/`EmployeeService`, `IDepartmentService`/`DepartmentService`, `EmployeeList.razor`, `EmployeeDetails.razor`, `EditEmployee.razor` (création & édition + binding dynamique `InputSelect` des départements), navigation, avatars locaux dans `wwwroot/images/`.
6. **Phase 13 à 15 — Tests & Validation E2E** : Tests unitaires/intégration (Models, SQLite persistance, API `WebApplicationFactory`, Services `HttpClient` mocké, Composants Blazor `bUnit`), puis démarrage réel API + Blazor WASM et validation E2E complète dans le navigateur.

