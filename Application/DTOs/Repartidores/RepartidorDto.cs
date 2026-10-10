using Domain.Enums;

namespace Application.DTOs;

public sealed record RepartidorDto(
    Guid Id,
    string Nombre,
    string Telefono,
    int CapacidadMaxima,
    EstadoRepartidor Estado);
