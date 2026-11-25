using EmployeeManagement.Api.Repositories;
using EmployeeManagement.Api.Services;
using EmployeeManagement.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IEmployeePhotoService _photoService;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(
        IEmployeeRepository employeeRepository,
        IDepartmentRepository departmentRepository,
        IEmployeePhotoService photoService,
        ILogger<EmployeesController> logger)
    {
        _employeeRepository = employeeRepository;
        _departmentRepository = departmentRepository;
        _photoService = photoService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees()
    {
        try
        {
            return Ok(await _employeeRepository.GetEmployees());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employees from the database.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error retrieving data from the database.");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Employee>> GetEmployee(int id)
    {
        try
        {
            var result = await _employeeRepository.GetEmployee(id);
            if (result == null)
            {
                return NotFound($"Employee with Id = {id} not found.");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee {EmployeeId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error retrieving data from the database.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Employee>> CreateEmployee([FromBody] Employee? employee)
    {
        try
        {
            if (employee == null)
            {
                return BadRequest("Employee payload cannot be null.");
            }

            var department = await _departmentRepository.GetDepartment(employee.DepartmentId);
            if (department == null)
            {
                ModelState.AddModelError(nameof(employee.DepartmentId),
                    $"Department with Id = {employee.DepartmentId} does not exist.");
                return BadRequest(ModelState);
            }

            var createdEmployee = await _employeeRepository.AddEmployee(employee);

            return CreatedAtAction(
                nameof(GetEmployee),
                new { id = createdEmployee.EmployeeId },
                createdEmployee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating employee.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error creating employee record.");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Employee>> UpdateEmployee(int id, [FromBody] Employee? employee)
    {
        try
        {
            if (employee == null)
            {
                return BadRequest("Employee payload cannot be null.");
            }

            if (id != employee.EmployeeId)
            {
                return BadRequest("Employee ID mismatch.");
            }

            var department = await _departmentRepository.GetDepartment(employee.DepartmentId);
            if (department == null)
            {
                ModelState.AddModelError(nameof(employee.DepartmentId),
                    $"Department with Id = {employee.DepartmentId} does not exist.");
                return BadRequest(ModelState);
            }

            var updatedEmployee = await _employeeRepository.UpdateEmployee(employee);
            if (updatedEmployee == null)
            {
                return NotFound($"Employee with Id = {id} not found.");
            }

            return Ok(updatedEmployee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee {EmployeeId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error updating employee record.");
        }
    }

    [HttpPost("{id:int}/photo")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ActionResult<Employee>> UploadPhoto(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        string? savedPhotoPath = null;
        try
        {
            var existingEmployee = await _employeeRepository.GetEmployee(id);
            if (existingEmployee == null)
            {
                return NotFound($"Employee with Id = {id} not found.");
            }

            var oldPhotoPath = existingEmployee.PhotoPath;

            var saveResult = await _photoService.SaveValidatedPhotoAsync(file, cancellationToken);
            if (!saveResult.Success || string.IsNullOrWhiteSpace(saveResult.RelativePath))
            {
                return BadRequest(saveResult.ErrorMessage ?? "Invalid image upload.");
            }

            savedPhotoPath = saveResult.RelativePath;

            var updatedEmployee = await _employeeRepository.UpdateEmployeePhoto(id, savedPhotoPath);
            if (updatedEmployee == null)
            {
                _photoService.DeleteManagedPhotoFile(savedPhotoPath);
                return NotFound($"Employee with Id = {id} not found.");
            }

            // Delete previous uploaded photo only after successful DB save and when unreferenced by others.
            if (!string.Equals(oldPhotoPath, savedPhotoPath, StringComparison.OrdinalIgnoreCase)
                && _photoService.IsManagedUploadPath(oldPhotoPath)
                && !await _employeeRepository.IsPhotoReferencedByAnyEmployee(oldPhotoPath))
            {
                _photoService.DeleteManagedPhotoFile(oldPhotoPath);
            }

            return Ok(updatedEmployee);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(savedPhotoPath))
            {
                _photoService.DeleteManagedPhotoFile(savedPhotoPath);
            }

            _logger.LogError(ex, "Error uploading profile photo for employee {EmployeeId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error uploading employee profile photo.");
        }
    }

    [HttpDelete("{id:int}/photo")]
    public async Task<ActionResult<Employee>> DeletePhoto(int id)
    {
        try
        {
            var existingEmployee = await _employeeRepository.GetEmployee(id);
            if (existingEmployee == null)
            {
                return NotFound($"Employee with Id = {id} not found.");
            }

            var oldPhotoPath = existingEmployee.PhotoPath;
            var updatedEmployee = await _employeeRepository.UpdateEmployeePhoto(id, _photoService.DefaultAvatarPath);
            if (updatedEmployee == null)
            {
                return NotFound($"Employee with Id = {id} not found.");
            }

            if (_photoService.IsManagedUploadPath(oldPhotoPath)
                && !await _employeeRepository.IsPhotoReferencedByAnyEmployee(oldPhotoPath))
            {
                _photoService.DeleteManagedPhotoFile(oldPhotoPath);
            }

            return Ok(updatedEmployee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile photo for employee {EmployeeId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error deleting employee profile photo.");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<Employee>> DeleteEmployee(int id)
    {
        try
        {
            var deletedEmployee = await _employeeRepository.DeleteEmployee(id);
            if (deletedEmployee == null)
            {
                return NotFound($"Employee with Id = {id} not found.");
            }

            if (_photoService.IsManagedUploadPath(deletedEmployee.PhotoPath)
                && !await _employeeRepository.IsPhotoReferencedByAnyEmployee(deletedEmployee.PhotoPath))
            {
                _photoService.DeleteManagedPhotoFile(deletedEmployee.PhotoPath);
            }

            return Ok(deletedEmployee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee {EmployeeId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Error deleting employee record.");
        }
    }
}
