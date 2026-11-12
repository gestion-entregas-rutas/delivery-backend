using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;

namespace Infrastructure.Data.Converters;

/// <summary>Traduce los valores geográficos del Domain a geometrías PostGIS (SRID 4326, X = longitud, Y = latitud).</summary>
internal static class Geo
{
    public const int Srid = 4326;
    private static readonly GeometryFactory Fabrica = new(new PrecisionModel(), Srid);

    public static Point APunto(GeoPoint g) => Fabrica.CreatePoint(new Coordinate(g.Longitud, g.Latitud));

    public static GeoPoint ADominio(Point p) => new(p.Y, p.X);

    public static Polygon APoligono(IEnumerable<GeoPoint> vertices)
    {
        var anillo = vertices.Select(v => new Coordinate(v.Longitud, v.Latitud)).ToList();
        anillo.Add(anillo[0]); // PostGIS exige el anillo cerrado
        return Fabrica.CreatePolygon(anillo.ToArray());
    }

    public static List<GeoPoint> ADominio(Polygon p) =>
        p.ExteriorRing.Coordinates.SkipLast(1).Select(c => new GeoPoint(c.Y, c.X)).ToList();
}

public sealed class GeoPointConverter()
    : ValueConverter<GeoPoint, Point>(g => Geo.APunto(g), p => Geo.ADominio(p));

public sealed class PoligonoConverter()
    : ValueConverter<List<GeoPoint>, Polygon>(v => Geo.APoligono(v), p => Geo.ADominio(p));

public sealed class PoligonoComparer() : ValueComparer<List<GeoPoint>>(
    (a, b) => a!.SequenceEqual(b!),
    v => v.Aggregate(0, (hash, g) => HashCode.Combine(hash, g)),
    v => v.ToList());
