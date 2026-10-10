using Application.DTOs;

namespace Application.Interfaces;

public interface IZonaService
{
    Task<ZonaDto> CrearAsync(CrearZonaRequest request, CancellationToken ct = default);
    Task<ZonaDto> ActualizarAsync(Guid id, ActualizarZonaRequest request, CancellationToken ct = default);
    Task<ZonaDto> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ZonaDto>> ListarAsync(CancellationToken ct = default);
}
