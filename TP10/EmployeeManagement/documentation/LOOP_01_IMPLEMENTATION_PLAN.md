# LOOP 01 — PLAN D'IMPLÉMENTATION

## 1. Objectif

Réaliser l'Atelier 10 « Le Framework ASP.NET Blazor » sous `.NET 7.0` (`net7.0`) avec :
- `EmployeeManagement.Models` (modèles partagés + validations)
- `EmployeeManagement.Api` (ASP.NET Core Web API + EF Core SQLite `employee_management.db` + Repositories + CORS + Swagger)
- `EmployeeManagement.Web` (Blazor WebAssembly + Services `HttpClient` + `EmployeeList`, `EmployeeDetails`, `EditEmployee` + `NavMenu` + avatars SVG/PNG dans `wwwroot/images`)
- `tests/EmployeeManagement.Tests` (xUnit + EF Core SQLite + `WebApplicationFactory` + bUnit)

## 2. Décisions techniques

1. **Cible .NET** : `net7.0` fixé via `global.json` (`7.0.317`, `rollForward: latestFeature` désactivé ou `disable`).
2. **Packages .NET 7.0** :
   - `Microsoft.EntityFrameworkCore.Sqlite` `7.0.20`
   - `Microsoft.EntityFrameworkCore.Design` `7.0.20`
   - `Swashbuckle.AspNetCore` `6.5.0`
   - `Microsoft.Extensions.Http` `7.0.0` (compatible `net7.0`)
   - `Microsoft.AspNetCore.Mvc.Testing` `7.0.20`
   - `bunit` `1.40.0`
   - `xunit` `2.4.2`, `xunit.runner.visualstudio` `2.4.5`, `Microsoft.NET.Test.Sdk` `17.5.0`
3. **Ports** :
   - API : `http://localhost:5280` (principal), `https://localhost:7280`
   - Blazor WebAssembly : `http://localhost:5281` (principal), `https://localhost:7281`
4. **Persistance SQLite** :
   - Fichier `employee_management.db` dans le répertoire d'exécution de l'API (ou chemin configuré dans `appsettings.json`, résolu de manière déterministe).
   - Initialisation via `EnsureCreated()` (sans jamais supprimer ni recréer une base existante) + `DbInitializer` idempotent qui vérifie l'existence des départements (`IT`, `HR`, `Finance`, `Marketing`, `Sales`) et n'insère les employés initiaux que si la table `Employees` est vide.
5. **Cohérence PDF / Cahier des charges** :
   - Utilisation des classes `*Base` (`EmployeeListBase`, `EmployeeDetailsBase`, `EditEmployeeBase`) comme montré dans le PDF, tout en ajoutant la création d'employé, la suppression avec confirmation, les états de chargement/erreur/liste vide, la validation complète (`DataAnnotationsValidator` + `ValidationSummary`), et le binding dynamique des départements (`DepartmentId` en `InputSelect`).

## 3. Ordre d'exécution

1. Création de la solution `EmployeeManagement.sln`, `global.json` et des 4 projets.
2. Implémentation de `EmployeeManagement.Models` (`Gender`, `Department`, `Employee`).
3. Implémentation de `EmployeeManagement.Api` (`AppDbContext`, `DbInitializer`, Repositories, Controllers, `Program.cs`, `appsettings.json`, `launchSettings.json`).
4. Implémentation de `EmployeeManagement.Web` (Services, `Program.cs`, `NavMenu`, `Home`, `EmployeeList`, `EmployeeDetails`, `EditEmployee`, CSS, images locales).
5. Implémentation de `tests/EmployeeManagement.Tests` et exécution de `dotnet test`.
6. Démarrage réel des serveurs API + Blazor WASM, vérification HTTP/CRUD/persistance SQLite après redémarrage, et vérification UI réelle dans le navigateur.
7. Rédaction de `LOOP_02_ARCHITECTURE.md`, `LOOP_03_TEST_REPORT.md`, `LOOP_04_AUTO_PASS.md` et `README.md`.

