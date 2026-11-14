using Domain.Common;
using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public sealed class Usuario : Entity
{
    private Usuario() { }
    public string NombreUsuario { get; private set; } = null!;
    public string NombreNormalizado { get; private set; } = null!;
    public string? Cedula { get; private set; }
    public string? NombreCompleto { get; private set; }
    public string? Correo { get; private set; }
    public string? CorreoNormalizado { get; private set; }
    public string? Telefono { get; private set; }
    public string ClaveHash { get; private set; } = null!;
    public RolUsuario Rol { get; private set; }
    public Guid? RepartidorId { get; private set; }
    public bool Activo { get; private set; } = true;
    public bool DebeCambiarCredenciales { get; private set; } = true;
    public Guid SelloSeguridad { get; private set; } = Guid.NewGuid();
    public int IntentosFallidos { get; private set; }
    public DateTimeOffset? BloqueadoHasta { get; private set; }

    public static string Normalizar(string valor) => valor.Trim().ToUpperInvariant();
    public static Usuario Crear(string nombre, string hash, RolUsuario rol, Guid? repartidorId)
    {
        ValidarNombre(nombre);
        if (!Enum.IsDefined(rol) || rol == RolUsuario.Cliente)
            throw new DomainException("Use el registro de cliente para cuentas de cliente.");
        if ((rol == RolUsuario.Conductor) != repartidorId.HasValue)
            throw new DomainException("Un conductor requiere repartidor; un administrador no debe tenerlo.");
        return new Usuario { NombreUsuario = nombre.Trim(), NombreNormalizado = Normalizar(nombre),
            ClaveHash = hash, Rol = rol, RepartidorId = repartidorId };
    }

    public static Usuario CrearCliente(string nombreCompleto, string correo, string? telefono, string hash, string? cedula = null)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto) || nombreCompleto.Trim().Length is < 3 or > 150)
            throw new DomainException("El nombre completo debe tener entre 3 y 150 caracteres.");
        correo = (correo ?? "").Trim();
        if (correo.Length is < 5 or > 254 || !correo.Contains('@'))
            throw new DomainException("El correo no es válido.");
        telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        if (telefono is { Length: > 30 })
            throw new DomainException("El teléfono no puede exceder 30 caracteres.");
        cedula = string.IsNullOrWhiteSpace(cedula) ? null : cedula.Trim();
        if (cedula is { Length: > 20 })
            throw new DomainException("La cédula no puede exceder 20 caracteres.");
        var correoNorm = Normalizar(correo);
        return new Usuario
        {
            NombreUsuario = correo,
            NombreNormalizado = correoNorm,
            Cedula = cedula,
            NombreCompleto = nombreCompleto.Trim(),
            Correo = correo,
            CorreoNormalizado = correoNorm,
            Telefono = telefono,
            ClaveHash = hash,
            Rol = RolUsuario.Cliente,
            DebeCambiarCredenciales = false
        };
    }

    public static void ValidarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length is < 3 or > 100)
            throw new DomainException("El usuario debe tener entre 3 y 100 caracteres.");
    }
    public void CambiarCredenciales(string nombre, string hash)
    {
        if (Rol == RolUsuario.Cliente)
            throw new DomainException("El cliente no puede cambiar credenciales por este flujo.");
        ValidarNombre(nombre);
        NombreUsuario = nombre.Trim(); NombreNormalizado = Normalizar(nombre);
        ClaveHash = hash; DebeCambiarCredenciales = false; RenovarSello();
    }
    public void RestablecerClave(string hash)
    {
        ClaveHash = hash; DebeCambiarCredenciales = Rol != RolUsuario.Cliente; RenovarSello();
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
