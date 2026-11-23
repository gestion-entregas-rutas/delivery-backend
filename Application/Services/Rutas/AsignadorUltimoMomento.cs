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
    TimeProvider reloj,
    EstimadorRutas estimador,
    ReglasPlanificacion reglas) : IAsignadorUltimoMomento
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

        // Con compromiso: inserción temporal validada (nunca el insertador geográfico sin tiempos).
        if (pedido.CompromisoFinEn.HasValue)
            return await AsignarConTiemposAsync(pedido, ct);

        var rutas = await db.Rutas.ConParadas()
            .Where(r => r.Fecha == pedido.FechaEntrega && r.ZonaId == pedido.ZonaId &&
                        (r.Estado == EstadoRuta.Planificada || r.Estado == EstadoRuta.EnCurso))
            .ToListAsync(ct);

        Ruta ruta;
        TipoAsignacion tipo;

        if (InsercionRutas.BuscarMejor(pedido, rutas.Where(r => r.SalidaProgramadaEn == null &&
                r.Paradas.All(p => p.Pedido.CompromisoFinEn == null))) is { } propuesta)
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

        return await GuardarAsync(pedido, ruta, tipo, ct);
    }

    private async Task<ResultadoAsignacionDto> GuardarAsync(Pedido pedido, Ruta ruta, TipoAsignacion tipo, CancellationToken ct)
    {
        await db.GuardarAsync(ct);

        await notificador.PedidoActualizadoAsync(pedido.ToDto(), ct);
        await notificador.RutaActualizadaAsync(ruta.ToDto(), ct);
        return new ResultadoAsignacionDto(tipo, ruta.Id);
    }

    /// <summary>
    /// Inserta en la ruta activa donde cumpla su compromiso sin poner en riesgo otras entregas
    /// (menor distancia adicional). Si ninguna sirve, abre una ruta con un repartidor libre desde
    /// el local. Si tampoco es posible, queda en espera para el próximo armado.
    /// </summary>
    private async Task<ResultadoAsignacionDto> AsignarConTiemposAsync(Pedido pedido, CancellationToken ct)
    {
        var rutas = await db.Rutas.ConParadas()
            .Where(r => r.Fecha == pedido.FechaEntrega && r.ZonaId == pedido.ZonaId &&
                        (r.Estado == EstadoRuta.Planificada || r.Estado == EstadoRuta.EnCurso))
            .ToListAsync(ct);

        (Ruta Ruta, int Posicion, double Costo)? mejor = null;
        foreach (var ruta in rutas.Where(r => r.TieneEspacio))
        {
            if (await estimador.ContextoAsync(ruta, ct) is not { } contexto) continue;
            var secuencia = ruta.Paradas.Select(p => p.Pedido).ToList();
            var antes = CalculadorTiemposRuta.Estimar(Restantes(secuencia), contexto);
            for (var i = ruta.PosicionMinimaInsercion; i <= secuencia.Count; i++)
            {
                var candidata = secuencia.ToList();
                candidata.Insert(i, pedido);
                var despues = CalculadorTiemposRuta.Estimar(Restantes(candidata), contexto);
                if (!CalculadorTiemposRuta.NoEmpeora(antes, despues, pedido.Id)) continue;
                var costo = despues.DistanciaEstimadaKm - antes.DistanciaEstimadaKm;
                if (mejor is null || costo < mejor.Value.Costo) mejor = (ruta, i, costo);
            }
        }

        if (mejor is { } elegido)
        {
            elegido.Ruta.AgregarPedido(pedido, elegido.Posicion);
            await estimador.ReestimarAsync(elegido.Ruta, ct);
            return await GuardarAsync(pedido, elegido.Ruta, TipoAsignacion.RutaExistente, ct);
        }

        var origen = await estimador.OrigenLocalAsync(ct);
        var repartidor = (await db.RepartidoresLibresAsync(pedido.FechaEntrega, ct)).FirstOrDefault();
        if (origen is null || repartidor is null ||
            OrigenSimulacion.RutasNuevas(reglas, pedido.FechaEntrega, origen, reloj.GetUtcNow()) is not { } nuevo ||
            CalculadorTiemposRuta.Evaluar([pedido], nuevo) is not { } estimacion)
            return new ResultadoAsignacionDto(TipoAsignacion.EnEspera, null);

        var zona = await db.Zonas.FirstAsync(z => z.Id == pedido.ZonaId, ct);
        var nueva = Ruta.Crear(zona, repartidor, pedido.FechaEntrega, reloj.GetUtcNow());
        nueva.AgregarPedido(pedido);
        nueva.AplicarEstimacion(estimacion, reloj.GetUtcNow());
        db.Rutas.Add(nueva);
        return await GuardarAsync(pedido, nueva, TipoAsignacion.RutaNueva, ct);
    }

    private static List<Pedido> Restantes(IEnumerable<Pedido> secuencia) =>
        secuencia.Where(p => p.Estado != EstadoPedido.Entregado).ToList();
}
