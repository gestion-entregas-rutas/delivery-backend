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

    [HttpPost("{id:guid}/pago/cancelar"), Authorize(Roles = "Cliente")]
    public async Task<ActionResult<CheckoutResponse>> CancelarPago(Guid id, [FromBody] CancelarPagoRequest? request, CancellationToken ct)
    {
        var resp = await ordenes.CancelarPagoAsync(ClienteId, id, request?.Motivo, ct);
        return Ok(resp);
    }

    [HttpGet("mias"), Authorize(Roles = "Cliente")]
    public async Task<ActionResult<IReadOnlyList<OrdenMiaDto>>> Mias(CancellationToken ct)
        => Ok(await ordenes.ListarMiasAsync(ClienteId, ct));

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
