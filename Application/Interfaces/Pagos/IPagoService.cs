namespace Application.Interfaces;

public sealed record OrdenPayPalCreada(string Id, string UrlAprobacion);
public sealed record CapturaPayPal(bool Completada, string Estado, string? IdCaptura, decimal? Importe, string? Moneda, string? MerchantId);

public interface IPayPalService
{
    Task<OrdenPayPalCreada> CrearOrdenAsync(Guid ordenId, string numeroOrden, decimal total, CancellationToken ct);
    Task<CapturaPayPal> CapturarAsync(string paypalOrderId, string requestId, CancellationToken ct);
}

public interface IPagoService
{
    /// <summary>Simula el inicio de un pago y devuelve una referencia de proveedor simulada.</summary>
    string CrearReferenciaSimulada(Guid ordenId, string? metodo, string? referenciaSolicitada);
    /// <summary>Simula la confirmación del pago (siempre válida en modo simulado).</summary>
    bool ConfirmarSimulado(string? referencia, string? referenciaEsperada);
}
