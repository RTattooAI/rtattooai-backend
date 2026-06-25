namespace RTattooAI.Application.Abstractions;

public interface IBlobStorage
{
    Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default);

    string GetReadUrl(string blobPath, TimeSpan validFor);
}
