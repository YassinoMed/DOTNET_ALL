using EmployeeManagement.Models;

namespace EmployeeManagement.Api.Data;

/// <summary>
/// Idempotent SQLite database initializer that creates missing schema and seeds initial data once.
/// Never drops or recreates an existing database.
/// </summary>
public static class DbInitializer
{
    private static readonly string[] DefaultDepartmentNames =
    {
        "IT",
        "HR",
        "Finance",
        "Marketing",
        "Sales"
    };

    public static void Initialize(AppDbContext context)
    {
        context.Database.EnsureCreated();

        var existingDepartments = context.Departments.ToList();
        var hadExistingDepartments = existingDepartments.Count > 0;

        foreach (var deptName in DefaultDepartmentNames)
        {
            if (!existingDepartments.Any(d =>
                    string.Equals(d.DepartmentName, deptName, StringComparison.OrdinalIgnoreCase)))
            {
                var department = new Department { DepartmentName = deptName };
                context.Departments.Add(department);
                existingDepartments.Add(department);
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            context.SaveChanges();
        }

        // Seed initial demo employees only on initial empty database setup.
        if (!hadExistingDepartments && !context.Employees.Any())
        {
            var deptByName = context.Departments
                .ToDictionary(d => d.DepartmentName, d => d.DepartmentId, StringComparer.OrdinalIgnoreCase);

            var initialEmployees = new List<Employee>
            {
                new()
                {
                    FirstName = "John",
                    LastName = "Hastings",
                    Email = "john.hastings@company.com",
                    DateOfBirth = new DateTime(1985, 10, 5),
                    Gender = Gender.Male,
                    DepartmentId = deptByName["IT"],
                    PhotoPath = "images/john.svg"
                },
                new()
                {
                    FirstName = "Sam",
                    LastName = "Galloway",
                    Email = "sam.galloway@company.com",
                    DateOfBirth = new DateTime(1988, 12, 22),
                    Gender = Gender.Male,
                    DepartmentId = deptByName["HR"],
                    PhotoPath = "images/sam.svg"
                },
                new()
                {
                    FirstName = "Mary",
                    LastName = "Smith",
                    Email = "mary.smith@company.com",
                    DateOfBirth = new DateTime(1992, 11, 11),
                    Gender = Gender.Female,
                    DepartmentId = deptByName["Finance"],
                    PhotoPath = "images/mary.svg"
                },
                new()
                {
                    FirstName = "Sara",
                    LastName = "Longway",
                    Email = "sara.longway@company.com",
                    DateOfBirth = new DateTime(1994, 3, 18),
                    Gender = Gender.Female,
                    DepartmentId = deptByName["Marketing"],
                    PhotoPath = "images/sara.svg"
                }
            };

            context.Employees.AddRange(initialEmployees);
            context.SaveChanges();
        }
    }
}

