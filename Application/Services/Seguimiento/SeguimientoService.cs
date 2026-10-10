using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class SeguimientoService(
    IAppDbContext db,
    INotificadorEventos notificador,
    TimeProvider reloj) : ISeguimientoService
{
    public async Task<PedidoDto> MarcarEnCaminoAsync(Guid pedidoId, CancellationToken ct = default)
    {
        var (ruta, _) = await CargarRutaDelPedidoAsync(pedidoId, ct);
        ruta.MarcarEnCamino(pedidoId);
        return await GuardarYNotificarAsync(ruta, pedidoId, ct);
    }

    public async Task<PedidoDto> RegistrarEntregaAsync(Guid pedidoId, CancellationToken ct = default)
    {
        var (ruta, repartidor) = await CargarRutaDelPedidoAsync(pedidoId, ct);
        ruta.RegistrarEntrega(pedidoId, repartidor, reloj.GetUtcNow());
        return await GuardarYNotificarAsync(ruta, pedidoId, ct);
    }

    private async Task<(Ruta Ruta, Repartidor Repartidor)> CargarRutaDelPedidoAsync(Guid pedidoId, CancellationToken ct)
    {
        var rutaId = await db.Pedidos.Where(p => p.Id == pedidoId).Select(p => p.RutaId).FirstOrDefaultAsync(ct);
        if (rutaId is null)
        {
            if (!await db.Pedidos.AnyAsync(p => p.Id == pedidoId, ct))
                throw new NoEncontradoException(nameof(Pedido), pedidoId);
            throw new DomainException("El pedido no está asignado a ninguna ruta.");
        }

        var ruta = await db.Rutas.ConParadas().FirstAsync(r => r.Id == rutaId, ct);
        var repartidor = await db.Repartidores.FirstAsync(r => r.Id == ruta.RepartidorId, ct);
        return (ruta, repartidor);
    }

    private async Task<PedidoDto> GuardarYNotificarAsync(Ruta ruta, Guid pedidoId, CancellationToken ct)
    {
        await db.GuardarAsync(ct);

        var pedido = ruta.Paradas.First(p => p.PedidoId == pedidoId).Pedido.ToDto();
        await notificador.PedidoActualizadoAsync(pedido, ct);
        await notificador.RutaActualizadaAsync(ruta.ToDto(), ct);
        return pedido;
    }
}
