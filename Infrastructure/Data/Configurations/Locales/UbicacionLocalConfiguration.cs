using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class UbicacionLocalConfiguration : IEntityTypeConfiguration<UbicacionLocal>
{
    public void Configure(EntityTypeBuilder<UbicacionLocal> b)
    {
        b.ToTable("UbicacionLocal");
        b.HasKey(l => l.Id);
        b.Property(l => l.Nombre).HasMaxLength(120).IsRequired();
        b.Property(l => l.Direccion).HasMaxLength(300).IsRequired();
    }
}
