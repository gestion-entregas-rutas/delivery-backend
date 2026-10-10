using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class RutaConfiguration : IEntityTypeConfiguration<Ruta>
{
    public void Configure(EntityTypeBuilder<Ruta> builder)
    {
        builder.ToTable("Rutas");

        builder.Property(ruta => ruta.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(ruta => ruta.TieneEspacio);
        builder.Ignore(ruta => ruta.PosicionMinimaInsercion);

        builder.HasOne<Zona>().WithMany().HasForeignKey(ruta => ruta.ZonaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Repartidor>().WithMany().HasForeignKey(ruta => ruta.RepartidorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(ruta => ruta.Paradas).WithOne().HasForeignKey(p => p.RutaId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(ruta => ruta.Paradas).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(ruta => new { ruta.Fecha, ruta.ZonaId, ruta.Estado });

        builder.HasIndex(r => new { r.RepartidorId, r.Fecha })
            .IsUnique()
            .HasFilter("\"Estado\" IN ('Planificada', 'EnCurso')");
    }
}
