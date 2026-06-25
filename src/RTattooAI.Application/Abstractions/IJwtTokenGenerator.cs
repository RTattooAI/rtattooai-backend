using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) Generate(Usuario usuario);
}
