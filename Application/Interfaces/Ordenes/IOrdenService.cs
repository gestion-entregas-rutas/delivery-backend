using Application.DTOs;

namespace Application.Interfaces;

public interface IOrdenService
{
    Task<CheckoutResponse> CheckoutAsync(Guid clienteId, CheckoutRequest request, string? idempotencyKey, CancellationToken ct);
    Task<CheckoutResponse> ConfirmarPagoAsync(Guid clienteId, Guid ordenId, string? referencia, string? idempotencyKey, CancellationToken ct);
    Task<CheckoutResponse> CancelarPagoAsync(Guid clienteId, Guid ordenId, string? motivo, CancellationToken ct);
    Task<IReadOnlyList<OrdenMiaDto>> ListarMiasAsync(Guid clienteId, CancellationToken ct);
    Task<IReadOnlyList<RetiroTiendaDto>> ListarRetirosTiendaAsync(CancellationToken ct);
    Task MarcarRetiradoAsync(Guid ordenId, CancellationToken ct);
}
