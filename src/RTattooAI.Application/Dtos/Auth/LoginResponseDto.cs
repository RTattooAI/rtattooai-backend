namespace RTattooAI.Application.Dtos.Auth;

public record LoginResponseDto(string Token, DateTime ExpiresAt, UserDto User);
