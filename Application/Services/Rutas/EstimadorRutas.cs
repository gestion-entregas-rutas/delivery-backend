using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// Recalcula tiempos de rutas existentes desde su punto real: el local para rutas planificadas
/// y, en rutas en curso, la posición más reciente conocida (GPS, última entrega o salida).
/// Sin un origen conocido no se estima: el resultado es "Sin estimación", nunca un dato inventado.
/// </summary>
public sealed class EstimadorRutas(IAppDbContext db, ReglasPlanificacion reglas, TimeProvider reloj)
{
    private GeoPoint? origenLocal;
    private bool origenCargado;

    /// <summary>
    /// Origen de rutas nuevas, en este orden: configuración (PlanificacionRutas:Origen*), ubicación del
    /// local guardada desde el panel y, si falta todo, el origen de la última ruta armada con GPS.
    /// </summary>
    public async Task<GeoPoint?> OrigenLocalAsync(CancellationToken ct)
    {
        if (origenCargado) return origenLocal;
        origenLocal = reglas.OrigenLocal;
        if (origenLocal is null)
        {
            var guardado = await db.UbicacionLocal.AsNoTracking()
                .Where(l => l.Id == UbicacionLocal.IdUnico)
                .Select(l => new { l.Latitud, l.Longitud })
                .FirstOrDefaultAsync(ct);
            if (guardado is not null) origenLocal = new GeoPoint(guardado.Latitud, guardado.Longitud);
        }
        if (origenLocal is null)
        {
            var ultima = await db.Rutas.AsNoTracking().Where(r => r.OrigenLatitud != null && r.OrigenLongitud != null)
                .OrderByDescending(r => r.CreadaEn).Select(r => new { r.OrigenLatitud, r.OrigenLongitud })
                .FirstOrDefaultAsync(ct);
            if (ultima is not null) origenLocal = new GeoPoint(ultima.OrigenLatitud!.Value, ultima.OrigenLongitud!.Value);
        }
        origenCargado = true;
        return origenLocal;
    }

    public async Task<ContextoPlanificacion?> ContextoAsync(Ruta ruta, CancellationToken ct)
    {
        if (ruta.Estado is not (EstadoRuta.Planificada or EstadoRuta.EnCurso) || ruta.ParadasRestantes.Count == 0)
            return null;
        var ahora = reloj.GetUtcNow();
        GeoPoint? origenRuta = ruta.OrigenLatitud is { } lat && ruta.OrigenLongitud is { } lon
            ? new GeoPoint(lat, lon) : await OrigenLocalAsync(ct);

        if (ruta.Estado == EstadoRuta.Planificada)
        {
            if (origenRuta is not { } local) return null;
            var programada = ruta.SalidaProgramadaEn ?? reglas.Instante(ruta.Fecha, reglas.InicioJornada);
            return reglas.ContextoOperativo(ruta.Fecha, local, programada > ahora ? programada : ahora);
        }

        // En curso: tomar la posición con el instante más reciente.
        var candidatos = new List<(GeoPoint Punto, DateTimeOffset En)>();
        if (origenRuta is { } salida) candidatos.Add((salida, ruta.IniciadaEn ?? DateTimeOffset.MinValue));
        if (ruta.Paradas.Where(p => p.Pedido.EntregadoEn.HasValue).MaxBy(p => p.Pedido.EntregadoEn) is { } entregada)
            candidatos.Add((entregada.Pedido.Ubicacion, entregada.Pedido.EntregadoEn!.Value));
        var gps = await db.Ubicaciones.AsNoTracking()
            .FirstOrDefaultAsync(u => u.RepartidorId == ruta.RepartidorId && u.RutaId == ruta.Id, ct);
        if (gps is not null && gps.CapturadaEn != default) candidatos.Add((gps.Punto, gps.CapturadaEn));
        if (candidatos.Count == 0) return null;
        var actual = candidatos.MaxBy(c => c.En);
        return reglas.ContextoOperativo(ruta.Fecha, actual.Punto, actual.En > ahora ? actual.En : ahora);
    }

    /// <summary>Estimación de las paradas restantes sin modificar la ruta.</summary>
    public async Task<EstimacionRuta?> CalcularAsync(Ruta ruta, CancellationToken ct) =>
        await ContextoAsync(ruta, ct) is { } contexto
            ? CalculadorTiemposRuta.Estimar(ruta.ParadasRestantes.Select(p => p.Pedido).ToList(), contexto)
            : null;

    /// <summary>Recalcula y guarda en la ruta (sin SaveChanges). Devuelve null si no hay origen conocido.</summary>
    public async Task<EstimacionRuta?> ReestimarAsync(Ruta ruta, CancellationToken ct)
    {
        var estimacion = await CalcularAsync(ruta, ct);
        if (estimacion is not null) ruta.ActualizarEstimaciones(estimacion, reloj.GetUtcNow());
        return estimacion;
    }

    /// <summary>Estado de la ruta para el simulador de capacidad. Null si no se puede estimar.</summary>
    public async Task<RutaEnSimulacion?> ParaSimulacionAsync(Ruta ruta, CancellationToken ct)
    {
        if (await ContextoAsync(ruta, ct) is not { } contexto) return null;
        var restantes = ruta.ParadasRestantes;
        var minima = restantes.Count(p => p.Orden <= ruta.PosicionMinimaInsercion);
        return new RutaEnSimulacion(ruta.Id, contexto, restantes.Select(p => DemandaEntrega.De(p.Pedido)).ToList(),
            ruta.Capacidad - ruta.Paradas.Count, minima);
    }

    /// <summary>
    /// Aplica un cambio sobre una o más rutas y comprueba que ninguna entrega que cumplía pase a
    /// exceder su compromiso. Sin <paramref name="forzar"/> rechaza con 409 CAMBIO_PONE_EN_RIESGO.
    /// Con <paramref name="forzar"/> acepta el cambio y las alertas quedan visibles en las estimaciones.
    /// </summary>
    public async Task ValidarCambioAsync(IReadOnlyList<Ruta> rutas, Action cambio, bool forzar, CancellationToken ct)
    {
        var previas = new HashSet<Guid>();
        foreach (var ruta in rutas)
            if (await CalcularAsync(ruta, ct) is { } antes) previas.UnionWith(antes.Atrasadas);

        cambio();

        var enRiesgo = new List<Dictionary<string, object?>>();
        foreach (var ruta in rutas)
        {
            if (await ReestimarAsync(ruta, ct) is not { } despues) continue;
            var pedidos = ruta.Paradas.ToDictionary(p => p.PedidoId, p => p.Pedido);
            foreach (var parada in despues.Paradas.Where(p => p.ExcedeCompromiso && !previas.Contains(p.PedidoId)))
            {
                var limite = pedidos[parada.PedidoId].CompromisoFinEn ?? despues.Contexto.FinJornada;
                enRiesgo.Add(new()
                {
                    ["pedidoId"] = parada.PedidoId,
                    ["rutaId"] = ruta.Id,
                    ["finalizacionEstimadaEn"] = parada.FinalizacionEn,
                    ["limiteEn"] = limite,
                    ["retrasoMinutos"] = limite is { } l ? (int)Math.Ceiling((parada.FinalizacionEn - l).TotalMinutes) : null
                });
            }
        }
        if (enRiesgo.Count > 0 && !forzar)
            throw new ConflictoException(
                "El cambio haría que otras entregas no se completen dentro de su intervalo comprometido. " +
                "Revise las alertas o confirme el cambio con forzar = true.",
                "CAMBIO_PONE_EN_RIESGO", new Dictionary<string, object?> { ["pedidosEnRiesgo"] = enRiesgo });
    }
}
