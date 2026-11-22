using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Bands;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Validation;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Services;

public class BandsService : IBandsService
{
    private const int MaxNombre = 150;
    private const int MaxAlias = 150;
    private const int MaxRegion = 100;
    private const int MaxDescripcion = 500;

    private readonly IBandaCriminalRepository _repository;

    public BandsService(IBandaCriminalRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<BandDto>> GetAllAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var bandas = await _repository.GetAllAsync(includeInactive, cancellationToken);
        var conteos = await _repository.CountTatuajesPorBandaAsync(cancellationToken);

        return bandas.Select(b => ToDto(b, conteos)).ToList();
    }

    public async Task<BandDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var banda = await _repository.GetByIdAsync(id, cancellationToken);
        if (banda is null)
        {
            return null;
        }

        var conteos = await _repository.CountTatuajesPorBandaAsync(cancellationToken);
        return ToDto(banda, conteos);
    }

    public async Task<BandDto> CreateAsync(SaveBandRequestDto request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var nombre = EnsureValid(request);

        if (await _repository.GetByNombreAsync(nombre, cancellationToken) is not null)
        {
            throw new InvalidRequestException($"Ya existe una banda registrada con el nombre '{nombre}'.");
        }

        var banda = new BandaCriminal
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Alias = Trim(request.Alias, MaxAlias),
            Region = Trim(request.Region, MaxRegion),
            Descripcion = Trim(request.Description, MaxDescripcion),
            NivelPeligro = BandDangerLevel.Normalize(request.DangerLevel),
            EstaActivo = true,
            RegistradoPorId = usuarioId
        };

        await _repository.AddAsync(banda, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return ToDto(banda, new Dictionary<Guid, int>());
    }

    public async Task<BandDto?> UpdateAsync(Guid id, SaveBandRequestDto request, CancellationToken cancellationToken = default)
    {
        var banda = await _repository.GetByIdAsync(id, cancellationToken);
        if (banda is null)
        {
            return null;
        }

        var nombre = EnsureValid(request);

        var conMismoNombre = await _repository.GetByNombreAsync(nombre, cancellationToken);
        if (conMismoNombre is not null && conMismoNombre.Id != id)
        {
            throw new InvalidRequestException($"Ya existe una banda registrada con el nombre '{nombre}'.");
        }

        banda.Nombre = nombre;
        banda.Alias = Trim(request.Alias, MaxAlias);
        banda.Region = Trim(request.Region, MaxRegion);
        banda.Descripcion = Trim(request.Description, MaxDescripcion);
        banda.NivelPeligro = BandDangerLevel.Normalize(request.DangerLevel);

        await _repository.SaveChangesAsync(cancellationToken);

        var conteos = await _repository.CountTatuajesPorBandaAsync(cancellationToken);
        return ToDto(banda, conteos);
    }

    public async Task<BandDto?> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        var banda = await _repository.GetByIdAsync(id, cancellationToken);
        if (banda is null)
        {
            return null;
        }

        banda.EstaActivo = active;
        await _repository.SaveChangesAsync(cancellationToken);

        var conteos = await _repository.CountTatuajesPorBandaAsync(cancellationToken);
        return ToDto(banda, conteos);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var banda = await _repository.GetByIdAsync(id, cancellationToken);
        if (banda is null)
        {
            return false;
        }

        var conteos = await _repository.CountTatuajesPorBandaAsync(cancellationToken);
        if (conteos.TryGetValue(id, out var tatuajes) && tatuajes > 0)
        {
            throw new ConflictException(
                $"La banda tiene {tatuajes} tatuaje(s) en el catálogo. Reasígnelos o desactive la banda en vez de borrarla.");
        }

        _repository.Remove(banda);
        await _repository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string EnsureValid(SaveBandRequestDto request)
    {
        var nombre = request.Name?.Trim() ?? string.Empty;

        if (nombre.Length == 0)
        {
            throw new InvalidRequestException("El nombre de la banda es obligatorio.");
        }

        if (nombre.Length > MaxNombre)
        {
            throw new InvalidRequestException($"El nombre de la banda no puede superar {MaxNombre} caracteres.");
        }

        if (!BandDangerLevel.IsValid(request.DangerLevel))
        {
            throw new InvalidRequestException(
                $"Nivel de peligro inválido. Valores admitidos: {string.Join(", ", BandDangerLevel.All)}.");
        }

        return nombre;
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static BandDto ToDto(BandaCriminal banda, IReadOnlyDictionary<Guid, int> conteos) => new(
        banda.Id,
        banda.Nombre,
        banda.Alias,
        banda.Region,
        banda.NivelPeligro,
        banda.Descripcion,
        banda.EstaActivo,
        conteos.TryGetValue(banda.Id, out var total) ? total : 0,
        banda.FechaRegistro);
}
