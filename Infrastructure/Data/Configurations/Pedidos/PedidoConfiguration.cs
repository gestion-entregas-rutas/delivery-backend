using Domain.Entities;
using Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("Pedidos");

        builder.Property(pedido => pedido.ClienteNombre).HasMaxLength(150).IsRequired();
        builder.Property(pedido => pedido.ClienteTelefono).HasMaxLength(30).IsRequired();
        builder.Property(pedido => pedido.Direccion).HasMaxLength(300).IsRequired();
        builder.Property(pedido => pedido.Estado).HasConversion<string>().HasMaxLength(20);

        builder.Property(p => p.Ubicacion)
            .HasConversion(new GeoPointConverter())
            .HasColumnType("geometry (point, 4326)");

        builder.HasOne<Zona>().WithMany().HasForeignKey(p => p.ZonaId).OnDelete(DeleteBehavior.Restrict);

        // Consulta del armado de rutas: pendientes de una fecha y zona.
        builder.HasIndex(pedido => new { pedido.FechaEntrega, pedido.ZonaId, pedido.Estado });
        builder.HasIndex(pedido => pedido.RutaId);
        builder.HasIndex(pedido => pedido.Ubicacion).HasMethod("gist");
    }
}
