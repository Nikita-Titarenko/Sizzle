namespace Sizzle.Application.Services;

public interface IFileService
{
    bool IsValidSize(Stream fileStream);

    Task SaveFile(Stream fileStream, string path);

    string? GetFileUrl(string path);

    bool IsValidMedia(string fileName, long length);

    void DeleteFile(string path);
}
