using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Application.Services;

public sealed class ResumenService(IAppDbContext db, TimeProvider reloj) : IResumenService
{
    public async Task<ResumenPedidosDto> ObtenerAsync(FiltroPedidos filtro, CancellationToken ct)
    {
        // Una sola consulta agrupada evita mezclar instantes distintos entre los indicadores.
        var grupos = await db.Pedidos.AsNoTracking().Filtrar(filtro)
            .GroupBy(p => new { p.Estado, p.ZonaId })
            .Select(g => new { g.Key.Estado, g.Key.ZonaId, Cantidad = g.Count() }).ToListAsync(ct);
        var estados = Enum.GetValues<EstadoPedido>().Select(e => new ConteoEstadoDto(e,
            grupos.Where(g => g.Estado == e).Sum(g => g.Cantidad))).ToList();
        var zonas = grupos.GroupBy(g => g.ZonaId).Select(g => new ConteoZonaDto(g.Key, g.Sum(x => x.Cantidad))).OrderBy(g => g.ZonaId).ToList();
        var total = grupos.Sum(g => g.Cantidad);
        var noCancelados = grupos.Where(g => g.Estado != EstadoPedido.Cancelado).Sum(g => g.Cantidad);
        var entregados = grupos.Where(g => g.Estado == EstadoPedido.Entregado).Sum(g => g.Cantidad);
        return new(total, noCancelados, entregados, noCancelados == 0 ? 0 : Math.Round(100m * entregados / noCancelados, 2),
            estados, zonas, reloj.GetUtcNow());
    }
}
