namespace RTattooAI.Application.Abstractions;

/// <summary>
/// Firma las URLs de lectura de imágenes. Sustituye a las SAS de Azure Blob cuando
/// el almacenamiento es local: el navegador no puede enviar el header Authorization
/// en un &lt;img src&gt;, así que la autorización viaja como firma HMAC con caducidad.
/// </summary>
public interface IFileUrlSigner
{
    /// <summary>Devuelve la ruta relativa firmada (p. ej. "/api/files/consultas/.../original.jpg?exp=...&amp;sig=...").</summary>
    string BuildSignedPath(string storagePath, TimeSpan validFor);

    /// <summary>Valida la firma y la caducidad de una ruta. No consulta el disco.</summary>
    bool Verify(string storagePath, long expiresAtUnixSeconds, string signature);
}
