using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProductoCaracteristicaConfiguration : IEntityTypeConfiguration<ProductoCaracteristica>
{
    public void Configure(EntityTypeBuilder<ProductoCaracteristica> b)
    {
        b.ToTable("ProductoCaracteristicas");
        b.Property(x => x.Texto).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => new { x.ProductoId, x.Orden });
        b.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
    }
}
