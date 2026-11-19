using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/carrito")]
public sealed class CarritoController(ICarritoService carrito) : ControllerBase
{
    private Guid? UsuarioId => Guid.TryParse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private string? TokenInvitado => Request.Headers.TryGetValue("X-Cart-Token", out var v) ? v.ToString() : Request.Query["tokenInvitado"].ToString() is { Length: > 0 } q ? q : null;

    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<CarritoDto>> Obtener(CancellationToken ct)
    {
        var dto = await carrito.ObtenerAsync(UsuarioId, TokenInvitado, true, ct);
        if (!string.IsNullOrWhiteSpace(dto.TokenInvitado)) Response.Headers["X-Cart-Token"] = dto.TokenInvitado;
        return Ok(dto);
    }

    [HttpPost("items"), AllowAnonymous]
    public async Task<ActionResult<CarritoDto>> Agregar([FromBody] AgregarItemCarritoRequest request, CancellationToken ct)
    {
        var dto = await carrito.AgregarItemAsync(UsuarioId, TokenInvitado, request.ProductoId, request.Cantidad, ct);
        if (!string.IsNullOrWhiteSpace(dto.TokenInvitado)) Response.Headers["X-Cart-Token"] = dto.TokenInvitado;
        return Ok(dto);
    }

    [HttpPut("items/{productoId:guid}"), AllowAnonymous]
    public async Task<ActionResult<CarritoDto>> Actualizar(Guid productoId, [FromBody] ActualizarItemCarritoRequest request, CancellationToken ct)
    {
        var dto = await carrito.ActualizarItemAsync(UsuarioId, TokenInvitado, productoId, request.Cantidad, ct);
        if (!string.IsNullOrWhiteSpace(dto.TokenInvitado)) Response.Headers["X-Cart-Token"] = dto.TokenInvitado;
        return Ok(dto);
    }

    [HttpDelete("items/{productoId:guid}"), AllowAnonymous]
    public async Task<ActionResult<CarritoDto>> Quitar(Guid productoId, CancellationToken ct)
    {
        var dto = await carrito.QuitarItemAsync(UsuarioId, TokenInvitado, productoId, ct);
        if (!string.IsNullOrWhiteSpace(dto.TokenInvitado)) Response.Headers["X-Cart-Token"] = dto.TokenInvitado;
        return Ok(dto);
    }

    [HttpPost("merge"), Authorize]
    public async Task<ActionResult<CarritoDto>> Merge([FromBody] MergeCarritoRequest request, CancellationToken ct)
    {
        var uid = UsuarioId ?? throw new UnauthorizedAccessException();
        var dto = await carrito.MergeAsync(uid, request.TokenInvitado ?? TokenInvitado ?? "", ct);
        return Ok(dto);
    }

    [HttpGet("cotizacion"), AllowAnonymous]
    public async Task<ActionResult<CotizacionDto>> Cotizar(CancellationToken ct)
        => Ok(await carrito.CotizarAsync(UsuarioId, TokenInvitado, ct));
}
