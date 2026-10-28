using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Sizzle.Application.Services;

namespace Sizzle.Infrastructure.Services;

public class FileService : IFileService
{
    private const string FilePath = "uploads";

    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly string[] ValidFileExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    private static readonly string[] ValidVideoExtensions = [".mp4", ".webm", ".mov", ".avi", ".mkv"];

    private const long MaxVideoSize = 100 * 1024 * 1024;

    private readonly string _path;

    private readonly string _baseUrl;

    public FileService(IWebHostEnvironment environment, IHttpContextAccessor accessor)
    {
        var request = accessor.HttpContext?.Request;
        if (request != null)
        {
            _baseUrl = new Uri(new Uri($"{request.Scheme}://{request.Host}"), FilePath.Replace("\\", "/")).ToString();
        }
        else
        {
            _baseUrl = $"/{FilePath}";
        }

        var webRootPath = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;

        _path = Path.Combine(webRootPath, FilePath);
    }

    public bool IsValidSize(Stream fileStream)
    {
        if (!fileStream.CanSeek)
        {
            return true;
        }

        return fileStream.Length <= MaxFileSize;
    }

    public async Task SaveFile(Stream fileStream, string path)
    {
        var fileNameWithoutExtension = Path.Combine(
            Path.GetDirectoryName(path) ?? string.Empty,
            Path.GetFileNameWithoutExtension(path));

        foreach (var extension in ValidFileExtensions)
        {
            var existing = fileNameWithoutExtension + extension;
            if (Exists(existing))
            {
                DeleteFile(existing);
                break;
            }
        }

        var relativePath = path.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_path, relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        await using var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(output);
    }

    public string? GetFileUrl(string path)
    {
        string? fileNameWithExtension = null;

        if (Exists(path) && IsSupportedExtension(Path.GetExtension(path)))
        {
            fileNameWithExtension = path;
        }

        foreach (var extension in ValidFileExtensions)
        {
            if (fileNameWithExtension == null && Exists(path + extension))
            {
                fileNameWithExtension = path + extension;
                break;
            }
        }

        if (fileNameWithExtension == null)
        {
            return null;
        }

        return new Uri(
            new Uri(_baseUrl.EndsWith('/') ? _baseUrl : _baseUrl + "/"),
            fileNameWithExtension.Replace("\\", "/")).ToString();
    }

    public bool IsValidMedia(string fileName, long length)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return length > 0 && (ValidFileExtensions.Contains(extension) && length <= MaxFileSize
            || ValidVideoExtensions.Contains(extension) && length <= MaxVideoSize);
    }

    public void DeleteFile(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_path, path));
        var root = Path.GetFullPath(_path) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static bool IsSupportedExtension(string extension)
    {
        return ValidFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || ValidVideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private bool Exists(string fileName)
    {
        return File.Exists(Path.Combine(_path, fileName));
    }
}
