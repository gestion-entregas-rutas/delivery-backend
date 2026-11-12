using Application.DTOs;

namespace Application.Interfaces;

public interface IRepartidorService
{
    Task<RepartidorDto> CrearAsync(CrearRepartidorRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RepartidorDto>> ListarAsync(bool soloDisponibles, CancellationToken ct = default);
    Task<RepartidorDto> CambiarDisponibilidadAsync(Guid id, bool disponible, CancellationToken ct = default);

    /// <summary>Ruta activa (planificada o en curso) del repartidor en una fecha, o null si no tiene.</summary>
    Task<RutaDto?> ObtenerRutaAsignadaAsync(Guid id, DateOnly fecha, CancellationToken ct = default);
}
