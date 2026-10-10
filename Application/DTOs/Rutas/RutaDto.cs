using Domain.Enums;

namespace Application.DTOs;

public sealed record RutaDto(
    Guid Id,
    DateOnly Fecha,
    Guid ZonaId,
    Guid RepartidorId,
    int Capacidad,
    EstadoRuta Estado,
    IReadOnlyList<ParadaDto> Paradas);
