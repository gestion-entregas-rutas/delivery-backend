using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ParadaRutaConfiguration : IEntityTypeConfiguration<ParadaRuta>
{
    public void Configure(EntityTypeBuilder<ParadaRuta> builder)
    {
        builder.ToTable("ParadasRuta");
        builder.HasKey(paradaRuta => new { paradaRuta.RutaId, paradaRuta.PedidoId });

        builder.HasOne(paradaRuta => paradaRuta.Pedido).WithMany().HasForeignKey(paradaRuta => paradaRuta.PedidoId).OnDelete(DeleteBehavior.Restrict);

        // Un pedido está como máximo en una parada: impide la doble asignación a nivel de BD.
        builder.HasIndex(paradaRuta => paradaRuta.PedidoId).IsUnique();
    }
}
