namespace RTattooAI.Application.Dtos.Auth;

public record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);
