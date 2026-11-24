using Application.DTOs;
using Domain.ValueObjects;

namespace Application.Interfaces;

public interface IDisponibilidadEntregas
{
    /// <summary>Intervalos del día para la dirección, marcados como disponibles o no. No reserva nada.</summary>
    Task<IntervalosDisponiblesDto> ListarAsync(DateOnly fecha, double latitud, double longitud, CancellationToken ct);

    /// <summary>
    /// Comprueba de nuevo un intervalo elegido. Debe llamarse dentro de la transacción y con el bloqueo
    /// de planificación de la fecha tomado. Lanza 422 si el intervalo no es de la rejilla y
    /// 409 INTERVALO_NO_DISPONIBLE si ya no cabe.
    /// </summary>
    Task VerificarAsync(DateOnly fecha, GeoPoint punto, DateTimeOffset inicio, DateTimeOffset fin, CancellationToken ct);
}
