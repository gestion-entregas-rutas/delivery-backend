using Application.DTOs;
using Application.Interfaces;

namespace Infrastructure.ExternalServices;

/// <summary>Notificador por defecto: no hace nada. La Api lo reemplaza por el de SignalR.</summary>
public sealed class NotificadorNulo : INotificadorEventos
{
    public Task PedidoActualizadoAsync(PedidoDto pedido, CancellationToken ct = default) => Task.CompletedTask;

    public Task RutaActualizadaAsync(RutaDto ruta, CancellationToken ct = default) => Task.CompletedTask;
    public Task PedidoReprogramadoAsync(PedidoDto pedido, DateOnly fechaAnterior, CancellationToken ct = default) => Task.CompletedTask;
    public Task UbicacionActualizadaAsync(UbicacionRepartidorDto ubicacion, DateOnly fecha, CancellationToken ct = default) => Task.CompletedTask;
}
