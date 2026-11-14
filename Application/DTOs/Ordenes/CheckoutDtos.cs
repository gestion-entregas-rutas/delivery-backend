namespace Application.DTOs;

public sealed record CheckoutItemRequest(Guid ProductoId, int Cantidad);
public sealed record MetodoPagoRequest(string? Tipo, string? Titular, string? Ultimos4, string? ReferenciaSimulada);
public sealed record DatosRetiroTiendaRequest(string CedulaQuienRetira, string NombreQuienRetira, string TelefonoContacto, DateOnly FechaRetiro, string FranjaHoraria);
public sealed record DatosDomicilioRequest(string DestinatarioNombre, string DestinatarioTelefono, string DireccionTexto, double Latitud, double Longitud, DateOnly FechaEntrega, TimeOnly HoraEntrega, string? Observaciones);

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
    DateTimeOffset? ExpiraEn);

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
    string? EstadoSeguimiento);

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
