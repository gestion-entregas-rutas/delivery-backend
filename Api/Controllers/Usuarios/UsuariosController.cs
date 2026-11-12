using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController, Route("api/usuarios"), Authorize(Policy = "Administrador")]
public sealed class UsuariosController(IUsuarioService usuarios) : ControllerBase
{
    /// <summary>Crea un administrador o conductor con clave temporal. El conductor requiere RepartidorId.</summary>
    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear(CrearUsuarioRequest r, CancellationToken ct)
    {
        var u = await usuarios.CrearAsync(r, ct); return Created("/api/usuarios", u);
    }
    /// <summary>Lista cuentas sin exponer hashes ni contraseñas.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar(CancellationToken ct) => Ok(await usuarios.ListarAsync(ct));

    /// <summary>Activa o desactiva una cuenta y revoca sus tokens anteriores.</summary>
    [HttpPut("{id:guid}/activo")]
    public async Task<IActionResult> Activo(Guid id, CambiarActivoRequest r, CancellationToken ct)
    {
        await usuarios.CambiarActivoAsync(Guid.Parse(User.FindFirst("sub")!.Value), id, r.Activo, ct); return NoContent();
    }
    /// <summary>Restablece la contraseña a una temporal y exige cambiarla al ingresar.</summary>
    [HttpPost("{id:guid}/restablecer-clave")]
    public async Task<IActionResult> Restablecer(Guid id, RestablecerClaveRequest r, CancellationToken ct)
    {
        await usuarios.RestablecerAsync(id, r.ClaveTemporal, ct); return NoContent();
    }
}
