using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// Oferta y verificación de intervalos de entrega. Considera rutas activas (con su avance real),
/// pedidos pendientes, reservas de checkout vigentes y repartidores libres. Modelo aproximado
/// propio (Haversine ajustado), sin servicios externos de ruteo.
/// </summary>
public sealed class DisponibilidadEntregasService(
    IAppDbContext db,
    ReglasPlanificacion reglas,
    EstimadorRutas estimador,
    TimeProvider reloj) : IDisponibilidadEntregas
{
    public async Task<IntervalosDisponiblesDto> ListarAsync(DateOnly fecha, double latitud, double longitud, CancellationToken ct)
    {
        var punto = new GeoPoint(latitud, longitud);
        var escenario = await CargarAsync(fecha, punto, ct);
        var ahora = reloj.GetUtcNow();
        var referencia = SimuladorCapacidad.Simular(escenario.Zonas, escenario.Libres, escenario.ContextoNuevas);
        var intervalos = reglas.IntervalosDelDia(fecha).Select(i =>
        {
            if (i.Fin <= ahora) return new IntervaloEntregaDto(i.Inicio, i.Fin, false, "Vencido");
            var viable = SimuladorCapacidad.AdmiteCandidata(escenario.Zonas, escenario.Zona.Id,
                Candidata(punto, i.Inicio, i.Fin), escenario.Libres, escenario.ContextoNuevas, referencia);
            return new IntervaloEntregaDto(i.Inicio, i.Fin, viable, viable ? null : "SinCapacidad");
        }).ToList();
        return new IntervalosDisponiblesDto(fecha, escenario.Zona.Id, escenario.Zona.Nombre,
            reglas.DuracionIntervaloMinutos, "HaversineAjustadoV1", ahora, intervalos);
    }

    public async Task VerificarAsync(DateOnly fecha, GeoPoint punto, DateTimeOffset inicio, DateTimeOffset fin, CancellationToken ct)
    {
        if (reglas.FechaLocal(inicio) != fecha || !reglas.EsIntervaloOfertado(fecha, inicio, fin))
            throw new DomainException("El intervalo elegido no corresponde a la oferta de la fecha de entrega.");
        var datos = new Dictionary<string, object?> { ["compromisoInicioEn"] = inicio, ["compromisoFinEn"] = fin };
        if (fin <= reloj.GetUtcNow())
            throw new ConflictoException("El intervalo elegido ya venció. Elija otro.", "INTERVALO_NO_DISPONIBLE", datos);
        var escenario = await CargarAsync(fecha, punto, ct);
        if (!SimuladorCapacidad.AdmiteCandidata(escenario.Zonas, escenario.Zona.Id, Candidata(punto, inicio, fin),
                escenario.Libres, escenario.ContextoNuevas))
            throw new ConflictoException("El intervalo elegido ya no está disponible. Consulte de nuevo los intervalos.",
                "INTERVALO_NO_DISPONIBLE", datos);
    }

    private static DemandaEntrega Candidata(GeoPoint punto, DateTimeOffset inicio, DateTimeOffset fin) =>
        new(Guid.NewGuid(), punto, inicio, fin, null);

    private sealed record Escenario(Zona Zona, IReadOnlyList<ZonaEnSimulacion> Zonas,
        IReadOnlyList<VehiculoLibre> Libres, ContextoPlanificacion? ContextoNuevas);

    private async Task<Escenario> CargarAsync(DateOnly fecha, GeoPoint punto, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = reglas.FechaLocal(ahora);
        if (fecha < hoy) throw new DomainException("La fecha de entrega no puede estar en el pasado.");
        if (fecha > hoy.AddDays(reglas.DiasMaximosOferta))
            throw new DomainException($"Solo se ofrecen intervalos hasta {reglas.DiasMaximosOferta} días adelante.");

        var zonas = await db.Zonas.AsNoTracking().Where(z => z.Activa).OrderBy(z => z.Nombre).ToListAsync(ct);
        var zona = zonas.FirstOrDefault(z => z.Contiene(punto))
                   ?? throw new DomainException("La dirección no tiene cobertura de entrega en una zona activa.");
        var origen = await estimador.OrigenLocalAsync(ct)
                     ?? throw new DomainException("No hay ubicación del local configurada (PlanificacionRutas:OrigenLatitud y " +
                                                  "OrigenLongitud) ni rutas armadas con GPS; no se pueden calcular intervalos.");

        var rutas = await db.Rutas.AsNoTracking().ConParadas()
            .Where(r => r.Fecha == fecha && (r.Estado == EstadoRuta.Planificada || r.Estado == EstadoRuta.EnCurso))
            .ToListAsync(ct);
        var pendientes = await db.Pedidos.AsNoTracking()
            .Where(p => p.FechaEntrega == fecha && p.Estado == EstadoPedido.Pendiente).ToListAsync(ct);
        // Reservas temporales: órdenes a domicilio con intervalo y pago pendiente que no venció.
        var reservas = await db.Ordenes.AsNoTracking()
            .Where(o => o.TipoEntrega == TipoEntrega.Domicilio && o.Estado == EstadoOrden.PendientePago &&
                        o.FechaEntrega == fecha && o.CompromisoFinEn != null && o.PedidoLogisticoId == null &&
                        o.Latitud != null && o.Longitud != null &&
                        db.PagosOrden.Any(p => p.OrdenId == o.Id && p.Estado == EstadoPago.Pendiente &&
                                               (p.ExpiraEn == null || p.ExpiraEn > ahora)))
            .ToListAsync(ct);

        var ocupados = rutas.Select(r => r.RepartidorId).ToHashSet();
        var libres = (await db.Repartidores.AsNoTracking()
                .Where(r => r.Estado != EstadoRepartidor.NoDisponible).OrderBy(r => r.Id).ToListAsync(ct))
            .Where(r => !ocupados.Contains(r.Id))
            .Select(r => new VehiculoLibre(r.Id, r.CapacidadMaxima)).ToList();

        var reservasPorZona = reservas
            .Select(o => new DemandaEntrega(o.Id, new GeoPoint(o.Latitud!.Value, o.Longitud!.Value),
                o.CompromisoInicioEn, o.CompromisoFinEn, null))
            .Select(d => (Demanda: d, ZonaId: zonas.FirstOrDefault(z => z.Contiene(d.Ubicacion))?.Id))
            .Where(x => x.ZonaId.HasValue)
            .ToLookup(x => x.ZonaId!.Value, x => x.Demanda);

        var simulacion = new List<ZonaEnSimulacion>();
        foreach (var z in zonas)
        {
            var rutasZona = new List<RutaEnSimulacion>();
            foreach (var ruta in rutas.Where(r => r.ZonaId == z.Id))
                if (await estimador.ParaSimulacionAsync(ruta, ct) is { } enSimulacion) rutasZona.Add(enSimulacion);
            var demanda = pendientes.Where(p => p.ZonaId == z.Id).Select(DemandaEntrega.De)
                .Concat(reservasPorZona[z.Id]).ToList();
            simulacion.Add(new ZonaEnSimulacion(z.Id, z.MaxPedidosPorRuta, rutasZona, demanda));
        }

        return new Escenario(zona, SimuladorCapacidad.OrdenarParaCandidata(simulacion, zona.Id, zona.MaxPedidosPorRuta),
            libres, OrigenSimulacion.RutasNuevas(reglas, fecha, origen, ahora));
    }
}
