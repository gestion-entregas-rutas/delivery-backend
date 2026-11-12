using Application.DTOs;
namespace Application.Interfaces;
public interface IResumenService
{
    Task<ResumenPedidosDto> ObtenerAsync(FiltroPedidos filtro, CancellationToken ct);
}
