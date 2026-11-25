using EmployeeManagement.Api.Data;
using EmployeeManagement.Api.Repositories;
using EmployeeManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Tests;

public class SqlitePersistenceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public SqlitePersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"emp_mgmt_test_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void DbInitializer_CreatesSchemaAndSeedsDataIdempotently()
    {
        using (var context = CreateContext())
        {
            DbInitializer.Initialize(context);
            DbInitializer.Initialize(context);
        }

        Assert.True(File.Exists(_dbPath));

        using (var verifyContext = CreateContext())
        {
            var departments = verifyContext.Departments.OrderBy(d => d.DepartmentName).ToList();
            var employees = verifyContext.Employees.Include(e => e.Department).ToList();

            Assert.Equal(5, departments.Count);
            Assert.Contains(departments, d => d.DepartmentName == "IT");
            Assert.Contains(departments, d => d.DepartmentName == "HR");
            Assert.Contains(departments, d => d.DepartmentName == "Finance");
            Assert.Contains(departments, d => d.DepartmentName == "Marketing");
            Assert.Contains(departments, d => d.DepartmentName == "Sales");

            Assert.Equal(4, employees.Count);
            Assert.All(employees, e => Assert.NotNull(e.Department));
        }
    }

    [Fact]
    public async Task EmployeeRepository_CrudOperationsPersistAcrossContextReopen()
    {
        using (var initContext = CreateContext())
        {
            DbInitializer.Initialize(initContext);
        }

        int createdId;
        int salesDeptId;

        using (var writeContext = CreateContext())
        {
            var deptRepo = new DepartmentRepository(writeContext);
            var empRepo = new EmployeeRepository(writeContext);
            var departments = (await deptRepo.GetDepartments()).ToList();
            var itDept = departments.First(d => d.DepartmentName == "IT");
            salesDeptId = departments.First(d => d.DepartmentName == "Sales").DepartmentId;

            var created = await empRepo.AddEmployee(new Employee
            {
                FirstName = "Nadia",
                LastName = "Benali",
                Email = "nadia.benali@company.com",
                DateOfBirth = new DateTime(1993, 6, 20),
                Gender = Gender.Female,
                DepartmentId = itDept.DepartmentId,
                PhotoPath = "images/mary.svg"
            });

            createdId = created.EmployeeId;
            Assert.True(createdId > 0);
            Assert.Equal("IT", created.Department?.DepartmentName);
        }

        // Re-open context (simulating process restart) and update department
        using (var updateContext = CreateContext())
        {
            var empRepo = new EmployeeRepository(updateContext);
            var loaded = await empRepo.GetEmployee(createdId);
            Assert.NotNull(loaded);
            Assert.Equal("IT", loaded!.Department?.DepartmentName);

            loaded.FirstName = "Nadia-Updated";
            loaded.DepartmentId = salesDeptId;
            var updated = await empRepo.UpdateEmployee(loaded);

            Assert.NotNull(updated);
            Assert.Equal("Sales", updated!.Department?.DepartmentName);
        }

        // Re-open again and verify update persisted, then delete and verify idempotent init
        using (var verifyContext = CreateContext())
        {
            DbInitializer.Initialize(verifyContext);
            var empRepo = new EmployeeRepository(verifyContext);
            var persisted = await empRepo.GetEmployee(createdId);

            Assert.NotNull(persisted);
            Assert.Equal("Nadia-Updated", persisted!.FirstName);
            Assert.Equal("Sales", persisted.Department?.DepartmentName);

            var deleted = await empRepo.DeleteEmployee(createdId);
            Assert.NotNull(deleted);
        }

        using (var finalContext = CreateContext())
        {
            var empRepo = new EmployeeRepository(finalContext);
            Assert.Null(await empRepo.GetEmployee(createdId));
        }
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}

