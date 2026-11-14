using System.Security.Claims;
using Application;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.PostgreSQL;

public sealed class Etapa6PostgresTests
{
    [PostgresFact]
    public async Task ConcurrenciaProductoYStock_CumpleEscenariosABCD()
    {
        await ConDbTemporal(async cs =>
        {
            // Escenario A: 2 admins editan producto -> 200 y 409
            using (var scope = CrearScope(cs))
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await SembrarBasicoAsync(db);
            }

            Guid productoId;
            string v1, v2;
            using (var scope = CrearScope(cs))
            {
                var cat = scope.ServiceProvider.GetRequiredService<ICatalogoService>();
                var p = (await cat.ListarProductosAsync(null, null, null, false, null, null, null, 1, 50, true, default)).Items.First();
                productoId = p.Id;
                v1 = p.Version;
                v2 = p.Version;
            }

            var reqBase = async (string version, string nombre) =>
            {
                using var scope = CrearScope(cs);
                var cat = scope.ServiceProvider.GetRequiredService<ICatalogoService>();
                var actual = await cat.ObtenerProductoAsync(productoId, true, default);
                return await cat.ActualizarProductoAsync(productoId, new ProductoUpsertRequest(
                    actual.CategoriaId, null, actual.Subcategoria, nombre, actual.Marca, actual.DescripcionCorta,
                    actual.PrecioBase, actual.PrecioOferta, actual.Stock, actual.Activo, actual.ImagenPrincipalUrl,
                    actual.GaleriaMultimedia.Select(m => new ProductoMediaRequest(m.Tipo, m.Url, m.Orden)).ToList(),
                    actual.FichaTecnica.Select(f => new ProductoFichaRequest(f.Clave, f.Valor)).ToList(),
                    actual.SobreEsteArticulo.ToList(), version), default);
            };

            var tA1 = Task.Run(() => reqBase(v1, "Edit A1"));
            var tA2 = Task.Run(() => reqBase(v2, "Edit A2"));
            var resultadosA = await Task.WhenAll(Settle(tA1), Settle(tA2));
            Assert.Equal(1, resultadosA.Count(r => r.ok));
            Assert.Equal(1, resultadosA.Count(r => !r.ok && r.error is ConflictoException ce && ce.Codigo == "CONFLICTO_VERSION_PRODUCTO"));

            // Preparar stock para B
            await AjustarStockDirecto(cs, productoId, 100);
            var cliente1 = await CrearCliente(cs, "cli1@test.com", "cliente1");
            var cliente2 = await CrearCliente(cs, "cli2@test.com", "cliente2");

            // Escenario B: stock 100, dos compras de 50 en paralelo -> ambos 201 y stock 0
            var checkoutReqB = new CheckoutRequest(
                [new CheckoutItemRequest(productoId, 50)],
                new MetodoPagoRequest("Simulado", null, null, "SIM-B"),
                "RetiroTienda",
                new DatosRetiroTiendaRequest("0102030405", "Retira Uno", "0991111111", DateOnly.FromDateTime(DateTime.Today.AddDays(1)), "Mañana"),
                null);

            var tB1 = Task.Run(() => Checkout(cs, cliente1, checkoutReqB, "k-b-1"));
            var tB2 = Task.Run(() => Checkout(cs, cliente2, checkoutReqB, "k-b-2"));
            var resB = await Task.WhenAll(Settle(tB1), Settle(tB2));
            Assert.Equal(2, resB.Count(r => r.ok));
            Assert.Equal(0, await LeerStock(cs, productoId));

            // Escenario C: stock 50, dos compras de 40 -> uno 201 y otro 409 STOCK_AGOTADO, stock 10
            await AjustarStockDirecto(cs, productoId, 50);
            var checkoutReqC = new CheckoutRequest(
                [new CheckoutItemRequest(productoId, 40)],
                new MetodoPagoRequest("Simulado", null, null, "SIM-C"),
                "RetiroTienda",
                new DatosRetiroTiendaRequest("0102030405", "Retira Uno", "0991111111", DateOnly.FromDateTime(DateTime.Today.AddDays(1)), "Mañana"),
                null);
            var tC1 = Task.Run(() => Checkout(cs, cliente1, checkoutReqC, "k-c-1"));
            var tC2 = Task.Run(() => Checkout(cs, cliente2, checkoutReqC, "k-c-2"));
            var resC = await Task.WhenAll(Settle(tC1), Settle(tC2));
            Assert.Equal(1, resC.Count(r => r.ok));
            var falloC = Assert.Single(resC, r => !r.ok);
            var confC = Assert.IsType<ConflictoException>(falloC.error);
            Assert.Equal("STOCK_AGOTADO", confC.Codigo);
            Assert.Equal(10, await LeerStock(cs, productoId));

            // Escenario D: compra y edición admin simultáneas -> sin restaurar stock ni perder descuento
            await AjustarStockDirecto(cs, productoId, 30);
            string versionActual;
            using (var scope = CrearScope(cs))
            {
                var cat = scope.ServiceProvider.GetRequiredService<ICatalogoService>();
                versionActual = (await cat.ObtenerProductoAsync(productoId, true, default)).Version;
            }
            var checkoutReqD = new CheckoutRequest(
                [new CheckoutItemRequest(productoId, 5)],
                new MetodoPagoRequest("Simulado", null, null, "SIM-D"),
                "RetiroTienda",
                new DatosRetiroTiendaRequest("0102030405", "Retira Uno", "0991111111", DateOnly.FromDateTime(DateTime.Today.AddDays(1)), "Mañana"),
                null);

            var tCompra = Task.Run(() => Checkout(cs, cliente1, checkoutReqD, "k-d-1"));
            var tEdit = Task.Run(async () =>
            {
                using var scope = CrearScope(cs);
                var cat = scope.ServiceProvider.GetRequiredService<ICatalogoService>();
                var actual = await cat.ObtenerProductoAsync(productoId, true, default);
                // Importante: mantener el stock observado para no revertir descuentos concurrentes
                return await cat.ActualizarProductoAsync(productoId, new ProductoUpsertRequest(
                    actual.CategoriaId, null, actual.Subcategoria, actual.Nombre + " D", actual.Marca, actual.DescripcionCorta,
                    actual.PrecioBase, actual.PrecioOferta, actual.Stock, actual.Activo, actual.ImagenPrincipalUrl,
                    actual.GaleriaMultimedia.Select(m => new ProductoMediaRequest(m.Tipo, m.Url, m.Orden)).ToList(),
                    actual.FichaTecnica.Select(f => new ProductoFichaRequest(f.Clave, f.Valor)).ToList(),
                    actual.SobreEsteArticulo.ToList(), actual.Version), default);
            });
            var resD = await Task.WhenAll(Settle(tCompra), Settle(tEdit));
            var compraOk = resD[0].ok;
            var stockFinal = await LeerStock(cs, productoId);
            Assert.InRange(stockFinal, 0, 30);
            if (compraOk)
                Assert.Equal(25, stockFinal);
            else
                Assert.Equal(30, stockFinal);
            // la edición puede tener éxito o conflicto, pero nunca debe restaurar el descuento de una compra exitosa
            Assert.True(resD[1].ok || resD[1].error is ConflictoException);
        });
    }

    [PostgresFact]
    public async Task FlujosCheckout_CarritoMerge_RetiroDomicilio_BorradoLogico_Idempotencia()
    {
        await ConDbTemporal(async cs =>
        {
            Guid productoId; Guid categoriaId; Guid zonaId;
            using (var scope = CrearScope(cs))
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await SembrarBasicoAsync(db);
                var producto = await db.Productos.FirstAsync();
                productoId = producto.Id; categoriaId = producto.CategoriaId;
                var zona = Zona.Crear("Cobertura Test", [new(-1,-1), new(-1,1), new(1,1), new(1,-1)], 20);
                db.Zonas.Add(zona); await db.SaveChangesAsync(); zonaId = zona.Id;
            }

            var clienteId = await CrearCliente(cs, "flujo@test.com", "clienteflujo");

            // Carrito: agregar como invitado (token) y luego merge al autenticado
            using (var scope = CrearScope(cs))
            {
                var carrito = scope.ServiceProvider.GetRequiredService<ICarritoService>();
                var creado = await carrito.AgregarItemAsync(null, "tok-invitado", productoId, 2, default);
                Assert.Equal("tok-invitado", creado.TokenInvitado);
                var merge = await carrito.MergeAsync(clienteId, "tok-invitado", default);
                Assert.Contains(merge.Items, i => i.ProductoId == productoId && i.Cantidad == 2);
                var mio = await carrito.ObtenerAsync(clienteId, null, true, default);
                Assert.Contains(mio.Items, i => i.ProductoId == productoId);
            }

            // Checkout retiro en tienda + idempotencia y confirmación duplicada
            CheckoutResponse retiro;
            var reqRetiro = new CheckoutRequest(
                [new CheckoutItemRequest(productoId, 1)],
                new MetodoPagoRequest("Simulado", null, null, "SIM-RET"),
                "RetiroTienda",
                new DatosRetiroTiendaRequest("0102030405", "Nombre", "0991111111", DateOnly.FromDateTime(DateTime.Today.AddDays(1)), "Tarde"),
                null);
            using (var scope = CrearScope(cs))
            {
                var ordenes = scope.ServiceProvider.GetRequiredService<IOrdenService>();
                retiro = await ordenes.CheckoutAsync(clienteId, reqRetiro, "idem-retiro-1", default);
                var retiro2 = await ordenes.CheckoutAsync(clienteId, reqRetiro, "idem-retiro-1", default);
                Assert.Equal(retiro.OrdenId, retiro2.OrdenId);
                var conf1 = await ordenes.ConfirmarPagoAsync(clienteId, retiro.OrdenId, retiro.ReferenciaPago, "idem-conf-1", default);
                Assert.Equal("Confirmado", conf1.EstadoPago);
                var conf2 = await ordenes.ConfirmarPagoAsync(clienteId, retiro.OrdenId, retiro.ReferenciaPago, "idem-conf-1", default);
                Assert.Equal(conf1.OrdenId, conf2.OrdenId);
            }

            // Checkout domicilio (cobertura válida) crea pedido logístico al confirmar
            await AjustarStockDirecto(cs, productoId, 20);
            using (var scope = CrearScope(cs))
            {
                var ordenes = scope.ServiceProvider.GetRequiredService<IOrdenService>();
                var reqDom = new CheckoutRequest(
                    [new CheckoutItemRequest(productoId, 1)],
                    new MetodoPagoRequest("Simulado", null, null, "SIM-DOM"),
                    "Domicilio",
                    null,
                    new DatosDomicilioRequest("Dest", "0992222222", "Dir dentro", 0, 0, DateOnly.FromDateTime(DateTime.Today.AddDays(1)), new TimeOnly(10,0), null));
                var dom = await ordenes.CheckoutAsync(clienteId, reqDom, "idem-dom-1", default);
                var domConf = await ordenes.ConfirmarPagoAsync(clienteId, dom.OrdenId, dom.ReferenciaPago, "idem-domc-1", default);
                Assert.NotNull(domConf.PedidoLogisticoId);
            }

            // Borrado lógico / visibilidad
            using (var scope = CrearScope(cs))
            {
                var cat = scope.ServiceProvider.GetRequiredService<ICatalogoService>();
                await cat.DesactivarProductoAsync(productoId, default);
                var lista = await cat.ListarProductosAsync(categoriaId, null, null, false, null, null, null, 1, 50, false, default);
                Assert.DoesNotContain(lista.Items, p => p.Id == productoId);
            }

            // Pago cancelado: reserva se libera (producto activo independiente)
            Guid prod2;
            using (var scope = CrearScope(cs))
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var catId = await db.Categorias.Select(c => c.Id).FirstAsync();
                var p = Producto.Crear(catId, null, "Audio", "Producto Test 2", "Marca", "Desc", 10m, null, 10, "https://img.test/p2.jpg");
                db.Productos.Add(p);
                await db.SaveChangesAsync();
                prod2 = p.Id;
            }
            using (var scope = CrearScope(cs))
            {
                var ordenes = scope.ServiceProvider.GetRequiredService<IOrdenService>();
                var req = new CheckoutRequest(
                    [new CheckoutItemRequest(prod2, 3)],
                    new MetodoPagoRequest("Simulado", null, null, "SIM-CAN"),
                    "RetiroTienda",
                    new DatosRetiroTiendaRequest("0102030405", "Nombre", "0991111111", DateOnly.FromDateTime(DateTime.Today.AddDays(1)), "Tarde"),
                    null);
                var o = await ordenes.CheckoutAsync(clienteId, req, "idem-can-1", default);
                Assert.Equal(7, await LeerStock(cs, prod2));
                var cancel = await ordenes.CancelarPagoAsync(clienteId, o.OrdenId, "fallo", default);
                Assert.Equal("Cancelado", cancel.EstadoPago);
                Assert.Equal(10, await LeerStock(cs, prod2));
            }
        });
    }

    // --- Helpers ---
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
            var cs = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = nombre, Pooling = false }.ConnectionString;
            await prueba(cs);
        }
        finally
        {
            if (!nombre.StartsWith("delivery_test_", StringComparison.Ordinal) || nombre == original.Database)
                throw new InvalidOperationException("Nombre de base temporal inválido.");
            await using var borrar = new NpgsqlCommand($"DROP DATABASE \"{nombre}\" WITH (FORCE)", conexion);
            await borrar.ExecuteNonQueryAsync();
        }
    }

    private static IServiceScope CrearScope(string cs)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = cs
        }).Build();
        var servicios = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(config);
        var provider = servicios.BuildServiceProvider();
        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        return scope;
    }

    private static async Task SembrarBasicoAsync(AppDbContext db)
    {
        if (!await db.Categorias.AnyAsync())
        {
            var cat = Categoria.Crear(1, "Electrónicos", 1);
            db.Categorias.Add(cat);
            await db.SaveChangesAsync();
        }
        if (!await db.Productos.AnyAsync())
        {
            var cat = await db.Categorias.FirstAsync();
            var p = Producto.Crear(cat.Id, null, "Audio", "Producto Test", "Marca", "Desc", 10m, null, 100, "https://img.test/p.jpg");
            db.Productos.Add(p);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<Guid> CrearCliente(string cs, string correo, string usuario)
    {
        using var scope = CrearScope(cs);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existente = await db.Usuarios.FirstOrDefaultAsync(x => x.Correo == correo || x.NombreUsuario == correo);
        if (existente is not null) return existente.Id;
        // hash simple no usado para login en estas pruebas (operamos por servicio con clienteId)
        var u = Usuario.CrearCliente($"{usuario} Nombre", correo, "0990000000", "HASH_PRUEBA", "0102030405");
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static async Task AjustarStockDirecto(string cs, Guid productoId, int stock)
    {
        using var scope = CrearScope(cs);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p = await db.Productos.FirstAsync(x => x.Id == productoId);
        p.AjustarStock(stock);
        await db.SaveChangesAsync();
    }

    private static async Task<int> LeerStock(string cs, Guid productoId)
    {
        using var scope = CrearScope(cs);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Productos.Where(x => x.Id == productoId).Select(x => x.Stock).FirstAsync();
    }

    private static async Task<CheckoutResponse> Checkout(string cs, Guid clienteId, CheckoutRequest req, string idem)
    {
        using var scope = CrearScope(cs);
        var svc = scope.ServiceProvider.GetRequiredService<IOrdenService>();
        return await svc.CheckoutAsync(clienteId, req, idem, default);
    }

    private static async Task<(bool ok, object? value, Exception? error)> Settle(Task t)
    {
        try { await t; return (true, null, null); }
        catch (Exception ex) { return (false, null, ex); }
    }
    private static async Task<(bool ok, object? value, Exception? error)> Settle<T>(Task<T> t)
    {
        try { var v = await t; return (true, v, null); }
        catch (Exception ex) { return (false, null, ex); }
    }
}
