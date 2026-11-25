using System.ComponentModel.DataAnnotations;
using EmployeeManagement.Models;

namespace EmployeeManagement.Tests;

public class ModelValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Employee_ValidModel_PassesValidation()
    {
        var employee = new Employee
        {
            EmployeeId = 1,
            FirstName = "Alice",
            LastName = "Martin",
            Email = "alice.martin@company.com",
            DateOfBirth = new DateTime(1992, 4, 15),
            Gender = Gender.Female,
            DepartmentId = 1,
            PhotoPath = "images/mary.svg"
        };

        var results = ValidateModel(employee);

        Assert.Empty(results);
    }

    [Fact]
    public void Employee_MissingRequiredFieldsAndInvalidDepartment_FailsValidation()
    {
        var employee = new Employee
        {
            FirstName = "",
            LastName = "",
            Email = "not-an-email",
            DateOfBirth = DateTime.Today.AddDays(5),
            DepartmentId = 0
        };

        var results = ValidateModel(employee);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Employee.FirstName)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Employee.LastName)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Employee.Email)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Employee.DepartmentId)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Employee.DateOfBirth)));
    }

    [Fact]
    public void Employee_DateOfBirthTooOld_FailsValidation()
    {
        var employee = new Employee
        {
            FirstName = "Bob",
            LastName = "Stone",
            Email = "bob@company.com",
            DateOfBirth = new DateTime(1850, 1, 1),
            DepartmentId = 1
        };

        var results = ValidateModel(employee);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Employee.DateOfBirth)));
    }

    [Fact]
    public void Department_ValidAndInvalid_AreValidatedProperly()
    {
        var valid = new Department { DepartmentId = 1, DepartmentName = "IT" };
        var invalid = new Department { DepartmentId = 2, DepartmentName = "" };

        Assert.Empty(ValidateModel(valid));
        Assert.NotEmpty(ValidateModel(invalid));
    }

    [Fact]
    public void Gender_ContainsExpectedValues()
    {
        var values = Enum.GetValues<Gender>();

        Assert.Equal(new[] { Gender.Male, Gender.Female, Gender.Other }, values);
    }
}

