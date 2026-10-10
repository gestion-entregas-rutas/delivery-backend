using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces;

public interface IAppDbContext
{
    DbSet<Pedido> Pedidos { get; }
    DbSet<Zona> Zonas { get; }
    DbSet<Repartidor> Repartidores { get; }
    DbSet<Ruta> Rutas { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Descarta las entidades en memoria para reintentar tras un conflicto de concurrencia.</summary>
    void LimpiarCambios();
}
