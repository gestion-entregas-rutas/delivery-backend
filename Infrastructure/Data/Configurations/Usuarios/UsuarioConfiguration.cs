using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;
public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios"); b.HasKey(x => x.Id);
        b.Property(x => x.NombreUsuario).HasMaxLength(100).IsRequired();
        b.Property(x => x.NombreNormalizado).HasMaxLength(100).IsRequired();
        b.Property(x => x.ClaveHash).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Rol).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.NombreNormalizado).IsUnique();
        b.HasIndex(x => x.RepartidorId).IsUnique();
        b.HasOne<Repartidor>().WithOne().HasForeignKey<Usuario>(x => x.RepartidorId).OnDelete(DeleteBehavior.Restrict);
    }
}
