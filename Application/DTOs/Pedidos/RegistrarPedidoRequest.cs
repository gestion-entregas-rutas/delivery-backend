
namespace Application.DTOs;

public sealed record RegistrarPedidoRequest(
    string ClienteNombre,
    string ClienteTelefono,
    string Direccion,
    double Latitud,
    double Longitud,
    DateOnly FechaEntrega,
    TimeOnly HoraEntrega,
    Guid? ZonaId = null,
    DetallesPedidoRequest? Detalles = null,
    DateTimeOffset? CompromisoInicioEn = null,
    DateTimeOffset? CompromisoFinEn = null,
    int? TiempoServicioMinutos = null);
