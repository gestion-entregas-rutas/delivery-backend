using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/rutas")]
[Produces("application/json")]
public sealed class RutasController(IRutaService rutas) : ControllerBase
{
    /// <summary>Arma las rutas de una fecha (y zona) con todos los pedidos pendientes.</summary>
    [HttpPost("armar")]
    [ProducesResponseType<ArmadoRutasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ArmadoRutasDto>> Armar(ArmarRutasRequest request, CancellationToken ct) =>
        await rutas.ArmarRutasAsync(request, ct);

    /// <summary>Obtiene una ruta con sus paradas en orden.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RutaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RutaDto>> Obtener(Guid id, CancellationToken ct) =>
        await rutas.ObtenerAsync(id, ct);

    /// <summary>Lista las rutas de una fecha, opcionalmente de una sola zona.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RutaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RutaDto>>> Listar(
        [FromQuery] DateOnly fecha, [FromQuery] Guid? zonaId, CancellationToken ct) =>
        Ok(await rutas.ListarAsync(fecha, zonaId, ct));

    /// <summary>Ajuste manual: agrega un pedido pendiente a la ruta.</summary>
    [HttpPost("{id:guid}/pedidos")]
    [ProducesResponseType<RutaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RutaDto>> AgregarPedido(Guid id, AgregarPedidoARutaRequest request, CancellationToken ct) =>
        await rutas.AgregarPedidoAsync(id, request.PedidoId, request.Posicion, ct);

    /// <summary>Ajuste manual: saca un pedido de la ruta y lo devuelve a pendientes.</summary>
    [HttpDelete("{id:guid}/pedidos/{pedidoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> QuitarPedido(Guid id, Guid pedidoId, CancellationToken ct)
    {
        await rutas.QuitarPedidoAsync(id, pedidoId, ct);
        return NoContent();
    }

    /// <summary>El repartidor sale a reparto: la ruta pasa a "en curso".</summary>
    [HttpPost("{id:guid}/iniciar")]
    [ProducesResponseType<RutaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RutaDto>> Iniciar(Guid id, CancellationToken ct) =>
        await rutas.IniciarAsync(id, ct);

    /// <summary>Cancela una ruta que aún no salió; sus pedidos vuelven a pendientes.</summary>
    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType<RutaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RutaDto>> Cancelar(Guid id, CancellationToken ct) =>
        await rutas.CancelarAsync(id, ct);
}
