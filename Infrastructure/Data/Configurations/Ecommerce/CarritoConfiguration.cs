using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class CarritoConfiguration : IEntityTypeConfiguration<Carrito>
{
    public void Configure(EntityTypeBuilder<Carrito> b)
    {
        b.ToTable("Carritos");
        b.Property(x => x.TokenInvitado).HasMaxLength(100);
        b.HasIndex(x => x.UsuarioId).IsUnique().HasFilter("\"UsuarioId\" IS NOT NULL");
        b.HasIndex(x => x.TokenInvitado).IsUnique().HasFilter("\"TokenInvitado\" IS NOT NULL");
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany<CarritoItem>("Items").WithOne().HasForeignKey(i => i.CarritoId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation("Items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
