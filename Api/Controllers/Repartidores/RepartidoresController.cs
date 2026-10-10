using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/repartidores")]
[Produces("application/json")]
public sealed class RepartidoresController(IRepartidorService repartidores) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<RepartidorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RepartidorDto>> Crear(CrearRepartidorRequest request, CancellationToken ct)
    {
        var repartidor = await repartidores.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Listar), repartidor);
    }

    /// <summary>Lista repartidores; con <c>soloDisponibles</c> se omiten los que están en ruta o no disponibles.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RepartidorDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RepartidorDto>>> Listar(
        [FromQuery] bool soloDisponibles = false, CancellationToken ct = default) =>
        Ok(await repartidores.ListarAsync(soloDisponibles, ct));

    /// <summary>Marca al repartidor como disponible o no disponible.</summary>
    [HttpPut("{id:guid}/disponibilidad")]
    [ProducesResponseType<RepartidorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RepartidorDto>> CambiarDisponibilidad(
        Guid id, CambiarDisponibilidadRequest request, CancellationToken ct) =>
        await repartidores.CambiarDisponibilidadAsync(id, request.Disponible, ct);

    /// <summary>Ruta activa (planificada o en curso) del repartidor en la fecha indicada.</summary>
    [HttpGet("{id:guid}/ruta")]
    [ProducesResponseType<RutaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RutaDto>> ObtenerRuta(Guid id, [FromQuery] DateOnly fecha, CancellationToken ct)
    {
        var ruta = await repartidores.ObtenerRutaAsignadaAsync(id, fecha, ct);
        return ruta is null ? NotFound() : ruta;
    }
}
