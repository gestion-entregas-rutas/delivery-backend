using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

namespace Infrastructure.ExternalServices;

/// <summary>
/// Planificador CVRP propio (sin librerías externas): agrupa por zona, respeta capacidad por ruta
/// (mínimo entre zona y repartidor), considera ventanas de tiempo por HoraEntrega y optimiza el orden
/// de visita con heurística de inserción por cercanía.
/// </summary>
public sealed class PlanificadorRutasPropio : IPlanificadorRutas
{
    public IReadOnlyList<AsignacionPlanificada> Planificar(
        Zona zona,
        IReadOnlyList<Pedido> pedidosPendientes,
        IReadOnlyList<Repartidor> repartidoresDisponibles)
    {
        if (pedidosPendientes.Count == 0 || repartidoresDisponibles.Count == 0) return [];

        var vehiculos = ConstruirVehiculos(zona, repartidoresDisponibles, pedidosPendientes.Count);
        if (vehiculos.Count == 0) return [];

        // Ventana/precedencia por HoraEntrega; desempate por id para estabilidad.
        var pedidos = pedidosPendientes
            .OrderBy(p => p.HoraEntrega)
            .ThenBy(p => p.Id)
            .ToList();

        var depot = Centro(pedidos);

        foreach (var pedido in pedidos)
        {
            var mejor = SeleccionarVehiculo(vehiculos, pedido, depot);
            mejor?.Pedidos.Add(pedido);
        }

        var resultado = new List<AsignacionPlanificada>();
        foreach (var v in vehiculos.Where(v => v.Pedidos.Count > 0))
        {
            var ordenados = OrdenarRuta(v.Pedidos, depot);
            resultado.Add(new AsignacionPlanificada(v.Repartidor, ordenados));
        }

        return resultado;
    }

    private static List<Vehiculo> ConstruirVehiculos(Zona zona, IReadOnlyList<Repartidor> repartidores, int totalPedidos)
    {
        var candidatos = repartidores
            .Select(r => new Vehiculo(r, Math.Min(zona.MaxPedidosPorRuta, r.CapacidadMaxima)))
            .Where(v => v.Capacidad > 0)
            .OrderByDescending(v => v.Capacidad)
            .ThenBy(v => v.Repartidor.Id)
            .ToList();

        if (candidatos.Count == 0) return candidatos;

        // Limitar a los vehículos estrictamente necesarios (capacidad efectiva total) para no
        // fragmentar rutas cuando hay repartidores de sobra.
        var seleccionados = new List<Vehiculo>();
        var capacidadAcumulada = 0;
        foreach (var v in candidatos)
        {
            seleccionados.Add(v);
            capacidadAcumulada += v.Capacidad;
            if (capacidadAcumulada >= totalPedidos) break;
        }
        return seleccionados;
    }

    private static Vehiculo? SeleccionarVehiculo(List<Vehiculo> vehiculos, Pedido pedido, GeoPoint depot)
    {
        Vehiculo? mejor = null;
        var mejorCosto = double.MaxValue;

        foreach (var v in vehiculos)
        {
            if (v.Pedidos.Count >= v.Capacidad) continue;
            var costo = CostoInsercion(v, pedido, depot);
            // Preferir vehículo con mejor costo; en empate, el que tenga más carga relativa para concentrar
            // y dejar capacidad disponible en otros (favorece compactación) o el menos cargado si igual.
            if (costo < mejorCosto ||
                (Math.Abs(costo - mejorCosto) < 1e-9 && mejor is not null && v.Pedidos.Count > mejor.Pedidos.Count) ||
                (Math.Abs(costo - mejorCosto) < 1e-9 && mejor is null))
            {
                mejorCosto = costo;
                mejor = v;
            }
        }

        return mejor ?? vehiculos.FirstOrDefault(v => v.Pedidos.Count < v.Capacidad);
    }

    private static double CostoInsercion(Vehiculo v, Pedido pedido, GeoPoint depot)
    {
        if (v.Pedidos.Count == 0)
            return pedido.Ubicacion.DistanciaKm(depot);

        var ultimo = v.Pedidos[^1];
        var distancia = ultimo.Ubicacion.DistanciaKm(pedido.Ubicacion);
        // Penalizar fuertemente invertir el orden temporal (HoraEntrega) para respetar ventanas.
        var penalizacionTiempo = pedido.HoraEntrega < ultimo.HoraEntrega ? 10_000 : 0;
        // Leve sesgo por carga para balancear cuando las distancias son similares.
        var sesgoCarga = v.Pedidos.Count * 0.01;
        return distancia + penalizacionTiempo + sesgoCarga;
    }

    private static IReadOnlyList<Pedido> OrdenarRuta(List<Pedido> pedidos, GeoPoint depot)
    {
        // Respeta ventanas de tiempo: orden principal por HoraEntrega. Dentro del mismo horario,
        // optimiza el recorrido por cercanía (vecino más cercano) para reducir distancia.
        var resultado = new List<Pedido>(pedidos.Count);
        var grupos = pedidos
            .GroupBy(p => p.HoraEntrega)
            .OrderBy(g => g.Key)
            .Select(g => g.ToList());

        var actual = depot;
        foreach (var grupo in grupos)
        {
            var restantes = new List<Pedido>(grupo);
            while (restantes.Count > 0)
            {
                var siguienteIdx = 0;
                var mejorDist = double.MaxValue;
                for (var i = 0; i < restantes.Count; i++)
                {
                    var d = actual.DistanciaKm(restantes[i].Ubicacion);
                    if (d < mejorDist || (Math.Abs(d - mejorDist) < 1e-12 && restantes[i].Id.CompareTo(restantes[siguienteIdx].Id) < 0))
                    {
                        mejorDist = d;
                        siguienteIdx = i;
                    }
                }
                var elegido = restantes[siguienteIdx];
                restantes.RemoveAt(siguienteIdx);
                resultado.Add(elegido);
                actual = elegido.Ubicacion;
            }
        }

        return resultado;
    }

    private static GeoPoint Centro(IReadOnlyCollection<Pedido> pedidos)
    {
        var lat = pedidos.Average(p => p.Ubicacion.Latitud);
        var lon = pedidos.Average(p => p.Ubicacion.Longitud);
        return new GeoPoint(lat, lon);
    }

    private sealed class Vehiculo(Repartidor repartidor, int capacidad)
    {
        public Repartidor Repartidor { get; } = repartidor;
        public int Capacidad { get; } = capacidad;
        public List<Pedido> Pedidos { get; } = [];
    }
}
