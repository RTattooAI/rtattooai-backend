namespace RTattooAI.Application.Dtos.Bands;

public record BandDto(
    Guid Id,
    string Name,
    string? Alias,
    string? Region,
    string? DangerLevel,
    string? Description,
    bool Active,
    int TattooCount,
    DateTime CreatedAt);

/// <summary>Alta y edición de una banda (RF-10). El nombre es la clave natural.</summary>
public record SaveBandRequestDto(
    string Name,
    string? Alias = null,
    string? Region = null,
    string? DangerLevel = null,
    string? Description = null);

public record UpdateBandStatusRequestDto(bool Active);
