using Domain.Entities;

namespace Domain.Interfaces;

/// <summary>Un repartidor con los pedidos que le tocan, ya en orden de visita.</summary>
public sealed record AsignacionPlanificada(Repartidor Repartidor, IReadOnlyList<Pedido> PedidosEnOrden);
