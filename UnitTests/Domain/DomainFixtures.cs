using Domain.Entities;
using Domain.ValueObjects;

namespace UnitTests.Domain;

internal static class DomainFixtures
{
    public static readonly DateOnly Hoy = new(2027, 2, 14);
    public static readonly DateTimeOffset Ahora = new(2027, 2, 14, 8, 0, 0, TimeSpan.Zero);

    // Cuadrado de ~2.2 km de lado alrededor de (0,0).
    public static Zona CrearZona(int max = 4) => Zona.Crear("Centro",
        [new(-0.01, -0.01), new(-0.01, 0.01), new(0.01, 0.01), new(0.01, -0.01)], max);

    public static Repartidor CrearRepartidor(int capacidad = 4) =>
        Repartidor.Crear("Ana", "0999999999", capacidad);

    public static Pedido CrearPedido(Zona zona, double lat = 0, double lon = 0, DateOnly? fecha = null) =>
        Pedido.Registrar("Luis", "0988888888", "Calle 1", new GeoPoint(lat, lon), zona,
            fecha ?? Hoy, new TimeOnly(10, 0), Hoy, Ahora);
}
