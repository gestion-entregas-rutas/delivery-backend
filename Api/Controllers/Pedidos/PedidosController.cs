using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/pedidos")]
[Produces("application/json")]
public sealed class PedidosController(IPedidoService pedidos) : ControllerBase
{
    /// <summary>Registra un pedido. Si es para hoy se intenta asignar a una ruta de inmediato.</summary>
    [HttpPost]
    [ProducesResponseType<RegistroPedidoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RegistroPedidoDto>> Registrar(RegistrarPedidoRequest request, CancellationToken ct)
    {
        var resultado = await pedidos.RegistrarAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = resultado.Pedido.Id }, resultado);
    }

    /// <summary>Obtiene un pedido por su identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoDto>> Obtener(Guid id, CancellationToken ct) =>
        await pedidos.ObtenerAsync(id, ct);

    /// <summary>Lista pedidos con filtros opcionales por fecha, zona y estado.</summary>
    [HttpGet]
    [ProducesResponseType<PaginaDto<PedidoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<PedidoDto>>> Listar([FromQuery] FiltroPedidos filtro, CancellationToken ct) =>
        await pedidos.ListarAsync(filtro, ct);

    /// <summary>Cancela un pedido que todavía no está asignado a una ruta.</summary>
    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PedidoDto>> Cancelar(Guid id, CancellationToken ct) =>
        await pedidos.CancelarAsync(id, ct);
}
