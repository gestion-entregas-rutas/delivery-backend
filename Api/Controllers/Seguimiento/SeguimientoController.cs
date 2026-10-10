using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Acciones del repartidor desde la app móvil. Cada cambio llega a la web por SignalR.</summary>
[ApiController]
[Route("api/seguimiento/pedidos/{pedidoId:guid}")]
[Produces("application/json")]
public sealed class SeguimientoController(ISeguimientoService seguimiento) : ControllerBase
{
    /// <summary>El repartidor sale hacia la dirección del pedido.</summary>
    [HttpPost("en-camino")]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PedidoDto>> MarcarEnCamino(Guid pedidoId, CancellationToken ct) =>
        await seguimiento.MarcarEnCaminoAsync(pedidoId, ct);

    /// <summary>El repartidor entregó el pedido. Al entregar la última parada se cierra la ruta.</summary>
    [HttpPost("entregado")]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PedidoDto>> RegistrarEntrega(Guid pedidoId, CancellationToken ct) =>
        await seguimiento.RegistrarEntregaAsync(pedidoId, ct);
}
