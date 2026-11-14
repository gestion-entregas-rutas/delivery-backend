using Application.DTOs;
using Application.Interfaces;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
public sealed class UbicacionesController(IUbicacionService ubicaciones) : ControllerBase
{
    /// <summary>Publica el GPS de la ruta en curso del conductor autenticado.</summary>
    [HttpPost("api/repartidores/me/ubicacion")]
    [Authorize(Roles = "Conductor", Policy = "Completo")]
    public async Task<ActionResult<RegistroUbicacionDto>> Registrar(RegistrarUbicacionRequest r, CancellationToken ct) =>
        await ubicaciones.RegistrarAsync(AccesoRecursos.Repartidor(User), r, ct);

    /// <summary>Última posición conocida de un repartidor, con fecha y precisión.</summary>
    [HttpGet("api/repartidores/{id:guid}/ubicacion")]
    public async Task<ActionResult<UbicacionRepartidorDto>> Obtener(Guid id, CancellationToken ct) =>
        await ubicaciones.ObtenerAsync(id, ct);

    /// <summary>Posiciones conocidas de rutas de una fecha y zona opcional. Solo administradores.</summary>
    [HttpGet("api/seguimiento/ubicaciones")]
    [Authorize(Policy = "Administrador")]
    public async Task<ActionResult<IReadOnlyList<UbicacionRepartidorDto>>> Listar(
        [FromQuery] DateOnly fecha, [FromQuery] Guid? zonaId, CancellationToken ct) =>
        Ok(await ubicaciones.ListarAsync(fecha, zonaId, ct));
}
