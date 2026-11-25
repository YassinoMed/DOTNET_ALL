# LOOP 02 — ARCHITECTURE DU PROJET

## 1. Vue d'ensemble

La solution `EmployeeManagement.sln` suit une architecture modulaire en 4 projets sous `.NET 7.0` (`global.json` fixé sur le SDK `7.0.317`) :

```
EmployeeManagement/
├── global.json
├── EmployeeManagement.sln
├── README.md
├── EmployeeManagement.Models/
│   ├── Department.cs
│   ├── Employee.cs            (inclut ValidDateOfBirthAttribute)
│   └── Gender.cs
├── EmployeeManagement.Api/
│   ├── Controllers/
│   │   ├── DepartmentsController.cs
│   │   └── EmployeesController.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── DbInitializer.cs
│   ├── Repositories/
│   │   ├── IDepartmentRepository.cs / DepartmentRepository.cs
│   │   └── IEmployeeRepository.cs / EmployeeRepository.cs
│   ├── Properties/launchSettings.json
│   ├── appsettings.json
│   ├── employee_management.db
│   └── Program.cs
├── EmployeeManagement.Web/
│   ├── Pages/
│   │   ├── Index.razor
│   │   ├── EmployeeList.razor / EmployeeListBase.cs
│   │   ├── EmployeeDetails.razor / EmployeeDetailsBase.cs
│   │   └── EditEmployee.razor / EditEmployeeBase.cs
│   ├── Services/
│   │   ├── HttpServiceHelper.cs
│   │   ├── IDepartmentService.cs / DepartmentService.cs
│   │   └── IEmployeeService.cs / EmployeeService.cs
│   ├── Shared/
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── wwwroot/
│   │   ├── appsettings.json
│   │   ├── css/app.css
│   │   ├── images/{john,sam,mary,sara,avatar-default}.svg
│   │   └── index.html
│   ├── Properties/launchSettings.json
│   ├── App.razor
│   ├── _Imports.razor
│   └── Program.cs
├── tests/EmployeeManagement.Tests/
│   ├── ApiIntegrationTests.cs
│   ├── BlazorComponentsTests.cs
│   ├── BlazorServicesTests.cs
│   ├── ModelValidationTests.cs
│   └── SqlitePersistenceTests.cs
└── documentation/
    ├── LOOP_00_ENVIRONMENT_AUDIT.md
    ├── LOOP_01_IMPLEMENTATION_PLAN.md
    ├── LOOP_02_ARCHITECTURE.md
    ├── LOOP_03_TEST_REPORT.md
    └── LOOP_04_AUTO_PASS.md
```

---

## 2. Dépendances entre projets

- `EmployeeManagement.Models` : aucune dépendance projet (modèles partagés + `DataAnnotations`).
- `EmployeeManagement.Api` -> référence `EmployeeManagement.Models`.
- `EmployeeManagement.Web` -> référence `EmployeeManagement.Models`.
- `EmployeeManagement.Tests` -> référence `EmployeeManagement.Models`, `EmployeeManagement.Api`, `EmployeeManagement.Web`.

---

## 3. Schéma SQLite et Relation Employee / Department

- Table `Departments` :
  - `DepartmentId` (`INTEGER`, clé primaire auto-incrémentée)
  - `DepartmentName` (`TEXT`, `NOT NULL`, longueur max 100, index unique)
- Table `Employees` :
  - `EmployeeId` (`INTEGER`, clé primaire auto-incrémentée)
  - `FirstName`, `LastName` (`TEXT`, `NOT NULL`, max 100)
  - `Email` (`TEXT`, `NOT NULL`, max 150)
  - `DateOfBirth` (`TEXT`, `NOT NULL`)
  - `Gender` (`INTEGER`, `NOT NULL`)
  - `DepartmentId` (`INTEGER`, `NOT NULL`, clé étrangère -> `Departments.DepartmentId`, `ON DELETE RESTRICT`)
  - `PhotoPath` (`TEXT`, max 255)

`EmployeeRepository.GetEmployees()` et `EmployeeRepository.GetEmployee(int)` utilisent `.Include(e => e.Department)` afin de charger la propriété de navigation `Department` et permettre l'affichage de `DepartmentName`.

---

## 4. Endpoints REST API (`http://localhost:5280`)

| Méthode | Route | Description | Réponses HTTP |
|---|---|---|---|
| `GET` | `/api/employees` | Liste tous les employés avec leur département | `200 OK`, `500` |
| `GET` | `/api/employees/{id:int}` | Récupère un employé par ID avec son département | `200 OK`, `404 NotFound`, `500` |
| `POST` | `/api/employees` | Crée un employé après validation modèle et FK | `201 Created`, `400 BadRequest`, `500` |
| `PUT` | `/api/employees/{id:int}` | Met à jour un employé (vérifie ID et FK département) | `200 OK`, `400 BadRequest`, `404 NotFound`, `500` |
| `DELETE` | `/api/employees/{id:int}` | Supprime un employé existant | `200 OK`, `404 NotFound`, `500` |
| `GET` | `/api/departments` | Liste les 5 départements | `200 OK`, `500` |
| `GET` | `/api/departments/{id:int}` | Récupère un département par ID | `200 OK`, `404 NotFound`, `500` |

---

## 5. Routes Blazor WebAssembly (`http://localhost:5281`)

- `/` : `Index.razor` (tableau de bord synthétique + accès rapide)
- `/EmployeeList` : `EmployeeList.razor` (liste, chargement, état vide, erreurs, View/Edit/Delete avec confirmation, Add Employee)
- `/EmployeeDetails` et `/EmployeeDetails/{Id}` : `EmployeeDetails.razor` (détails complets + département, gestion ID absent/invalide/inexistant)
- `/editemployee` et `/editemployee/{Id}` : `EditEmployee.razor` (création et modification, `InputSelect` dynamique des départements, validation et anti-double soumission)

