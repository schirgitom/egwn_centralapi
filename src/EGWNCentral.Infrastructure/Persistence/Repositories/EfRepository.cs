using EGWNCentral.Core.Abstractions.Persistence;
using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Persistence.Repositories;

public sealed class EfRepository<TEntity>(ApplicationDbContext dbContext) : IRepository<TEntity>
    where TEntity : SoftDeletableEntity
{
    public IQueryable<TEntity> Query(bool asNoTracking = false)
    {
        var query = dbContext.Set<TEntity>().AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TEntity>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();
    }

    public void Remove(TEntity entity)
    {
        dbContext.Set<TEntity>().Remove(entity);
    }
}
