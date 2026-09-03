using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EGWNCentral.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerNumber)
            .HasMaxLength(64)
            .HasDefaultValueSql("'EGWN-' || lpad(nextval('customer_number_seq')::text, 2, '0')")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(320);

        builder.HasIndex(x => x.CustomerNumber)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasMany(x => x.MeteringPoints)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
