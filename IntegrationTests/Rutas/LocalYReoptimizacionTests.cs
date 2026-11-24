using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Infrastructure.ExternalServices;
using IntegrationTests.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fabrica = IntegrationTests.Autenticacion.AutenticacionTests.Fabrica;

namespace IntegrationTests.Rutas;

/// <summary>Ubicación del local guardada en el servidor y reoptimización de rutas planificadas.</summary>
public sealed class LocalYReoptimizacionTests
{
    private const string Clave = "Temporal-2026!Segura";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private static readonly DateOnly Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

    private static async Task<(Guid ZonaId, Guid[] Repartidores)> Sembrar(IServiceProvider servicios, int pedidos = 0)
    {
        using var scope = servicios.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var hash = new ProtectorClaves().Proteger(Clave);
        var admin = Usuario.Crear("admin", hash, RolUsuario.Administrador, null);
        admin.CambiarCredenciales("admin", hash);
        var a = Repartidor.Crear("Ana", "0991", 4);
        var b = Repartidor.Crear("Beto", "0992", 4);
        var zona = Zona.Crear("Centro", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 4);
        db.Usuarios.AddRange(admin, Usuario.CrearCliente("Cliente Prueba", "cliente@test.com", null, hash));
        db.Repartidores.AddRange(a, b);
        db.Zonas.Add(zona);
        for (var i = 0; i < pedidos; i++)
            db.Pedidos.Add(Pedido.Registrar($"Cliente {i}", "0990000000", $"Calle {i}",
                new Domain.ValueObjects.GeoPoint(0.01 * i, 0.01 * i), zona, Fecha, new TimeOnly(10, 0),
                DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return (zona.Id, [a.Id, b.Id]);
    }

    [Fact]
    public async Task LocalGuardado_HabilitaIntervalosDeLaTienda()
    {
        await using var app = new Fabrica();
        await Sembrar(app.Services);
        using var admin = app.CreateClient();
        await AmpliacionesPedidosTests.Ingresar(admin, "admin");
        using var cliente = app.CreateClient();
        await AmpliacionesPedidosTests.Ingresar(cliente, "cliente@test.com");
        var url = $"/api/entregas/intervalos?fecha={Fecha:yyyy-MM-dd}&latitud=0.01&longitud=0.01";

        // Sin local configurado ni rutas previas: la tienda no puede ofrecer horarios.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.GetAsync("/api/local")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await cliente.GetAsync(url)).StatusCode);

        var guardar = await admin.PutAsJsonAsync("/api/local", new GuardarUbicacionLocalRequest("Bodega", "Av. Principal", 0, 0));
        Assert.Equal(HttpStatusCode.OK, guardar.StatusCode);
        var local = (await (await admin.GetAsync("/api/local")).Content.ReadFromJsonAsync<UbicacionLocalDto>(Json))!;
        Assert.Equal("Bodega", local.Nombre);

        var oferta = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, oferta.StatusCode);
        Assert.NotEmpty((await oferta.Content.ReadFromJsonAsync<IntervalosDisponiblesDto>(Json))!.Intervalos);

        // Solo el administrador puede cambiar el local.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.PutAsJsonAsync("/api/local", new GuardarUbicacionLocalRequest("X", "Y", 0, 0))).StatusCode);
    }

    [Fact]
    public async Task Reoptimizar_VistaPreviaNoCambiaNada_YAplicarRearmaSoloLasPlanificadas()
    {
        await using var app = new Fabrica();
        var (zonaId, _) = await Sembrar(app.Services, pedidos: 3);
        using var admin = app.CreateClient();
        await AmpliacionesPedidosTests.Ingresar(admin, "admin");
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PutAsJsonAsync("/api/local", new GuardarUbicacionLocalRequest("Bodega", "", 0, 0))).StatusCode);

        // Armado inicial: las rutas quedan planificadas.
        var armado = await admin.PostAsJsonAsync("/api/rutas/armar", new ArmarRutasRequest(Fecha, zonaId));
        Assert.Equal(HttpStatusCode.OK, armado.StatusCode);
        var inicial = (await armado.Content.ReadFromJsonAsync<ArmadoRutasDto>(Json))!;
        Assert.NotEmpty(inicial.Rutas);

        // Entra un pedido nuevo después del armado.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var zona = await db.Zonas.FirstAsync(z => z.Id == zonaId);
            db.Pedidos.Add(Pedido.Registrar("Tarde", "0990000009", "Calle tarde",
                new Domain.ValueObjects.GeoPoint(0.02, 0.02), zona, Fecha, new TimeOnly(11, 0),
                DateOnly.FromDateTime(DateTime.UtcNow), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var previa = await admin.PostAsJsonAsync("/api/rutas/reoptimizar", new ReoptimizarRutasRequest(Fecha, zonaId));
        Assert.Equal(HttpStatusCode.OK, previa.StatusCode);
        var vista = (await previa.Content.ReadFromJsonAsync<ReoptimizacionDto>(Json))!;
        Assert.False(vista.Aplicada);
        Assert.Equal(inicial.Rutas.Count, vista.RutasReemplazadas);
        Assert.Equal(4, vista.Propuestas.Sum(p => p.Paradas.Count) + vista.PedidosSinAsignar);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            // La vista previa no cancela ni crea rutas.
            Assert.Equal(inicial.Rutas.Count, await db.Rutas.CountAsync(r => r.Estado == EstadoRuta.Planificada));
            Assert.Equal(1, await db.Pedidos.CountAsync(p => p.Estado == EstadoPedido.Pendiente));
        }

        var aplicar = await admin.PostAsJsonAsync("/api/rutas/reoptimizar", new ReoptimizarRutasRequest(Fecha, zonaId, null, true));
        Assert.Equal(HttpStatusCode.OK, aplicar.StatusCode);
        var resultado = (await aplicar.Content.ReadFromJsonAsync<ReoptimizacionDto>(Json))!;
        Assert.True(resultado.Aplicada);
        Assert.NotEmpty(resultado.Rutas);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            Assert.Equal(inicial.Rutas.Count, await db.Rutas.CountAsync(r => r.Estado == EstadoRuta.Cancelada));
            Assert.Equal(resultado.Rutas.Count, await db.Rutas.CountAsync(r => r.Estado == EstadoRuta.Planificada));
            // El pedido que llegó tarde ya tiene ruta.
            Assert.Equal(resultado.PedidosSinAsignar, await db.Pedidos.CountAsync(p => p.Estado == EstadoPedido.Pendiente));
        }
    }

    [Fact]
    public async Task Reoptimizar_NoAceptaFechasPasadas()
    {
        await using var app = new Fabrica();
        await Sembrar(app.Services);
        using var admin = app.CreateClient();
        await AmpliacionesPedidosTests.Ingresar(admin, "admin");
        var r = await admin.PostAsJsonAsync("/api/rutas/reoptimizar",
            new ReoptimizarRutasRequest(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3))));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
    }
}
