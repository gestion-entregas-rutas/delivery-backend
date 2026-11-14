using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Enums;
using IntegrationTests.Pedidos;
using Fabrica = IntegrationTests.Autenticacion.AutenticacionTests.Fabrica;
namespace IntegrationTests.Seguimiento;

public sealed class GpsTests
{
    [Fact]
    public async Task Gps_SoloAceptaRutaPropiaEnCurso_NoRetrocedeYPermiteConsultaAdministrativa()
    {
        await using var app = new Fabrica(); using var conductor = app.CreateClient(); using var admin = app.CreateClient();
        await app.Crear("chofer", RolUsuario.Conductor, false); await app.Crear("admin", RolUsuario.Administrador, false);
        var ruta = await app.RutaPara("chofer");
        await AmpliacionesPedidosTests.Ingresar(conductor, "chofer"); await AmpliacionesPedidosTests.Ingresar(admin, "admin");
        var me = await conductor.GetFromJsonAsync<JsonElement>("/api/autenticacion/me");
        var rep = me.GetProperty("repartidorId").GetGuid(); var ahora = DateTimeOffset.UtcNow;
        var dato = new Application.DTOs.RegistrarUbicacionRequest(ruta, 0.001, 0.001, 10, ahora);
        Assert.Equal(HttpStatusCode.BadRequest, (await conductor.PostAsJsonAsync("/api/repartidores/me/ubicacion",
            new { rutaId = ruta, latitud = 0, longitud = 0, precisionMetros = 10, capturadaEn = "2026-10-24T12:00:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await conductor.PostAsJsonAsync("/api/repartidores/me/ubicacion", dato)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await conductor.PostAsync($"/api/rutas/{ruta}/iniciar", null)).StatusCode);
        var alta = await conductor.PostAsJsonAsync("/api/repartidores/me/ubicacion", dato);
        Assert.Equal(HttpStatusCode.OK, alta.StatusCode);
        Assert.True((await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("actualizada").GetBoolean());
        var anterior = await conductor.PostAsJsonAsync("/api/repartidores/me/ubicacion", dato with { CapturadaEn = ahora.AddSeconds(-20) });
        Assert.False((await anterior.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("actualizada").GetBoolean());
        var lectura = await admin.GetFromJsonAsync<JsonElement>($"/api/repartidores/{rep}/ubicacion");
        Assert.Equal(0.001, lectura.GetProperty("latitud").GetDouble()); Assert.True(lectura.GetProperty("esReciente").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await conductor.GetAsync($"/api/repartidores/{Guid.NewGuid()}/ubicacion")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await conductor.GetAsync("/api/seguimiento/ubicaciones?fecha=2026-10-24")).StatusCode);
        var lista = await admin.GetFromJsonAsync<JsonElement>($"/api/seguimiento/ubicaciones?fecha={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");
        Assert.Single(lista.EnumerateArray());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await conductor.PostAsJsonAsync("/api/repartidores/me/ubicacion", dato with { CapturadaEn = ahora.AddMinutes(-10) })).StatusCode);
    }
}
