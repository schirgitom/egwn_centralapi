using System.Text.Json;
using EGWNCentral.Core.Domain.Entities;
using EGWNCentral.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<KeycloakMembership> KeycloakMemberships => Set<KeycloakMembership>();

    public DbSet<MeteringPoint> MeteringPoints => Set<MeteringPoint>();

    public DbSet<EnergyCommunity> EnergyCommunities => Set<EnergyCommunity>();

    public DbSet<CommunityParticipation> CommunityParticipations => Set<CommunityParticipation>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    private static readonly JsonSerializerOptions EventJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<Guid, OutboxMessage> _pendingSnapshots = [];
    private readonly HashSet<Guid> _registrationCodeRotations = [];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ModelSeedData.Configure(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var customers = new List<Customer>();
        if (HasSnapshotChanges())
        {
            CustomerSnapshotBuilder.ReferencedCommunities(this).Load();
            customers = LoadAffectedCustomers(CustomerSnapshotBuilder.AffectedCustomers(this).ToList());
        }
        PrepareChanges();
        EnqueueSnapshots(customers);
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        _pendingSnapshots.Clear();
        _registrationCodeRotations.Clear();
        return result;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var customers = new List<Customer>();
        if (HasSnapshotChanges())
        {
            await CustomerSnapshotBuilder.ReferencedCommunities(this).LoadAsync(cancellationToken);
            customers = LoadAffectedCustomers(await CustomerSnapshotBuilder.AffectedCustomers(this).ToListAsync(cancellationToken));
        }
        PrepareChanges();
        EnqueueSnapshots(customers);
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        _pendingSnapshots.Clear();
        _registrationCodeRotations.Clear();
        return result;
    }

    internal void RotateRegistrationCode(Customer customer, string registrationCode)
    {
        if (Entry(customer).State == EntityState.Detached || !RegistrationCodeGenerator.IsValid(registrationCode))
        {
            throw new ArgumentException("A tracked customer and valid registration code are required.");
        }

        customer.RegistrationCode = registrationCode;
        _registrationCodeRotations.Add(customer.Id);
    }

    private bool HasSnapshotChanges()
        => ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            (entry.Entity is SoftDeletableEntity || entry.Metadata.Name == "CustomerMeteringPoints"));

    private List<Customer> LoadAffectedCustomers(List<Customer> persistedCustomers)
    {
        // Added customers are not returned by the database query, but relationship fixup includes their new links.
        var addedCustomers = ChangeTracker.Entries<Customer>()
            .Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity);
        return persistedCustomers.Concat(addedCustomers).DistinctBy(customer => customer.Id).ToList();
    }

    private void EnqueueSnapshots(List<Customer> customers)
    {
        foreach (var customer in customers)
        {
            var added = Entry(customer).State == EntityState.Added;
            if (!_pendingSnapshots.TryGetValue(customer.Id, out var pending))
            {
                pending = new OutboxMessage
                {
                    Id = added ? customer.Id : Guid.NewGuid(),
                    OccurredAtUtc = added ? customer.CreatedAtUtc : DateTimeOffset.UtcNow
                };
                _pendingSnapshots.Add(customer.Id, pending);
                OutboxMessages.Add(pending);
            }

            if (added)
            {
                customer.CreatedAtUtc = pending.OccurredAtUtc;
            }
            var eventType = customer.IsDeleted ? "customer.deleted" : added ? "customer.created" : "customer.updated";
            var snapshot = new CustomerSnapshot(pending.Id, eventType, 1, pending.OccurredAtUtc,
                CustomerSnapshotBuilder.Build(this, customer));
            pending.Payload = JsonSerializer.Serialize(snapshot, EventJsonOptions);
        }
    }

    private void PrepareChanges()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Customer>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.RegistrationCode))
            {
                entry.Entity.RegistrationCode = GenerateRegistrationCode();
            }
            else if (entry.Property(customer => customer.RegistrationCode).IsModified &&
                     !_registrationCodeRotations.Contains(entry.Entity.Id))
            {
                throw new InvalidOperationException("A customer's registration code can only be changed through rotation.");
            }
            if (entry.State == EntityState.Added && !RegistrationCodeGenerator.IsValid(entry.Entity.RegistrationCode))
            {
                throw new InvalidOperationException("Registration codes must contain six uppercase letters or digits, excluding O and 0.");
            }
        }

        foreach (var entry in ChangeTracker.Entries<SoftDeletableEntity>().ToList())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }

            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
        }

    }

    private string GenerateRegistrationCode()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var code = RegistrationCodeGenerator.Generate();
            if (!Customers.Local.Any(customer => customer.RegistrationCode == code) &&
                !Customers.IgnoreQueryFilters().Any(customer => customer.RegistrationCode == code))
            {
                return code;
            }
        }
        throw new InvalidOperationException("Unable to generate a unique registration code after 100 attempts.");
    }
}
