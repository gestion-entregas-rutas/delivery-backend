using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class PagoOrdenConfiguration : IEntityTypeConfiguration<PagoOrden>
{
    public void Configure(EntityTypeBuilder<PagoOrden> b)
    {
        b.ToTable("PagosOrden");
        b.Property(x => x.Metodo).HasMaxLength(50).IsRequired();
        b.Property(x => x.Referencia).HasMaxLength(120);
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.OrdenId);
        b.HasOne<Orden>().WithMany().HasForeignKey(x => x.OrdenId).OnDelete(DeleteBehavior.Cascade);
    }
}
