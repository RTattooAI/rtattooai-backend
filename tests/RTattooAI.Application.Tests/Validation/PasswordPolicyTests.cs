using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Validation;
using Xunit;

namespace RTattooAI.Application.Tests.Validation;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("corta1")]              // menos de 8 caracteres
    [InlineData("solamenteletras")]     // sin dígitos
    [InlineData("12345678")]            // sin letras y además muy común
    [InlineData("password1")]           // está en la lista de prohibidas
    public void EnsureValid_Throws_WhenPasswordIsWeak(string password)
    {
        Assert.Throws<InvalidRequestException>(() => PasswordPolicy.EnsureValid(password));
    }

    [Fact]
    public void EnsureValid_Throws_WhenPasswordContainsTheEmail()
    {
        Assert.Throws<InvalidRequestException>(() => PasswordPolicy.EnsureValid("analista01x9", "analista01@pnp.gob.pe"));
    }

    [Fact]
    public void EnsureValid_Accepts_ReasonablePassword()
    {
        PasswordPolicy.EnsureValid("Tatuajes2026", "perito@pnp.gob.pe");
    }
}
