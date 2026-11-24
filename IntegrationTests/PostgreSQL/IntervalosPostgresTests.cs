using Application;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Services;
using Infrastructure;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.PostgreSQL;

/// <summary>
/// Etapa 8: dos compras simultáneas por el último cupo. El bloqueo pg_advisory_xact_lock por fecha
/// y la nueva verificación en checkout deben dejar exactamente una reserva.
/// </summary>
public sealed class IntervalosPostgresTests
{
    [PostgresFact]
    public async Task ComprasSimultaneas_PorElUltimoCupo_SoloUnaReserva_YElPedidoHeredaElCompromiso()
    {
        var reglas = new ReglasPlanificacion();
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var inicio = reglas.Instante(fecha, new(9, 0));
        var fin = reglas.Instante(fecha, new(9, 30));
        await ConDbTemporal(async cs =>
        {
            Guid productoId;
            using (var scope = CrearScope(cs))
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Zonas.Add(Zona.Crear("Centro", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 4));
                db.Repartidores.Add(Repartidor.Crear("Único", "0991", 1)); // un solo cupo para la fecha
                var categoria = Categoria.Crear(1, "Electrónicos", 1);
                db.Categorias.Add(categoria);
                await db.SaveChangesAsync();
                var producto = Producto.Crear(categoria.Id, null, "Audio", "Producto", "Marca", "Desc", 10m, null, 50, "https://img.test/p.jpg");
                db.Productos.Add(producto);
                await db.SaveChangesAsync();
                productoId = producto.Id;
            }
            var clientes = new[] { await CrearCliente(cs, "a@test.com"), await CrearCliente(cs, "b@test.com") };

            CheckoutRequest Solicitud() => new([new CheckoutItemRequest(productoId, 1)],
                new MetodoPagoRequest("Simulado", null, null, "SIM-INT"), "Domicilio", null,
                new DatosDomicilioRequest("Dest", "0992222222", "Calle", 0.01, 0.01, fecha, new(12, 0), null, inicio, fin));

            var resultados = await Task.WhenAll(clientes.Select((c, i) => Task.Run(async () =>
            {
                using var scope = CrearScope(cs);
                try { return (Ok: true, Resp: await scope.ServiceProvider.GetRequiredService<IOrdenService>()
                    .CheckoutAsync(c, Solicitud(), $"int-{i}", default), Error: (Exception?)null); }
                catch (Exception ex) { return (Ok: false, Resp: (CheckoutResponse?)null, Error: ex); }
            })));

            Assert.Single(resultados, r => r.Ok);
            var rechazo = Assert.Single(resultados, r => !r.Ok);
            Assert.Equal("INTERVALO_NO_DISPONIBLE", Assert.IsType<ConflictoException>(rechazo.Error).Codigo);
            var ganador = resultados.Single(r => r.Ok);
            Assert.Equal(inicio, ganador.Resp!.CompromisoInicioEn);
            Assert.Equal(49, await StockRestante(cs, productoId)); // el rechazo no descontó stock

            using (var scope = CrearScope(cs))
            {
                var ordenes = scope.ServiceProvider.GetRequiredService<IOrdenService>();
                var clienteGanador = clientes[Array.FindIndex(resultados, r => r.Ok)];
                var confirmada = await ordenes.ConfirmarPagoAsync(clienteGanador, ganador.Resp.OrdenId, ganador.Resp.ReferenciaPago, null, default);
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pedido = await db.Pedidos.AsNoTracking().SingleAsync(p => p.Id == confirmada.PedidoLogisticoId);
                Assert.Equal(inicio, pedido.CompromisoInicioEn);
                Assert.Equal(fin, pedido.CompromisoFinEn);
            }
        });
    }

    private static async Task<int> StockRestante(string cs, Guid productoId)
    {
        using var scope = CrearScope(cs);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Productos
            .Where(p => p.Id == productoId).Select(p => p.Stock).SingleAsync();
    }

    private static async Task<Guid> CrearCliente(string cs, string correo)
    {
        using var scope = CrearScope(cs);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u = Usuario.CrearCliente("Cliente " + correo, correo, "0990000000", "HASH_PRUEBA", null);
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static IServiceScope CrearScope(string cs)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = cs,
            ["PlanificacionRutas:FinOfertaIntervalos"] = "18:00:00",
            ["PlanificacionRutas:OrigenLatitud"] = "0",
            ["PlanificacionRutas:OrigenLongitud"] = "0"
        }).Build();
        var provider = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(config).BuildServiceProvider();
        var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        return scope;
    }

    private static async Task ConDbTemporal(Func<string, Task> prueba)
    {
        var original = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DELIVERY_TEST_CONNECTION"));
        var nombre = "delivery_test_" + Guid.NewGuid().ToString("N");
        var control = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = "postgres", Pooling = false };
        await using var conexion = new NpgsqlConnection(control.ConnectionString);
        await conexion.OpenAsync();
        await using (var crear = new NpgsqlCommand($"CREATE DATABASE \"{nombre}\"", conexion)) await crear.ExecuteNonQueryAsync();
        try
        {
            await prueba(new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = nombre, Pooling = false }.ConnectionString);
        }
        finally
        {
            if (!nombre.StartsWith("delivery_test_", StringComparison.Ordinal) || nombre == original.Database)
                throw new InvalidOperationException("Nombre de base temporal inválido.");
            await using var borrar = new NpgsqlCommand($"DROP DATABASE \"{nombre}\" WITH (FORCE)", conexion);
            await borrar.ExecuteNonQueryAsync();
        }
    }
}
