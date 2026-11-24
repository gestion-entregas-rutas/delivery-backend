using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Oferta de intervalos de entrega a domicilio (tienda y panel).</summary>
[ApiController]
[Route("api/entregas")]
[Produces("application/json")]
public sealed class EntregasController(IDisponibilidadEntregas disponibilidad) : ControllerBase
{
    /// <summary>
    /// Intervalos del día para una dirección, marcados como disponibles o no. Consultar NO reserva:
    /// el checkout vuelve a comprobar el intervalo elegido (409 INTERVALO_NO_DISPONIBLE si ya no cabe).
    /// </summary>
    [HttpGet("intervalos")]
    [ProducesResponseType<IntervalosDisponiblesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IntervalosDisponiblesDto>> Intervalos(
        [FromQuery] DateOnly fecha, [FromQuery] double latitud, [FromQuery] double longitud, CancellationToken ct) =>
        await disponibilidad.ListarAsync(fecha, latitud, longitud, ct);
}
