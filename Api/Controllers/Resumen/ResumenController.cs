using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController, Route("api/resumen"), Authorize(Policy = "Administrador")]
public sealed class ResumenController(IResumenService resumen) : ControllerBase
{
    /// <summary>Indicadores de todos los pedidos filtrados por fecha o rango, zona, estado y búsqueda.</summary>
    [HttpGet("pedidos")]
    [ProducesResponseType<ResumenPedidosDto>(200)]
    public async Task<ActionResult<ResumenPedidosDto>> Obtener([FromQuery] FiltroPedidos filtro, CancellationToken ct) =>
        await resumen.ObtenerAsync(filtro, ct);
}
