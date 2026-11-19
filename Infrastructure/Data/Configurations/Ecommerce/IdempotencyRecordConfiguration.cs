using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("IdempotencyRecords");
        b.Property(x => x.Key).HasMaxLength(120).IsRequired();
        b.Property(x => x.Operacion).HasMaxLength(80).IsRequired();
        b.HasIndex(x => new { x.Key, x.Operacion }).IsUnique();
        b.HasIndex(x => x.OrdenId);
    }
}
