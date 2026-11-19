using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/categorias")]
public sealed class CategoriasController(ICatalogoService catalogo) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CategoriaDto>>> Listar(CancellationToken ct) =>
        Ok(await catalogo.ListarCategoriasAsync(ct));

    [HttpPost, Authorize(Policy = "Administrador")]
    public async Task<ActionResult<CategoriaDto>> Crear([FromBody] CrearCategoriaRequest request, CancellationToken ct)
    {
        var creada = await catalogo.CrearCategoriaAsync(request, ct);
        return Created($"/api/categorias/{creada.Id}", creada);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "Administrador")]
    public async Task<ActionResult<CategoriaDto>> Actualizar(Guid id, [FromBody] ActualizarCategoriaRequest request, CancellationToken ct)
        => Ok(await catalogo.ActualizarCategoriaAsync(id, request, ct));

    [HttpGet("{id:guid}/subcategorias"), AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SubcategoriaDto>>> ListarSubcategorias(Guid id, [FromQuery] bool incluirInactivas = false, CancellationToken ct = default)
    {
        var admin = incluirInactivas && User.IsInRole("Administrador");
        return Ok(await catalogo.ListarSubcategoriasAsync(id, admin, ct));
    }
}

[ApiController]
[Route("api/subcategorias")]
public sealed class SubcategoriasController(ICatalogoService catalogo) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SubcategoriaDto>>> Listar([FromQuery] Guid? categoriaId, [FromQuery] bool incluirInactivas = false, CancellationToken ct = default)
    {
        var admin = incluirInactivas && User.IsInRole("Administrador");
        return Ok(await catalogo.ListarSubcategoriasAsync(categoriaId, admin, ct));
    }

    [HttpPost, Authorize(Policy = "Administrador")]
    public async Task<ActionResult<SubcategoriaDto>> Crear([FromBody] CrearSubcategoriaRequest request, CancellationToken ct)
    {
        var creada = await catalogo.CrearSubcategoriaAsync(request, ct);
        return Created($"/api/subcategorias/{creada.Id}", creada);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "Administrador")]
    public async Task<ActionResult<SubcategoriaDto>> Actualizar(Guid id, [FromBody] ActualizarSubcategoriaRequest request, CancellationToken ct)
        => Ok(await catalogo.ActualizarSubcategoriaAsync(id, request, ct));
}
