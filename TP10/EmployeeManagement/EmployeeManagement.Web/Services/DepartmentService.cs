using System.Net;
using System.Net.Http.Json;
using EmployeeManagement.Models;

namespace EmployeeManagement.Web.Services;

public class DepartmentService : IDepartmentService
{
    private readonly HttpClient _httpClient;

    public DepartmentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<Department>> GetDepartments()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/departments");
            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, "load departments");
            var departments = await response.Content.ReadFromJsonAsync<Department[]>();
            return departments ?? Array.Empty<Department>();
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, "load departments");
        }
    }

    public async Task<Department?> GetDepartment(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/departments/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await HttpServiceHelper.EnsureSuccessOrThrowAsync(response, $"load department #{id}");
            return await response.Content.ReadFromJsonAsync<Department>();
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            throw HttpServiceHelper.TranslateNetworkException(ex, $"load department #{id}");
        }
    }
}

