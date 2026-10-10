using Domain.Enums;

namespace Application.DTOs;

public sealed record PedidoDto(
    Guid Id,
    string ClienteNombre,
    string ClienteTelefono,
    string Direccion,
    PuntoDto Ubicacion,
    Guid ZonaId,
    DateOnly FechaEntrega,
    TimeOnly HoraEntrega,
    EstadoPedido Estado,
    Guid? RutaId,
    DateTimeOffset RegistradoEn,
    DateTimeOffset? EntregadoEn);
