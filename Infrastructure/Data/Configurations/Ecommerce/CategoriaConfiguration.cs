using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.ToTable("Categorias");
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Numero).IsUnique();
        b.HasIndex(x => x.Activa);
    }
}
