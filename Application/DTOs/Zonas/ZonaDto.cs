
namespace Application.DTOs;

public sealed record ZonaDto(
    Guid Id,
    string Nombre,
    bool Activa,
    int MaxPedidosPorRuta,
    IReadOnlyList<PuntoDto> Poligono);
