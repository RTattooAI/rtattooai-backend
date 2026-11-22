namespace RTattooAI.Application.Dtos.Analyses;

/// <summary>
/// Una fotografía recibida para analizar. El servicio no conoce el tipo de la capa web,
/// solo el contenido y lo que el cliente declara sobre él (que después se verifica).
/// </summary>
/// <param name="Source">Procedencia declarada: <c>archivo</c> o <c>camara</c>.</param>
public record AnalysisImageInput(
    Stream Content,
    string FileName,
    string ContentType,
    long Length,
    string Source);
