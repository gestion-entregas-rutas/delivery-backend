using Api.Services;
using Application.DTOs;
using Application.Interfaces;
using Application.Mappings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/autenticacion")]
[Authorize(Policy = "Identificado")]
public sealed class AutenticacionController(IUsuarioService usuarios, EmisorJwt tokens, IAppDbContext db) : ControllerBase
{
    /// <summary>Inicia sesión. Una clave temporal produce una sesión limitada al cambio de credenciales.</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("Login")]
    public async Task<ActionResult<SesionDto>> Login(LoginRequest r, CancellationToken ct) =>
        tokens.Crear(await usuarios.AutenticarAsync(r, ct));

    /// <summary>Registra un cliente y devuelve una sesión autenticada.</summary>
    [HttpPost("registro-cliente"), AllowAnonymous, EnableRateLimiting("Login")]
    public async Task<ActionResult<SesionDto>> RegistroCliente(RegistroClienteRequest r, CancellationToken ct) =>
        tokens.Crear(await usuarios.RegistrarClienteAsync(r, ct));

    /// <summary>Consulta la identidad autenticada, incluido el cambio obligatorio pendiente.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioDto>> Me(CancellationToken ct) =>
        (await db.Usuarios.SingleAsync(x => x.Id == Guid.Parse(User.FindFirst("sub")!.Value), ct)).ToDto();

    /// <summary>Cambia el usuario y la contraseña propios; revoca los JWT anteriores y devuelve una nueva sesión.</summary>
    [HttpPut("credenciales"), EnableRateLimiting("Login")]
    public async Task<ActionResult<SesionDto>> Cambiar(CambiarCredencialesRequest r, CancellationToken ct) =>
        tokens.Crear(await usuarios.CambiarCredencialesAsync(Guid.Parse(User.FindFirst("sub")!.Value), r, ct));

    /// <summary>Cierra todas las sesiones del usuario y desconecta sus canales SignalR de esta instancia.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await usuarios.CerrarSesionesAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct); return NoContent();
    }
}
