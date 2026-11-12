using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Net.WebSockets;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace IntegrationTests.Autenticacion;

public sealed class AutenticacionTests
{
    private const string Temporal = "Temporal-2026!Segura";
    private const string Nueva = "Nueva-Clave2026!Segura";

    [Theory]
    [InlineData("expirado")]
    [InlineData("emisor")]
    [InlineData("audiencia")]
    [InlineData("firma")]
    public async Task Jwt_RechazaTokenExpirado_EmisorAudienciaOFirmaIncorrectos(string defecto)
    {
        await using var app = new Fabrica(); using var c = app.CreateClient();
        await app.Crear("jwt", RolUsuario.Administrador, false);
        var sesion = await Login(c, "jwt", Temporal);
        var original = new JwtSecurityTokenHandler().ReadJwtToken(sesion.GetProperty("accessToken").GetString());
        var token = new JwtSecurityToken(defecto == "emisor" ? "otro" : original.Issuer,
            defecto == "audiencia" ? "otro" : original.Audiences.Single(),
            original.Claims.Where(c => c.Type is not ("aud" or "iss" or "nbf" or "exp" or "iat")),
            DateTime.UtcNow.AddHours(-2), defecto == "expirado" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(defecto == "firma"
                ? "Firma-Incorrecta-Para-Pruebas-1234567890" : app.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        c.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/autenticacion/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_RevocaElToken_YSwaggerExponeElContratoBearer()
    {
        await using var app = new Fabrica(); using var c = app.CreateClient();
        var openapi = await c.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        Assert.Equal("bearer", openapi.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.True(openapi.GetProperty("paths").GetProperty("/api/usuarios").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/swagger")).StatusCode);
        await app.Crear("salir", RolUsuario.Administrador, false);
        Token(c, await Login(c, "salir", Temporal));
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/autenticacion/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/autenticacion/me")).StatusCode);
    }

    [Fact]
    public async Task ClaveTemporal_SoloPermiteCambio_YRevocaTokenAnterior()
    {
        await using var app = new Fabrica();
        using var cliente = app.CreateClient();
        await app.Crear("chofer", RolUsuario.Conductor, true);
        var sesion = await Login(cliente, "chofer", Temporal);
        Assert.True(sesion.GetProperty("usuario").GetProperty("debeCambiarCredenciales").GetBoolean());
        Token(cliente, sesion);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/repartidores")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/autenticacion/me")).StatusCode);
        var respuesta = await cliente.PutAsJsonAsync("/api/autenticacion/credenciales",
            new { claveActual = Temporal, nuevoNombreUsuario = "chofer.nuevo", nuevaClave = Nueva });
        respuesta.EnsureSuccessStatusCode();
        var nueva = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(nueva.GetProperty("usuario").GetProperty("debeCambiarCredenciales").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/autenticacion/me")).StatusCode);
        Token(cliente, nueva);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/autenticacion/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/autenticacion/login", new { nombreUsuario = "chofer", clave = Temporal })).StatusCode);
    }

    [Fact]
    public async Task Conductor_NoGestionaUsuarios_NiConsultaRutasOPedidosAjenos_NiSuscribeFechas()
    {
        await using var app = new Fabrica(); using var cliente = app.CreateClient();
        await app.Crear("conductor", RolUsuario.Conductor, false);
        Token(cliente, await Login(cliente, "conductor", Temporal));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/rutas/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/pedidos/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync($"/api/seguimiento/pedidos/{Guid.NewGuid()}/entregado", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/repartidores/{Guid.NewGuid()}/ruta?fecha=2026-10-17")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/pedidos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync("/hubs/seguimiento/negotiate?negotiateVersion=1", null)).StatusCode);
        var propia = await app.RutaPara("conductor");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/rutas/{propia}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync($"/api/rutas/{propia}/iniciar", null)).StatusCode);
    }

    [Fact]
    public async Task SignalR_Conductor_SoloSeSuscribeASuGrupo()
    {
        await using var app = new Fabrica(); using var cliente = app.CreateClient();
        await app.Crear("movil", RolUsuario.Conductor, false);
        var sesion = await Login(cliente, "movil", Temporal);
        var rep = sesion.GetProperty("usuario").GetProperty("repartidorId").GetGuid();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var ws = await app.Server.CreateWebSocketClient().ConnectAsync(new Uri(
            "ws://localhost/hubs/seguimiento?access_token=" + Uri.EscapeDataString(sesion.GetProperty("accessToken").GetString()!)), limite.Token);
        await Enviar(ws, "{\"protocol\":\"json\",\"version\":1}", limite.Token);
        await Recibir(ws, limite.Token);
        await Enviar(ws, JsonSerializer.Serialize(new { type = 1, invocationId = "1", target = "SuscribirseARepartidor", arguments = new object[] { rep } }), limite.Token);
        Assert.DoesNotContain("error", await Recibir(ws, limite.Token));
        await Enviar(ws, JsonSerializer.Serialize(new { type = 1, invocationId = "2", target = "SuscribirseARepartidor", arguments = new object[] { Guid.NewGuid() } }), limite.Token);
        Assert.Contains("Acceso denegado", await Recibir(ws, limite.Token));
        await Enviar(ws, JsonSerializer.Serialize(new { type = 1, invocationId = "3", target = "SuscribirseAFecha", arguments = new object[] { "2026-10-17" } }), limite.Token);
        Assert.Contains("Acceso denegado", await Recibir(ws, limite.Token));
        await Enviar(ws, JsonSerializer.Serialize(new { type = 1, invocationId = "4", target = "SeguirPedido", arguments = new object[] { Guid.NewGuid() } }), limite.Token);
        Assert.Contains("Acceso denegado", await Recibir(ws, limite.Token));
    }

    private static async Task Enviar(WebSocket ws, string json, CancellationToken ct) =>
        await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json + "\u001e")), WebSocketMessageType.Text, true, ct);
    private static async Task<string> Recibir(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192]; var texto = new StringBuilder();
        do
        {
            var r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            texto.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
        } while (!texto.ToString().Contains('\u001e'));
        return texto.ToString();
    }

    [Fact]
    public async Task Administrador_CreaConductor_RestableceClave_YDesactivaCuenta()
    {
        await using var app = new Fabrica(); using var admin = app.CreateClient(); using var conductor = app.CreateClient();
        await app.Crear("admin", RolUsuario.Administrador, false);
        var rep = await app.Repartidor();
        Token(admin, await Login(admin, "admin", Temporal));
        var alta = await admin.PostAsJsonAsync("/api/usuarios", new { nombreUsuario = "nuevo", claveTemporal = Temporal, rol = "Conductor", repartidorId = rep });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var cuenta = await alta.Content.ReadFromJsonAsync<JsonElement>(); var id = cuenta.GetProperty("id").GetGuid();
        Assert.False(cuenta.TryGetProperty("claveHash", out _));
        Token(conductor, await Login(conductor, "nuevo", Temporal));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/usuarios/{id}/restablecer-clave", new { claveTemporal = Nueva })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await conductor.GetAsync("/api/autenticacion/me")).StatusCode);
        Token(conductor, await Login(conductor, "nuevo", Nueva));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/usuarios/{id}/activo", new { activo = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await conductor.GetAsync("/api/autenticacion/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await conductor.PostAsJsonAsync("/api/autenticacion/login", new { nombreUsuario = "nuevo", clave = Nueva })).StatusCode);
    }

    [Fact]
    public async Task AnonimoYTokenManipulado_NoAcceden_YLoginBloqueaTrasCincoFallos()
    {
        await using var app = new Fabrica(); using var cliente = app.CreateClient();
        await app.Crear("bloqueado", RolUsuario.Conductor, false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsync("/hubs/seguimiento/negotiate?negotiateVersion=1", null)).StatusCode);
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", "token.manipulado.invalido");
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/autenticacion/me")).StatusCode);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/autenticacion/login", new { nombreUsuario = "bloqueado", clave = "incorrecta" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/autenticacion/login", new { nombreUsuario = "bloqueado", clave = Temporal })).StatusCode);
    }

    private static void Token(HttpClient c, JsonElement s) => c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.GetProperty("accessToken").GetString());
    private static async Task<JsonElement> Login(HttpClient c, string nombre, string clave)
    {
        var r = await c.PostAsJsonAsync("/api/autenticacion/login", new { nombreUsuario = nombre, clave });
        r.EnsureSuccessStatusCode(); return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    internal sealed class Fabrica : WebApplicationFactory<Program>
    {
        private readonly string bd = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder b)
        {
            b.UseSetting("Jwt:Key", "Clave-Solo-Pruebas-Integracion-2026-123456789");
            b.UseSetting("Jwt:Issuer", "pruebas"); b.UseSetting("Jwt:Audience", "pruebas-clientes");
            b.ConfigureServices(s =>
            {
                s.AddDbContext<ContextoPruebas>(o => o.UseInMemoryDatabase(bd));
                s.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ContextoPruebas>());
            });
        }
        public async Task<Guid> Repartidor()
        {
            using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var rep = Domain.Entities.Repartidor.Crear("Ana", "0991111111", 4); db.Repartidores.Add(rep); await db.SaveChangesAsync(); return rep.Id;
        }
        public async Task Crear(string nombre, RolUsuario rol, bool temporal)
        {
            Guid? rep = rol == RolUsuario.Conductor ? await Repartidor() : null;
            using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var hash = new ProtectorClaves().Proteger(Temporal); var u = Usuario.Crear(nombre, hash, rol, rep);
            if (!temporal) u.CambiarCredenciales(nombre, hash);
            db.Usuarios.Add(u); await db.SaveChangesAsync();
        }
        public async Task<Guid> RutaPara(string nombre)
        {
            using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var u = await db.Usuarios.SingleAsync(u => u.NombreUsuario == nombre);
            var rep = await db.Repartidores.SingleAsync(r => r.Id == u.RepartidorId);
            var zona = Zona.Crear("Centro", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 4);
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
            var pedido = Pedido.Registrar("Cliente", "0991111111", "Calle 1", new(0, 0), zona, hoy, new(12, 0), hoy, DateTimeOffset.UtcNow);
            var ruta = Ruta.Crear(zona, rep, hoy, DateTimeOffset.UtcNow); ruta.AgregarPedido(pedido);
            db.Zonas.Add(zona); db.Pedidos.Add(pedido); db.Rutas.Add(ruta); await db.SaveChangesAsync(); return ruta.Id;
        }
    }

    private sealed class ContextoPruebas(DbContextOptions<ContextoPruebas> options) : DbContext(options), IAppDbContext
    {
        public DbSet<Usuario> Usuarios => Set<Usuario>(); public DbSet<Pedido> Pedidos => Set<Pedido>();
        public DbSet<UltimaUbicacionRepartidor> Ubicaciones => Set<UltimaUbicacionRepartidor>();
        public DbSet<Ruta> Rutas => Set<Ruta>(); public DbSet<Zona> Zonas => Set<Zona>();
        public DbSet<Repartidor> Repartidores => Set<Repartidor>();
        public void LimpiarCambios() => ChangeTracker.Clear();
        protected override void OnModelCreating(ModelBuilder b)
        {
            b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            foreach (var t in b.Model.GetEntityTypes().Where(t => typeof(Domain.Common.Entity).IsAssignableFrom(t.ClrType)))
                b.Entity(t.ClrType).Property("Version").ValueGeneratedNever();
        }
    }
}
