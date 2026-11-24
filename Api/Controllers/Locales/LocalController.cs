using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Ubicación del local (punto de salida de las rutas). Solo administradores.</summary>
[ApiController]
[Route("api/local")]
[Produces("application/json")]
public sealed class LocalController(IUbicacionLocalService local) : ControllerBase
{
    /// <summary>Devuelve la ubicación guardada del local; 204 si todavía no se configuró.</summary>
    [HttpGet]
    [ProducesResponseType<UbicacionLocalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<UbicacionLocalDto>> Obtener(CancellationToken ct) =>
        await local.ObtenerAsync(ct) is { } dto ? dto : NoContent();

    /// <summary>Crea o reemplaza la ubicación del local.</summary>
    [HttpPut]
    [ProducesResponseType<UbicacionLocalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UbicacionLocalDto>> Guardar(GuardarUbicacionLocalRequest request, CancellationToken ct) =>
        await local.GuardarAsync(request, ct);
}
