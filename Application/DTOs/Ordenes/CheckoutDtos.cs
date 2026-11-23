namespace Application.DTOs;

public sealed record CheckoutItemRequest(Guid ProductoId, int Cantidad);
public sealed record MetodoPagoRequest(string? Tipo, string? Titular, string? Ultimos4, string? ReferenciaSimulada);
public sealed record DatosRetiroTiendaRequest(string CedulaQuienRetira, string NombreQuienRetira, string TelefonoContacto, DateOnly FechaRetiro, string FranjaHoraria);
/// <param name="CompromisoInicioEn">Intervalo elegido de GET /api/entregas/intervalos (opcional por compatibilidad).
/// Si se envía, el backend lo vuelve a comprobar y lo reserva mientras el pago esté pendiente.</param>
public sealed record DatosDomicilioRequest(string DestinatarioNombre, string DestinatarioTelefono, string DireccionTexto, double Latitud, double Longitud, DateOnly FechaEntrega, TimeOnly HoraEntrega, string? Observaciones,
    DateTimeOffset? CompromisoInicioEn = null, DateTimeOffset? CompromisoFinEn = null);

public sealed record CheckoutRequest(
    List<CheckoutItemRequest> Items,
    MetodoPagoRequest? MetodoPago,
    string TipoEntrega,
    DatosRetiroTiendaRequest? DatosRetiroTienda,
    DatosDomicilioRequest? DatosDomicilio);

public sealed record CheckoutResponse(
    Guid OrdenId,
    string NumeroOrden,
    string EstadoOrden,
    string EstadoPago,
    decimal Total,
    string TipoEntrega,
    string? CodigoRetiro,
    Guid? PedidoLogisticoId,
    string? EstadoSeguimiento,
    Guid? PagoId,
    string? ReferenciaPago,
    DateTimeOffset? ExpiraEn,
    DateTimeOffset? CompromisoInicioEn = null,
    DateTimeOffset? CompromisoFinEn = null);

public sealed record OrdenMiaDto(
    Guid OrdenId,
    string NumeroOrden,
    string EstadoOrden,
    string EstadoPago,
    decimal Total,
    string TipoEntrega,
    DateTimeOffset CreadaEn,
    string? CodigoRetiro,
    Guid? PedidoLogisticoId,
    string? EstadoSeguimiento,
    DateTimeOffset? CompromisoInicioEn = null,
    DateTimeOffset? CompromisoFinEn = null,
    DateTimeOffset? FinalizacionEstimadaEn = null,
    Domain.Services.ResultadoPuntualidad? Puntualidad = null);

public sealed record RetiroTiendaDto(
    Guid OrdenId,
    string NumeroOrden,
    string EstadoOrden,
    decimal Total,
    DateTimeOffset CreadaEn,
    string? CodigoRetiro,
    string? CedulaQuienRetira,
    string? NombreQuienRetira,
    string? TelefonoContacto,
    DateOnly? FechaRetiro,
    string? FranjaHoraria);

public sealed record ConfirmarPagoRequest(string? Referencia);
public sealed record CancelarPagoRequest(string? Motivo);
public sealed record InicioPagoPayPalResponse(Guid OrdenId, string PayPalOrderId, string UrlAprobacion, string EstadoPago);
