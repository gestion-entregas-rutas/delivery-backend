using Application.DTOs;
using Domain.ValueObjects;

namespace Application.Mappings;

public static class PuntoMapeos
{
    public static PuntoDto ToDto(this GeoPoint p) => new(p.Latitud, p.Longitud);

    public static GeoPoint ToGeoPoint(this PuntoDto p) => new(p.Latitud, p.Longitud);
}
