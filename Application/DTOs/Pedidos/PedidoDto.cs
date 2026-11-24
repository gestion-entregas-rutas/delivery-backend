using Domain.Enums;
using Domain.Services;

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
    DateTimeOffset? EntregadoEn,
    DetallesPedidoDto? Detalles = null,
    uint Version = 0,
    DateTimeOffset? CompromisoInicioEn = null,
    DateTimeOffset? CompromisoFinEn = null,
    int? TiempoServicioMinutos = null,
    ResultadoPuntualidad? Puntualidad = null)
{
    public string DestinatarioNombre => ClienteNombre;
    public string DestinatarioTelefono => ClienteTelefono;
}
