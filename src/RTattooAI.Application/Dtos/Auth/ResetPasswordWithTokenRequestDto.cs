namespace RTattooAI.Application.Dtos.Auth;

public record ResetPasswordWithTokenRequestDto(string Token, string NewPassword);
