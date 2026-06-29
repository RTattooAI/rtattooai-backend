namespace RTattooAI.Application.Dtos.Users;

public record CreateUserRequestDto(string FullName, string Email, string Password, string Role);
