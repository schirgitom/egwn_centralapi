using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EGWNCentral.Infrastructure.Persistence.Configurations;

public sealed class MeteringPointConfiguration : IEntityTypeConfiguration<MeteringPoint>
{
    public void Configure(EntityTypeBuilder<MeteringPoint> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MeteringPointCode)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.EnergyDirection)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(200);

        builder.HasIndex(x => x.MeteringPointCode)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
