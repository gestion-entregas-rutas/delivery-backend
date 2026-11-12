using Domain.Entities;
using Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;
public sealed class UbicacionConfiguration : IEntityTypeConfiguration<UltimaUbicacionRepartidor>
{
    public void Configure(EntityTypeBuilder<UltimaUbicacionRepartidor> b)
    {
        b.ToTable("UbicacionesRepartidores"); b.HasKey(u => u.Id);
        b.HasIndex(u => u.RepartidorId).IsUnique();
        b.Property(u => u.Punto).HasConversion(new GeoPointConverter()).HasColumnType("geometry (point, 4326)");
        b.HasOne<Repartidor>().WithMany().HasForeignKey(u => u.RepartidorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Ruta>().WithMany().HasForeignKey(u => u.RutaId).OnDelete(DeleteBehavior.Restrict);
    }
}
