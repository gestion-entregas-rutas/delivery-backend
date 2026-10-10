using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// Canal en tiempo real. El servidor emite <see cref="Eventos.PedidoActualizado"/> y
/// <see cref="Eventos.RutaActualizada"/>; cada cliente elige qué grupos escuchar:
/// el panel web por fecha, el repartidor por su id y un cliente por pedido.
/// </summary>
public sealed class SeguimientoHub : Hub
{
    public static class Eventos
    {
        public const string PedidoActualizado = "PedidoActualizado";
        public const string RutaActualizada = "RutaActualizada";
    }

    public static class Grupos
    {
        public static string Fecha(DateOnly fecha) => $"fecha:{fecha:yyyy-MM-dd}";
        public static string Repartidor(Guid id) => $"repartidor:{id}";
        public static string Pedido(Guid id) => $"pedido:{id}";
    }

    /// <summary>Panel del negocio: recibe todo lo que ocurre en las entregas de una fecha.</summary>
    public Task SuscribirseAFecha(DateOnly fecha) =>
        Groups.AddToGroupAsync(Context.ConnectionId, Grupos.Fecha(fecha));

    public Task CancelarFecha(DateOnly fecha) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupos.Fecha(fecha));

    /// <summary>App del repartidor: recibe los cambios de sus rutas.</summary>
    public Task SuscribirseARepartidor(Guid repartidorId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, Grupos.Repartidor(repartidorId));

    /// <summary>Seguimiento opcional para el cliente de un pedido.</summary>
    public Task SeguirPedido(Guid pedidoId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, Grupos.Pedido(pedidoId));

    public Task DejarDeSeguirPedido(Guid pedidoId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupos.Pedido(pedidoId));
}
