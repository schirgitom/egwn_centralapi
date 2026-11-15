using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EGWNCentral.Infrastructure.Persistence.Configurations;

public sealed class KeycloakMembershipConfiguration : IEntityTypeConfiguration<KeycloakMembership>
{
    public void Configure(EntityTypeBuilder<KeycloakMembership> builder)
    {
        builder.HasKey(x => x.KeycloakUserId);

        builder.Property(x => x.KeycloakUserId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CustomerId);
    }
}
