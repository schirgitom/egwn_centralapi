using EGWNCentral.Core.Abstractions.Persistence;
using EGWNCentral.Core.Domain.Entities;
using EGWNCentral.Infrastructure.Persistence.Repositories;

namespace EGWNCentral.Infrastructure.Persistence;

public sealed class UnitOfWork(ApplicationDbContext dbContext) : IUnitOfWork
{
    private IRepository<Customer>? _customers;
    private IRepository<MeteringPoint>? _meteringPoints;
    private IRepository<EnergyCommunity>? _energyCommunities;
    private IRepository<CommunityParticipation>? _communityParticipations;

    public IRepository<Customer> Customers => _customers ??= new EfRepository<Customer>(dbContext);

    public IRepository<MeteringPoint> MeteringPoints => _meteringPoints ??= new EfRepository<MeteringPoint>(dbContext);

    public IRepository<EnergyCommunity> EnergyCommunities => _energyCommunities ??= new EfRepository<EnergyCommunity>(dbContext);

    public IRepository<CommunityParticipation> CommunityParticipations => _communityParticipations ??= new EfRepository<CommunityParticipation>(dbContext);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
