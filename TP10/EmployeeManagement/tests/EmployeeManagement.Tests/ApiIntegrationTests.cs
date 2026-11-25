using System.Net;
using System.Net.Http.Json;
using EmployeeManagement.Api.Data;
using EmployeeManagement.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeManagement.Tests;

public class CustomApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"emp_mgmt_api_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // Ignore temp cleanup race if SQLite handle is still closing.
            }
        }
    }
}

public class ApiIntegrationTests : IClassFixture<CustomApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDepartments_ReturnsFiveSeededDepartments()
    {
        var response = await _client.GetAsync("/api/departments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var departments = await response.Content.ReadFromJsonAsync<Department[]>();
        Assert.NotNull(departments);
        Assert.Equal(5, departments!.Length);
    }

    [Fact]
    public async Task GetDepartment_InvalidId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/departments/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployees_ReturnsSeededEmployeesWithDepartment()
    {
        var response = await _client.GetAsync("/api/employees");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var employees = await response.Content.ReadFromJsonAsync<Employee[]>();
        Assert.NotNull(employees);
        Assert.True(employees!.Length >= 4);
        Assert.All(employees, e => Assert.NotNull(e.Department));
    }

    [Fact]
    public async Task GetEmployee_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/employees/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateUpdateAndDeleteEmployee_FullLifecycleWorks()
    {
        var departments = await _client.GetFromJsonAsync<Department[]>("/api/departments");
        Assert.NotNull(departments);
        var itDept = departments!.First(d => d.DepartmentName == "IT");
        var financeDept = departments!.First(d => d.DepartmentName == "Finance");

        var newEmployee = new Employee
        {
            FirstName = "Karim",
            LastName = "Ziani",
            Email = "karim.ziani@company.com",
            DateOfBirth = new DateTime(1991, 8, 12),
            Gender = Gender.Male,
            DepartmentId = itDept.DepartmentId,
            PhotoPath = "images/john.svg"
        };

        // POST
        var createResponse = await _client.PostAsJsonAsync("/api/employees", newEmployee);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(created);
        Assert.Equal("IT", created!.Department?.DepartmentName);

        // PUT (change department to Finance)
        created.LastName = "Ziani-Modified";
        created.DepartmentId = financeDept.DepartmentId;
        created.Department = null;

        var updateResponse = await _client.PutAsJsonAsync($"/api/employees/{created.EmployeeId}", created);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<Employee>();
        Assert.NotNull(updated);
        Assert.Equal("Ziani-Modified", updated!.LastName);
        Assert.Equal("Finance", updated.Department?.DepartmentName);

        // DELETE
        var deleteResponse = await _client.DeleteAsync($"/api/employees/{created.EmployeeId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var getDeletedResponse = await _client.GetAsync($"/api/employees/{created.EmployeeId}");
        Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
    }

    [Fact]
    public async Task CreateEmployee_WithInvalidPayloadOrUnknownDepartment_ReturnsBadRequest()
    {
        var invalidEmployee = new Employee
        {
            FirstName = "",
            LastName = "X",
            Email = "invalid",
            DateOfBirth = new DateTime(1990, 1, 1),
            DepartmentId = 99999
        };

        var response = await _client.PostAsJsonAsync("/api/employees", invalidEmployee);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEmployee_IdMismatch_ReturnsBadRequest()
    {
        var employee = new Employee
        {
            EmployeeId = 2,
            FirstName = "Test",
            LastName = "User",
            Email = "test@company.com",
            DateOfBirth = new DateTime(1990, 1, 1),
            DepartmentId = 1
        };

        var response = await _client.PutAsJsonAsync("/api/employees/1", employee);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ApiResponse_IncludesCorsAllowAnyOriginHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/departments");
        request.Headers.Add("Origin", "http://localhost:5281");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Contains("*", values);
    }
}

