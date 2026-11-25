using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Models;

/// <summary>
/// Validates that a date of birth falls within a realistic calendar range.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ValidDateOfBirthAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateTime dateOfBirth)
        {
            return new ValidationResult("Date of birth is required.", new[] { validationContext.MemberName ?? nameof(Employee.DateOfBirth) });
        }

        if (dateOfBirth == default || dateOfBirth < new DateTime(1900, 1, 1))
        {
            return new ValidationResult("Date of birth must be on or after 1900-01-01.", new[] { validationContext.MemberName ?? nameof(Employee.DateOfBirth) });
        }

        if (dateOfBirth > DateTime.Today)
        {
            return new ValidationResult("Date of birth cannot be in the future.", new[] { validationContext.MemberName ?? nameof(Employee.DateOfBirth) });
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Represents an employee record linked to a department.
/// </summary>
public class Employee
{
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 100 characters.")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 100 characters.")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Date of birth is required.")]
    [DataType(DataType.Date)]
    [ValidDateOfBirth]
    public DateTime DateOfBirth { get; set; } = new DateTime(1990, 1, 1);

    [EnumDataType(typeof(Gender), ErrorMessage = "Please select a valid gender.")]
    public Gender Gender { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a department.")]
    public int DepartmentId { get; set; }

    public Department? Department { get; set; }

    [StringLength(255, ErrorMessage = "Photo path must not exceed 255 characters.")]
    public string PhotoPath { get; set; } = "images/avatar-default.svg";
}
