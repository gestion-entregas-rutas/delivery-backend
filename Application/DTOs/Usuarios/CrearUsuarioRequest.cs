using Domain.Enums;
namespace Application.DTOs;
public sealed record CrearUsuarioRequest(string NombreUsuario, string ClaveTemporal, RolUsuario Rol, Guid? RepartidorId);
