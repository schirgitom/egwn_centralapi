using EGWNCentral.Core.Domain.Entities;

namespace EGWNCentral.Core.Abstractions.Persistence;

public interface IUnitOfWork
{
    IRepository<Customer> Customers { get; }

    IRepository<MeteringPoint> MeteringPoints { get; }

    IRepository<EnergyCommunity> EnergyCommunities { get; }

    IRepository<CommunityParticipation> CommunityParticipations { get; }

    void RotateRegistrationCode(Customer customer, string registrationCode);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
