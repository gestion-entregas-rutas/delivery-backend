using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Zona> Zonas => Set<Zona>();
    public DbSet<Repartidor> Repartidores => Set<Repartidor>();
    public DbSet<Ruta> Rutas => Set<Ruta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var tipo in modelBuilder.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            var entidad = modelBuilder.Entity(tipo.ClrType);
            entidad.Property(nameof(Entity.Id)).ValueGeneratedNever(); // el Id lo genera el Domain
            entidad.Property(nameof(Entity.Version)).IsRowVersion();   // xmin: concurrencia optimista
        }
    }

    public void LimpiarCambios() => ChangeTracker.Clear();

    /// <summary>
    /// Una violación de unicidad (p. ej. dos rutas activas del mismo repartidor creadas a la vez)
    /// es otra forma de choque de concurrencia: se reporta igual para que Application reintente.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DbUpdateConcurrencyException("Conflicto de unicidad por operaciones simultáneas.", ex);
        }
    }
}
