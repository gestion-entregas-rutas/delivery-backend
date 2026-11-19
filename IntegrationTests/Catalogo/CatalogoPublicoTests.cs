using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fabrica = IntegrationTests.Autenticacion.AutenticacionTests.Fabrica;

namespace IntegrationTests.Catalogo;

public sealed class CatalogoPublicoTests
{
    [Fact]
    public async Task Catalogo_AdmiteLecturaAnonimaPeroNoCambios()
    {
        await using var app = new Fabrica();
        using var cliente = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/categorias")).StatusCode);
        var productos = await cliente.GetFromJsonAsync<JsonElement>("/api/productos?buscar=PRODUCTO_INEXISTENTE");
        Assert.Equal(0, productos.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.PostAsJsonAsync("/api/productos", new { })).StatusCode);
    }
}
