using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
namespace Application.Services;

public sealed class UsuarioService(IAppDbContext db, IProtectorClaves claves, TimeProvider reloj, IRevocadorSesiones sesiones) : IUsuarioService
{
    public async Task<Usuario> AutenticarAsync(LoginRequest r, CancellationToken ct)
    {
        var u = await db.Usuarios.SingleOrDefaultAsync(x => x.NombreNormalizado == Usuario.Normalizar(r.NombreUsuario ?? ""), ct);
        var ahora = reloj.GetUtcNow();
        if (u is null || !u.Activo || u.BloqueadoHasta > ahora) throw new CredencialesInvalidasException();
        if (string.IsNullOrEmpty(r.Clave) || r.Clave.Length > 128 || !claves.Verificar(u.ClaveHash, r.Clave))
        {
            u.RegistrarFallo(ahora); await db.GuardarAsync(ct); throw new CredencialesInvalidasException();
        }
        u.RegistrarIngreso(); await db.GuardarAsync(ct); return u;
    }
    public async Task<Usuario> CambiarCredencialesAsync(Guid id, CambiarCredencialesRequest r, CancellationToken ct)
    {
        var u = await Buscar(id, ct);
        if (string.IsNullOrEmpty(r.ClaveActual) || r.ClaveActual.Length > 128 || !claves.Verificar(u.ClaveHash, r.ClaveActual)) throw new CredencialesInvalidasException();
        ValidarClave(r.NuevaClave);
        if (claves.Verificar(u.ClaveHash, r.NuevaClave)) throw new DomainException("La nueva clave debe ser diferente.");
        Usuario.ValidarNombre(r.NuevoNombreUsuario);
        await ExigirNombreLibre(r.NuevoNombreUsuario, id, ct);
        u.CambiarCredenciales(r.NuevoNombreUsuario, claves.Proteger(r.NuevaClave));
        await db.GuardarAsync(ct); sesiones.Revocar(id); return u;
    }
    public async Task<UsuarioDto> CrearAsync(CrearUsuarioRequest r, CancellationToken ct)
    {
        ValidarClave(r.ClaveTemporal); Usuario.ValidarNombre(r.NombreUsuario);
        await ExigirNombreLibre(r.NombreUsuario, null, ct);
        if (r.RepartidorId.HasValue && !await db.Repartidores.AnyAsync(x => x.Id == r.RepartidorId, ct))
            throw new NoEncontradoException("Repartidor", r.RepartidorId.Value);
        if (r.RepartidorId.HasValue && await db.Usuarios.AnyAsync(x => x.RepartidorId == r.RepartidorId, ct))
            throw new DomainException("El repartidor ya tiene una cuenta.");
        var u = Usuario.Crear(r.NombreUsuario, claves.Proteger(r.ClaveTemporal), r.Rol, r.RepartidorId);
        db.Usuarios.Add(u); await db.GuardarAsync(ct); return u.ToDto();
    }
    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct) =>
        (await db.Usuarios.AsNoTracking().OrderBy(u => u.NombreUsuario).ToListAsync(ct)).Select(u => u.ToDto()).ToList();
    public async Task CambiarActivoAsync(Guid actorId, Guid id, bool activo, CancellationToken ct)
    {
        if (actorId == id) throw new DomainException("No puede desactivar su propia cuenta.");
        (await Buscar(id, ct)).CambiarActivo(activo); await db.GuardarAsync(ct); sesiones.Revocar(id);
    }
    public async Task RestablecerAsync(Guid id, string clave, CancellationToken ct)
    {
        ValidarClave(clave); (await Buscar(id, ct)).RestablecerClave(claves.Proteger(clave)); await db.GuardarAsync(ct); sesiones.Revocar(id);
    }
    private async Task<Usuario> Buscar(Guid id, CancellationToken ct) =>
        await db.Usuarios.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException("Usuario", id);
    public async Task CerrarSesionesAsync(Guid id, CancellationToken ct)
    {
        (await Buscar(id, ct)).CerrarSesiones(); await db.GuardarAsync(ct); sesiones.Revocar(id);
    }
    private async Task ExigirNombreLibre(string nombre, Guid? id, CancellationToken ct)
    {
        var normalizado = Usuario.Normalizar(nombre);
        if (await db.Usuarios.AnyAsync(x => x.NombreNormalizado == normalizado && x.Id != id, ct))
            throw new DomainException("El nombre de usuario ya está registrado.");
    }
    public static void ValidarClave(string clave)
    {
        if (string.IsNullOrEmpty(clave) || clave.Length is < 12 or > 128 || !clave.Any(char.IsUpper) ||
            !clave.Any(char.IsLower) || !clave.Any(char.IsDigit) || !clave.Any(c => !char.IsLetterOrDigit(c)))
            throw new DomainException("La clave requiere 12–128 caracteres, mayúscula, minúscula, número y símbolo.");
    }
}
