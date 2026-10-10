using Domain.Entities;

namespace Domain.Interfaces;

/// <summary>
/// Armado masivo de rutas (CVRP) para una fecha y zona. Infrastructure lo implementa
/// (p. ej. con OR-Tools); el Domain solo fija el contrato y valida el resultado al crear las rutas.
/// Cada asignación no debe exceder min(zona.MaxPedidosPorRuta, repartidor.CapacidadMaxima).
/// </summary>
public interface IPlanificadorRutas
{
    IReadOnlyList<AsignacionPlanificada> Planificar(
        Zona zona,
        IReadOnlyList<Pedido> pedidosPendientes,
        IReadOnlyList<Repartidor> repartidoresDisponibles);
}
