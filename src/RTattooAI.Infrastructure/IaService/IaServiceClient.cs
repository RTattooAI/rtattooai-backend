using System.Net.Http.Headers;
using System.Net.Http.Json;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Ia;
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
                .ToList());
    }

    public async Task<EnrollResultDto> EnrollAsync(Stream image, string fileName, string contentType, string band, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var imageContent = new StreamContent(image);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(imageContent, "image", fileName);
        content.Add(new StringContent(band), "band");

        using var response = await _httpClient.PostAsync("/enroll", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<EnrollResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("El servicio IA devolvió una respuesta vacía.");

        return new EnrollResultDto(body.CatalogId, body.Band);
    }
}
