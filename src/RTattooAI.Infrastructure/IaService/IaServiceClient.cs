using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Exceptions;
using RTattooAI.Infrastructure.IaService.Dtos;

namespace RTattooAI.Infrastructure.IaService;

public class IaServiceClient : IIaServiceClient
{
    private readonly HttpClient _httpClient;

    public IaServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IdentifyResultDto> IdentifyAsync(Stream image, string fileName, string contentType, int topK = 5, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var imageContent = new StreamContent(image);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(imageContent, "image", fileName);
        content.Add(new StringContent(topK.ToString()), "top_k");

        using var response = await _httpClient.PostAsync("/identify", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<IdentifyResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("El servicio IA devolvió una respuesta vacía.");

        return new IdentifyResultDto(
            body.ImageWidth,
            body.ImageHeight,
            body.ModelVersion,
            body.ProcessingMs,
            body.Detections.Select(d => new IdentifyDetectionDto(
                new IdentifyBboxDto(d.Bbox.X, d.Bbox.Y, d.Bbox.Width, d.Bbox.Height),
                d.Confidence,
                d.Verdict,
                d.Matches.Select(m => new IdentifyMatchDto(m.CatalogId, m.Band, m.Similarity, m.ReferenceImageUrl)).ToList()))
                .ToList(),
            body.Threshold,
            body.Margin,
            body.ReviewThreshold);
    }

    public async Task<EnrollResultDto> EnrollAsync(Stream image, string fileName, string contentType, string band, string? catalogId = null, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var imageContent = new StreamContent(image);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(imageContent, "image", fileName);
        content.Add(new StringContent(band), "band");
        if (!string.IsNullOrWhiteSpace(catalogId))
        {
            content.Add(new StringContent(catalogId), "catalog_id");
        }

        using var response = await _httpClient.PostAsync("/enroll", content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict && catalogId is not null)
        {
            throw new CatalogIdTakenException(catalogId);
        }
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<EnrollResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("El servicio IA devolvió una respuesta vacía.");

        return new EnrollResultDto(body.CatalogId, body.Band);
    }

    public async Task<bool> RemoveFromGalleryAsync(string catalogId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"/gallery/{Uri.EscapeDataString(catalogId)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<GalleryStateDto?> GetGalleryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/gallery", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<GalleryResponse>(cancellationToken: cancellationToken);
            if (body is null)
            {
                return null;
            }

            return new GalleryStateDto(
                [.. body.Entries.Select(e => new GalleryEntryDto(e.CatalogId, e.Views, e.ImageSha256))],
                [.. body.SharedVectors.Select(g => (IReadOnlyList<string>)g)]);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<IaInfoDto?> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/info", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<InfoResponse>(cancellationToken: cancellationToken);
            if (body is null)
            {
                return null;
            }

            return new IaInfoDto(
                body.ModelVersion, body.Detector, body.Embedder, body.Head, body.EmbeddingDim,
                body.Threshold, body.Margin, body.DetConf, body.DetIou,
                body.GallerySize, body.GalleryIdentities, body.GalleryBands);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
