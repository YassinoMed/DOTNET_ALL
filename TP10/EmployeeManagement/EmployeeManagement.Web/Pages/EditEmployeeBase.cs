using EmployeeManagement.Models;
using EmployeeManagement.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace EmployeeManagement.Web.Pages;

public class EditEmployeeBase : ComponentBase
{
    protected const long MaxPhotoFileSizeBytes = 2 * 1024 * 1024;
    protected const string DefaultAvatarPath = "images/avatar-default.svg";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    [Inject]
    public IEmployeeService EmployeeService { get; set; } = default!;

    [Inject]
    public IDepartmentService DepartmentService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Parameter]
    public string? Id { get; set; }

    public Employee Employee { get; set; } = new();
    public List<Department> Departments { get; set; } = new();
    public bool IsLoading { get; set; } = true;
    public bool IsSubmitting { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? PhotoErrorMessage { get; set; }
    public string? PhotoFeedbackMessage { get; set; }
    public bool IsCreateMode => Employee.EmployeeId == 0 && string.IsNullOrWhiteSpace(Id);

    private byte[]? _stagedPhotoBytes;
    private string? _stagedPhotoFileName;
    private string? _stagedPhotoContentType;
    private string? _stagedPreviewDataUrl;
    private bool _pendingPhotoRemoval;

    protected bool HasStagedPhoto => _stagedPhotoBytes != null;
    protected bool HasCustomOrStagedPhoto =>
        HasStagedPhoto
        || (!_pendingPhotoRemoval
            && !string.IsNullOrWhiteSpace(Employee.PhotoPath)
            && !string.Equals(Employee.PhotoPath, DefaultAvatarPath, StringComparison.OrdinalIgnoreCase));

    protected string DisplayedPhotoUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_stagedPreviewDataUrl))
            {
                return _stagedPreviewDataUrl;
            }

            if (_pendingPhotoRemoval)
            {
                return DefaultAvatarPath;
            }

            return EmployeeService.ResolvePhotoUrl(Employee.PhotoPath);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadDataAsync();
    }

    protected async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        SuccessMessage = null;
        ResetPhotoStaging();

        try
        {
            Departments = (await DepartmentService.GetDepartments()).ToList();

            if (string.IsNullOrWhiteSpace(Id))
            {
                Employee = new Employee
                {
                    DateOfBirth = new DateTime(1995, 1, 1),
                    Gender = Gender.Male,
                    PhotoPath = DefaultAvatarPath,
                    DepartmentId = 0
                };
                return;
            }

            if (!int.TryParse(Id, out var employeeId) || employeeId <= 0)
            {
                ErrorMessage = $"Invalid employee ID '{Id}'.";
                return;
            }

            var loaded = await EmployeeService.GetEmployee(employeeId);
            if (loaded == null)
            {
                ErrorMessage = $"Employee with ID {employeeId} was not found.";
                return;
            }

            Employee = loaded;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to load form data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected async Task HandlePhotoSelectedAsync(InputFileChangeEventArgs e)
    {
        PhotoErrorMessage = null;
        PhotoFeedbackMessage = null;

        var file = e.File;
        if (file == null)
        {
            return;
        }

        var extension = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            PhotoErrorMessage = "Unsupported image format. Please choose a JPG, JPEG, PNG, or WebP file.";
            return;
        }

        if (file.Size <= 0)
        {
            PhotoErrorMessage = "Selected image file is empty.";
            return;
        }

        if (file.Size > MaxPhotoFileSizeBytes)
        {
            PhotoErrorMessage = "Selected image exceeds the 2 MB maximum size limit.";
            return;
        }

        try
        {
            using var stream = file.OpenReadStream(MaxPhotoFileSizeBytes);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var contentType = ResolveContentType(extension, file.ContentType);
            _stagedPhotoBytes = bytes;
            _stagedPhotoFileName = file.Name;
            _stagedPhotoContentType = contentType;
            _stagedPreviewDataUrl = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
            _pendingPhotoRemoval = false;
            PhotoFeedbackMessage = $"Preview ready ({file.Name}, {bytes.Length / 1024.0:F1} KB). Click Save to apply.";
        }
        catch (Exception ex)
        {
            PhotoErrorMessage = $"Could not read selected image: {ex.Message}";
        }
    }

    protected void RemovePhoto()
    {
        PhotoErrorMessage = null;
        _stagedPhotoBytes = null;
        _stagedPhotoFileName = null;
        _stagedPhotoContentType = null;
        _stagedPreviewDataUrl = null;
        _pendingPhotoRemoval = true;
        Employee.PhotoPath = DefaultAvatarPath;
        PhotoFeedbackMessage = "Default avatar selected. Click Save to confirm removal.";
    }

    protected void CancelStagedPhoto()
    {
        PhotoErrorMessage = null;
        PhotoFeedbackMessage = null;
        _stagedPhotoBytes = null;
        _stagedPhotoFileName = null;
        _stagedPhotoContentType = null;
        _stagedPreviewDataUrl = null;
        _pendingPhotoRemoval = false;
    }

    protected async Task HandleValidSubmitAsync()
    {
        if (IsSubmitting)
        {
            return;
        }

        IsSubmitting = true;
        ErrorMessage = null;
        SuccessMessage = null;
        PhotoErrorMessage = null;

        try
        {
            var wasCreate = Employee.EmployeeId == 0;
            Employee.Department = null;

            Employee savedEmployee;
            if (wasCreate)
            {
                savedEmployee = await EmployeeService.CreateEmployee(Employee);
                // Keep newly created ID so a subsequent retry updates rather than duplicating.
                Employee.EmployeeId = savedEmployee.EmployeeId;
            }
            else
            {
                savedEmployee = await EmployeeService.UpdateEmployee(Employee);
            }

            if (_stagedPhotoBytes != null && !string.IsNullOrWhiteSpace(_stagedPhotoFileName))
            {
                savedEmployee = await EmployeeService.UploadEmployeePhoto(
                    savedEmployee.EmployeeId,
                    _stagedPhotoBytes,
                    _stagedPhotoFileName,
                    _stagedPhotoContentType ?? "image/jpeg");
                Employee.PhotoPath = savedEmployee.PhotoPath;
                ResetPhotoStaging();
            }
            else if (_pendingPhotoRemoval)
            {
                savedEmployee = await EmployeeService.DeleteEmployeePhoto(savedEmployee.EmployeeId);
                Employee.PhotoPath = savedEmployee.PhotoPath;
                ResetPhotoStaging();
            }

            SuccessMessage = wasCreate ? "Employee created successfully." : "Employee updated successfully.";
            NavigationManager.NavigateTo(wasCreate
                ? "/EmployeeList?saved=created"
                : "/EmployeeList?saved=updated");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    protected void Cancel()
    {
        NavigationManager.NavigateTo("/EmployeeList");
    }

    private void ResetPhotoStaging()
    {
        _stagedPhotoBytes = null;
        _stagedPhotoFileName = null;
        _stagedPhotoContentType = null;
        _stagedPreviewDataUrl = null;
        _pendingPhotoRemoval = false;
        PhotoErrorMessage = null;
        PhotoFeedbackMessage = null;
    }

    private static string ResolveContentType(string extension, string? browserContentType)
    {
        if (!string.IsNullOrWhiteSpace(browserContentType) && browserContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return browserContentType;
        }

        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }
}
