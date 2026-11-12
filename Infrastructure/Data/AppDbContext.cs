using Application.Interfaces;
using Domain.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UltimaUbicacionRepartidor> Ubicaciones => Set<UltimaUbicacionRepartidor>();
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
        // Las paradas pertenecen al agregado Ruta. Cambiarlas debe comprobar y renovar
        // tambien su xmin, aunque ninguna propiedad escalar de la ruta haya cambiado.
        // De lo contrario dos pedidos distintos pueden ocupar el ultimo cupo a la vez.
        ChangeTracker.DetectChanges();
        var rutasModificadas = ChangeTracker.Entries<ParadaRuta>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.RutaId)
            .ToHashSet();
        // Dos entregas concurrentes tambien deben serializarse sobre la ruta;
        // asi la ultima entrega puede reconocer que debe completar la salida.
        rutasModificadas.UnionWith(ChangeTracker.Entries<Pedido>()
            .Where(e => e.State == EntityState.Modified && e.Entity.RutaId.HasValue)
            .Select(e => e.Entity.RutaId!.Value));
        foreach (var ruta in ChangeTracker.Entries<Ruta>()
                     .Where(e => rutasModificadas.Contains(e.Entity.Id) &&
                                 e.State is EntityState.Unchanged or EntityState.Modified))
        {
            // Escribir el mismo valor fuerza UPDATE ... WHERE xmin = version original.
            ruta.Property(r => r.Capacidad).IsModified = true;
        }

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
