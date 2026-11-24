
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
    int? TiempoServicioMinutos = null,
    // true (panel con selector de horarios): el intervalo debe ser de la oferta y se vuelve a comprobar
    // con bloqueo por fecha, igual que en el checkout. false conserva el registro libre anterior.
    bool VerificarDisponibilidad = false);
