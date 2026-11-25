using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EmployeeManagement.Api.Data;
using EmployeeManagement.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmployeeManagement.Tests;

public class PhotoTestApiFactory : WebApplicationFactory<Program>, IDisposable
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"emp_photo_db_{Guid.NewGuid():N}.db");
    public string TempWebRoot { get; } = Path.Combine(Path.GetTempPath(), $"emp_photo_webroot_{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Path.Combine(TempWebRoot, "uploads", "employees"));
        builder.UseWebRoot(TempWebRoot);

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={DbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(DbPath)) File.Delete(DbPath);
            if (Directory.Exists(TempWebRoot)) Directory.Delete(TempWebRoot, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup locks
        }
    }
}

public class EmployeePhotoTests : IClassFixture<PhotoTestApiFactory>
{
    private readonly PhotoTestApiFactory _factory;
    private readonly HttpClient _client;

    public EmployeePhotoTests(PhotoTestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static byte[] CreateValidJpegBytes() =>
        new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01, 0xFF, 0xD9 };

    private static byte[] CreateValidPngBytes()
    {
        var bytes = new byte[32];
        byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        sig.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), 13);
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), 100);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), 100);
        return bytes;
    }

    private static byte[] CreateValidWebPBytes() =>
        new byte[]
        {
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
            0x1A, 0x00, 0x00, 0x00,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P',
            (byte)'V', (byte)'P', (byte)'8', (byte)' ',
            0x0E, 0x00, 0x00, 0x00
        };

    private static MultipartFormDataContent BuildFileContent(byte[] bytes, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var payload = new ByteArrayContent(bytes);
        payload.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(payload, "file", fileName);
        return form;
    }

    private string MapRelativePathToDisk(string relativePhotoPath)
    {
        var trimmed = relativePhotoPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_factory.TempWebRoot, trimmed);
    }

    [Fact]
    public async Task UploadPhoto_JpgPngWebP_SucceedsAndGeneratesUniqueNames()
    {
        // JPG
        using (var jpgForm = BuildFileContent(CreateValidJpegBytes(), "my-unsafe-name.jpg", "image/jpeg"))
        {
            var resp = await _client.PostAsync("/api/employees/1/photo", jpgForm);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var emp = await resp.Content.ReadFromJsonAsync<Employee>();
            Assert.NotNull(emp);
            Assert.StartsWith("/uploads/employees/", emp!.PhotoPath);
            Assert.EndsWith(".jpg", emp.PhotoPath);
            Assert.DoesNotContain("my-unsafe-name", emp.PhotoPath);
            Assert.True(File.Exists(MapRelativePathToDisk(emp.PhotoPath)));

            var staticGet = await _client.GetAsync(emp.PhotoPath);
            Assert.Equal(HttpStatusCode.OK, staticGet.StatusCode);
        }

        // PNG
        using (var pngForm = BuildFileContent(CreateValidPngBytes(), "avatar.png", "image/png"))
        {
            var resp = await _client.PostAsync("/api/employees/2/photo", pngForm);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var emp = await resp.Content.ReadFromJsonAsync<Employee>();
            Assert.NotNull(emp);
            Assert.EndsWith(".png", emp!.PhotoPath);
            Assert.True(File.Exists(MapRelativePathToDisk(emp.PhotoPath)));
        }

        // WebP
        using (var webpForm = BuildFileContent(CreateValidWebPBytes(), "avatar.webp", "image/webp"))
        {
            var resp = await _client.PostAsync("/api/employees/3/photo", webpForm);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var emp = await resp.Content.ReadFromJsonAsync<Employee>();
            Assert.NotNull(emp);
            Assert.EndsWith(".webp", emp!.PhotoPath);
            Assert.True(File.Exists(MapRelativePathToDisk(emp.PhotoPath)));
        }
    }

    [Fact]
    public async Task UploadPhoto_RejectsOversizedForbiddenAndDisguisedExecutableFiles()
    {
        // > 2 MB
        var oversized = new byte[(2 * 1024 * 1024) + 128];
        CreateValidJpegBytes().CopyTo(oversized, 0);
        using (var oversizedForm = BuildFileContent(oversized, "huge.jpg", "image/jpeg"))
        {
            var resp = await _client.PostAsync("/api/employees/1/photo", oversizedForm);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        // Disallowed extension (.exe / .svg)
        using (var svgForm = BuildFileContent("<svg></svg>"u8.ToArray(), "icon.svg", "image/svg+xml"))
        {
            var resp = await _client.PostAsync("/api/employees/1/photo", svgForm);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        // Disguised PE executable (MZ header) renamed to .png
        var disguisedExe = new byte[] { (byte)'M', (byte)'Z', 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        using (var exeForm = BuildFileContent(disguisedExe, "malware.png", "image/png"))
        {
            var resp = await _client.PostAsync("/api/employees/1/photo", exeForm);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        // Corrupted bytes with .jpg extension
        var corrupted = "not-a-jpeg-file-at-all"u8.ToArray();
        using (var corruptForm = BuildFileContent(corrupted, "broken.jpg", "image/jpeg"))
        {
            var resp = await _client.PostAsync("/api/employees/1/photo", corruptForm);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }

        // Mismatched magic bytes (PNG bytes uploaded with .jpg extension)
        using (var mismatchForm = BuildFileContent(CreateValidPngBytes(), "mismatch.jpg", "image/jpeg"))
        {
            var resp = await _client.PostAsync("/api/employees/1/photo", mismatchForm);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }
    }

    [Fact]
    public async Task UploadAndDeletePhoto_NonExistentEmployee_ReturnsNotFound()
    {
        using var form = BuildFileContent(CreateValidPngBytes(), "photo.png", "image/png");
        var uploadResp = await _client.PostAsync("/api/employees/99999/photo", form);
        Assert.Equal(HttpStatusCode.NotFound, uploadResp.StatusCode);

        var deleteResp = await _client.DeleteAsync("/api/employees/99999/photo");
        Assert.Equal(HttpStatusCode.NotFound, deleteResp.StatusCode);
    }

    [Fact]
    public async Task ReplaceAndDeletePhoto_CleansOldFilePreservesOthersAndPersistsInSqlite()
    {
        // Upload first photo for employee 4
        string firstPhotoPath;
        using (var form1 = BuildFileContent(CreateValidJpegBytes(), "first.jpg", "image/jpeg"))
        {
            var resp1 = await _client.PostAsync("/api/employees/4/photo", form1);
            Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
            firstPhotoPath = (await resp1.Content.ReadFromJsonAsync<Employee>())!.PhotoPath;
        }
        var firstDiskPath = MapRelativePathToDisk(firstPhotoPath);
        Assert.True(File.Exists(firstDiskPath));

        // Upload replacement photo for employee 4 -> old file deleted, new file saved
        string secondPhotoPath;
        using (var form2 = BuildFileContent(CreateValidPngBytes(), "second.png", "image/png"))
        {
            var resp2 = await _client.PostAsync("/api/employees/4/photo", form2);
            Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
            secondPhotoPath = (await resp2.Content.ReadFromJsonAsync<Employee>())!.PhotoPath;
        }
        var secondDiskPath = MapRelativePathToDisk(secondPhotoPath);
        Assert.NotEqual(firstPhotoPath, secondPhotoPath);
        Assert.False(File.Exists(firstDiskPath));
        Assert.True(File.Exists(secondDiskPath));

        // Verify persistence directly in a fresh AppDbContext reading the same SQLite file
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_factory.DbPath}")
            .Options;
        using (var verifyDb = new AppDbContext(dbOptions))
        {
            var persisted = await verifyDb.Employees.AsNoTracking().FirstAsync(e => e.EmployeeId == 4);
            Assert.Equal(secondPhotoPath, persisted.PhotoPath);
        }

        // Delete photo -> reverts to default avatar and removes second file from disk
        var deleteResp = await _client.DeleteAsync("/api/employees/4/photo");
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);
        var afterDelete = await deleteResp.Content.ReadFromJsonAsync<Employee>();
        Assert.Equal("images/avatar-default.svg", afterDelete!.PhotoPath);
        Assert.False(File.Exists(secondDiskPath));
    }

    [Fact]
    public async Task ReplacePhoto_DoesNotDeleteFileWhenReferencedByAnotherEmployee()
    {
        // Upload photo for employee 2
        string sharedPath;
        using (var form = BuildFileContent(CreateValidPngBytes(), "shared.png", "image/png"))
        {
            var resp = await _client.PostAsync("/api/employees/2/photo", form);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            sharedPath = (await resp.Content.ReadFromJsonAsync<Employee>())!.PhotoPath;
        }

        // Assign same PhotoPath to employee 3 in DB to simulate shared reference
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emp3 = await db.Employees.FirstAsync(e => e.EmployeeId == 3);
            emp3.PhotoPath = sharedPath;
            await db.SaveChangesAsync();
        }

        // Delete photo on employee 2 -> shared file MUST NOT be deleted because employee 3 still uses it
        var deleteResp = await _client.DeleteAsync("/api/employees/2/photo");
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);
        Assert.True(File.Exists(MapRelativePathToDisk(sharedPath)));
    }
}

