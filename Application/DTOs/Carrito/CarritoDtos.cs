namespace Application.DTOs;

public sealed record AgregarItemCarritoRequest(Guid ProductoId, int Cantidad);
public sealed record ActualizarItemCarritoRequest(int Cantidad);
public sealed record MergeCarritoRequest(string? TokenInvitado);

public sealed record CarritoItemDto(
    Guid ProductoId,
    string Nombre,
    string ImagenPrincipalUrl,
    decimal PrecioUnitario,
    decimal? PrecioOferta,
    decimal PrecioAplicado,
    int Cantidad,
    int StockDisponible,
    bool Activo,
    bool Disponible,
    decimal Subtotal,
    string? Advertencia);

public sealed record CarritoDto(
    Guid Id,
    string? TokenInvitado,
    IReadOnlyList<CarritoItemDto> Items,
    decimal Subtotal,
    decimal Descuento,
    decimal Envio,
    decimal Total,
    IReadOnlyList<string> Advertencias);

public sealed record CotizacionItemDto(
    Guid ProductoId,
    string Nombre,
    int CantidadSolicitada,
    int CantidadFinal,
    decimal PrecioUnitario,
    decimal Subtotal,
    bool Disponible,
    string? Advertencia);

public sealed record CotizacionDto(
    IReadOnlyList<CotizacionItemDto> Items,
    decimal Subtotal,
    decimal Descuento,
    decimal Envio,
    decimal Total,
    bool RequiereRevision,
    IReadOnlyList<string> Advertencias);
