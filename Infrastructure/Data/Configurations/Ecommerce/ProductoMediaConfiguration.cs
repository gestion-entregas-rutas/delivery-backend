using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProductoMediaConfiguration : IEntityTypeConfiguration<ProductoMedia>
{
    public void Configure(EntityTypeBuilder<ProductoMedia> b)
    {
        b.ToTable("ProductoMedias");
        b.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => new { x.ProductoId, x.Orden });
        b.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
    }
}
