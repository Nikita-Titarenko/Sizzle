using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Services.Recipes;
using Sizzle.Infrastructure.Options;

namespace Sizzle.Infrastructure.Services;

public sealed class GcsVideoStorage(StorageClient storageClient, IOptions<GeminiOptions> options) : IAIVideoStorage
{
    public async Task<string> UploadAsync(RecipeMediaUploadDto video, string mimeType, CancellationToken cancellationToken)
    {
        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new InvalidOperationException("Gemini:BucketName must be configured.");
        }

        var name = $"recipe-generation/{Guid.NewGuid():N}{Path.GetExtension(video.FileName).ToLowerInvariant()}";
        await using var stream = video.OpenReadStream();
        await storageClient.UploadObjectAsync(bucket, name, mimeType, stream, cancellationToken: cancellationToken);
        return $"gs://{bucket}/{name}";
    }

    public Task DeleteAsync(string fileUri, CancellationToken cancellationToken)
    {
        var objectPrefix = "recipe-generation/";
        var bucketPrefix = $"gs://{options.Value.BucketName}/";
        var prefix = bucketPrefix + objectPrefix;
        if (!fileUri.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid Gemini video URI.", nameof(fileUri));
        }

        return storageClient.DeleteObjectAsync(options.Value.BucketName, fileUri[bucketPrefix.Length..],
            cancellationToken: cancellationToken);
    }
}