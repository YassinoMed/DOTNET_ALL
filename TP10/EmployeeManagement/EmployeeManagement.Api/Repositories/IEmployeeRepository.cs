using EmployeeManagement.Models;

namespace EmployeeManagement.Api.Repositories;

public interface IEmployeeRepository
{
    Task<IEnumerable<Employee>> GetEmployees();
    Task<Employee?> GetEmployee(int employeeId);
    Task<Employee> AddEmployee(Employee employee);
    Task<Employee?> UpdateEmployee(Employee employee);
    Task<Employee?> UpdateEmployeePhoto(int employeeId, string photoPath);
    Task<bool> IsPhotoReferencedByAnyEmployee(string photoPath, int? excludingEmployeeId = null);
    Task<Employee?> DeleteEmployee(int employeeId);
}
