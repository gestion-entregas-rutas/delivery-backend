using Domain.Entities;
using Domain.Services;

namespace Domain.Interfaces;

/// <summary>
/// Armado heurístico propio para una fecha y zona. Con contexto se comprueba la
/// finalización estimada de cada entrega; sin contexto se conserva el armado histórico.
/// Cada asignación no debe exceder min(zona.MaxPedidosPorRuta, repartidor.CapacidadMaxima).
/// </summary>
public interface IPlanificadorRutas
{
    IReadOnlyList<AsignacionPlanificada> Planificar(
        Zona zona,
        IReadOnlyList<Pedido> pedidosPendientes,
        IReadOnlyList<Repartidor> repartidoresDisponibles,
        ContextoPlanificacion? contexto = null);
}
