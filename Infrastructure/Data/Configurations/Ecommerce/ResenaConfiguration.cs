using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ResenaConfiguration : IEntityTypeConfiguration<Resena>
{
    public void Configure(EntityTypeBuilder<Resena> b)
    {
        b.ToTable("Resenas");
        b.Property(x => x.Comentario).HasMaxLength(2000);
        b.HasIndex(x => new { x.ProductoId, x.ClienteId }).IsUnique();
        b.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
    }
}
