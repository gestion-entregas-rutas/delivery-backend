using Application.DTOs;

namespace Application.Interfaces;

/// <summary>Empuja cambios a la web y a la app móvil en tiempo real (SignalR en Infrastructure/Api).</summary>
public interface INotificadorEventos
{
    Task PedidoActualizadoAsync(PedidoDto pedido, CancellationToken ct = default);
    Task RutaActualizadaAsync(RutaDto ruta, CancellationToken ct = default);
}
