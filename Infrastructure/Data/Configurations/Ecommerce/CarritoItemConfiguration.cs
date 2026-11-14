using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class CarritoItemConfiguration : IEntityTypeConfiguration<CarritoItem>
{
    public void Configure(EntityTypeBuilder<CarritoItem> b)
    {
        b.ToTable("CarritoItems");
        b.HasIndex(x => new { x.CarritoId, x.ProductoId }).IsUnique();
        b.HasOne<Producto>().WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}
