using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ZonaConfiguration : IEntityTypeConfiguration<Zona>
{
    public void Configure(EntityTypeBuilder<Zona> builder)
    {
        builder.ToTable("Zonas");

        builder.Property(zona => zona.Nombre).HasMaxLength(100).IsRequired();

        // Se mapea el campo privado: el Domain solo expone el polígono como lectura.
        builder.Ignore(zona => zona.Poligono);
        builder.Property<List<GeoPoint>>("_poligono")
            .HasColumnName("Poligono")
            .HasColumnType("geometry (polygon, 4326)")
            .HasConversion(new PoligonoConverter(), new PoligonoComparer())
            .IsRequired();

        builder.HasIndex(zona => zona.Activa);
    }
}
