using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/zonas")]
[Produces("application/json")]
public sealed class ZonasController(IZonaService zonas) : ControllerBase
{
    /// <summary>Crea una zona de reparto a partir de su polígono.</summary>
    [HttpPost]
    [ProducesResponseType<ZonaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ZonaDto>> Crear(CrearZonaRequest request, CancellationToken ct)
    {
        var zona = await zonas.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = zona.Id }, zona);
    }

    /// <summary>Actualiza nombre, polígono, máximo de pedidos por ruta y estado de una zona.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ZonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZonaDto>> Actualizar(Guid id, ActualizarZonaRequest request, CancellationToken ct) =>
        await zonas.ActualizarAsync(id, request, ct);

    /// <summary>Obtiene una zona por su identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ZonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ZonaDto>> Obtener(Guid id, CancellationToken ct) =>
        await zonas.ObtenerAsync(id, ct);

    /// <summary>Lista todas las zonas ordenadas por nombre.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ZonaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ZonaDto>>> Listar(CancellationToken ct) =>
        Ok(await zonas.ListarAsync(ct));
}
