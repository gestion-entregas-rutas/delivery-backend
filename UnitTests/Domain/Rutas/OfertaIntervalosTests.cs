using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Domain.Rutas;

/// <summary>Etapas 5, 6 y 8: rejilla de intervalos, simulación de capacidad y re-estimación.</summary>
public sealed class OfertaIntervalosTests
{
    private static readonly DateOnly Fecha = DomainFixtures.Hoy;
    private static readonly ReglasPlanificacion Reglas = new()
    {
        MargenTrasladoMinutos = 0, FactorDistancia = 1, VelocidadPromedioKmh = 30,
        FinOfertaIntervalos = new(10, 0), DuracionIntervaloMinutos = 30
    };
    private static DateTimeOffset Hora(int h, int m = 0) => Reglas.Instante(Fecha, new(h, m));
    private static ContextoPlanificacion Local() => Reglas.CrearContexto(Fecha, new(0, 0), Hora(7));
    private static DemandaEntrega Demanda(int h, int m, int finH, int finM, double lat = 0) =>
        new(Guid.NewGuid(), new GeoPoint(lat, 0), Hora(h, m), Hora(finH, finM), 5);
    private static ZonaEnSimulacion Zona(Guid id, IReadOnlyList<RutaEnSimulacion>? rutas = null, params DemandaEntrega[] pendientes) =>
        new(id, 4, rutas ?? [], pendientes);

    [Fact]
    public void Rejilla_DesdeAperturaHastaCierreDeOferta_YSoloAceptaIntervalosExactos()
    {
        var intervalos = Reglas.IntervalosDelDia(Fecha);
        Assert.Equal(4, intervalos.Count);
        Assert.Equal((Hora(8), Hora(8, 30)), intervalos[0]);
        Assert.Equal((Hora(9, 30), Hora(10)), intervalos[^1]);
        Assert.True(Reglas.EsIntervaloOfertado(Fecha, Hora(8, 30), Hora(9)));
        Assert.False(Reglas.EsIntervaloOfertado(Fecha, Hora(8, 15), Hora(8, 45)));
        Assert.False(Reglas.EsIntervaloOfertado(Fecha, Hora(9, 30), Hora(10, 30)));
        Assert.Throws<DomainException>(() => new ReglasPlanificacion().IntervalosDelDia(Fecha));
    }

    [Fact]
    public void Configuracion_RechazaOrigenIncompleto_YFinDeOfertaAntesDeApertura()
    {
        Assert.Throws<DomainException>(() => new ReglasPlanificacion { OrigenLatitud = -1.2 }.Validar());
        Assert.Throws<DomainException>(() => new ReglasPlanificacion { FinOfertaIntervalos = new(7, 0) }.Validar());
        Assert.Equal(new GeoPoint(-1.2, -78.6), new ReglasPlanificacion { OrigenLatitud = -1.2, OrigenLongitud = -78.6 }.OrigenLocal);
    }

    [Fact]
    public void EstimacionTolerante_MarcaExcesos_MientrasLaEstrictaDevuelveNull()
    {
        var tarde = Demanda(8, 0, 8, 3);
        var estimacion = CalculadorTiemposRuta.Estimar([tarde], Local());
        Assert.True(Assert.Single(estimacion.Paradas).ExcedeCompromiso);
        Assert.Contains(tarde.Id, estimacion.Atrasadas);
        Assert.Null(CalculadorTiemposRuta.Evaluar([tarde], Local()));
    }

    [Fact]
    public void HorarioImposible_NoSeAdmiteAunqueSobrenRepartidores()
    {
        var zona = Guid.NewGuid();
        // A ~33 km del local: 66 minutos de traslado no caben en un intervalo de 8:00 a 8:30.
        var lejos = Demanda(8, 0, 8, 30, lat: 0.3);
        var libres = new[] { new VehiculoLibre(Guid.NewGuid(), 4), new VehiculoLibre(Guid.NewGuid(), 4) };
        Assert.False(SimuladorCapacidad.AdmiteCandidata([Zona(zona)], zona, lejos, libres, Local()));
        Assert.True(SimuladorCapacidad.AdmiteCandidata([Zona(zona)], zona, Demanda(9, 0, 9, 30, lat: 0.3), libres, Local()));
    }

    [Fact]
    public void SinRepartidores_NiRutasAbiertas_NoHayDisponibilidad()
    {
        var zona = Guid.NewGuid();
        Assert.False(SimuladorCapacidad.AdmiteCandidata([Zona(zona)], zona, Demanda(8, 0, 8, 30), [], Local()));
        Assert.False(SimuladorCapacidad.AdmiteCandidata([Zona(zona)], zona, Demanda(8, 0, 8, 30),
            [new VehiculoLibre(Guid.NewGuid(), 4)], contextoRutasNuevas: null));
    }

    [Fact]
    public void ReservaVigente_ConsumeLaCapacidad_YNoSePrometeDosVeces()
    {
        var zona = Guid.NewGuid();
        var unCupo = new[] { new VehiculoLibre(Guid.NewGuid(), 1) };
        var reservada = Demanda(8, 0, 8, 30);
        Assert.True(SimuladorCapacidad.AdmiteCandidata([Zona(zona)], zona, Demanda(8, 0, 8, 30), unCupo, Local()));
        Assert.False(SimuladorCapacidad.AdmiteCandidata([Zona(zona, null, reservada)], zona, Demanda(8, 0, 8, 30), unCupo, Local()));
        var dosCupos = new[] { new VehiculoLibre(Guid.NewGuid(), 2) };
        Assert.True(SimuladorCapacidad.AdmiteCandidata([Zona(zona, null, reservada)], zona, Demanda(8, 0, 8, 30), dosCupos, Local()));
    }

    [Fact]
    public void CandidataQueRetrasariaUnaEntregaComprometida_SeRechazaSiNoHayOtroRepartidor()
    {
        var zona = Guid.NewGuid();
        var comprometida = Demanda(8, 0, 8, 10);           // ajustada: termina 8:05 desde el local
        var ruta = new RutaEnSimulacion(Guid.NewGuid(), Local(), [comprometida], CuposLibres: 3);
        // Ubicada a ~11 km (22 min): solo cabe antes de la comprometida, lo que la haría llegar tarde.
        var candidata = Demanda(8, 0, 8, 30, lat: 0.1);
        Assert.False(SimuladorCapacidad.AdmiteCandidata([Zona(zona, [ruta])], zona, candidata, [], Local()));
        Assert.True(SimuladorCapacidad.AdmiteCandidata([Zona(zona, [ruta])], zona, candidata,
            [new VehiculoLibre(Guid.NewGuid(), 4)], Local()));
    }

    [Fact]
    public void RutaYaAtrasada_NoBloqueaInsercionesQueNoEmpeoran()
    {
        var zona = Guid.NewGuid();
        var vencida = Demanda(8, 0, 8, 3);                  // ya no se puede cumplir
        var ruta = new RutaEnSimulacion(Guid.NewGuid(), Local(), [vencida], CuposLibres: 3);
        Assert.True(SimuladorCapacidad.AdmiteCandidata([Zona(zona, [ruta])], zona, Demanda(9, 0, 9, 30), [], Local()));
    }

    [Fact]
    public void ZonaDeLaCandidataSeProcesaAlFinal_YOtrasZonasConsumenRepartidores()
    {
        var zonaA = Guid.NewGuid();
        var zonaB = Guid.NewGuid();
        var unRepartidor = new[] { new VehiculoLibre(Guid.NewGuid(), 4) };
        var otraZona = Zona(zonaA, null, Demanda(8, 0, 8, 30, lat: 0.05));
        var ordenadas = SimuladorCapacidad.OrdenarParaCandidata([Zona(zonaB), otraZona], zonaB, 4);
        Assert.Equal(zonaB, ordenadas[^1].ZonaId);
        Assert.False(SimuladorCapacidad.AdmiteCandidata(ordenadas, zonaB, Demanda(8, 0, 8, 30, lat: -0.05), unRepartidor, Local()));
    }

    [Fact]
    public void Reestimacion_EnCurso_SoloCalculaParadasRestantes()
    {
        var zona = DomainFixtures.CrearZona();
        var repartidor = DomainFixtures.CrearRepartidor();
        var ruta = Ruta.Crear(zona, repartidor, Fecha, DomainFixtures.Ahora);
        var a = DomainFixtures.CrearPedido(zona);
        var b = DomainFixtures.CrearPedido(zona, lat: 0.005);
        ruta.AgregarPedido(a); ruta.AgregarPedido(b);
        ruta.Iniciar(repartidor, Hora(8));
        ruta.MarcarEnCamino(a.Id);
        ruta.RegistrarEntrega(a.Id, repartidor, Hora(8, 10));

        var restantes = ruta.ParadasRestantes.Select(p => p.Pedido).ToList();
        Assert.Equal([b.Id], restantes.Select(p => p.Id));
        var contexto = Reglas.ContextoOperativo(Fecha, a.Ubicacion, Hora(8, 10));
        ruta.ActualizarEstimaciones(CalculadorTiemposRuta.Estimar(restantes, contexto), Hora(8, 10));

        Assert.Null(ruta.Paradas[0].FinalizacionEstimadaEn);
        Assert.NotNull(ruta.Paradas[1].FinalizacionEstimadaEn);
        Assert.Null(ruta.SalidaProgramadaEn); // en curso no se reescribe la salida programada
        Assert.Throws<DomainException>(() => ruta.ActualizarEstimaciones(
            CalculadorTiemposRuta.Estimar([a, b], contexto), Hora(8, 10)));
        Assert.Equal(EstadoRuta.EnCurso, ruta.Estado);
    }

    [Fact]
    public void Migraciones_IncluyenTodoElModelo()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=solo_modelo", n => n.UseNetTopologySuite()).Options;
        using var db = new AppDbContext(opciones);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
