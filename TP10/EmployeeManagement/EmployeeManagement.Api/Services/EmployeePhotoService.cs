using System.Buffers.Binary;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace EmployeeManagement.Api.Services;

/// <summary>
/// Validates, stores, and cleans up employee profile images under wwwroot/uploads/employees/.
/// Enforces extension allow-listing, 2 MB size cap, binary magic-header verification,
/// unique GUID file naming, and path-traversal protection.
/// </summary>
public sealed class EmployeePhotoService : IEmployeePhotoService
{
    public const long MaxAllowedFileSizeBytes = 2 * 1024 * 1024;
    public const string ManagedUploadPrefix = "/uploads/employees/";
    public const string FallbackAvatarPath = "images/avatar-default.svg";

    private static readonly Dictionary<string, string> AllowedExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ".jpg",
        [".jpeg"] = ".jpg",
        [".png"] = ".png",
        [".webp"] = ".webp"
    };

    private readonly string _uploadsRootFullPath;
    private readonly ILogger<EmployeePhotoService> _logger;

    public EmployeePhotoService(IWebHostEnvironment environment, ILogger<EmployeePhotoService> logger)
    {
        var webRoot = !string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? environment.WebRootPath
            : Path.Combine(environment.ContentRootPath, "wwwroot");

        var uploadsDir = Path.Combine(webRoot, "uploads", "employees");
        Directory.CreateDirectory(uploadsDir);

        _uploadsRootFullPath = Path.GetFullPath(uploadsDir);
        if (!_uploadsRootFullPath.EndsWith(Path.DirectorySeparatorChar))
        {
            _uploadsRootFullPath += Path.DirectorySeparatorChar;
        }

        _logger = logger;
    }

    public long MaxFileSizeBytes => MaxAllowedFileSizeBytes;
    public string DefaultAvatarPath => FallbackAvatarPath;

    public bool IsManagedUploadPath(string? photoPath)
    {
        return !string.IsNullOrWhiteSpace(photoPath)
               && photoPath.StartsWith(ManagedUploadPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<PhotoSaveResult> SaveValidatedPhotoAsync(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return new PhotoSaveResult(false, null, "Please select a non-empty image file.");
        }

        if (file.Length > MaxAllowedFileSizeBytes)
        {
            return new PhotoSaveResult(false, null, "Image file exceeds the maximum allowed size of 2 MB.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensionMap.TryGetValue(extension, out var normalizedExt))
        {
            return new PhotoSaveResult(false, null, "Unsupported file format. Only JPG, JPEG, PNG, and WebP images are allowed.");
        }

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        {
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                totalRead += read;
                if (totalRead > MaxAllowedFileSizeBytes)
                {
                    return new PhotoSaveResult(false, null, "Image file exceeds the maximum allowed size of 2 MB.");
                }

                memory.Write(buffer, 0, read);
            }

            bytes = memory.ToArray();
        }

        if (bytes.Length == 0)
        {
            return new PhotoSaveResult(false, null, "The uploaded file is empty.");
        }

        if (!MatchesExpectedImageFormat(bytes, normalizedExt))
        {
            return new PhotoSaveResult(
                false,
                null,
                "Invalid or corrupted image content. The file signature does not match an allowed JPG, PNG, or WebP image.");
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{normalizedExt}";
        var targetFullPath = Path.GetFullPath(Path.Combine(_uploadsRootFullPath, uniqueFileName));

        if (!targetFullPath.StartsWith(_uploadsRootFullPath, StringComparison.Ordinal))
        {
            return new PhotoSaveResult(false, null, "Invalid storage path.");
        }

        await File.WriteAllBytesAsync(targetFullPath, bytes, cancellationToken);

        return new PhotoSaveResult(true, $"{ManagedUploadPrefix}{uniqueFileName}", null);
    }

    public void DeleteManagedPhotoFile(string? photoPath)
    {
        if (!IsManagedUploadPath(photoPath))
        {
            return;
        }

        var fileName = photoPath!.Substring(ManagedUploadPrefix.Length);
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.Contains(".."))
        {
            return;
        }

        var candidateFullPath = Path.GetFullPath(Path.Combine(_uploadsRootFullPath, fileName));
        if (!candidateFullPath.StartsWith(_uploadsRootFullPath, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (File.Exists(candidateFullPath))
            {
                File.Delete(candidateFullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete obsolete employee photo at {PhotoPath}.", candidateFullPath);
        }
    }

    private static bool MatchesExpectedImageFormat(ReadOnlySpan<byte> data, string normalizedExtension)
    {
        return normalizedExtension switch
        {
            ".jpg" => IsValidJpeg(data),
            ".png" => IsValidPng(data),
            ".webp" => IsValidWebP(data),
            _ => false
        };
    }

    private static bool IsValidJpeg(ReadOnlySpan<byte> data)
    {
        // JPEG SOI (FF D8) + marker prefix (FF) + valid marker byte (APPn/SOF/DQT/DHT/DRI/COM)
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8 || data[2] != 0xFF)
        {
            return false;
        }

        var marker = data[3];
        return (marker >= 0xE0 && marker <= 0xEF)
               || marker == 0xC0
               || marker == 0xC2
               || marker == 0xDB
               || marker == 0xC4
               || marker == 0xDD
               || marker == 0xFE;
    }

    private static bool IsValidPng(ReadOnlySpan<byte> data)
    {
        // PNG 8-byte signature + 4-byte chunk len + "IHDR" + 4-byte width + 4-byte height
        if (data.Length < 24)
        {
            return false;
        }

        ReadOnlySpan<byte> signature = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        if (!data[..8].SequenceEqual(signature))
        {
            return false;
        }

        if (data[12] != (byte)'I' || data[13] != (byte)'H' || data[14] != (byte)'D' || data[15] != (byte)'R')
        {
            return false;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
        return width > 0 && height > 0;
    }

    private static bool IsValidWebP(ReadOnlySpan<byte> data)
    {
        // RIFF....WEBP followed by VP8 / VP8L / VP8X chunk
        if (data.Length < 16)
        {
            return false;
        }

        var isRiff = data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F';
        var isWebP = data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P';
        if (!isRiff || !isWebP)
        {
            return false;
        }

        var isVp8Prefix = data[12] == (byte)'V' && data[13] == (byte)'P' && data[14] == (byte)'8';
        var validChunkSuffix = data[15] == (byte)' ' || data[15] == (byte)'L' || data[15] == (byte)'X';
        return isVp8Prefix && validChunkSuffix;
    }
}

