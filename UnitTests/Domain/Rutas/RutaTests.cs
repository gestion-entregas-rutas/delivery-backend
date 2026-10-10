using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Services;

namespace UnitTests.Domain;

public class RutaTests
{
    private static (Zona zona, Repartidor rep, Ruta ruta) Escenario(int maxZona = 4, int capRep = 4)
    {
        var zona = DomainFixtures.CrearZona(maxZona);
        var rep = DomainFixtures.CrearRepartidor(capRep);
        return (zona, rep, Ruta.Crear(zona, rep, DomainFixtures.Hoy, DomainFixtures.Ahora));
    }

    [Theory]
    [InlineData(4, 6, 4)]
    [InlineData(4, 2, 2)]
    public void Crear_CapacidadEsElMenorEntreZonaYRepartidor(int zona, int rep, int esperada)
    {
        var (_, _, ruta) = Escenario(zona, rep);
        Assert.Equal(esperada, ruta.Capacidad);
    }

    [Fact]
    public void Crear_ConRepartidorNoDisponible_Falla()
    {
        var zona = DomainFixtures.CrearZona();
        var rep = DomainFixtures.CrearRepartidor();
        rep.MarcarNoDisponible();
        Assert.Throws<DomainException>(() => Ruta.Crear(zona, rep, DomainFixtures.Hoy, DomainFixtures.Ahora));
    }

    [Fact]
    public void AgregarPedido_MasAllaDeLaCapacidad_Falla()
    {
        var (zona, _, ruta) = Escenario(maxZona: 2);
        ruta.AgregarPedido(DomainFixtures.CrearPedido(zona));
        ruta.AgregarPedido(DomainFixtures.CrearPedido(zona));

        Assert.Throws<DomainException>(() => ruta.AgregarPedido(DomainFixtures.CrearPedido(zona)));
    }

    [Fact]
    public void AgregarPedido_AsignaYOrdenaParadas()
    {
        var (zona, _, ruta) = Escenario();
        var a = DomainFixtures.CrearPedido(zona);
        var b = DomainFixtures.CrearPedido(zona);
        ruta.AgregarPedido(a);
        ruta.AgregarPedido(b, posicion: 0);

        Assert.Equal([b.Id, a.Id], ruta.Paradas.Select(p => p.PedidoId));
        Assert.Equal([1, 2], ruta.Paradas.Select(p => p.Orden));
        Assert.Equal(EstadoPedido.AsignadoARuta, a.Estado);
    }

    [Fact]
    public void AgregarPedido_YaAsignadoAOtraRuta_Falla()
    {
        var (zona, rep, ruta1) = Escenario();
        var ruta2 = Ruta.Crear(zona, rep, DomainFixtures.Hoy, DomainFixtures.Ahora);
        var pedido = DomainFixtures.CrearPedido(zona);
        ruta1.AgregarPedido(pedido);

        Assert.Throws<TransicionInvalidaException>(() => ruta2.AgregarPedido(pedido));
    }

    [Fact]
    public void AgregarPedido_DeOtraFecha_Falla()
    {
        var (zona, _, ruta) = Escenario();
        var pedido = DomainFixtures.CrearPedido(zona, fecha: DomainFixtures.Hoy.AddDays(1));
        Assert.Throws<DomainException>(() => ruta.AgregarPedido(pedido));
    }

    [Fact]
    public void QuitarPedido_LoDevuelveAPendiente()
    {
        var (zona, _, ruta) = Escenario();
        var pedido = DomainFixtures.CrearPedido(zona);
        ruta.AgregarPedido(pedido);
        ruta.QuitarPedido(pedido.Id);

        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
        Assert.Empty(ruta.Paradas);
    }

    [Fact]
    public void FlujoCompleto_EntregarTodo_CompletaRutaYLiberaRepartidor()
    {
        var (zona, rep, ruta) = Escenario();
        var pedido = DomainFixtures.CrearPedido(zona);
        ruta.AgregarPedido(pedido);

        ruta.Iniciar(rep, DomainFixtures.Ahora);
        Assert.Equal(EstadoRepartidor.EnRuta, rep.Estado);

        ruta.MarcarEnCamino(pedido.Id);
        ruta.RegistrarEntrega(pedido.Id, rep, DomainFixtures.Ahora);

        Assert.Equal(EstadoRuta.Completada, ruta.Estado);
        Assert.Equal(EstadoRepartidor.Disponible, rep.Estado);
    }

    [Fact]
    public void Iniciar_SinPedidos_Falla()
    {
        var (_, rep, ruta) = Escenario();
        Assert.Throws<DomainException>(() => ruta.Iniciar(rep, DomainFixtures.Ahora));
    }

    [Fact]
    public void RutaEnCurso_NoPermiteInsertarAntesDeParadasYaEnCamino()
    {
        var (zona, rep, ruta) = Escenario();
        var a = DomainFixtures.CrearPedido(zona);
        var b = DomainFixtures.CrearPedido(zona);
        ruta.AgregarPedido(a);
        ruta.AgregarPedido(b);
        ruta.Iniciar(rep, DomainFixtures.Ahora);
        ruta.MarcarEnCamino(a.Id);

        Assert.Equal(1, ruta.PosicionMinimaInsercion);
        Assert.Throws<DomainException>(() => ruta.AgregarPedido(DomainFixtures.CrearPedido(zona), posicion: 0));
    }

    [Fact]
    public void Cancelar_DevuelveLosPedidosAPendientes()
    {
        var (zona, _, ruta) = Escenario();
        var pedido = DomainFixtures.CrearPedido(zona);
        ruta.AgregarPedido(pedido);
        ruta.Cancelar();

        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
        Assert.Equal(EstadoRuta.Cancelada, ruta.Estado);
    }

    [Fact]
    public void Insercion_ElegiLaPosicionQueMenosKilometrosAgrega()
    {
        var (zona, _, ruta) = Escenario();
        ruta.AgregarPedido(DomainFixtures.CrearPedido(zona, 0, -0.008));
        ruta.AgregarPedido(DomainFixtures.CrearPedido(zona, 0, 0.008));
        var nuevo = DomainFixtures.CrearPedido(zona, 0, 0.0);

        var propuesta = InsercionRutas.BuscarMejor(nuevo, [ruta]);

        Assert.NotNull(propuesta);
        Assert.Equal(1, propuesta.Value.Posicion); // entre las dos paradas: costo casi cero
    }

    [Fact]
    public void Insercion_ConRutaLlena_DevuelveNull()
    {
        var (zona, _, ruta) = Escenario(maxZona: 1);
        ruta.AgregarPedido(DomainFixtures.CrearPedido(zona));

        Assert.Null(InsercionRutas.BuscarMejor(DomainFixtures.CrearPedido(zona), [ruta]));
    }
}
