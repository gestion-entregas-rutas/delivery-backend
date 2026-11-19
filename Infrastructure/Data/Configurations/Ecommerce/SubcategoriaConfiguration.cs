using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class SubcategoriaConfiguration : IEntityTypeConfiguration<Subcategoria>
{
    public void Configure(EntityTypeBuilder<Subcategoria> b)
    {
        b.ToTable("Subcategorias");
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.HasIndex(x => new { x.CategoriaId, x.Nombre }).IsUnique();
        b.HasOne<Categoria>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
    }
}
