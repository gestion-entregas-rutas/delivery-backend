using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProductoFichaTecnicaConfiguration : IEntityTypeConfiguration<ProductoFichaTecnica>
{
    public void Configure(EntityTypeBuilder<ProductoFichaTecnica> b)
    {
        b.ToTable("ProductoFichasTecnicas");
        b.Property(x => x.Clave).HasMaxLength(150).IsRequired();
        b.Property(x => x.Valor).HasMaxLength(500).IsRequired();
        b.HasIndex(x => new { x.ProductoId, x.Orden });
        b.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
    }
}
