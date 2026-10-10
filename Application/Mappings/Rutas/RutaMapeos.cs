using Application.DTOs;
using Domain.Entities;

namespace Application.Mappings;

public static class RutaMapeos
{
    public static RutaDto ToDto(this Ruta ruta) => new
        (
        ruta.Id, 
        ruta.Fecha, 
        ruta.ZonaId, 
        ruta.RepartidorId, 
        ruta.Capacidad, 
        ruta.Estado,
        ruta.Paradas.Select(p => new ParadaDto(p.Orden, p.Pedido.ToDto())).ToList());
}
