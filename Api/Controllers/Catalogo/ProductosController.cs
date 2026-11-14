using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/productos")]
public sealed class ProductosController(ICatalogoService catalogo) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<PaginaDto<ProductoResumenDto>>> Listar(
        [FromQuery] Guid? categoriaId, [FromQuery] Guid? subcategoriaId, [FromQuery] string? buscar, [FromQuery] bool soloOfertas = false,
        [FromQuery] decimal? precioMin = null, [FromQuery] decimal? precioMax = null, [FromQuery] string? orden = null,
        [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 20, [FromQuery] bool incluirInactivos = false, CancellationToken ct = default)
    {
        var adminVeInactivos = incluirInactivos && User.IsInRole("Administrador");
        return Ok(await catalogo.ListarProductosAsync(categoriaId, subcategoriaId, buscar, soloOfertas, precioMin, precioMax, orden, pagina, tamanoPagina, adminVeInactivos, ct));
    }

    [HttpGet("{id:guid}"), AllowAnonymous]
    public async Task<ActionResult<ProductoDetalleDto>> Obtener(Guid id, [FromQuery] bool incluirInactivos = false, CancellationToken ct = default)
    {
        var adminVeInactivos = incluirInactivos && User.IsInRole("Administrador");
        return Ok(await catalogo.ObtenerProductoAsync(id, adminVeInactivos, ct));
    }

    [HttpPost, Authorize(Policy = "Administrador")]
    public async Task<ActionResult<ProductoDetalleDto>> Crear([FromBody] ProductoUpsertRequest request, CancellationToken ct)
    {
        var creado = await catalogo.CrearProductoAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "Administrador")]
    public async Task<ActionResult<ProductoDetalleDto>> Actualizar(Guid id, [FromBody] ProductoUpsertRequest request, CancellationToken ct)
        => Ok(await catalogo.ActualizarProductoAsync(id, request, ct));

    [HttpPatch("{id:guid}/stock"), Authorize(Policy = "Administrador")]
    public async Task<ActionResult<ProductoDetalleDto>> AjustarStock(Guid id, [FromBody] AjustarStockRequest request, CancellationToken ct)
        => Ok(await catalogo.AjustarStockAsync(id, request.Stock, ct));

    [HttpDelete("{id:guid}"), Authorize(Policy = "Administrador")]
    public async Task<IActionResult> Desactivar(Guid id, CancellationToken ct)
    {
        await catalogo.DesactivarProductoAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/activar"), Authorize(Policy = "Administrador")]
    public async Task<ActionResult<ProductoDetalleDto>> Reactivar(Guid id, CancellationToken ct)
        => Ok(await catalogo.ReactivarProductoAsync(id, ct));

    [HttpPost("{id:guid}/resenas"), Authorize(Roles = "Cliente", Policy = "Completo")]
    public async Task<ActionResult<ResenaDto>> CrearResena(Guid id, [FromBody] CrearResenaRequest request, CancellationToken ct)
    {
        var clienteId = Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var creada = await catalogo.CrearResenaAsync(id, clienteId, request, ct);
        return StatusCode(201, creada);
    }
}

public sealed record AjustarStockRequest(int Stock);
