using Bunit;
using EmployeeManagement.Models;
using EmployeeManagement.Web.Pages;
using EmployeeManagement.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeManagement.Tests;

internal sealed class FakeEmployeeService : IEmployeeService
{
    public List<Employee> Store { get; } = new()
    {
        new Employee
        {
            EmployeeId = 1,
            FirstName = "John",
            LastName = "Hastings",
            Email = "john.hastings@company.com",
            DateOfBirth = new DateTime(1985, 10, 5),
            Gender = Gender.Male,
            DepartmentId = 1,
            Department = new Department { DepartmentId = 1, DepartmentName = "IT" },
            PhotoPath = "images/john.svg"
        },
        new Employee
        {
            EmployeeId = 2,
            FirstName = "Sara",
            LastName = "Longway",
            Email = "sara.longway@company.com",
            DateOfBirth = new DateTime(1994, 3, 18),
            Gender = Gender.Female,
            DepartmentId = 2,
            Department = new Department { DepartmentId = 2, DepartmentName = "HR" },
            PhotoPath = "/uploads/employees/sara-custom.png"
        }
    };

    public int? LastDeletedId { get; private set; }
    public int? LastPhotoDeletedId { get; private set; }
    public Employee? LastUpdatedEmployee { get; private set; }

    public Task<IEnumerable<Employee>> GetEmployees() => Task.FromResult<IEnumerable<Employee>>(Store.ToList());

    public Task<Employee?> GetEmployee(int id) =>
        Task.FromResult(Store.FirstOrDefault(e => e.EmployeeId == id));

    public Task<Employee> CreateEmployee(Employee employee)
    {
        employee.EmployeeId = Store.Count == 0 ? 1 : Store.Max(e => e.EmployeeId) + 1;
        Store.Add(employee);
        return Task.FromResult(employee);
    }

    public Task<Employee> UpdateEmployee(Employee employee)
    {
        LastUpdatedEmployee = employee;
        var index = Store.FindIndex(e => e.EmployeeId == employee.EmployeeId);
        if (index >= 0)
        {
            Store[index] = employee;
        }
        return Task.FromResult(employee);
    }

    public Task<Employee> UploadEmployeePhoto(int employeeId, byte[] fileBytes, string fileName, string contentType)
    {
        var emp = Store.First(e => e.EmployeeId == employeeId);
        emp.PhotoPath = $"/uploads/employees/uploaded_{employeeId}.png";
        return Task.FromResult(emp);
    }

    public Task<Employee> DeleteEmployeePhoto(int employeeId)
    {
        LastPhotoDeletedId = employeeId;
        var emp = Store.First(e => e.EmployeeId == employeeId);
        emp.PhotoPath = "images/avatar-default.svg";
        return Task.FromResult(emp);
    }

    public Task DeleteEmployee(int id)
    {
        LastDeletedId = id;
        Store.RemoveAll(e => e.EmployeeId == id);
        return Task.CompletedTask;
    }

    public string ResolvePhotoUrl(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return "images/avatar-default.svg";
        }

        if (photoPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return $"http://localhost:5280{photoPath}";
        }

        return photoPath.TrimStart('/');
    }
}

internal sealed class FakeDepartmentService : IDepartmentService
{
    public List<Department> Departments { get; } = new()
    {
        new Department { DepartmentId = 1, DepartmentName = "IT" },
        new Department { DepartmentId = 2, DepartmentName = "HR" },
        new Department { DepartmentId = 3, DepartmentName = "Finance" }
    };

    public Task<IEnumerable<Department>> GetDepartments() =>
        Task.FromResult<IEnumerable<Department>>(Departments);

    public Task<Department?> GetDepartment(int id) =>
        Task.FromResult(Departments.FirstOrDefault(d => d.DepartmentId == id));
}

public class BlazorComponentsTests : TestContext
{
    [Fact]
    public void EmployeeList_RendersCircularPhotosAndDeletesAfterConfirmation()
    {
        var employeeService = new FakeEmployeeService();
        Services.AddSingleton<IEmployeeService>(employeeService);

        var cut = RenderComponent<EmployeeList>();

        Assert.Contains("John Hastings", cut.Markup);
        Assert.Contains("Sara Longway", cut.Markup);
        Assert.Contains("employee-avatar-circle", cut.Markup);
        Assert.Contains("http://localhost:5280/uploads/employees/sara-custom.png", cut.Markup);

        // Click Delete on first employee -> opens modal
        cut.FindAll("button.btn-danger")[0].Click();
        Assert.Contains("Confirm Deletion", cut.Markup);

        // Confirm delete
        cut.Find("[role='dialog'] button.btn-danger").Click();

        Assert.Equal(1, employeeService.LastDeletedId);
        Assert.Contains("Employee John Hastings was deleted.", cut.Markup);
        Assert.Single(cut.FindAll(".employee-card"));
        Assert.DoesNotContain("john.hastings@company.com", cut.Markup);
        Assert.Contains("Sara Longway", cut.Markup);
    }

    [Fact]
    public void EmployeeDetails_DisplaysDepartmentAndResolvedPhotoAndHandlesInvalidIds()
    {
        Services.AddSingleton<IEmployeeService>(new FakeEmployeeService());

        var validCut = RenderComponent<EmployeeDetails>(p => p.Add(x => x.Id, "2"));
        Assert.Contains("Sara Longway", validCut.Markup);
        Assert.Contains("HR", validCut.Markup);
        Assert.Contains("http://localhost:5280/uploads/employees/sara-custom.png", validCut.Markup);

        var invalidCut = RenderComponent<EmployeeDetails>(p => p.Add(x => x.Id, "abc"));
        Assert.Contains("Invalid employee ID", invalidCut.Markup);

        var missingCut = RenderComponent<EmployeeDetails>(p => p.Add(x => x.Id, "999"));
        Assert.Contains("was not found", missingCut.Markup);

        var defaultCut = RenderComponent<EmployeeDetails>();
        Assert.Contains("John Hastings", defaultCut.Markup);
    }

    [Fact]
    public void EditEmployee_BindsDepartmentsAndPersistsDepartmentChange()
    {
        var employeeService = new FakeEmployeeService();
        var departmentService = new FakeDepartmentService();
        Services.AddSingleton<IEmployeeService>(employeeService);
        Services.AddSingleton<IDepartmentService>(departmentService);

        var cut = RenderComponent<EditEmployee>(p => p.Add(x => x.Id, "1"));

        var select = cut.Find("#department");
        Assert.Contains("IT", select.InnerHtml);
        Assert.Contains("HR", select.InnerHtml);
        Assert.Contains("Finance", select.InnerHtml);
        Assert.Equal("1", select.GetAttribute("value"));

        select.Change("3");
        cut.Find("form").Submit();

        Assert.NotNull(employeeService.LastUpdatedEmployee);
        Assert.Equal(3, employeeService.LastUpdatedEmployee!.DepartmentId);

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/EmployeeList?saved=updated", nav.Uri);
    }

    [Fact]
    public void EditEmployee_RemovePhotoAndSave_InvokesDeletePhotoEndpoint()
    {
        var employeeService = new FakeEmployeeService();
        var departmentService = new FakeDepartmentService();
        Services.AddSingleton<IEmployeeService>(employeeService);
        Services.AddSingleton<IDepartmentService>(departmentService);

        var cut = RenderComponent<EditEmployee>(p => p.Add(x => x.Id, "2"));

        Assert.Contains("http://localhost:5280/uploads/employees/sara-custom.png", cut.Markup);

        cut.Find("button.btn-outline-danger").Click();
        Assert.Contains("Default avatar selected", cut.Markup);

        cut.Find("form").Submit();

        Assert.Equal(2, employeeService.LastPhotoDeletedId);
        Assert.Equal("images/avatar-default.svg", employeeService.Store.First(e => e.EmployeeId == 2).PhotoPath);
    }

    [Fact]
    public void EditEmployee_CreateModeValidation_BlocksInvalidDepartmentAndEmptyFields()
    {
        var employeeService = new FakeEmployeeService();
        var departmentService = new FakeDepartmentService();
        Services.AddSingleton<IEmployeeService>(employeeService);
        Services.AddSingleton<IDepartmentService>(departmentService);

        var cut = RenderComponent<EditEmployee>();

        cut.Find("form").Submit();

        Assert.Contains("First name is required.", cut.Markup);
        Assert.Contains("Please select a department.", cut.Markup);
        Assert.Equal(2, employeeService.Store.Count);
    }
}
