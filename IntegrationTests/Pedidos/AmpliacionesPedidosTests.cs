using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Fabrica = IntegrationTests.Autenticacion.AutenticacionTests.Fabrica;
namespace IntegrationTests.Pedidos;

public sealed class AmpliacionesPedidosTests
{
    internal static async Task Ingresar(HttpClient c, string nombre)
    {
        var r = await c.PostAsJsonAsync("/api/autenticacion/login", new { nombreUsuario = nombre, clave = "Temporal-2026!Segura" });
        r.EnsureSuccessStatusCode(); var s = await r.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.GetProperty("accessToken").GetString());
    }

    [Fact]
    public async Task PedidoAmpliado_ConservaContratoAnterior_PermiteEdicionPendiente_YRechazaVersionDesactualizada()
    {
        await using var app = new Fabrica(); using var c = app.CreateClient();
        await app.Crear("admin", RolUsuario.Administrador, false); await Ingresar(c, "admin");
        var zona = Zona.Crear("Centro", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 4);
        using (var scope = app.Services.CreateScope())
        { var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>(); db.Zonas.Add(zona); await db.SaveChangesAsync(); }
        var fecha = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var alta = await c.PostAsJsonAsync("/api/pedidos", new
        {
            clienteNombre = "Destinatario", clienteTelefono = "0991111111", direccion = "Calle 1", latitud = 0, longitud = 0,
            fechaEntrega = fecha, horaEntrega = "12:00:00", zonaId = zona.Id,
            detalles = new { remitenteNombre = "Origen", remitenteTelefono = "0992222222", remitenteCorreo = "origen@example.com",
                destinatarioCorreo = "destino@example.com", observaciones = "Frágil", pesoKg = 2.5m, fragil = true, prioridad = "Alta", tipoCarga = "General" }
        });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var pedido = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pedido");
        Assert.Equal("Destinatario", pedido.GetProperty("clienteNombre").GetString());
        Assert.Equal("Destinatario", pedido.GetProperty("destinatarioNombre").GetString());
        Assert.Equal("Origen", pedido.GetProperty("detalles").GetProperty("remitenteNombre").GetString());
        var id = pedido.GetProperty("id").GetGuid();
        var request = new Application.DTOs.ActualizarPedidoRequest("Editado", "0991111111", "Calle 2", 0, 0, fecha,
            new(13, 0), pedido.GetProperty("version").GetUInt32(), zona.Id);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/pedidos/{id}", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/pedidos/{id}", request with { Version = 999 })).StatusCode);
        var final = await c.GetFromJsonAsync<JsonElement>($"/api/pedidos/{id}");
        Assert.Equal("Editado", final.GetProperty("clienteNombre").GetString());
        Assert.Equal(pedido.GetProperty("registradoEn").GetString(), final.GetProperty("registradoEn").GetString());
    }

    [Fact]
    public async Task Resumen_NoDependeDePaginacion_YComparteFiltrosConListado()
    {
        await using var app = new Fabrica(); using var c = app.CreateClient();
        await app.Crear("admin", RolUsuario.Administrador, false); await Ingresar(c, "admin");
        var fecha = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var zona = Zona.Crear("Centro", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 4); db.Zonas.Add(zona);
            for (var i = 0; i < 4; i++)
            {
                var p = Pedido.Registrar("Cliente", "099", "Calle", new(0, 0), zona, i == 3 ? fecha.AddDays(1) : fecha,
                    new(12, 0), fecha, DateTimeOffset.UtcNow);
                if (i == 0) p.Cancelar();
                if (i == 1) { p.AsignarARuta(Guid.NewGuid()); p.MarcarEnCamino(); p.MarcarEntregado(DateTimeOffset.UtcNow); }
                db.Pedidos.Add(p);
            }
            await db.SaveChangesAsync();
        }
        var resumen = await c.GetFromJsonAsync<JsonElement>($"/api/resumen/pedidos?fecha={fecha:yyyy-MM-dd}&tamano=1");
        Assert.Equal(3, resumen.GetProperty("total").GetInt32()); Assert.Equal(2, resumen.GetProperty("noCancelados").GetInt32());
        Assert.Equal(50m, resumen.GetProperty("porcentajeEntregado").GetDecimal());
        var listado = await c.GetFromJsonAsync<JsonElement>($"/api/pedidos?desde={fecha:yyyy-MM-dd}&hasta={fecha:yyyy-MM-dd}&buscar=CLIENTE&tamano=1");
        Assert.Equal(3, listado.GetProperty("total").GetInt32()); Assert.Single(listado.GetProperty("items").EnumerateArray());
        var invalido = await c.GetAsync($"/api/resumen/pedidos?desde={fecha.AddDays(1):yyyy-MM-dd}&hasta={fecha:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalido.StatusCode);
        var error = await invalido.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(error.TryGetProperty("traceId", out _));
    }
}
