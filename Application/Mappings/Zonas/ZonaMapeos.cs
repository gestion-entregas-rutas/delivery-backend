using Application.DTOs;
using Domain.Entities;

namespace Application.Mappings;

public static class ZonaMapeos
{
    public static ZonaDto ToDto(this Zona zona) => new
        (
        zona.Id, 
        zona.Nombre, 
        zona.Activa, 
        zona.MaxPedidosPorRuta, 
        zona.Poligono.Select(v => v.ToDto()).ToList());
}
