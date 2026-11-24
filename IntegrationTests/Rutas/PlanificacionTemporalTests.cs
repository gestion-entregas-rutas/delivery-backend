using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using IntegrationTests.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fabrica = IntegrationTests.Autenticacion.AutenticacionTests.Fabrica;

namespace IntegrationTests.Rutas;

public sealed class PlanificacionTemporalTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    [Fact]
    public async Task Api_RegistraIntervalos_ExigeGps_PersisteEstimaciones_YDejaImposiblePendiente()
    {
        await using var app = new Fabrica();
        using var cliente = app.CreateClient();
        await app.Crear("planificador", RolUsuario.Administrador, false);
        await AmpliacionesPedidosTests.Ingresar(cliente, "planificador");
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var reglas = new ReglasPlanificacion();
        Guid zonaId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var zona = Zona.Crear("Temporal", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 4);
            zonaId = zona.Id;
            db.Zonas.Add(zona);
            db.Repartidores.Add(Repartidor.Crear("Uno", "0991", 4));
            await db.SaveChangesAsync();
        }
        RegistrarPedidoRequest Solicitud(string nombre, int fin) => new(nombre, "0991234567", "Calle", 0, 0,
            fecha, new(8, 0), zonaId, CompromisoInicioEn: reglas.Instante(fecha, new(8, 0)),
            CompromisoFinEn: reglas.Instante(fecha, new(8, fin)), TiempoServicioMinutos: 5);
        var registro = await cliente.PostAsJsonAsync("/api/pedidos", Solicitud("Viable", 30));
        registro.EnsureSuccessStatusCode();
        var pedido = (await registro.Content.ReadFromJsonAsync<RegistroPedidoDto>(Json))!.Pedido;
        Assert.Equal("SinEstimacion", pedido.Puntualidad!.Estado);
        var imposible = await cliente.PostAsJsonAsync("/api/pedidos", Solicitud("Sin tiempo", 5));
        imposible.EnsureSuccessStatusCode(); // Intervalo válido, pero falta tiempo para traslado + atención.
        var sinGps = await cliente.PostAsJsonAsync("/api/rutas/armar", new ArmarRutasRequest(fecha, zonaId));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sinGps.StatusCode);
        var armado = await cliente.PostAsJsonAsync("/api/rutas/armar", new ArmarRutasRequest(fecha, zonaId, new(0, 0)));
        armado.EnsureSuccessStatusCode();
        var resultado = (await armado.Content.ReadFromJsonAsync<ArmadoRutasDto>(Json))!;
        Assert.Equal(1, resultado.PedidosSinAsignar);
        var ruta = Assert.Single(resultado.Rutas);
        var parada = Assert.Single(ruta.Paradas);
        Assert.Equal(pedido.Id, parada.Pedido.Id);
        Assert.Equal(reglas.Instante(fecha, new(8, 10)), parada.FinalizacionEstimadaEn);
        Assert.Equal("DentroDelPlazoPrevisto", parada.Pedido.Puntualidad!.Estado);
        var recargada = await cliente.GetFromJsonAsync<RutaDto>($"/api/rutas/{ruta.Id}", Json);
        Assert.Equal(parada.FinalizacionEstimadaEn, recargada!.Paradas[0].FinalizacionEstimadaEn);
        Assert.Equal(new PuntoDto(0, 0), recargada.Origen);
        using var verificacion = app.Services.CreateScope();
        var contexto = verificacion.ServiceProvider.GetRequiredService<IAppDbContext>();
        Assert.Equal(1, await contexto.Pedidos.CountAsync(p => p.Estado == EstadoPedido.Pendiente));
        // Los clientes existentes pueden seguir consultando el contrato OpenAPI.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public async Task Api_RechazaIntervaloIncompleto_YNoBorraCompromisoAlEditarConContratoAnterior()
    {
        await using var app = new Fabrica();
        using var cliente = app.CreateClient();
        await app.Crear("editor", RolUsuario.Administrador, false);
        await AmpliacionesPedidosTests.Ingresar(cliente, "editor");
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var reglas = new ReglasPlanificacion();
        Guid zonaId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var zona = Zona.Crear("Prueba", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)]);
            zonaId = zona.Id; db.Zonas.Add(zona); await db.SaveChangesAsync();
        }
        var solicitud = new RegistrarPedidoRequest("Cliente", "0991234567", "Calle", 0, 0, fecha, new(10, 0), zonaId,
            CompromisoInicioEn: reglas.Instante(fecha, new(10, 0)));
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/pedidos", solicitud)).StatusCode);
        solicitud = solicitud with { CompromisoFinEn = reglas.Instante(fecha, new(10, 30)) };
        var registro = await cliente.PostAsJsonAsync("/api/pedidos", solicitud);
        registro.EnsureSuccessStatusCode();
        var pedido = (await registro.Content.ReadFromJsonAsync<RegistroPedidoDto>(Json))!.Pedido;
        var editar = await cliente.PutAsJsonAsync($"/api/pedidos/{pedido.Id}",
            new ActualizarPedidoRequest("Nuevo nombre", "0991234567", "Calle", 0, 0, fecha, new(10, 0), pedido.Version, zonaId));
        editar.EnsureSuccessStatusCode();
        var actualizado = (await editar.Content.ReadFromJsonAsync<PedidoDto>(Json))!;
        Assert.Equal(solicitud.CompromisoFinEn, actualizado.CompromisoFinEn);
        Assert.Equal("Nuevo nombre", actualizado.ClienteNombre);
    }
}
