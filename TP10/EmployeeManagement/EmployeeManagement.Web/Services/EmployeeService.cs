using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EmployeeManagement.Models;

namespace EmployeeManagement.Web.Services;

public class EmployeeService : IEmployeeService
{
    private const string DefaultAvatar = "images/avatar-default.svg";
    private readonly HttpClient _httpClient;

    public EmployeeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<Employee>> GetEmployees()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/employees");
            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, "load employees");
            var employees = await response.Content.ReadFromJsonAsync<Employee[]>();
            return employees ?? Array.Empty<Employee>();
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, "load employees");
        }
    }

    public async Task<Employee?> GetEmployee(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/employees/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, $"load employee #{id}");
            return await response.Content.ReadFromJsonAsync<Employee>();
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, $"load employee #{id}");
        }
    }

    public async Task<Employee> CreateEmployee(Employee employee)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/employees", employee);
            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, "create employee");
            var created = await response.Content.ReadFromJsonAsync<Employee>();
            return created ?? throw new HttpRequestException("API returned an empty response after creating employee.");
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, "create employee");
        }
    }

    public async Task<Employee> UpdateEmployee(Employee employee)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/employees/{employee.EmployeeId}", employee);
            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, $"update employee #{employee.EmployeeId}");
            var updated = await response.Content.ReadFromJsonAsync<Employee>();
            return updated ?? throw new HttpRequestException("API returned an empty response after updating employee.");
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, $"update employee #{employee.EmployeeId}");
        }
    }

    public async Task<Employee> UploadEmployeePhoto(int employeeId, byte[] fileBytes, string fileName, string contentType)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync($"api/employees/{employeeId}/photo", content);
            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, $"upload photo for employee #{employeeId}");
            var updated = await response.Content.ReadFromJsonAsync<Employee>();
            return updated ?? throw new HttpRequestException("API returned an empty response after uploading photo.");
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, $"upload photo for employee #{employeeId}");
        }
    }

    public async Task<Employee> DeleteEmployeePhoto(int employeeId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/employees/{employeeId}/photo");
            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, $"remove photo for employee #{employeeId}");
            var updated = await response.Content.ReadFromJsonAsync<Employee>();
            return updated ?? throw new HttpRequestException("API returned an empty response after removing photo.");
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, $"remove photo for employee #{employeeId}");
        }
    }

    public async Task DeleteEmployee(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/employees/{id}");
            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, $"delete employee #{id}");
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, $"delete employee #{id}");
        }
    }

    public string ResolvePhotoUrl(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return DefaultAvatar;
        }

        if (photoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || photoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || photoPath.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return photoPath;
        }

        var trimmed = photoPath.TrimStart('/');
        if (trimmed.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) && _httpClient.BaseAddress != null)
        {
            return new Uri(_httpClient.BaseAddress, trimmed).ToString();
        }

        return trimmed;
    }
}
