using Api.Controllers;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services;

public static class PreparacionLocal
{
    public static async Task EjecutarAsync(IServiceProvider services, IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("La preparacion local requiere Development.");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            connection.Port != 5432 || connection.Database != "delivery")
            throw new InvalidOperationException("Solo se permite PostgreSQL local:5432, base delivery.");

        Console.WriteLine("Destino verificado: PostgreSQL local:5432 / delivery.");
        await db.Database.MigrateAsync();
        // Todos los datos de esta ejecucion se confirman juntos.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(54325289)");
        await scope.ServiceProvider.GetRequiredService<ICatalogoService>()
            .SembrarCatalogoInicialAsync(CancellationToken.None);

        if (!await db.Pedidos.AnyAsync(p => p.ClienteNombre.StartsWith("Cliente Volumen ")))
        {
            var seeder = new TestingController(db, environment,
                scope.ServiceProvider.GetRequiredService<TimeProvider>());
            await seeder.PoblarEscenarioAltoVolumen(CancellationToken.None);
        }
        else
            Console.WriteLine("Los pedidos de prueba ya existen; no se duplicaran.");

        await transaction.CommitAsync();
        Console.WriteLine($"Preparacion completada. Categorias: {await db.Categorias.CountAsync()}, " +
            $"productos: {await db.Productos.CountAsync()}, zonas: {await db.Zonas.CountAsync()}, " +
            $"repartidores: {await db.Repartidores.CountAsync()}, pedidos: {await db.Pedidos.CountAsync()}.");
    }
}
