using Application.DTOs;

namespace Application.Interfaces;

/// <summary>Decide qué hacer con un pedido del mismo día: insertarlo, crear ruta nueva o esperar.</summary>
public interface IAsignadorUltimoMomento
{
    Task<ResultadoAsignacionDto> AsignarAsync(Guid pedidoId, CancellationToken ct = default);
}
