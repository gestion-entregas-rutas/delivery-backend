namespace Application.DTOs;

/// <param name="Motivo">Null si está disponible; "Vencido" o "SinCapacidad" en caso contrario.</param>
public sealed record IntervaloEntregaDto(DateTimeOffset InicioEn, DateTimeOffset FinEn, bool Disponible, string? Motivo);

/// <summary>
/// Oferta de intervalos para una dirección y fecha. Mostrar un intervalo NO lo reserva: el checkout
/// vuelve a comprobarlo y puede responder 409 INTERVALO_NO_DISPONIBLE.
/// </summary>
public sealed record IntervalosDisponiblesDto(
    DateOnly Fecha,
    Guid ZonaId,
    string ZonaNombre,
    int DuracionMinutos,
    string ModeloEstimacion,
    DateTimeOffset CalculadoEn,
    IReadOnlyList<IntervaloEntregaDto> Intervalos);

/// <summary>Alerta calculada (no persistida) de una parada que no cumpliría o ya no cumplió su plazo.</summary>
public sealed record AlertaPuntualidadDto(Guid PedidoId, int Orden, string Estado, int? RetrasoMinutos);
