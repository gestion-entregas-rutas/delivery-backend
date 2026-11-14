using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.ExternalServices;
using UnitTests.Domain;

namespace UnitTests.Infrastructure;

public class PlanificadorRutasPropioTests
{
    private readonly PlanificadorRutasPropio _planificador = new();

    private static List<Pedido> Pedidos(Zona zona, int cantidad, TimeOnly? horaBase = null)
    {
        var hora = horaBase ?? new TimeOnly(9, 0);
        return Enumerable.Range(0, cantidad)
            .Select(i => Pedido.Registrar($"Cliente {i}", "0999999999", "Calle 1",
                new GeoPoint(-0.009 + i * 0.0015, -0.009 + i * 0.0015),
                zona, DomainFixtures.Hoy, hora.AddMinutes(i * 10), DomainFixtures.Hoy, DomainFixtures.Ahora))
            .ToList();
    }

    [Fact]
    public void Planificar_AsignaCadaPedidoUnaSolaVezYRespetaLaCapacidad()
    {
        var zona = DomainFixtures.CrearZona(max: 4);
        var pedidos = Pedidos(zona, 10);
        var repartidores = new[] { DomainFixtures.CrearRepartidor(4), DomainFixtures.CrearRepartidor(4), DomainFixtures.CrearRepartidor(4) };

        var plan = _planificador.Planificar(zona, pedidos, repartidores);

        var asignados = plan.SelectMany(a => a.PedidosEnOrden).ToList();
        Assert.Equal(10, asignados.Count);
        Assert.Equal(10, asignados.Select(p => p.Id).Distinct().Count());
        Assert.All(plan, a => Assert.InRange(a.PedidosEnOrden.Count, 1, 4));
    }

    [Fact]
    public void Planificar_UsaLaCapacidadMinimaEntreZonaYRepartidor()
    {
        var zona = DomainFixtures.CrearZona(max: 4);
        var pedidos = Pedidos(zona, 4);
        var repartidor = DomainFixtures.CrearRepartidor(capacidad: 2);

        var plan = _planificador.Planificar(zona, pedidos, [repartidor, DomainFixtures.CrearRepartidor(2)]);

        Assert.All(plan, a => Assert.True(a.PedidosEnOrden.Count <= 2));
        Assert.Equal(4, plan.SelectMany(a => a.PedidosEnOrden).Count());
    }

    [Fact]
    public void Planificar_SinPedidosOSinRepartidores_NoCreaRutas()
    {
        var zona = DomainFixtures.CrearZona();
        Assert.Empty(_planificador.Planificar(zona, [], [DomainFixtures.CrearRepartidor()]));
        Assert.Empty(_planificador.Planificar(zona, Pedidos(zona, 2), []));
    }

    [Fact]
    public void Planificar_RespetaVentanasDeTiempo_HoraEntregaEnOrdenNoDecrecientePorRuta()
    {
        var zona = DomainFixtures.CrearZona(max: 10);
        var pedidos = new[]
        {
            Pedido.Registrar("A", "099", "Dir", new GeoPoint(0.001, 0.001), zona, DomainFixtures.Hoy, new TimeOnly(11, 0), DomainFixtures.Hoy, DomainFixtures.Ahora),
            Pedido.Registrar("B", "099", "Dir", new GeoPoint(0.002, 0.001), zona, DomainFixtures.Hoy, new TimeOnly(9, 0), DomainFixtures.Hoy, DomainFixtures.Ahora),
            Pedido.Registrar("C", "099", "Dir", new GeoPoint(0.003, 0.001), zona, DomainFixtures.Hoy, new TimeOnly(10, 0), DomainFixtures.Hoy, DomainFixtures.Ahora),
        };
        var plan = _planificador.Planificar(zona, pedidos, [DomainFixtures.CrearRepartidor(10)]);
        var ruta = Assert.Single(plan).PedidosEnOrden;
        Assert.Equal(new[] { new TimeOnly(9,0), new TimeOnly(10,0), new TimeOnly(11,0) }, ruta.Select(p => p.HoraEntrega).ToArray());
    }
}
