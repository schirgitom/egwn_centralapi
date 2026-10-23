using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EGWNCentral.Infrastructure.Persistence.Configurations;

public sealed class EnergyCommunityConfiguration : IEntityTypeConfiguration<EnergyCommunity>
{
    public void Configure(EntityTypeBuilder<EnergyCommunity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Source)
            .HasColumnName("Quelle")
            .HasMaxLength(500);

        builder.HasMany(x => x.Participations)
            .WithOne(x => x.EnergyCommunity)
            .HasForeignKey(x => x.EnergyCommunityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
