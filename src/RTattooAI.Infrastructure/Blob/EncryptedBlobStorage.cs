using System.Security.Cryptography;
using RTattooAI.Application.Abstractions;

namespace RTattooAI.Infrastructure.Blob;

/// <summary>
/// Cifrado en reposo de las imágenes (RNF-06). Envuelve al almacenamiento real y
/// cifra con AES-256-GCM antes de escribir; al leer, descifra.
///
/// En Azure el cifrado en reposo lo daba el propio Blob Storage. Al pasar a
/// infraestructura propia esa garantía desapareció: los archivos quedaban en claro
/// en el volumen. Este envoltorio la restituye a nivel de aplicación.
///
/// Dos decisiones deliberadas:
///
/// - Los archivos escritos antes de activar el cifrado se siguen leyendo. Cada
///   archivo cifrado empieza por una cabecera propia; si no está, el contenido se
///   devuelve tal cual. Así activar el cifrado no invalida el material ya guardado,
///   aunque este solo quede protegido cuando se vuelva a escribir.
///
/// - GCM autentica además de cifrar: un archivo manipulado en disco falla al
///   descifrar en vez de devolver píxeles alterados en silencio.
///
/// La clave es irrecuperable: sin ella las imágenes cifradas se pierden. Debe
/// respaldarse junto con la copia de seguridad y nunca dentro del repositorio.
/// </summary>
public class EncryptedBlobStorage : IBlobStorage
{
    /// <summary>Marca de formato: "RTAI" + versión.</summary>
    private static readonly byte[] Magic = "RTAI1"u8.ToArray();

    private const int NonceSize = 12;   // recomendado para GCM
    private const int TagSize = 16;

    private readonly IBlobStorage _inner;
    private readonly byte[] _key;

    public EncryptedBlobStorage(IBlobStorage inner, byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("La clave de cifrado debe ser de 32 bytes (AES-256).", nameof(key));
        }

        _inner = inner;
        _key = key;
    }

    public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var plain = new MemoryStream();
        await content.CopyToAsync(plain, cancellationToken);

        using var cipher = new MemoryStream(Encrypt(plain.ToArray()));
        await _inner.UploadAsync(blobPath, cipher, contentType, cancellationToken);
    }

    public async Task<byte[]> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var stored = await _inner.DownloadAsync(blobPath, cancellationToken);
        return IsEncrypted(stored) ? Decrypt(stored) : stored;
    }

    public string GetReadUrl(string blobPath, TimeSpan validFor) => _inner.GetReadUrl(blobPath, validFor);

    private static bool IsEncrypted(byte[] content) =>
        content.Length >= Magic.Length + NonceSize + TagSize && content.AsSpan(0, Magic.Length).SequenceEqual(Magic);

    private byte[] Encrypt(byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var output = new byte[Magic.Length + NonceSize + TagSize + ciphertext.Length];
        Magic.CopyTo(output, 0);
        nonce.CopyTo(output, Magic.Length);
        tag.CopyTo(output, Magic.Length + NonceSize);
        ciphertext.CopyTo(output, Magic.Length + NonceSize + TagSize);

        return output;
    }

    private byte[] Decrypt(byte[] stored)
    {
        var nonce = stored.AsSpan(Magic.Length, NonceSize);
        var tag = stored.AsSpan(Magic.Length + NonceSize, TagSize);
        var ciphertext = stored.AsSpan(Magic.Length + NonceSize + TagSize);

        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}
