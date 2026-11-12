using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class RepartidorConfiguration : IEntityTypeConfiguration<Repartidor>
{
    public void Configure(EntityTypeBuilder<Repartidor> builder)
    {
        builder.ToTable("Repartidores");

        builder.Property(repartidor => repartidor.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(repartidor => repartidor.Telefono).HasMaxLength(30).IsRequired();
        builder.Property(repartidor => repartidor.Estado).HasConversion<string>().HasMaxLength(20);

        builder.Ignore(repartidor => repartidor.EstaDisponible);
        builder.HasIndex(repartidor => repartidor.Estado);
    }
}
