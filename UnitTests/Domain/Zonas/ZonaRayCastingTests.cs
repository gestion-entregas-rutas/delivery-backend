using Domain.Entities;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public class ZonaRayCastingTests
{
    private static Zona ZonaCuadrada() => Zona.Crear("Centro",
        [new(-0.01, -0.01), new(-0.01, 0.01), new(0.01, 0.01), new(0.01, -0.01)], 4);

    [Fact]
    public void Contiene_PuntoDentro_RetornaTrue()
    {
        var zona = ZonaCuadrada();
        Assert.True(zona.Contiene(new GeoPoint(0, 0)));
        Assert.True(zona.Contiene(new GeoPoint(0.005, -0.005)));
    }

    [Fact]
    public void Contiene_PuntoFuera_RetornaFalse()
    {
        var zona = ZonaCuadrada();
        Assert.False(zona.Contiene(new GeoPoint(0.02, 0)));
        Assert.False(zona.Contiene(new GeoPoint(-0.02, -0.02)));
    }

    [Fact]
    public void Contiene_PoligonoConcavo_RayCastingCorrecto()
    {
        // Polígono en forma de "L" (concavo)
        var zona = Zona.Crear("L", [
            new(0, 0), new(0, 2), new(1, 2), new(1, 1), new(2, 1), new(2, 0)
        ], 4);
        Assert.True(zona.Contiene(new GeoPoint(0.5, 0.5))); // dentro de la base
        Assert.True(zona.Contiene(new GeoPoint(0.5, 1.5))); // dentro del brazo vertical
        Assert.False(zona.Contiene(new GeoPoint(1.5, 1.5))); // hueco cóncavo
        Assert.False(zona.Contiene(new GeoPoint(3, 0.5))); // fuera
    }
}
