using Application.DTOs;

namespace Application.Interfaces;

public interface IUbicacionLocalService
{
    /// <summary>Ubicación guardada del local o null si todavía no se configuró.</summary>
    Task<UbicacionLocalDto?> ObtenerAsync(CancellationToken ct = default);

    /// <summary>Crea o reemplaza la ubicación del local.</summary>
    Task<UbicacionLocalDto> GuardarAsync(GuardarUbicacionLocalRequest request, CancellationToken ct = default);
}
