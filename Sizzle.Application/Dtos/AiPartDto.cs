namespace Sizzle.Application.Dtos;

public sealed record AiPartDto(string? Text = null, string? FileUri = null, string? MimeType = null, byte[]? Data = null);