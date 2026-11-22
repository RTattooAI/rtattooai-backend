using System.Security.Cryptography;
using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Infrastructure.Blob;
using Xunit;

namespace RTattooAI.Application.Tests.Security;

/// <summary>Cifrado en reposo de las imágenes (RNF-06).</summary>
public class EncryptedBlobStorageTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] Plaintext = "contenido de una fotografía"u8.ToArray();

    /// <summary>Almacenamiento en memoria: sustituye al disco para poder inspeccionar lo escrito.</summary>
    private sealed class FakeStorage : IBlobStorage
    {
        public readonly Dictionary<string, byte[]> Files = [];

        public Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            Files[blobPath] = buffer.ToArray();
            return Task.CompletedTask;
        }

        public Task<byte[]> DownloadAsync(string blobPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files[blobPath]);

        public string GetReadUrl(string blobPath, TimeSpan validFor) => $"/api/files/{blobPath}";
    }

    [Fact]
    public async Task Upload_ThenDownload_ReturnsTheOriginalBytes()
    {
        var inner = new FakeStorage();
        var sut = new EncryptedBlobStorage(inner, Key);

        await sut.UploadAsync("consultas/1/original.jpg", new MemoryStream(Plaintext), "image/jpeg");
        var leido = await sut.DownloadAsync("consultas/1/original.jpg");

        Assert.Equal(Plaintext, leido);
    }

    [Fact]
    public async Task WhatReachesDiskIsNotThePlaintext()
    {
        var inner = new FakeStorage();
        var sut = new EncryptedBlobStorage(inner, Key);

        await sut.UploadAsync("consultas/1/original.jpg", new MemoryStream(Plaintext), "image/jpeg");

        var enDisco = inner.Files["consultas/1/original.jpg"];
        Assert.NotEqual(Plaintext, enDisco);
        Assert.DoesNotContain("fotografía", System.Text.Encoding.UTF8.GetString(enDisco), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SameContentTwice_ProducesDifferentCiphertext()
    {
        var inner = new FakeStorage();
        var sut = new EncryptedBlobStorage(inner, Key);

        await sut.UploadAsync("a.jpg", new MemoryStream(Plaintext), "image/jpeg");
        await sut.UploadAsync("b.jpg", new MemoryStream(Plaintext), "image/jpeg");

        // Nonce aleatorio por archivo: dos imágenes idénticas no deben delatarse entre sí.
        Assert.NotEqual(inner.Files["a.jpg"], inner.Files["b.jpg"]);
    }

    [Fact]
    public async Task FilesWrittenBeforeEncryptionWasEnabled_AreStillReadable()
    {
        var inner = new FakeStorage();
        inner.Files["consultas/viejo/original.jpg"] = Plaintext;

        var sut = new EncryptedBlobStorage(inner, Key);
        var leido = await sut.DownloadAsync("consultas/viejo/original.jpg");

        Assert.Equal(Plaintext, leido);
    }

    [Fact]
    public async Task TamperedFile_FailsInsteadOfReturningAlteredBytes()
    {
        var inner = new FakeStorage();
        var sut = new EncryptedBlobStorage(inner, Key);
        await sut.UploadAsync("consultas/1/original.jpg", new MemoryStream(Plaintext), "image/jpeg");

        var almacenado = inner.Files["consultas/1/original.jpg"];
        almacenado[^1] ^= 0xFF;

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            sut.DownloadAsync("consultas/1/original.jpg"));
    }

    [Fact]
    public async Task AnotherKeyCannotDecrypt()
    {
        var inner = new FakeStorage();
        await new EncryptedBlobStorage(inner, Key)
            .UploadAsync("consultas/1/original.jpg", new MemoryStream(Plaintext), "image/jpeg");

        var otra = new EncryptedBlobStorage(inner, RandomNumberGenerator.GetBytes(32));

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() =>
            otra.DownloadAsync("consultas/1/original.jpg"));
    }

    [Fact]
    public void ConstructorRejectsKeysThatAreNotAes256()
    {
        var inner = new Mock<IBlobStorage>().Object;

        Assert.Throws<ArgumentException>(() => new EncryptedBlobStorage(inner, RandomNumberGenerator.GetBytes(16)));
    }

    [Fact]
    public void GetReadUrlIsDelegatedUnchanged()
    {
        var sut = new EncryptedBlobStorage(new FakeStorage(), Key);

        Assert.Equal("/api/files/consultas/1/original.jpg", sut.GetReadUrl("consultas/1/original.jpg", TimeSpan.FromHours(1)));
    }
}
