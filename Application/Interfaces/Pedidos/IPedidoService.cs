using Application.DTOs;

namespace Application.Interfaces;

public interface IPedidoService
{
    /// <summary>Registra el pedido; si es para hoy, intenta asignarlo de inmediato.</summary>
    Task<RegistroPedidoDto> RegistrarAsync(RegistrarPedidoRequest request, CancellationToken ct = default);
    Task<PedidoDto> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<PaginaDto<PedidoDto>> ListarAsync(FiltroPedidos filtro, CancellationToken ct = default);
    Task<PedidoDto> CancelarAsync(Guid id, CancellationToken ct = default);
}
