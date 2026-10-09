using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EGWNCentral.Infrastructure.Persistence.Configurations;

public sealed class CommunityParticipationConfiguration : IEntityTypeConfiguration<CommunityParticipation>
{
    public void Configure(EntityTypeBuilder<CommunityParticipation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParticipationPercentage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_CommunityParticipations_ParticipationPercentage",
            "\"ParticipationPercentage\" >= 0 AND \"ParticipationPercentage\" <= 100"));

        builder.HasIndex(x => new { x.EnergyCommunityId, x.MeteringPointId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(x => x.MeteringPoint)
            .WithMany(x => x.CommunityParticipations)
            .HasForeignKey(x => x.MeteringPointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
