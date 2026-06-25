using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using RTattooAI.Application.Abstractions;

namespace RTattooAI.Infrastructure.Blob;

public class AzureBlobStorage : IBlobStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorage(IConfiguration configuration)
    {
        var connectionString = configuration["Blob:ConnectionString"]
            ?? throw new InvalidOperationException("Blob:ConnectionString no está configurado.");
        var containerName = configuration["Blob:ContainerName"]
            ?? throw new InvalidOperationException("Blob:ContainerName no está configurado.");

        _container = new BlobContainerClient(connectionString, containerName);
        _container.CreateIfNotExists();
    }

    public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        await blobClient.UploadAsync(
            content,
            new BlobHttpHeaders { ContentType = contentType },
            cancellationToken: cancellationToken);
    }

    public async Task<byte[]> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        var result = await blobClient.DownloadContentAsync(cancellationToken);
        return result.Value.Content.ToArray();
    }

    public string GetReadUrl(string blobPath, TimeSpan validFor)
    {
        var blobClient = _container.GetBlobClient(blobPath);

        if (!blobClient.CanGenerateSasUri)
        {
            return blobClient.Uri.ToString();
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(validFor)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }
}
