using Application.DTOs;
using Domain.Entities;
namespace Application.Mappings;
public static class UsuarioMapeos
{
    public static UsuarioDto ToDto(this Usuario u) => new(u.Id, u.NombreUsuario, u.Rol,
        u.RepartidorId, u.Activo, u.DebeCambiarCredenciales, u.NombreCompleto, u.Correo, u.Telefono, u.Cedula);
}
