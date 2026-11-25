using EmployeeManagement.Models;

namespace EmployeeManagement.Web.Services;

public interface IEmployeeService
{
    Task<IEnumerable<Employee>> GetEmployees();
    Task<Employee?> GetEmployee(int id);
    Task<Employee> CreateEmployee(Employee employee);
    Task<Employee> UpdateEmployee(Employee employee);
    Task<Employee> UploadEmployeePhoto(int employeeId, byte[] fileBytes, string fileName, string contentType);
    Task<Employee> DeleteEmployeePhoto(int employeeId);
    Task DeleteEmployee(int id);
    string ResolvePhotoUrl(string? photoPath);
}
