using Google.Apis.Auth.OAuth2;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using Sizzle.Application.Dtos;
using Sizzle.Application.Services.Recipes;
using Sizzle.Infrastructure.Options;
using System.Runtime.CompilerServices;

namespace Sizzle.Infrastructure.Services;

public class GeminiService(IOptions<GeminiOptions> options, GoogleCredential credential) : IAIService
{
    public async Task<string> GenerateJsonAsync(string systemInstruction, IReadOnlyList<AiMessageDto> messages,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ProjectId) || string.IsNullOrWhiteSpace(settings.Location) ||
            string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new InvalidOperationException("Gemini:ProjectId, Gemini:Location and Gemini:Model must be configured.");
        }

        using var client = new Client(enterprise: true, credential: credential, project: settings.ProjectId, location: settings.Location,
            httpOptions: new HttpOptions { Timeout = 600_000 });
        var contents = messages.Select(message => new Content
        {
            Role = message.Role,
            Parts = message.Parts.Select(part => part.FileUri is not null
                ? new Part { FileData = new FileData { FileUri = part.FileUri, MimeType = part.MimeType } }
                : part.Data is not null
                    ? new Part { InlineData = new Blob { Data = part.Data, MimeType = part.MimeType } }
                    : new Part { Text = part.Text }).ToList()
        }).ToList();
        var response = await client.Models.GenerateContentAsync(settings.Model, contents, new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = systemInstruction }] },
            ResponseMimeType = "application/json"
        }, cancellationToken);
        return response.Text ?? throw new InvalidOperationException("AI returned no review.");
    }

    public async Task<GeneratedRecipeImage> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ProjectId) || string.IsNullOrWhiteSpace(settings.ImageLocation) ||
            string.IsNullOrWhiteSpace(settings.ImageModel))
        {
            throw new InvalidOperationException("Gemini:ProjectId, Gemini:ImageLocation and Gemini:ImageModel must be configured.");
        }

        using var client = new Client(enterprise: true, credential: credential, project: settings.ProjectId, location: settings.ImageLocation,
            httpOptions: new HttpOptions { Timeout = 600_000 });
        var response = await client.Models.GenerateContentAsync(settings.ImageModel, prompt,
            new GenerateContentConfig { ResponseModalities = ["TEXT", "IMAGE"] }, cancellationToken);
        var image = response.Candidates?.SelectMany(candidate => candidate.Content?.Parts ?? [])
            .Select(part => part.InlineData)
            .FirstOrDefault(data => data?.Data is { Length: > 0 } && data.MimeType is "image/png" or "image/jpeg");
        if (image?.Data is not { Length: > 0 } bytes || image.MimeType is null)
        {
            throw new InvalidOperationException("Image generation returned no supported image.");
        }

        return new GeneratedRecipeImage(bytes, image.MimeType);
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(string systemInstruction, IReadOnlyList<AiMessageDto> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ProjectId) || string.IsNullOrWhiteSpace(settings.Location) ||
            string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new InvalidOperationException("Gemini:ProjectId, Gemini:Location and Gemini:Model must be configured.");
        }

        using var client = new Client(enterprise: true, credential: credential, project: settings.ProjectId, location: settings.Location,
            httpOptions: new HttpOptions { Timeout = 600_000 });
        var contents = messages.Select(message => new Content
        {
            Role = message.Role,
            Parts = message.Parts.Select(part => part.FileUri is not null
                ? new Part { FileData = new FileData { FileUri = part.FileUri, MimeType = part.MimeType } }
                : new Part { Text = part.Text }).ToList()
        }).ToList();
        await foreach (var response in client.Models.GenerateContentStreamAsync(settings.Model, contents, new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = systemInstruction }] },
            ResponseMimeType = "application/json"
        }, cancellationToken).WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(response.Text)) yield return response.Text;
        }
    }
}