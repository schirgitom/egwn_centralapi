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

        builder.Property(x => x.RegistrationCode)
            .HasMaxLength(6)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("generate_customer_registration_code()")
            .IsRequired();

        builder.HasIndex(x => x.RegistrationCode).IsUnique();
        builder.ToTable("Customers", table => table.HasCheckConstraint(
            "CK_Customers_RegistrationCode_Format", "\"RegistrationCode\" ~ '^[A-NP-Z1-9]{6}$'"));

        builder.Property(x => x.Email)
            .HasMaxLength(320);

        builder.HasIndex(x => x.CustomerNumber)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasMany(x => x.MeteringPoints)
            .WithMany(x => x.Customers)
            .UsingEntity<Dictionary<string, object>>(
                "CustomerMeteringPoints",
                right => right.HasOne<MeteringPoint>().WithMany().HasForeignKey("MeteringPointId"),
                left => left.HasOne<Customer>().WithMany().HasForeignKey("CustomerId"),
                join => join.HasKey("CustomerId", "MeteringPointId"));

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
