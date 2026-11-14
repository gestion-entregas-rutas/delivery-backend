using Application.DTOs;

namespace Application.Interfaces;

/// <summary>Acciones del repartidor desde la app móvil.</summary>
public interface ISeguimientoService
{
    Task<PedidoDto> MarcarEnCaminoAsync(Guid pedidoId, CancellationToken ct = default);
    Task<PedidoDto> RegistrarEntregaAsync(Guid pedidoId, CancellationToken ct = default);
}
