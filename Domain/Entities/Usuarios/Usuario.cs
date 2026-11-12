using Domain.Common;
using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public sealed class Usuario : Entity
{
    private Usuario() { }
    public string NombreUsuario { get; private set; } = null!;
    public string NombreNormalizado { get; private set; } = null!;
    public string ClaveHash { get; private set; } = null!;
    public RolUsuario Rol { get; private set; }
    public Guid? RepartidorId { get; private set; }
    public bool Activo { get; private set; } = true;
    public bool DebeCambiarCredenciales { get; private set; } = true;
    public Guid SelloSeguridad { get; private set; } = Guid.NewGuid();
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }

    public static string Normalizar(string nombre) => nombre.Trim().ToUpperInvariant();
    public static Usuario Crear(string nombre, string hash, RolUsuario rol, Guid? repartidorId)
    {
        ValidarNombre(nombre);
        if (!Enum.IsDefined(rol) || (rol == RolUsuario.Conductor) != repartidorId.HasValue)
            throw new DomainException("Un conductor requiere repartidor; un administrador no debe tenerlo.");
        return new Usuario { NombreUsuario = nombre.Trim(), NombreNormalizado = Normalizar(nombre),
            ClaveHash = hash, Rol = rol, RepartidorId = repartidorId };
    }
    public static void ValidarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length is < 3 or > 100)
            throw new DomainException("El usuario debe tener entre 3 y 100 caracteres.");
    }
    public void CambiarCredenciales(string nombre, string hash)
    {
        ValidarNombre(nombre);
        NombreUsuario = nombre.Trim(); NombreNormalizado = Normalizar(nombre);
        ClaveHash = hash; DebeCambiarCredenciales = false; RenovarSello();
    }
    public void RestablecerClave(string hash)
    {
        ClaveHash = hash; DebeCambiarCredenciales = true; RenovarSello();
        IntentosFallidos = 0; BloqueadoHasta = null;
    }
    public void CambiarActivo(bool activo) { Activo = activo; RenovarSello(); }
    public void RegistrarFallo(DateTimeOffset ahora)
    {
        if (++IntentosFallidos >= 5) { BloqueadoHasta = ahora.AddMinutes(15); IntentosFallidos = 0; }
    }
    public void RegistrarIngreso() { IntentosFallidos = 0; BloqueadoHasta = null; }
    public void CerrarSesiones() => RenovarSello();
    private void RenovarSello() => SelloSeguridad = Guid.NewGuid();
}
