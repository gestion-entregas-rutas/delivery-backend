using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("Productos");
        b.Property(x => x.Nombre).HasMaxLength(250).IsRequired();
        b.Property(x => x.Marca).HasMaxLength(150);
        b.Property(x => x.SubcategoriaTexto).HasMaxLength(150);
        b.Property(x => x.DescripcionCorta).HasMaxLength(1000);
        b.Property(x => x.ImagenPrincipalUrl).HasMaxLength(1000);
        b.Property(x => x.PrecioBase).HasPrecision(18, 2);
        b.Property(x => x.PrecioOferta).HasPrecision(18, 2);
        b.HasIndex(x => x.CategoriaId);
        b.HasIndex(x => x.SubcategoriaId);
        b.HasIndex(x => x.Activo);
        b.HasIndex(x => x.PrecioBase);
        b.HasOne<Categoria>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Subcategoria>().WithMany().HasForeignKey(x => x.SubcategoriaId).OnDelete(DeleteBehavior.SetNull);
    }
}
