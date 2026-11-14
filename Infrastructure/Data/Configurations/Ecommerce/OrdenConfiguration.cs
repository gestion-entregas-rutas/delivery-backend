using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class OrdenConfiguration : IEntityTypeConfiguration<Orden>
{
    public void Configure(EntityTypeBuilder<Orden> b)
    {
        b.ToTable("Ordenes");
        b.Property(x => x.NumeroOrden).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.NumeroOrden).IsUnique();
        b.Property(x => x.Estado).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.EstadoPago).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.TipoEntrega).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.Property(x => x.CodigoRetiro).HasMaxLength(40);
        b.Property(x => x.IdempotencyKey).HasMaxLength(120);
        b.HasIndex(x => new { x.ClienteId, x.CreadaEn });
        b.HasIndex(x => x.IdempotencyKey);

        b.Property(x => x.RetiroCedula).HasMaxLength(20);
        b.Property(x => x.RetiroNombre).HasMaxLength(150);
        b.Property(x => x.RetiroTelefono).HasMaxLength(30);
        b.Property(x => x.RetiroFranja).HasMaxLength(50);
        b.Property(x => x.DestinatarioNombre).HasMaxLength(150);
        b.Property(x => x.DestinatarioTelefono).HasMaxLength(30);
        b.Property(x => x.DireccionTexto).HasMaxLength(400);
        b.Property(x => x.ObservacionesEntrega).HasMaxLength(500);

        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Pedido>().WithMany().HasForeignKey(x => x.PedidoLogisticoId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany<DetalleOrden>("Detalles").WithOne().HasForeignKey(d => d.OrdenId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation("Detalles").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
