using Domain.Entities;
using Infrastructure.ExternalServices;
using UnitTests.Domain;

namespace UnitTests.Infrastructure;

public class PlanificadorRutasOrToolsTests
{
    private readonly PlanificadorRutasOrTools _planificador = new(segundosLimite: 1);

    private static List<Pedido> Pedidos(Zona zona, int cantidad) =>
        Enumerable.Range(0, cantidad)
            .Select(i => DomainFixtures.CrearPedido(zona, lat: -0.009 + i * 0.0015, lon: -0.009 + i * 0.0015))
            .ToList();

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
    public void Planificar_ConPocosPedidos_UsaUnaSolaRutaPequena()
    {
        var zona = DomainFixtures.CrearZona(max: 4);
        var pedidos = Pedidos(zona, 2);
        var repartidores = new[] { DomainFixtures.CrearRepartidor(4), DomainFixtures.CrearRepartidor(4) };

        var plan = _planificador.Planificar(zona, pedidos, repartidores);

        Assert.Single(plan);
        Assert.Equal(2, plan[0].PedidosEnOrden.Count);
    }

    [Fact]
    public void Planificar_SinCapacidadSuficiente_DejaPedidosSinAsignar()
    {
        var zona = DomainFixtures.CrearZona(max: 4);
        var pedidos = Pedidos(zona, 5);

        var plan = _planificador.Planificar(zona, pedidos, [DomainFixtures.CrearRepartidor(2)]);

        Assert.Equal(2, plan.SelectMany(a => a.PedidosEnOrden).Count());
    }

    [Fact]
    public void Planificar_SinRepartidores_DevuelveVacio()
    {
        var zona = DomainFixtures.CrearZona();

        Assert.Empty(_planificador.Planificar(zona, Pedidos(zona, 3), []));
    }
}
