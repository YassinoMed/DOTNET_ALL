using EmployeeManagement.Models;
using EmployeeManagement.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EmployeeManagement.Web.Pages;

public class EmployeeListBase : ComponentBase
{
    [Inject]
    public IEmployeeService EmployeeService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    public List<Employee> Employees { get; set; } = new();
    public bool IsLoading { get; set; } = true;
    public bool IsDeleting { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public Employee? EmployeePendingDelete { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ReadStatusFromQuery();
        await LoadEmployeesAsync();
    }

    protected async Task LoadEmployeesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var result = await EmployeeService.GetEmployees();
            Employees = result.ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load employees: {ex.Message}";
            Employees = new List<Employee>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void RequestDelete(Employee employee)
    {
        EmployeePendingDelete = employee;
    }

    protected void CancelDelete()
    {
        if (!IsDeleting)
        {
            EmployeePendingDelete = null;
        }
    }

    protected async Task ConfirmDeleteAsync()
    {
        if (EmployeePendingDelete == null || IsDeleting)
        {
            return;
        }

        IsDeleting = true;
        ErrorMessage = null;
        var target = EmployeePendingDelete;

        try
        {
            await EmployeeService.DeleteEmployee(target.EmployeeId);
            Employees.RemoveAll(e => e.EmployeeId == target.EmployeeId);
            SuccessMessage = $"Employee {target.FirstName} {target.LastName} was deleted.";
            EmployeePendingDelete = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete employee: {ex.Message}";
        }
        finally
        {
            IsDeleting = false;
        }
    }

    protected void ClearMessages()
    {
        SuccessMessage = null;
        ErrorMessage = null;
    }

    private void ReadStatusFromQuery()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return;
        }

        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in query)
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0] == "saved")
            {
                SuccessMessage = kv[1] == "created"
                    ? "Employee created successfully."
                    : "Employee updated successfully.";
            }
        }
    }
}

