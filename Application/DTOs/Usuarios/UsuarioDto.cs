using Domain.Enums;
namespace Application.DTOs;
public sealed record UsuarioDto(Guid Id, string NombreUsuario, RolUsuario Rol, Guid? RepartidorId,
    bool Activo, bool DebeCambiarCredenciales, string? NombreCompleto = null, string? Correo = null, string? Telefono = null, string? Cedula = null);
