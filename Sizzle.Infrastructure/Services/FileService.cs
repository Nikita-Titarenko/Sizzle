using Sizzle.Application.Services;

namespace Sizzle.Infrastructure.Services;

public class FileService : IFileService
{
    private const long MaxFileSize = 5 * 1024 * 1024;

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
        var relativePath = path.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", relativePath);
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

    public string GetFileUrl(string path)
    {
        return $"/{path.Replace('\\', '/')}";
    }
}
