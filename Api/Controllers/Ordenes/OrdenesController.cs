using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/ordenes")]
[Authorize]
public sealed class OrdenesController(IOrdenService ordenes) : ControllerBase
{
    private Guid ClienteId => Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("checkout"), Authorize(Roles = "Cliente")]
    public async Task<ActionResult<CheckoutResponse>> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var key = Request.Headers.TryGetValue("Idempotency-Key", out var v) ? v.ToString() : null;
        var resp = await ordenes.CheckoutAsync(ClienteId, request, key, ct);
        return StatusCode(201, resp);
    }

    [HttpPost("{id:guid}/pago/confirmar"), Authorize(Roles = "Cliente")]
    public async Task<ActionResult<CheckoutResponse>> ConfirmarPago(Guid id, [FromBody] ConfirmarPagoRequest? request, CancellationToken ct)
    {
        var key = Request.Headers.TryGetValue("Idempotency-Key", out var v) ? v.ToString() : null;
        var resp = await ordenes.ConfirmarPagoAsync(ClienteId, id, request?.Referencia, key, ct);
        return Ok(resp);
    }

    /// <summary>Inicia una orden de pago PayPal en entorno Sandbox.</summary>
    [HttpPost("{id:guid}/paypal/orden"), Authorize(Roles = "Cliente")]
    [ProducesResponseType(typeof(InicioPagoPayPalResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InicioPagoPayPalResponse>> IniciarPayPal(Guid id, CancellationToken ct)
        => Ok(await ordenes.IniciarPayPalAsync(ClienteId, id, ct));

    /// <summary>Captura y confirma en el backend el pago aprobado por el comprador en PayPal.</summary>
    [HttpPost("{id:guid}/paypal/captura"), Authorize(Roles = "Cliente")]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckoutResponse>> CapturarPayPal(Guid id, CancellationToken ct)
        => Ok(await ordenes.CapturarPayPalAsync(ClienteId, id, ct));

    [HttpPost("{id:guid}/pago/cancelar"), Authorize(Roles = "Cliente")]
    public async Task<ActionResult<CheckoutResponse>> CancelarPago(Guid id, [FromBody] CancelarPagoRequest? request, CancellationToken ct)
    {
        var resp = await ordenes.CancelarPagoAsync(ClienteId, id, request?.Motivo, ct);
        return Ok(resp);
    }

    [HttpGet("mias"), Authorize(Roles = "Cliente")]
    public async Task<ActionResult<IReadOnlyList<OrdenMiaDto>>> Mias(CancellationToken ct)
        => Ok(await ordenes.ListarMiasAsync(ClienteId, ct));

    /// <summary>Obtiene el resumen y los productos de una orden perteneciente al cliente autenticado.</summary>
    [HttpGet("{id:guid}/detalle"), Authorize(Roles = "Cliente")]
    [ProducesResponseType(typeof(OrdenDetalleMiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenDetalleMiaDto>> ObtenerDetalleMia(Guid id, CancellationToken ct)
        => Ok(await ordenes.ObtenerDetalleMiaAsync(ClienteId, id, ct));

    [HttpGet("retiros-tienda"), Authorize(Policy = "Administrador")]
    public async Task<ActionResult<IReadOnlyList<RetiroTiendaDto>>> RetirosTienda(CancellationToken ct)
        => Ok(await ordenes.ListarRetirosTiendaAsync(ct));

    [HttpPatch("{id:guid}/marcar-retirado"), Authorize(Policy = "Administrador")]
    public async Task<IActionResult> MarcarRetirado(Guid id, CancellationToken ct)
    {
        await ordenes.MarcarRetiradoAsync(id, ct);
        return NoContent();
    }
}
