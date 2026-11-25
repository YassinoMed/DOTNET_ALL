using System.Net;
using System.Net.Http.Json;
using EmployeeManagement.Models;
using EmployeeManagement.Web.Services;

namespace EmployeeManagement.Tests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);
}

public class BlazorServicesTests
{
    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        return new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost:5280/")
        };
    }

    [Fact]
    public async Task EmployeeService_GetEmployees_DeserializesJson()
    {
        var expected = new[]
        {
            new Employee
            {
                EmployeeId = 1,
                FirstName = "John",
                LastName = "Hastings",
                Email = "john@company.com",
                DepartmentId = 1,
                Department = new Department { DepartmentId = 1, DepartmentName = "IT" }
            }
        };

        var client = CreateClient((req, _) =>
        {
            Assert.Equal("api/employees", req.RequestUri!.PathAndQuery.TrimStart('/'));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            });
        });

        var service = new EmployeeService(client);
        var result = (await service.GetEmployees()).ToList();

        Assert.Single(result);
        Assert.Equal("John", result[0].FirstName);
        Assert.Equal("IT", result[0].Department?.DepartmentName);
    }

    [Fact]
    public async Task EmployeeService_GetEmployee_ReturnsNullOnNotFound()
    {
        var client = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var service = new EmployeeService(client);
        var result = await service.GetEmployee(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task EmployeeService_ThrowsDescriptiveErrorOnServerFailureAndTimeout()
    {
        var serverErrorClient = CreateClient((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Database unavailable")
            }));

        var serviceWithError = new EmployeeService(serverErrorClient);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => serviceWithError.GetEmployees());
        Assert.Contains("Database unavailable", ex.Message);

        var timeoutClient = CreateClient((_, _) =>
            throw new TaskCanceledException("Timed out"));

        var serviceWithTimeout = new EmployeeService(timeoutClient);
        var timeoutEx = await Assert.ThrowsAsync<HttpRequestException>(() => serviceWithTimeout.GetEmployees());
        Assert.Contains("timed out", timeoutEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DepartmentService_GetDepartmentsAndNotFound_WorkCorrectly()
    {
        var client = CreateClient((req, _) =>
        {
            if (req.RequestUri!.PathAndQuery.EndsWith("/api/departments/99"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new Department { DepartmentId = 1, DepartmentName = "IT" },
                    new Department { DepartmentId = 2, DepartmentName = "HR" }
                })
            });
        });

        var service = new DepartmentService(client);
        var departments = (await service.GetDepartments()).ToList();
        var missing = await service.GetDepartment(99);

        Assert.Equal(2, departments.Count);
        Assert.Null(missing);
    }

    [Fact]
    public async Task EmployeeService_UploadAndDeletePhotoAndResolveUrl_WorkCorrectly()
    {
        string? capturedMethod = null;
        string? capturedContentType = null;

        var client = CreateClient((req, _) =>
        {
            capturedMethod = req.Method.Method;
            capturedContentType = req.Content?.Headers.ContentType?.MediaType;

            if (req.RequestUri!.PathAndQuery.EndsWith("/api/employees/1/photo") && req.Method == HttpMethod.Post)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new Employee
                    {
                        EmployeeId = 1,
                        FirstName = "John",
                        LastName = "Hastings",
                        PhotoPath = "/uploads/employees/abc123.png"
                    })
                });
            }

            if (req.RequestUri!.PathAndQuery.EndsWith("/api/employees/1/photo") && req.Method == HttpMethod.Delete)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new Employee
                    {
                        EmployeeId = 1,
                        FirstName = "John",
                        LastName = "Hastings",
                        PhotoPath = "images/avatar-default.svg"
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        var service = new EmployeeService(client);

        var uploaded = await service.UploadEmployeePhoto(1, new byte[] { 1, 2, 3 }, "pic.png", "image/png");
        Assert.Equal("POST", capturedMethod);
        Assert.Equal("multipart/form-data", capturedContentType);
        Assert.Equal("/uploads/employees/abc123.png", uploaded.PhotoPath);
        Assert.Equal("http://localhost:5280/uploads/employees/abc123.png", service.ResolvePhotoUrl(uploaded.PhotoPath));

        var deleted = await service.DeleteEmployeePhoto(1);
        Assert.Equal("DELETE", capturedMethod);
        Assert.Equal("images/avatar-default.svg", service.ResolvePhotoUrl(deleted.PhotoPath));
        Assert.Equal("images/avatar-default.svg", service.ResolvePhotoUrl(null));
    }
}

