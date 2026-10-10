using Api.Hubs;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

/// <summary>Implementa el puerto de notificaciones de Application empujando los cambios por SignalR.</summary>
public sealed class NotificadorSignalR(IHubContext<SeguimientoHub> hub, ILogger<NotificadorSignalR> logger)
    : INotificadorEventos
{
    public Task PedidoActualizadoAsync(PedidoDto pedido, CancellationToken ct = default) =>
        EnviarAsync(SeguimientoHub.Eventos.PedidoActualizado, pedido,
            [SeguimientoHub.Grupos.Fecha(pedido.FechaEntrega), SeguimientoHub.Grupos.Pedido(pedido.Id)], ct);

    public Task RutaActualizadaAsync(RutaDto ruta, CancellationToken ct = default) =>
        EnviarAsync(SeguimientoHub.Eventos.RutaActualizada, ruta,
            [SeguimientoHub.Grupos.Fecha(ruta.Fecha), SeguimientoHub.Grupos.Repartidor(ruta.RepartidorId)], ct);

    // Una falla de red hacia los clientes nunca debe deshacer ni romper la operación ya guardada.
    private async Task EnviarAsync(string evento, object carga, string[] grupos, CancellationToken ct)
    {
        try
        {
            await hub.Clients.Groups(grupos).SendAsync(evento, carga, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo notificar el evento {Evento}.", evento);
        }
    }
}
