using Sizzle.Application.Dtos;

namespace Sizzle.Application.Services.Recipes;

public interface IAIService
{
    IAsyncEnumerable<string> GenerateStreamAsync(string systemInstruction, IReadOnlyList<AiMessageDto> messages, CancellationToken cancellationToken = default);

    Task<GeneratedRecipeImage> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default);

    Task<string> GenerateJsonAsync(string systemInstruction, IReadOnlyList<AiMessageDto> messages,
        CancellationToken cancellationToken = default);
}