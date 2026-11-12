namespace Application.DTOs;

/// <summary>Última ubicación conocida, no una garantía de posición actual. La frescura se evalúa al consultar.</summary>
public sealed record UbicacionRepartidorDto(Guid RepartidorId, Guid RutaId, double Latitud, double Longitud,
    double PrecisionMetros, DateTimeOffset CapturadaEn, DateTimeOffset RecibidaEn, bool EsReciente);
