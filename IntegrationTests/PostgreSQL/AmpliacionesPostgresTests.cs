using Application;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
namespace IntegrationTests.PostgreSQL;

public sealed class AmpliacionesPostgresTests
{
    [PostgresFact]
    public async Task TransferenciaAtomica_EdicionConXmin_YFiltrosGeograficos_FuncionanEnPostgres()
    {
        var original = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DELIVERY_TEST_CONNECTION"));
        var nombre = "delivery_test_" + Guid.NewGuid().ToString("N");
        var control = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = "postgres", Pooling = false };
        await using var conexion = new NpgsqlConnection(control.ConnectionString); await conexion.OpenAsync();
        await using (var crear = new NpgsqlCommand($"CREATE DATABASE \"{nombre}\"", conexion)) await crear.ExecuteNonQueryAsync();
        try
        {
            var prueba = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = nombre, Pooling = false };
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = prueba.ConnectionString }).Build();
            var servicios = new ServiceCollection().AddLogging().AddApplication().AddInfrastructure(config);
            await using var provider = servicios.BuildServiceProvider();
            using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            var zona = Zona.Crear("Pruebas", [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1)], 3);
            var rep1 = Repartidor.Crear("Uno", "0991", 3); var rep2 = Repartidor.Crear("Dos", "0992", 3);
            var fecha = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            Pedido Nuevo(string nombreCliente) => Pedido.Registrar(nombreCliente, "099", "Calle", new(0, 0), zona,
                fecha, new(12, 0), fecha, DateTimeOffset.UtcNow);
            var a = Ruta.Crear(zona, rep1, fecha, DateTimeOffset.UtcNow); var b = Ruta.Crear(zona, rep2, fecha, DateTimeOffset.UtcNow);
            var p = Nuevo("Transferir"); var p2 = Nuevo("Destino"); var pendiente = Nuevo("Pendiente");
            a.AgregarPedido(p); b.AgregarPedido(p2);
            db.AddRange(zona, rep1, rep2, a, b, pendiente); await db.SaveChangesAsync();
            var rutas = scope.ServiceProvider.GetRequiredService<IRutaService>();
            var versionA = a.Version; var versionB = b.Version;
            var salida = await rutas.TransferirAsync(a.Id, p.Id, new(b.Id, versionA, versionB, 0), default);
            Assert.Empty(salida.Origen.Paradas); Assert.Equal(2, salida.Destino.Paradas.Count);
            Assert.NotEqual(versionA, salida.Origen.Version); Assert.NotEqual(versionB, salida.Destino.Version);
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.Set<ParadaRuta>().CountAsync(x => x.PedidoId == p.Id));
            Assert.Equal(b.Id, (await db.Pedidos.SingleAsync(x => x.Id == p.Id)).RutaId);
            await Assert.ThrowsAsync<ConflictoException>(() => rutas.ReordenarAsync(b.Id, new([p2.Id, p.Id], versionB), default));
            var reordenada = await rutas.ReordenarAsync(b.Id, new([p2.Id, p.Id], salida.Destino.Version), default);
            Assert.Equal(p2.Id, reordenada.Paradas[0].Pedido.Id);

            var pedidos = scope.ServiceProvider.GetRequiredService<IPedidoService>();
            var anterior = await pedidos.ObtenerAsync(pendiente.Id); var version = anterior.Version;
            var r = new ActualizarPedidoRequest(anterior.ClienteNombre, anterior.ClienteTelefono, anterior.Direccion,
                0, 0, fecha, anterior.HoraEntrega, version, zona.Id,
                new(Observaciones: "Solo cambia el dato adicional", PesoKg: 1.25m));
            var editado = await pedidos.ActualizarAsync(pendiente.Id, r);
            Assert.NotEqual(version, editado.Version); Assert.Equal(1.25m, editado.Detalles!.PesoKg);
            await Assert.ThrowsAsync<ConflictoException>(() => pedidos.ActualizarAsync(pendiente.Id, r));
            var resumen = await scope.ServiceProvider.GetRequiredService<IResumenService>().ObtenerAsync(new(Desde: fecha, Hasta: fecha, Buscar: "Pendiente"), default);
            Assert.Equal(1, resumen.Total);

            // Verificar que xmin impide que dos instancias sobrescriban la última posición.
            b = await db.Rutas.SingleAsync(x => x.Id == b.Id); rep2 = await db.Repartidores.SingleAsync(x => x.Id == rep2.Id);
            b.Iniciar(rep2, DateTimeOffset.UtcNow); await db.SaveChangesAsync();
            var gps = scope.ServiceProvider.GetRequiredService<IUbicacionService>();
            var ahora = DateTimeOffset.UtcNow;
            Assert.True((await gps.RegistrarAsync(rep2.Id, new(b.Id, 0, 0, 10, ahora), default)).Actualizada);
            Assert.False((await gps.RegistrarAsync(rep2.Id, new(b.Id, 1, 1, 20, ahora.AddSeconds(-1)), default)).Actualizada);
            Assert.Single(await gps.ListarAsync(fecha, zona.Id, default));
        }
        finally
        {
            // Solo se elimina la base temporal recién creada, nunca la base configurada del usuario.
            if (!nombre.StartsWith("delivery_test_", StringComparison.Ordinal) || nombre == original.Database)
                throw new InvalidOperationException("Nombre de base temporal inválido.");
            await using var borrar = new NpgsqlCommand($"DROP DATABASE \"{nombre}\" WITH (FORCE)", conexion);
            await borrar.ExecuteNonQueryAsync();
        }
    }
}
