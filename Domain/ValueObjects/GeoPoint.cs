using Domain.Exceptions;

namespace Domain.ValueObjects;

public readonly record struct GeoPoint
{
    private const double RadioTierraKm = 6371.0088;

    public double Latitud { get; }
    public double Longitud { get; }

    public GeoPoint(double latitud, double longitud)
    {
        if (double.IsNaN(latitud) || latitud is < -90 or > 90)
            throw new DomainException("La latitud debe estar entre -90 y 90.");
        if (double.IsNaN(longitud) || longitud is < -180 or > 180)
            throw new DomainException("La longitud debe estar entre -180 y 180.");

        Latitud = latitud;
        Longitud = longitud;
    }

    /// <summary>Distancia en km sobre la esfera (fórmula de Haversine).</summary>
    public double DistanciaKm(GeoPoint otro)
    {
        var dLat = ARadianes(otro.Latitud - Latitud);
        var dLon = ARadianes(otro.Longitud - Longitud);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ARadianes(Latitud)) * Math.Cos(ARadianes(otro.Latitud)) *
                Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * RadioTierraKm * Math.Asin(Math.Sqrt(a));
    }

    private static double ARadianes(double grados) => grados * Math.PI / 180;
}
