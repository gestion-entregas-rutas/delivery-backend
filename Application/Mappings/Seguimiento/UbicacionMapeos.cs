using Application.DTOs;
using Domain.Entities;
namespace Application.Mappings;
public static class UbicacionMapeos
{
    public static UbicacionRepartidorDto ToDto(this UltimaUbicacionRepartidor u, DateTimeOffset ahora) =>
        new(u.RepartidorId, u.RutaId, u.Punto.Latitud, u.Punto.Longitud, u.PrecisionMetros,
            u.CapturadaEn, u.RecibidaEn, u.CapturadaEn >= ahora.AddMinutes(-2) && u.CapturadaEn <= ahora.AddMinutes(1));
}
