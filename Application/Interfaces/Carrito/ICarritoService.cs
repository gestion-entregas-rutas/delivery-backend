using Application.DTOs;

namespace Application.Interfaces;

public interface ICarritoService
{
    Task<CarritoDto> ObtenerAsync(Guid? usuarioId, string? tokenInvitado, bool crearSiNoExiste, CancellationToken ct);
    Task<CarritoDto> AgregarItemAsync(Guid? usuarioId, string? tokenInvitado, Guid productoId, int cantidad, CancellationToken ct);
    Task<CarritoDto> ActualizarItemAsync(Guid? usuarioId, string? tokenInvitado, Guid productoId, int cantidad, CancellationToken ct);
    Task<CarritoDto> QuitarItemAsync(Guid? usuarioId, string? tokenInvitado, Guid productoId, CancellationToken ct);
    Task<CarritoDto> MergeAsync(Guid usuarioId, string tokenInvitado, CancellationToken ct);
    Task<CotizacionDto> CotizarAsync(Guid? usuarioId, string? tokenInvitado, CancellationToken ct);
}
