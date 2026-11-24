using Domain.Enums;

namespace Application.DTOs;

public sealed record RutaDto(
    Guid Id,
    DateOnly Fecha,
    Guid ZonaId,
    Guid RepartidorId,
    int Capacidad,
    EstadoRuta Estado,
    IReadOnlyList<ParadaDto> Paradas,
    uint Version = 0,
    PuntoDto? Origen = null,
    DateTimeOffset? SalidaProgramadaEn = null,
    DateTimeOffset? EstimacionesActualizadasEn = null,
    string? ModeloEstimacion = null,
    double? DistanciaEstimadaKm = null,
    IReadOnlyList<AlertaPuntualidadDto>? Alertas = null);
