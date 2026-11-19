using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class DetalleOrdenConfiguration : IEntityTypeConfiguration<DetalleOrden>
{
    public void Configure(EntityTypeBuilder<DetalleOrden> b)
    {
        b.ToTable("DetalleOrdenes");
        b.Property(x => x.NombreProducto).HasMaxLength(250).IsRequired();
        b.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.HasIndex(x => x.OrdenId);
        b.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}
