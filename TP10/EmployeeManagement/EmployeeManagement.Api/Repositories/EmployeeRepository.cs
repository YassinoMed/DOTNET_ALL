using EmployeeManagement.Api.Data;
using EmployeeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Api.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _appDbContext;

    public EmployeeRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task<IEnumerable<Employee>> GetEmployees()
    {
        return await _appDbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .OrderBy(e => e.EmployeeId)
            .ToListAsync();
    }

    public async Task<Employee?> GetEmployee(int employeeId)
    {
        return await _appDbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
    }

    public async Task<Employee> AddEmployee(Employee employee)
    {
        // Prevent EF Core from attempting to insert a detached navigation entity.
        employee.Department = null;

        if (string.IsNullOrWhiteSpace(employee.PhotoPath))
        {
            employee.PhotoPath = "images/avatar-default.svg";
        }

        var result = await _appDbContext.Employees.AddAsync(employee);
        await _appDbContext.SaveChangesAsync();

        return (await GetEmployee(result.Entity.EmployeeId))!;
    }

    public async Task<Employee?> UpdateEmployee(Employee employee)
    {
        var existing = await _appDbContext.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == employee.EmployeeId);

        if (existing == null)
        {
            return null;
        }

        existing.FirstName = employee.FirstName;
        existing.LastName = employee.LastName;
        existing.Email = employee.Email;
        existing.DateOfBirth = employee.DateOfBirth;
        existing.Gender = employee.Gender;
        existing.DepartmentId = employee.DepartmentId;
        existing.PhotoPath = string.IsNullOrWhiteSpace(employee.PhotoPath)
            ? existing.PhotoPath
            : employee.PhotoPath;

        await _appDbContext.SaveChangesAsync();

        return await GetEmployee(existing.EmployeeId);
    }

    public async Task<Employee?> UpdateEmployeePhoto(int employeeId, string photoPath)
    {
        var existing = await _appDbContext.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

        if (existing == null)
        {
            return null;
        }

        existing.PhotoPath = photoPath;
        await _appDbContext.SaveChangesAsync();

        return await GetEmployee(existing.EmployeeId);
    }

    public async Task<bool> IsPhotoReferencedByAnyEmployee(string photoPath, int? excludingEmployeeId = null)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return false;
        }

        return await _appDbContext.Employees
            .AsNoTracking()
            .AnyAsync(e => e.PhotoPath == photoPath
                           && (!excludingEmployeeId.HasValue || e.EmployeeId != excludingEmployeeId.Value));
    }

    public async Task<Employee?> DeleteEmployee(int employeeId)
    {
        var existing = await _appDbContext.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

        if (existing == null)
        {
            return null;
        }

        _appDbContext.Employees.Remove(existing);
        await _appDbContext.SaveChangesAsync();
        return existing;
    }
}
