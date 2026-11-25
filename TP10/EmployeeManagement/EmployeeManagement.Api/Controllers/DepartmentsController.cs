using EmployeeManagement.Api.Repositories;
using EmployeeManagement.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<DepartmentsController> _logger;

    public DepartmentsController(
        IDepartmentRepository departmentRepository,
        ILogger<DepartmentsController> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Department>>> GetDepartments()
    {
        try
        {
            return Ok(await _departmentRepository.GetDepartments());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments from the database.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error retrieving data from the database.");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Department>> GetDepartment(int id)
    {
        try
        {
            var result = await _departmentRepository.GetDepartment(id);
            if (result == null)
            {
                return NotFound($"Department with Id = {id} not found.");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving department {DepartmentId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error retrieving data from the database.");
        }
    }
}

