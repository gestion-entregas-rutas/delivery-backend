
namespace Application.DTOs;

public sealed record CrearZonaRequest(string Nombre, IReadOnlyList<PuntoDto> Poligono, int MaxPedidosPorRuta = 4);
