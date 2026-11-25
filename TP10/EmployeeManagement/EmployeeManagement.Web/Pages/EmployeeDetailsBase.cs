using EmployeeManagement.Models;
using EmployeeManagement.Web.Services;
using Microsoft.AspNetCore.Components;

namespace EmployeeManagement.Web.Pages;

public class EmployeeDetailsBase : ComponentBase
{
    [Inject]
    public IEmployeeService EmployeeService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Parameter]
    public string? Id { get; set; }

    public Employee? Employee { get; set; }
    public bool IsLoading { get; set; } = true;
    public bool IsDeleting { get; set; }
    public bool ConfirmDeleteVisible { get; set; }
    public string? ErrorMessage { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await LoadEmployeeAsync();
    }

    protected async Task LoadEmployeeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        Employee = null;
        ConfirmDeleteVisible = false;

        try
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                var employees = (await EmployeeService.GetEmployees()).ToList();
                if (employees.Count == 0)
                {
                    ErrorMessage = "No employees exist in the directory yet.";
                    return;
                }

                Employee = await EmployeeService.GetEmployee(employees[0].EmployeeId) ?? employees[0];
                return;
            }

            if (!int.TryParse(Id, out var employeeId) || employeeId <= 0)
            {
                ErrorMessage = $"Invalid employee ID '{Id}'. Please provide a positive integer ID.";
                return;
            }

            var loaded = await EmployeeService.GetEmployee(employeeId);
            if (loaded == null)
            {
                ErrorMessage = $"Employee with ID {employeeId} was not found.";
                return;
            }

            Employee = loaded;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to load employee details: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void ShowDeleteConfirmation()
    {
        ConfirmDeleteVisible = true;
    }

    protected void CancelDelete()
    {
        if (!IsDeleting)
        {
            ConfirmDeleteVisible = false;
        }
    }

    protected async Task ConfirmDeleteAsync()
    {
        if (Employee == null || IsDeleting)
        {
            return;
        }

        IsDeleting = true;
        try
        {
            await EmployeeService.DeleteEmployee(Employee.EmployeeId);
            NavigationManager.NavigateTo("/EmployeeList");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete employee: {ex.Message}";
            ConfirmDeleteVisible = false;
        }
        finally
        {
            IsDeleting = false;
        }
    }
}

