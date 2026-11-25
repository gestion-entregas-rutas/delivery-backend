using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Services;
using Domain.ValueObjects;
using Infrastructure.ExternalServices;
using IntegrationTests.Pedidos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Fabrica = IntegrationTests.Autenticacion.AutenticacionTests.Fabrica;

namespace IntegrationTests.Entregas;

/// <summary>Etapa 8: flujo de intervalos (oferta, reservas) y validación de cambios de ruta con el API real.</summary>
public sealed class IntervalosEntregaTests
{
    private const string Clave = "Temporal-2026!Segura";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private static readonly ReglasPlanificacion Reglas = new();
    private static readonly DateOnly Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
    private static DateTimeOffset Hora(int h, int m = 0) => Reglas.Instante(Fecha, new(h, m));

    /// <summary>Misma base en memoria que <see cref="Fabrica"/>, con la ubicación del local configurada.</summary>
    private static WebApplicationFactory<Program> ConLocal(Fabrica fabrica) => fabrica.WithWebHostBuilder(b =>
    {
        b.UseSetting("PlanificacionRutas:OrigenLatitud", "0");
        b.UseSetting("PlanificacionRutas:OrigenLongitud", "0");
        b.UseSetting("PlanificacionRutas:FinOfertaIntervalos", "18:00:00");
    });

    private static async Task<Guid> Sembrar(IServiceProvider servicios, int capacidadRepartidor = 4)
    {
        using var scope = servicios.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var hash = new ProtectorClaves().Proteger(Clave);
        var admin = Usuario.Crear("admin", hash, RolUsuario.Administrador, null);
        admin.CambiarCredenciales("admin", hash);
        var repartidor = Repartidor.Crear("Moto", "0991", capacidadRepartidor);
        var conductor = Usuario.Crear("conductor", hash, RolUsuario.Conductor, repartidor.Id);
        conductor.CambiarCredenciales("conductor", hash);
        var zona = Zona.Crear("Centro", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 4);
        db.Usuarios.AddRange(admin, conductor, Usuario.CrearCliente("Cliente Prueba", "cliente@test.com", null, hash));
        db.Repartidores.Add(repartidor);
        db.Zonas.Add(zona);
        await db.SaveChangesAsync();
        return zona.Id;
    }

    [Fact]
    public async Task Tienda_ConsultaIntervalos_ConCoberturaYFechaValidas()
    {
        await using var fabrica = new Fabrica();
        await using var app = ConLocal(fabrica);
        await Sembrar(app.Services);
        using var cliente = app.CreateClient();
        await AmpliacionesPedidosTests.Ingresar(cliente, "cliente@test.com");

        var r = await cliente.GetAsync($"/api/entregas/intervalos?fecha={Fecha:yyyy-MM-dd}&latitud=0.01&longitud=0.01");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var oferta = (await r.Content.ReadFromJsonAsync<IntervalosDisponiblesDto>(Json))!;
        Assert.Equal(20, oferta.Intervalos.Count); // 08:00–18:00 en bloques de 30 minutos
        Assert.Equal(Hora(8), oferta.Intervalos[0].InicioEn);
        Assert.All(oferta.Intervalos, i => Assert.True(i.Disponible));
        Assert.Equal("HaversineAjustadoV1", oferta.ModeloEstimacion);

        var sinCobertura = await cliente.GetAsync($"/api/entregas/intervalos?fecha={Fecha:yyyy-MM-dd}&latitud=5&longitud=5");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sinCobertura.StatusCode);
        var pasada = await cliente.GetAsync($"/api/entregas/intervalos?fecha={Fecha.AddDays(-10):yyyy-MM-dd}&latitud=0&longitud=0");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pasada.StatusCode);

        using var conductor = app.CreateClient();
        await AmpliacionesPedidosTests.Ingresar(conductor, "conductor");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await conductor.GetAsync($"/api/entregas/intervalos?fecha={Fecha:yyyy-MM-dd}&latitud=0&longitud=0")).StatusCode);
    }

    [Fact]
    public async Task SinUbicacionDelLocal_NoSeInventaOrigen()
    {
        await using var app = new Fabrica();
        await Sembrar(app.Services);
        using var scope = app.Services.CreateScope();
        var oferta = scope.ServiceProvider.GetRequiredService<IDisponibilidadEntregas>();
        await Assert.ThrowsAsync<DomainException>(() => oferta.ListarAsync(Fecha, 0, 0, default));
    }

    [Fact]
    public async Task ReservaConPagoPendiente_OcupaCapacidad_YAlVencerSeLibera()
    {
        await using var fabrica = new Fabrica();
        await using var app = ConLocal(fabrica);
        await Sembrar(app.Services, capacidadRepartidor: 1);
        Guid ordenId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var ahora = DateTimeOffset.UtcNow;
            var orden = Orden.Crear("ORD-PRUEBA", Guid.NewGuid(), TipoEntrega.Domicilio, 10, ahora, null);
            orden.ConfigurarDomicilio("Dest", "0991", "Calle", 0, 0, Fecha, new(8, 0), null);
            orden.ReservarIntervalo(Hora(8), Hora(8, 30));
            db.Ordenes.Add(orden);
            db.PagosOrden.Add(PagoOrden.Crear(orden.Id, "Simulado", "SIM", EstadoPago.Pendiente, ahora, ahora.AddMinutes(15)));
            await db.SaveChangesAsync();
            ordenId = orden.Id;
        }

        using (var scope = app.Services.CreateScope())
        {
            var oferta = scope.ServiceProvider.GetRequiredService<IDisponibilidadEntregas>();
            // El único cupo está reservado: otro cliente no puede tomar ningún intervalo.
            var error = await Assert.ThrowsAsync<ConflictoException>(() =>
                oferta.VerificarAsync(Fecha, new GeoPoint(0, 0), Hora(9), Hora(9, 30), default));
            Assert.Equal("INTERVALO_NO_DISPONIBLE", error.Codigo);
            Assert.All((await oferta.ListarAsync(Fecha, 0, 0, default)).Intervalos, i => Assert.False(i.Disponible));
            // Intervalos fuera de la rejilla se rechazan como regla de negocio.
            await Assert.ThrowsAsync<DomainException>(() =>
                oferta.VerificarAsync(Fecha, new GeoPoint(0, 0), Hora(9, 10), Hora(9, 40), default));
        }

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var pago = db.PagosOrden.Single(p => p.OrdenId == ordenId);
            pago.Expirar();
            db.Ordenes.Single(o => o.Id == ordenId).MarcarExpirada();
            await db.SaveChangesAsync();
        }

        using (var scope = app.Services.CreateScope())
        {
            var oferta = scope.ServiceProvider.GetRequiredService<IDisponibilidadEntregas>();
            await oferta.VerificarAsync(Fecha, new GeoPoint(0, 0), Hora(9), Hora(9, 30), default);
        }
    }

    [Fact]
    public async Task Panel_RegistraPedidoConVerificacion_NoPrometeDosVecesElUltimoCupo()
    {
        await using var fabrica = new Fabrica();
        await using var app = ConLocal(fabrica);
        var zonaId = await Sembrar(app.Services, capacidadRepartidor: 1);
        using var admin = app.CreateClient();
        await AmpliacionesPedidosTests.Ingresar(admin, "admin");
        RegistrarPedidoRequest Solicitud(string nombre, DateTimeOffset desde, DateTimeOffset hasta) => new(nombre, "0991234567", "Calle", 0, 0,
            Fecha, new(9, 0), zonaId, CompromisoInicioEn: desde, CompromisoFinEn: hasta, VerificarDisponibilidad: true);

        var primero = await admin.PostAsJsonAsync("/api/pedidos", Solicitud("Uno", Hora(9), Hora(9, 30)));
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        var pedido = (await primero.Content.ReadFromJsonAsync<RegistroPedidoDto>(Json))!.Pedido;
        Assert.Equal(Hora(9), pedido.CompromisoInicioEn);

        // Un solo repartidor con un solo cupo: ya no queda horario para otro pedido ese día.
        var segundo = await admin.PostAsJsonAsync("/api/pedidos", Solicitud("Dos", Hora(10), Hora(10, 30)));
        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Equal("INTERVALO_NO_DISPONIBLE", (await segundo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());

        var fueraDeRejilla = await admin.PostAsJsonAsync("/api/pedidos", Solicitud("Tres", Hora(9, 10), Hora(9, 40)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fueraDeRejilla.StatusCode);
        var sinHorario = await admin.PostAsJsonAsync("/api/pedidos", new RegistrarPedidoRequest("Cuatro", "0991", "Calle", 0, 0,
            Fecha, new(9, 0), zonaId, VerificarDisponibilidad: true));
        Assert.Equal(HttpStatusCode.BadRequest, sinHorario.StatusCode);
    }

    [Fact]
    public async Task CambioDeRuta_QuePoneEnRiesgo_SeRechaza_SalvoForzar_YAvancesRecalculan()
    {
        await using var fabrica = new Fabrica();
        await using var app = ConLocal(fabrica);
        var zonaId = await Sembrar(app.Services);
        using var admin = app.CreateClient();
        await AmpliacionesPedidosTests.Ingresar(admin, "admin");

        async Task<Guid> Registrar(string nombre, double longitud, DateTimeOffset desde, DateTimeOffset hasta)
        {
            var r = await admin.PostAsJsonAsync("/api/pedidos", new RegistrarPedidoRequest(nombre, "0991234567", "Calle", 0, longitud,
                Fecha, new(8, 0), zonaId, CompromisoInicioEn: desde, CompromisoFinEn: hasta));
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<RegistroPedidoDto>(Json))!.Pedido.Id;
        }
        var cerca = await Registrar("Cerca", 0, Hora(8), Hora(8, 30));    // en el local
        var lejos = await Registrar("Lejos", 0.05, Hora(8), Hora(9, 30)); // a ~5,6 km

        var armado = await admin.PostAsJsonAsync("/api/rutas/armar", new ArmarRutasRequest(Fecha, zonaId, new(0, 0), Hora(8)));
        armado.EnsureSuccessStatusCode();
        var ruta = Assert.Single((await armado.Content.ReadFromJsonAsync<ArmadoRutasDto>(Json))!.Rutas);
        Assert.Equal([cerca, lejos], ruta.Paradas.Select(p => p.Pedido.Id));
        Assert.Empty(ruta.Alertas!);

        // Visitar primero la lejana deja la cercana fuera de su intervalo.
        var url = $"/api/rutas/{ruta.Id}/paradas/orden";
        var rechazo = await admin.PutAsJsonAsync(url, new ReordenarParadasRequest([lejos, cerca], ruta.Version));
        Assert.Equal(HttpStatusCode.Conflict, rechazo.StatusCode);
        var problema = await rechazo.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CAMBIO_PONE_EN_RIESGO", problema.GetProperty("codigo").GetString());
        Assert.Equal(cerca, problema.GetProperty("pedidosEnRiesgo")[0].GetProperty("pedidoId").GetGuid());
        var sinCambios = await admin.GetFromJsonAsync<RutaDto>($"/api/rutas/{ruta.Id}", Json);
        Assert.Equal([cerca, lejos], sinCambios!.Paradas.Select(p => p.Pedido.Id));

        var forzado = await admin.PutAsJsonAsync(url, new ReordenarParadasRequest([lejos, cerca], ruta.Version, Forzar: true));
        Assert.Equal(HttpStatusCode.OK, forzado.StatusCode);
        var conAlerta = (await forzado.Content.ReadFromJsonAsync<RutaDto>(Json))!;
        var alerta = Assert.Single(conAlerta.Alertas!);
        Assert.Equal(cerca, alerta.PedidoId);
        Assert.Equal("RetrasoPrevisto", alerta.Estado);
        Assert.True(alerta.RetrasoMinutos > 0);
        Assert.All(conAlerta.Paradas, p => Assert.NotNull(p.FinalizacionEstimadaEn)); // estimación vigente, no invalidada

        // Avances del reparto: iniciar y entregar recalculan las paradas restantes.
        (await admin.PostAsync($"/api/rutas/{ruta.Id}/iniciar", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/seguimiento/pedidos/{lejos}/en-camino", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/seguimiento/pedidos/{lejos}/entregado", null)).EnsureSuccessStatusCode();
        var enCurso = await admin.GetFromJsonAsync<RutaDto>($"/api/rutas/{ruta.Id}", Json);
        Assert.Equal(EstadoRuta.EnCurso, enCurso!.Estado);
        Assert.NotNull(enCurso.EstimacionesActualizadasEn);
        Assert.Null(enCurso.Paradas.Single(p => p.Pedido.Id == lejos).FinalizacionEstimadaEn);
        Assert.NotNull(enCurso.Paradas.Single(p => p.Pedido.Id == cerca).FinalizacionEstimadaEn);
        // Se entregó dos días antes del compromiso: la puntualidad lo refleja sin alterar el intervalo.
        Assert.Equal("EntregadoAntesDelIntervalo", enCurso.Paradas.Single(p => p.Pedido.Id == lejos).Pedido.Puntualidad!.Estado);

        var recalculo = await admin.PostAsync($"/api/rutas/{ruta.Id}/estimaciones/recalcular", null);
        Assert.Equal(HttpStatusCode.OK, recalculo.StatusCode);
    }
}
