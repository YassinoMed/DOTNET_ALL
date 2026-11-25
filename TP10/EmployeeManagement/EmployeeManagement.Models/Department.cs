using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Models;

/// <summary>
/// Represents an organizational department.
/// </summary>
public class Department
{
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "Department name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Department name must be between 2 and 100 characters.")]
    public string DepartmentName { get; set; } = string.Empty;
}

