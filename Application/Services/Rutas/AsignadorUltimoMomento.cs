using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

internal sealed class AsignadorUltimoMomento(
    IAppDbContext db,
    INotificadorEventos notificador,
    TimeProvider reloj) : IAsignadorUltimoMomento
{
    private const int MaxIntentos = 3;
    public async Task<ResultadoAsignacionDto> AsignarAsync(Guid pedidoId, CancellationToken ct = default)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await IntentarAsync(pedidoId, ct);
            }
            catch (ConflictoException) when (intento < MaxIntentos)
            {
                db.LimpiarCambios();
            }
        }
    }
    private async Task<ResultadoAsignacionDto> IntentarAsync(Guid pedidoId, CancellationToken ct)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, ct)
                     ?? throw new NoEncontradoException(nameof(Pedido), pedidoId);

        if (pedido.Estado != EstadoPedido.Pendiente)
            return new ResultadoAsignacionDto(TipoAsignacion.EnEspera, pedido.RutaId);

        var rutas = await db.Rutas.ConParadas()
            .Where(r => r.Fecha == pedido.FechaEntrega && r.ZonaId == pedido.ZonaId &&
                        (r.Estado == EstadoRuta.Planificada || r.Estado == EstadoRuta.EnCurso))
            .ToListAsync(ct);

        Ruta ruta;
        TipoAsignacion tipo;

        if (InsercionRutas.BuscarMejor(pedido, rutas) is { } propuesta)
        {
            ruta = propuesta.Ruta;
            ruta.AgregarPedido(pedido, propuesta.Posicion);
            tipo = TipoAsignacion.RutaExistente;
        }
        else
        {
            var repartidor = (await db.RepartidoresLibresAsync(pedido.FechaEntrega, ct)).FirstOrDefault();
            if (repartidor is null)
                return new ResultadoAsignacionDto(TipoAsignacion.EnEspera, null);

            var zona = await db.Zonas.FirstAsync(z => z.Id == pedido.ZonaId, ct);
            ruta = Ruta.Crear(zona, repartidor, pedido.FechaEntrega, reloj.GetUtcNow());
            ruta.AgregarPedido(pedido);
            db.Rutas.Add(ruta);
            tipo = TipoAsignacion.RutaNueva;
        }

        await db.GuardarAsync(ct);

        await notificador.PedidoActualizadoAsync(pedido.ToDto(), ct);
        await notificador.RutaActualizadaAsync(ruta.ToDto(), ct);
        return new ResultadoAsignacionDto(tipo, ruta.Id);
    }
}
