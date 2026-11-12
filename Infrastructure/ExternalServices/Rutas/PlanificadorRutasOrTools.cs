using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;

namespace Infrastructure.ExternalServices;

public sealed class PlanificadorRutasOrTools(int segundosLimite = 2) : IPlanificadorRutas
{
    private const long CostoFijoPorRutaMetros = 3_000;
    private const long PenalizacionPedidoOmitidoMetros = 100_000_000;

    public IReadOnlyList<AsignacionPlanificada> Planificar(
        Zona zona,
        IReadOnlyList<Pedido> pedidosPendientes,
        IReadOnlyList<Repartidor> repartidoresDisponibles)
    {
        if (pedidosPendientes.Count == 0 || repartidoresDisponibles.Count == 0) return [];

        var vehiculos = Math.Min(repartidoresDisponibles.Count, pedidosPendientes.Count);
        var puntos = new List<GeoPoint> { Centro(pedidosPendientes) };
        puntos.AddRange(pedidosPendientes.Select(p => p.Ubicacion));

        var matriz = MatrizDistanciasMetros(puntos);
        var nodos = puntos.Count;

        var manager = new RoutingIndexManager(nodos, vehiculos, 0);
        var routing = new RoutingModel(manager);

        var distancia = routing.RegisterTransitCallback((desde, hasta) =>
            matriz[manager.IndexToNode(desde), manager.IndexToNode(hasta)]);
        routing.SetArcCostEvaluatorOfAllVehicles(distancia);
        routing.SetFixedCostOfAllVehicles(CostoFijoPorRutaMetros);

        var demanda = routing.RegisterUnaryTransitCallback(i => manager.IndexToNode(i) == 0 ? 0 : 1);
        var capacidades = repartidoresDisponibles.Take(vehiculos)
            .Select(r => (long)Math.Min(zona.MaxPedidosPorRuta, r.CapacidadMaxima))
            .ToArray();
        routing.AddDimensionWithVehicleCapacity(demanda, 0, capacidades, true, "Capacidad");

        for (var nodo = 1; nodo < nodos; nodo++)
            routing.AddDisjunction([manager.NodeToIndex(nodo)], PenalizacionPedidoOmitidoMetros);

        var parametros = operations_research_constraint_solver.DefaultRoutingSearchParameters();
        parametros.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
        parametros.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
        parametros.TimeLimit = new Duration { Seconds = segundosLimite };

        var solucion = routing.SolveWithParameters(parametros);
        if (solucion is null) return [];

        var asignaciones = new List<AsignacionPlanificada>();
        for (var v = 0; v < vehiculos; v++)
        {
            var pedidos = new List<Pedido>();
            for (var indice = routing.Start(v); !routing.IsEnd(indice); indice = solucion.Value(routing.NextVar(indice)))
            {
                var nodo = manager.IndexToNode(indice);
                if (nodo != 0) pedidos.Add(pedidosPendientes[nodo - 1]);
            }

            if (pedidos.Count > 0)
                asignaciones.Add(new AsignacionPlanificada(repartidoresDisponibles[v], pedidos));
        }

        return asignaciones;
    }

    private static GeoPoint Centro(IReadOnlyList<Pedido> pedidos) => new(
        pedidos.Average(p => p.Ubicacion.Latitud),
        pedidos.Average(p => p.Ubicacion.Longitud));

    private static long[,] MatrizDistanciasMetros(IReadOnlyList<GeoPoint> puntos)
    {
        var matriz = new long[puntos.Count, puntos.Count];
        for (var i = 0; i < puntos.Count; i++)
            for (var j = i + 1; j < puntos.Count; j++)
                matriz[i, j] = matriz[j, i] = (long)Math.Round(puntos[i].DistanciaKm(puntos[j]) * 1000);
        return matriz;
    }
}
