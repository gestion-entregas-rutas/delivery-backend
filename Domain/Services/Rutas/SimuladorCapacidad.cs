using Domain.ValueObjects;

namespace Domain.Services;

/// <summary>Ruta existente (planificada o en curso) tal como está ahora, con sus entregas restantes en orden.</summary>
/// <param name="CuposLibres">Capacidad de la ruta menos todas sus paradas (incluidas las entregadas).</param>
/// <param name="PosicionMinima">Entregas iniciales que no pueden moverse (por ejemplo, la que está en camino).</param>
public sealed record RutaEnSimulacion(Guid RutaId, ContextoPlanificacion Contexto,
    IReadOnlyList<DemandaEntrega> Restantes, int CuposLibres, int PosicionMinima = 0);

public sealed record VehiculoLibre(Guid RepartidorId, int CapacidadMaxima);

/// <param name="Pendientes">Pedidos pendientes y reservas temporales aún sin ruta.</param>
public sealed record ZonaEnSimulacion(Guid ZonaId, int MaxPedidosPorRuta,
    IReadOnlyList<RutaEnSimulacion> Rutas, IReadOnlyList<DemandaEntrega> Pendientes);

public sealed record ResultadoSimulacion(IReadOnlySet<Guid> Ubicadas, int RepartidoresUsados);

/// <summary>
/// Simulación propia de capacidad para una fecha: inserta lo pendiente en las rutas existentes
/// (sin alterar entregas fijas ni empeorar compromisos) y, si no cabe, abre rutas con repartidores
/// libres desde el local. No reserva nada ni modifica entidades. Es una heurística: que no ubique
/// una entrega no demuestra que no exista ninguna solución.
/// </summary>
public static class SimuladorCapacidad
{
    public static ResultadoSimulacion Simular(IReadOnlyList<ZonaEnSimulacion> zonas,
        IReadOnlyList<VehiculoLibre> libres, ContextoPlanificacion? contextoRutasNuevas)
    {
        var ubicadas = new HashSet<Guid>();
        var disponibles = libres.Where(v => v.CapacidadMaxima > 0).DistinctBy(v => v.RepartidorId)
            .OrderByDescending(v => v.CapacidadMaxima).ThenBy(v => v.RepartidorId).ToList();
        var usados = 0;

        foreach (var zona in zonas)
        {
            var rutas = zona.Rutas.Select(r => new Abierta(r.Contexto, r.Restantes.ToList(), r.CuposLibres, r.PosicionMinima))
                .ToList();
            foreach (var demanda in zona.Pendientes.DistinctBy(d => d.Id)
                         .OrderBy(d => d.Fin ?? DateTimeOffset.MaxValue).ThenBy(d => d.Id))
            {
                if (Insertar(rutas, demanda)) { ubicadas.Add(demanda.Id); continue; }
                // Abrir una ruta nueva desde el local si la entrega, sola, se puede cumplir.
                if (contextoRutasNuevas is null || disponibles.Count == 0 ||
                    CalculadorTiemposRuta.Evaluar([demanda], contextoRutasNuevas) is null) continue;
                var vehiculo = disponibles[0];
                disponibles.RemoveAt(0);
                usados++;
                rutas.Add(new Abierta(contextoRutasNuevas, [demanda],
                    Math.Min(zona.MaxPedidosPorRuta, vehiculo.CapacidadMaxima) - 1, 0));
                ubicadas.Add(demanda.Id);
            }
        }
        return new(ubicadas, usados);
    }

    /// <summary>
    /// ¿Se puede comprometer <paramref name="candidata"/> sin desplazar nada de lo que ya se ubicaba?
    /// Compara la simulación con y sin la candidata para no prometer dos veces la misma capacidad.
    /// <paramref name="zonas"/> debe venir de <see cref="OrdenarParaCandidata"/>.
    /// </summary>
    public static bool AdmiteCandidata(IReadOnlyList<ZonaEnSimulacion> zonas, Guid zonaCandidata,
        DemandaEntrega candidata, IReadOnlyList<VehiculoLibre> libres, ContextoPlanificacion? contextoRutasNuevas,
        ResultadoSimulacion? referencia = null)
    {
        referencia ??= Simular(zonas, libres, contextoRutasNuevas);
        var conCandidata = zonas.Select(z => z.ZonaId != zonaCandidata ? z :
            z with { Pendientes = z.Pendientes.Append(candidata).ToList() }).ToList();
        var resultado = Simular(conCandidata, libres, contextoRutasNuevas);
        return resultado.Ubicadas.Contains(candidata.Id) && referencia.Ubicadas.IsSubsetOf(resultado.Ubicadas);
    }

    /// <summary>
    /// Deja al final la zona de la candidata: las demás consumen repartidores libres igual
    /// con y sin candidata, y la comparación de <see cref="AdmiteCandidata"/> queda limpia.
    /// </summary>
    public static IReadOnlyList<ZonaEnSimulacion> OrdenarParaCandidata(IReadOnlyList<ZonaEnSimulacion> zonas,
        Guid zonaCandidata, int maxPedidosZonaCandidata)
    {
        var ordenadas = zonas.Where(z => z.ZonaId != zonaCandidata).Concat(zonas.Where(z => z.ZonaId == zonaCandidata)).ToList();
        if (ordenadas.All(z => z.ZonaId != zonaCandidata))
            ordenadas.Add(new ZonaEnSimulacion(zonaCandidata, maxPedidosZonaCandidata, [], []));
        return ordenadas;
    }

    private static bool Insertar(List<Abierta> rutas, DemandaEntrega demanda)
    {
        Abierta? elegida = null;
        var posicion = 0;
        var mejorCosto = double.MaxValue;
        foreach (var ruta in rutas.Where(r => r.CuposLibres > 0))
        {
            var antes = CalculadorTiemposRuta.Estimar(ruta.Secuencia, ruta.Contexto);
            for (var i = ruta.PosicionMinima; i <= ruta.Secuencia.Count; i++)
            {
                var candidata = ruta.Secuencia.ToList();
                candidata.Insert(i, demanda);
                var despues = CalculadorTiemposRuta.Estimar(candidata, ruta.Contexto);
                if (!CalculadorTiemposRuta.NoEmpeora(antes, despues, demanda.Id)) continue;
                var costo = despues.DistanciaEstimadaKm - antes.DistanciaEstimadaKm;
                if (costo >= mejorCosto) continue;
                mejorCosto = costo; elegida = ruta; posicion = i;
            }
        }
        if (elegida is null) return false;
        elegida.Secuencia.Insert(posicion, demanda);
        elegida.CuposLibres--;
        return true;
    }

    private sealed class Abierta(ContextoPlanificacion contexto, List<DemandaEntrega> secuencia, int cupos, int minima)
    {
        public ContextoPlanificacion Contexto { get; } = contexto;
        public List<DemandaEntrega> Secuencia { get; } = secuencia;
        public int CuposLibres { get; set; } = cupos;
        public int PosicionMinima { get; } = minima;
    }
}

/// <summary>Punto de salida para rutas nuevas cuando se simula la oferta.</summary>
public static class OrigenSimulacion
{
    public static ContextoPlanificacion? RutasNuevas(ReglasPlanificacion reglas, DateOnly fecha, GeoPoint? origen, DateTimeOffset ahora)
    {
        if (origen is not { } punto) return null;
        try { return reglas.CrearContexto(fecha, punto, ahora); }
        catch (Exceptions.DomainException) { return null; } // jornada ya cerrada para hoy
    }
}
