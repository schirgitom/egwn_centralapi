using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Abstractions.Persistence;
using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Services;

public sealed class MeteringPointService(IUnitOfWork unitOfWork) : IMeteringPointService
{
    public async Task<IReadOnlyCollection<MeteringPoint>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await unitOfWork.MeteringPoints.Query(asNoTracking: true)
            .OrderBy(x => x.MeteringPointCode)
            .ToListAsync(cancellationToken);
    }

    public Task<MeteringPoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.MeteringPoints.Query(asNoTracking: true)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<MeteringPoint> CreateAsync(Guid customerId, string meteringPointCode, string energyDirection, string? description, CancellationToken cancellationToken = default)
    {
        var customerExists = await unitOfWork.Customers.Query(asNoTracking: true).AnyAsync(x => x.Id == customerId, cancellationToken);
        if (!customerExists)
        {
            throw new KeyNotFoundException($"Customer '{customerId}' not found.");
        }

        var normalizedCode = Normalize(meteringPointCode);
        var normalizedEnergyDirection = NormalizeEnergyDirection(energyDirection);
        var exists = await unitOfWork.MeteringPoints.Query(asNoTracking: true)
            .AnyAsync(x => x.MeteringPointCode == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Metering point '{normalizedCode}' already exists.");
        }

        var meteringPoint = new MeteringPoint
        {
            CustomerId = customerId,
            MeteringPointCode = normalizedCode,
            EnergyDirection = normalizedEnergyDirection,
            Description = NormalizeNullable(description)
        };

        await unitOfWork.MeteringPoints.AddAsync(meteringPoint, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return meteringPoint;
    }

    public async Task<MeteringPoint?> UpdateAsync(Guid id, string? description, string? energyDirection, CancellationToken cancellationToken = default)
    {
        var meteringPoint = await unitOfWork.MeteringPoints.GetByIdAsync(id, cancellationToken);
        if (meteringPoint is null)
        {
            return null;
        }

        if (description is not null)
        {
            meteringPoint.Description = NormalizeNullable(description);
        }

        if (energyDirection is not null)
        {
            meteringPoint.EnergyDirection = NormalizeEnergyDirection(energyDirection);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return meteringPoint;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var meteringPoint = await unitOfWork.MeteringPoints.GetByIdAsync(id, cancellationToken);
        if (meteringPoint is null)
        {
            return false;
        }

        unitOfWork.MeteringPoints.Remove(meteringPoint);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value cannot be empty.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeEnergyDirection(string energyDirection)
    {
        var normalized = Normalize(energyDirection).ToLowerInvariant();
        if (normalized is not ("generation" or "consumption"))
        {
            throw new ArgumentException("Energy direction must be either 'generation' or 'consumption'.", nameof(energyDirection));
        }

        return normalized;
    }
}
