using Application.DTOs;
using Domain.Entities;
namespace Application.Interfaces;
public interface IUsuarioService
{
    Task<Usuario> AutenticarAsync(LoginRequest request, CancellationToken ct);
    Task<Usuario> RegistrarClienteAsync(RegistroClienteRequest request, CancellationToken ct);
    Task<Usuario> CambiarCredencialesAsync(Guid id, CambiarCredencialesRequest request, CancellationToken ct);
    Task<UsuarioDto> CrearAsync(CrearUsuarioRequest request, CancellationToken ct);
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct);
    Task CambiarActivoAsync(Guid actorId, Guid id, bool activo, CancellationToken ct);
    Task RestablecerAsync(Guid id, string clave, CancellationToken ct);
    Task CerrarSesionesAsync(Guid id, CancellationToken ct);
}
