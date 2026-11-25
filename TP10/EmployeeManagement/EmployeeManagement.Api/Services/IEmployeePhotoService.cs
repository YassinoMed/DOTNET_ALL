using Microsoft.AspNetCore.Http;

namespace EmployeeManagement.Api.Services;

public sealed record PhotoSaveResult(bool Success, string? RelativePath, string? ErrorMessage);

public interface IEmployeePhotoService
{
    long MaxFileSizeBytes { get; }
    string DefaultAvatarPath { get; }
    bool IsManagedUploadPath(string? photoPath);
    Task<PhotoSaveResult> SaveValidatedPhotoAsync(IFormFile? file, CancellationToken cancellationToken = default);
    void DeleteManagedPhotoFile(string? photoPath);
}

