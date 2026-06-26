using EGWNCentral.Core.Domain.Entities;

namespace EGWNCentral.Core.Abstractions.Persistence;

public interface IRepository<TEntity> where TEntity : SoftDeletableEntity
{
    IQueryable<TEntity> Query(bool asNoTracking = false);

    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Remove(TEntity entity);
}
