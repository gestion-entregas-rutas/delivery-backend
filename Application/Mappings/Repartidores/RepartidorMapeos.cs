using Application.DTOs;
using Domain.Entities;

namespace Application.Mappings;

public static class RepartidorMapeos
{
    public static RepartidorDto ToDto(this Repartidor repartidor) => new
        (
        repartidor.Id, 
        repartidor.Nombre, 
        repartidor.Telefono, 
        repartidor.CapacidadMaxima, 
        repartidor.Estado);
}
