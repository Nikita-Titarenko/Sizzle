namespace Sizzle.Application.Dtos;

public sealed record AiMessageDto(string Role, IReadOnlyList<AiPartDto> Parts);