using Application.DTOs;
namespace Application.Interfaces;
public interface IUbicacionService
{
    Task<RegistroUbicacionDto> RegistrarAsync(Guid repartidorId, RegistrarUbicacionRequest request, CancellationToken ct);
    Task<UbicacionRepartidorDto> ObtenerAsync(Guid repartidorId, CancellationToken ct);
    Task<IReadOnlyList<UbicacionRepartidorDto>> ListarAsync(DateOnly fecha, Guid? zonaId, CancellationToken ct);
}
