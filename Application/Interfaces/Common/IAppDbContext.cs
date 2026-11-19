using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces;

public interface IAppDbContext
{
    DbSet<Usuario> Usuarios { get; }
    DbSet<UltimaUbicacionRepartidor> Ubicaciones { get; }
    DbSet<Pedido> Pedidos { get; }
    DbSet<Zona> Zonas { get; }
    DbSet<Repartidor> Repartidores { get; }
    DbSet<Ruta> Rutas { get; }
    DbSet<Categoria> Categorias { get; }
    DbSet<Subcategoria> Subcategorias { get; }
    DbSet<Producto> Productos { get; }
    DbSet<ProductoMedia> ProductoMedias { get; }
    DbSet<ProductoFichaTecnica> ProductoFichasTecnicas { get; }
    DbSet<ProductoCaracteristica> ProductoCaracteristicas { get; }
    DbSet<Resena> Resenas { get; }
    DbSet<Carrito> Carritos { get; }
    DbSet<CarritoItem> CarritoItems { get; }
    DbSet<Orden> Ordenes { get; }
    DbSet<DetalleOrden> DetalleOrdenes { get; }
    DbSet<PagoOrden> PagosOrden { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Descarta las entidades en memoria para reintentar tras un conflicto de concurrencia.</summary>
    void LimpiarCambios();
}
