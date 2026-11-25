# EmployeeManagement — Atelier 10 ASP.NET Core Blazor WebAssembly

Application pédagogique de gestion des employés réalisée conformément à l'**Atelier 10 : Le Framework ASP.NET Blazor** (`.NET 7.0`), avec client **Blazor WebAssembly**, **ASP.NET Core Web API**, persistance **SQLite** via Entity Framework Core et suite de tests automatisés.

---

## 1. Architecture

```text
EmployeeManagement/
├── global.json                        # Épingle le SDK .NET 7.0.317
├── EmployeeManagement.sln             # Solution .NET
├── EmployeeManagement.Models/         # Modèles partagés (Employee, Department, Gender)
├── EmployeeManagement.Api/            # REST API + EF Core SQLite (employee_management.db)
├── EmployeeManagement.Web/            # Application cliente Blazor WebAssembly
├── tests/EmployeeManagement.Tests/    # Tests xUnit, SQLite, WebApplicationFactory et bUnit
└── documentation/                     # Audits, plans, architecture, rapport de tests et auto-pass
```

---

## 2. Prérequis et Versions vérifiées

- **OS** : macOS (Apple Silicon `arm64`, testé sur Darwin `27.3.0`)
- **.NET SDK** : `7.0.317` (`net7.0`)
- **SQLite** : `/usr/bin/sqlite3` (`3.54.0`) + `Microsoft.EntityFrameworkCore.Sqlite` `7.0.20`
- **Packages clés** :
  - `Microsoft.EntityFrameworkCore.Sqlite` `7.0.20`
  - `Microsoft.EntityFrameworkCore.Design` `7.0.20`
  - `Swashbuckle.AspNetCore` `6.5.0`
  - `Microsoft.Extensions.Http` `7.0.0`
  - `Microsoft.AspNetCore.Mvc.Testing` `7.0.20`, `bunit` `1.40.0`, `xunit` `2.4.2`

> **Note IDE macOS** : Visual Studio 2022 (Windows) n'existe pas nativement sur macOS. Ce projet est développé et validé via le CLI `.NET` (`dotnet`).

---

## 3. Configuration SQLite

- Chaîne de connexion dans `EmployeeManagement.Api/appsettings.json` :
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=employee_management.db"
  }
  ```
- Au démarrage, `DbInitializer.Initialize()` crée le schéma s'il est absent (`EnsureCreated()`), garantit les 5 départements (`IT`, `HR`, `Finance`, `Marketing`, `Sales`) et insère 4 employés de démonstration uniquement lors de l'initialisation initiale.
- Une base existante n'est jamais supprimée ni écrasée.

---

## 4. Lancement de l'application

Ouvrir deux terminaux dans `EmployeeManagement/` :

### Terminal 1 — Lancer l'API REST (`http://localhost:5280`)
```bash
dotnet run --project EmployeeManagement.Api --launch-profile http
```
Swagger disponible sur : `http://localhost:5280/swagger`

### Terminal 2 — Lancer l'application Blazor WebAssembly (`http://localhost:5281`)
```bash
dotnet run --project EmployeeManagement.Web --launch-profile http
```
Application accessible sur : `http://localhost:5281`

> **Ports choisis** : `5280` (API) et `5281` (Blazor) évitent les ports `5000` et `7000` occupés par macOS AirPlay Receiver (`ControlCenter`). L'URL de l'API côté Blazor est configurable dans `EmployeeManagement.Web/wwwroot/appsettings.json`.

---

## 5. Exécution des tests automatisés

```bash
cd EmployeeManagement
dotnet test --verbosity normal
```

Couverture actuelle : **23 tests** (`ModelValidationTests`, `SqlitePersistenceTests`, `ApiIntegrationTests`, `BlazorServicesTests`, `BlazorComponentsTests`).

---

## 6. Fonctionnalités implémentées

- **Liste des employés (`/EmployeeList`)** : affichage des cartes employés avec avatar, département, email, genre, date de naissance, états chargement/vide/erreur, boutons `View`, `Edit`, `Delete` (modale de confirmation) et `Add Employee`.
- **Détails d'un employé (`/EmployeeDetails/{Id}` et `/EmployeeDetails`)** : affichage complet avec nom du département chargé par relation (`Include`), gestion propre des IDs absents, invalides ou introuvables.
- **Création et édition (`/editemployee` et `/editemployee/{Id}`)** : formulaire `EditForm` avec validation (`DataAnnotationsValidator`), liste déroulante dynamique des départements (`<InputSelect>` lié à `DepartmentId`), aperçu d'avatar, prévention de double soumission et retour vers la liste.
- **API REST CRUD** : `/api/employees` (`GET`, `GET/{id}`, `POST`, `PUT/{id}`, `DELETE/{id}`) et `/api/departments` (`GET`, `GET/{id}`) avec validation et CORS configuré.

---

## 7. Limites connues

- La vérification interactive dans Google Chrome via Chrome DevTools MCP n'a pas pu être exécutée durant l'audit automatique car le débogage distant Chrome (`chrome://inspect/#remote-debugging`) n'était pas activé sur le poste.
- .NET 7.0 est en fin de support officiel mais conservé intentionnellement pour respecter l'énoncé pédagogique du TP.

