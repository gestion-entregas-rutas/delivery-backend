using Domain.Entities;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.ExternalServices;
using UnitTests.Domain;

namespace UnitTests.Infrastructure;

public sealed class PlanificacionTemporalTests
{
    private static readonly DateOnly Fecha = DomainFixtures.Hoy;
    private static readonly ReglasPlanificacion Reglas = new()
    { MargenTrasladoMinutos = 0, FactorDistancia = 1, VelocidadPromedioKmh = 30 };
    private static DateTimeOffset Hora(int hora, int minuto = 0) => Reglas.Instante(Fecha, new(hora, minuto));
    private static ContextoPlanificacion Contexto(GeoPoint? origen = null) =>
        Reglas.CrearContexto(Fecha, origen ?? new(0, 0), Hora(7));
    private static Pedido Crear(Zona zona, int inicio = 8, int finMinuto = 30, double lat = 0, int servicio = 5)
    {
        var p = DomainFixtures.CrearPedido(zona, lat);
        p.ConfigurarCompromiso(Hora(inicio), Hora(inicio, finMinuto), servicio);
        return p;
    }

    [Fact]
    public void LlegaTemprano_EsperaYCompletaAtencionAntesDeSalir()
    {
        var p = Crear(DomainFixtures.CrearZona(), inicio: 10);
        var calculo = CalculadorTiemposRuta.Evaluar([p], Contexto())!;
        var parada = Assert.Single(calculo.Paradas);
        Assert.Equal(Hora(8), parada.LlegadaEn);
        Assert.Equal(Hora(10), parada.InicioAtencionEn);
        Assert.Equal(Hora(10, 5), parada.FinalizacionEn);
    }

    [Fact]
    public void LlegarDentroDelIntervalo_NoBastaSiLaAtencionTerminaTarde()
    {
        var p = Crear(DomainFixtures.CrearZona(), finMinuto: 5);
        var contexto = Contexto() with { Salida = Hora(8, 1) };
        Assert.Null(CalculadorTiemposRuta.Evaluar([p], contexto));
        Assert.NotNull(CalculadorTiemposRuta.Evaluar([p], Contexto()));
    }

    [Fact]
    public void UsaOrigenReal_YMargenDeTraslado()
    {
        var p = Crear(DomainFixtures.CrearZona());
        var cercano = CalculadorTiemposRuta.Evaluar([p], Contexto())!;
        var lejos = CalculadorTiemposRuta.Evaluar([p], Contexto(new(0.1, 0)));
        Assert.NotNull(lejos);
        Assert.True(lejos.Paradas[0].LlegadaEn > cercano.Paradas[0].LlegadaEn);
        var conMargen = Contexto() with { Reglas = new ReglasPlanificacion { MargenTrasladoMinutos = 5 } };
        Assert.Equal(Hora(8, 5), CalculadorTiemposRuta.Evaluar([p], conMargen)!.Paradas[0].LlegadaEn);
    }

    [Fact]
    public void AbreOtroRepartidorAunqueAmbosPedidosQuepanEnUnaMoto()
    {
        var zona = DomainFixtures.CrearZona();
        var plan = new PlanificadorRutasPropio().Planificar(zona,
            [Crear(zona, finMinuto: 5), Crear(zona, finMinuto: 5)],
            [DomainFixtures.CrearRepartidor(), DomainFixtures.CrearRepartidor()], Contexto());
        Assert.Equal(2, plan.Count);
        Assert.All(plan, a => Assert.Single(a.PedidosEnOrden));
        Assert.All(plan, a => Assert.Equal(Hora(8, 5), a.Estimacion!.Paradas[0].FinalizacionEn));
    }

    [Fact]
    public void SinTiempoSuficiente_DejaPedidoSinAsignarSinModificarSuEstado()
    {
        var zona = DomainFixtures.CrearZona();
        var pedidos = new[] { Crear(zona, finMinuto: 5), Crear(zona, finMinuto: 5) };
        var plan = new PlanificadorRutasPropio().Planificar(zona, pedidos,
            [DomainFixtures.CrearRepartidor()], Contexto());
        Assert.Single(Assert.Single(plan).PedidosEnOrden);
        Assert.All(pedidos, p => Assert.Equal(global::Domain.Enums.EstadoPedido.Pendiente, p.Estado));
    }

    [Fact]
    public void InsercionReevaluaTodasLasParadas_NoRetrasaCompromisosExistentes()
    {
        var zona = DomainFixtures.CrearZona();
        var tarde = Crear(zona, inicio: 9, finMinuto: 5);
        var flexible = Crear(zona, finMinuto: 30);
        var plan = new PlanificadorRutasPropio().Planificar(zona, [tarde, flexible],
            [DomainFixtures.CrearRepartidor()], Contexto());
        var ruta = Assert.Single(plan);
        Assert.Equal(new[] { flexible.Id, tarde.Id }, ruta.PedidosEnOrden.Select(p => p.Id));
        Assert.Equal(Hora(9, 5), ruta.Estimacion!.Paradas[1].FinalizacionEn);
    }

    [Fact]
    public void RespetaCapacidadYFinDeJornada()
    {
        var zona = DomainFixtures.CrearZona(max: 1);
        var pedidos = new[] { Crear(zona), Crear(zona) };
        var plan = new PlanificadorRutasPropio().Planificar(zona, pedidos,
            [DomainFixtures.CrearRepartidor(10)], Contexto());
        Assert.Single(Assert.Single(plan).PedidosEnOrden);
        Assert.Null(CalculadorTiemposRuta.Evaluar([pedidos[0]], Contexto() with { FinJornada = Hora(8, 4) }));
    }

    [Fact]
    public void SinOrigen_NoPlanificaCompromisosConElAlgoritmoAnterior()
    {
        var zona = DomainFixtures.CrearZona();
        Assert.Throws<DomainException>(() => new PlanificadorRutasPropio().Planificar(zona,
            [Crear(zona)], [DomainFixtures.CrearRepartidor()]));
    }

    [Fact]
    public void PedidoAntiguo_NoRecibeUnCompromisoInventado()
    {
        var zona = DomainFixtures.CrearZona();
        var p = DomainFixtures.CrearPedido(zona);
        var ruta = Assert.Single(new PlanificadorRutasPropio().Planificar(zona, [p],
            [DomainFixtures.CrearRepartidor()], Contexto()));
        Assert.NotNull(ruta.Estimacion);
        Assert.Null(p.CompromisoInicioEn);
        Assert.Null(p.CompromisoFinEn);
        Assert.Equal("SinCompromiso", Puntualidad.Calcular(null, null, null,
            ruta.Estimacion.Paradas[0].FinalizacionEn, Hora(8)).Estado);
    }

    [Fact]
    public void SalidaHoy_NoSeCalculaEnElPasadoYUsaZonaDeEcuador()
    {
        Assert.Equal(13, Hora(8).UtcDateTime.Hour);
        Assert.Equal(Hora(11), Reglas.CrearContexto(Fecha, new(0, 0), Hora(11)).Salida);
        Assert.Throws<DomainException>(() => Reglas.CrearContexto(Fecha, new(0, 0), Hora(11), Hora(8)));
        Assert.Throws<DomainException>(() => Reglas.CrearContexto(Fecha, new(0, 0), Hora(7), Hora(7)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    [InlineData(double.NaN)]
    public void RechazaVelocidadInvalida(double velocidad) =>
        Assert.Throws<DomainException>(() => new ReglasPlanificacion { VelocidadPromedioKmh = velocidad }.Validar());

    [Fact]
    public void CompromisoInvalido_NoModificaPedido()
    {
        var p = DomainFixtures.CrearPedido(DomainFixtures.CrearZona());
        Assert.Throws<DomainException>(() => p.ConfigurarCompromiso(Hora(10), null));
        Assert.Throws<DomainException>(() => p.ConfigurarCompromiso(Hora(10), Hora(9)));
        Assert.Throws<DomainException>(() => p.ConfigurarCompromiso(Hora(10), Hora(10, 3), 5));
        Assert.Null(p.CompromisoFinEn);
    }

    [Fact]
    public void CambiarOrden_InvalidaEstimacionesPeroNoElCompromiso()
    {
        var zona = DomainFixtures.CrearZona();
        var repartidor = DomainFixtures.CrearRepartidor();
        var p = Crear(zona);
        var ruta = Ruta.Crear(zona, repartidor, Fecha, Hora(7));
        ruta.AgregarPedido(p);
        ruta.AplicarEstimacion(CalculadorTiemposRuta.Evaluar([p], Contexto())!, Hora(7));
        Assert.NotNull(ruta.Paradas[0].FinalizacionEstimadaEn);
        ruta.ReordenarParadas([p.Id]);
        Assert.Null(ruta.EstimacionesActualizadasEn);
        Assert.Null(ruta.Paradas[0].FinalizacionEstimadaEn);
        Assert.Equal(Hora(8, 30), p.CompromisoFinEn);
    }

    [Fact]
    public void PuntualidadSeDerivaDeLaFinalizacion_YDistingueEstimacionDeRealidad()
    {
        Assert.Equal("SinEstimacion", Puntualidad.Calcular(Hora(10), Hora(10, 30), null, null, Hora(9)).Estado);
        var previsto = Puntualidad.Calcular(Hora(10), Hora(10, 30), null, Hora(10, 45), Hora(9));
        Assert.Equal("RetrasoPrevisto", previsto.Estado);
        Assert.Equal(15, previsto.RetrasoMinutos);
        Assert.Equal("DentroDelPlazoPrevisto", Puntualidad.Calcular(Hora(10), Hora(10, 30), null, Hora(10, 20), Hora(9)).Estado);
        Assert.Equal("PlazoVencido", Puntualidad.Calcular(Hora(10), Hora(10, 30), null, Hora(10, 20), Hora(11)).Estado);
        var real = Puntualidad.Calcular(Hora(10), Hora(10, 30), Hora(10, 35), Hora(10, 20), Hora(11));
        Assert.Equal("EntregadoConRetraso", real.Estado);
        Assert.Equal(5, real.RetrasoMinutos);
    }
}
