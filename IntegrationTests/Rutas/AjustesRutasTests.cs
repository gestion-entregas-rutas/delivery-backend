using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using IntegrationTests.Pedidos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fabrica = IntegrationTests.Autenticacion.AutenticacionTests.Fabrica;
namespace IntegrationTests.Rutas;

public sealed class AjustesRutasTests
{
    [Fact]
    public async Task Administrador_TransfiereYReordena_ConductorNoPuede_YVersionAntiguaEs409()
    {
        await using var app = new Fabrica(); using var admin = app.CreateClient(); using var conductor = app.CreateClient();
        await app.Crear("admin", RolUsuario.Administrador, false); await app.Crear("conductor", RolUsuario.Conductor, false);
        await AmpliacionesPedidosTests.Ingresar(admin, "admin"); await AmpliacionesPedidosTests.Ingresar(conductor, "conductor");
        Guid origenId, destinoId, pedidoId, otroId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var rep1 = await db.Repartidores.FirstAsync(); var rep2 = Repartidor.Crear("Dos", "0992", 4);
            var zona = Zona.Crear("Centro", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 4);
            var fecha = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            var origen = Ruta.Crear(zona, rep1, fecha, DateTimeOffset.UtcNow); var destino = Ruta.Crear(zona, rep2, fecha, DateTimeOffset.UtcNow);
            Pedido Nuevo() => Pedido.Registrar("Cliente", "099", "Calle", new(0, 0), zona, fecha, new(12, 0), fecha, DateTimeOffset.UtcNow);
            var p = Nuevo(); var otro = Nuevo(); origen.AgregarPedido(p); destino.AgregarPedido(otro);
            db.Repartidores.Add(rep2); db.Zonas.Add(zona); db.Rutas.AddRange(origen, destino); await db.SaveChangesAsync();
            origenId = origen.Id; destinoId = destino.Id; pedidoId = p.Id; otroId = otro.Id;
        }
        var url = $"/api/rutas/{origenId}/pedidos/{pedidoId}/transferir";
        var solicitud = new Application.DTOs.TransferirPedidoRequest(destinoId, 0, 0, 0);
        Assert.Equal(HttpStatusCode.Forbidden, (await conductor.PostAsJsonAsync(url, solicitud)).StatusCode);
        var respuesta = await admin.PostAsJsonAsync(url, solicitud); Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var datos = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(datos.GetProperty("origen").GetProperty("paradas").EnumerateArray());
        Assert.Equal(2, datos.GetProperty("destino").GetProperty("paradas").GetArrayLength());
        var reordenar = $"/api/rutas/{destinoId}/paradas/orden";
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(reordenar, new Application.DTOs.ReordenarParadasRequest([otroId, pedidoId], 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(reordenar, new Application.DTOs.ReordenarParadasRequest([otroId, pedidoId], 999))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await conductor.PutAsJsonAsync(reordenar, new Application.DTOs.ReordenarParadasRequest([otroId, pedidoId], 0))).StatusCode);
    }
}
