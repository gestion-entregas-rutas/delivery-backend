
namespace Application.DTOs;

public sealed record ActualizarZonaRequest(
    string Nombre,
    IReadOnlyList<PuntoDto> Poligono,
    int MaxPedidosPorRuta,
    bool Activa);
