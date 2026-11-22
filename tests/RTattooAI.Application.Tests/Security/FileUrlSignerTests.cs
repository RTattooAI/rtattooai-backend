using Microsoft.Extensions.Configuration;
using RTattooAI.Infrastructure.Blob;
using Xunit;

namespace RTattooAI.Application.Tests.Security;

/// <summary>
/// La firma sustituye a las SAS de Azure: si se puede falsificar o reutilizar para
/// otra ruta, cualquiera podría leer las imágenes de análisis ajenos.
/// </summary>
public class FileUrlSignerTests
{
    private const string StoragePath = "consultas/6f9619ff-8b86-d011-b42d-00cf4fc964ff/original.jpg";

    private static FileUrlSigner CreateSigner(string key = "clave-de-firma-de-pruebas-muy-larga-1234") =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Files:SigningKey"] = key })
            .Build());

    private static (long Exp, string Sig) Parse(string signedPath)
    {
        var query = signedPath[(signedPath.IndexOf('?') + 1)..].Split('&');
        return (long.Parse(query[0].Split('=')[1]), query[1].Split('=')[1]);
    }

    [Fact]
    public void BuildSignedPath_ProducesAVerifiableUrl()
    {
        var signer = CreateSigner();

        var signed = signer.BuildSignedPath(StoragePath, TimeSpan.FromMinutes(10));
        var (exp, sig) = Parse(signed);

        Assert.StartsWith("/api/files/" + StoragePath + "?", signed);
        Assert.True(signer.Verify(StoragePath, exp, sig));
    }

    [Fact]
    public void Verify_Fails_WhenSignatureIsTampered()
    {
        var signer = CreateSigner();
        var (exp, sig) = Parse(signer.BuildSignedPath(StoragePath, TimeSpan.FromMinutes(10)));

        var tampered = sig[..^1] + (sig[^1] == 'a' ? "b" : "a");

        Assert.False(signer.Verify(StoragePath, exp, tampered));
    }

    [Fact]
    public void Verify_Fails_WhenPathIsSwapped()
    {
        // Firmar una imagen no debe habilitar la lectura de la de otra consulta.
        var signer = CreateSigner();
        var (exp, sig) = Parse(signer.BuildSignedPath(StoragePath, TimeSpan.FromMinutes(10)));

        Assert.False(signer.Verify("consultas/00000000-0000-0000-0000-000000000000/original.jpg", exp, sig));
    }

    [Fact]
    public void Verify_Fails_WhenUrlHasExpired()
    {
        var signer = CreateSigner();
        var (_, sig) = Parse(signer.BuildSignedPath(StoragePath, TimeSpan.FromMinutes(10)));
        var expired = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();

        Assert.False(signer.Verify(StoragePath, expired, sig));
    }

    [Fact]
    public void Verify_Fails_WhenSigningKeyIsDifferent()
    {
        var (exp, sig) = Parse(CreateSigner().BuildSignedPath(StoragePath, TimeSpan.FromMinutes(10)));
        var otroServidor = CreateSigner("otra-clave-de-firma-completamente-distinta");

        Assert.False(otroServidor.Verify(StoragePath, exp, sig));
    }

    [Fact]
    public void Verify_Fails_WhenSignatureIsEmpty()
    {
        var signer = CreateSigner();

        Assert.False(signer.Verify(StoragePath, DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(), ""));
    }
}
