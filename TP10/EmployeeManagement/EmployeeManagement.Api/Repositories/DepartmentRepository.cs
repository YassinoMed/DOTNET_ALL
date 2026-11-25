using EmployeeManagement.Api.Data;
using EmployeeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Api.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly AppDbContext _appDbContext;

    public DepartmentRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task<IEnumerable<Department>> GetDepartments()
    {
        return await _appDbContext.Departments
            .AsNoTracking()
            .OrderBy(d => d.DepartmentId)
            .ToListAsync();
    }

    public async Task<Department?> GetDepartment(int departmentId)
    {
        return await _appDbContext.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId);
    }
}

