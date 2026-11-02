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
            .Include(x => x.Customers)
            .OrderBy(x => x.MeteringPointCode)
            .ToListAsync(cancellationToken);
    }

    public Task<MeteringPoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.MeteringPoints.Query(asNoTracking: true)
            .Include(x => x.Customers)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<MeteringPoint> CreateAsync(IReadOnlyCollection<Guid> customerIds, string meteringPointCode, string energyDirection, string? description, CancellationToken cancellationToken = default)
    {
        var customers = await LoadCustomersAsync(customerIds, cancellationToken);

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
            Customers = customers,
            MeteringPointCode = normalizedCode,
            EnergyDirection = normalizedEnergyDirection,
            Description = NormalizeNullable(description)
        };

        await unitOfWork.MeteringPoints.AddAsync(meteringPoint, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return meteringPoint;
    }

    public async Task<MeteringPoint?> UpdateAsync(Guid id, string? description, string? energyDirection, IReadOnlyCollection<Guid>? customerIds = null, CancellationToken cancellationToken = default)
    {
        var meteringPoint = await unitOfWork.MeteringPoints.Query()
            .Include(x => x.Customers)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (meteringPoint is null)
        {
            return null;
        }

        var customers = customerIds is null ? null : await LoadCustomersAsync(customerIds, cancellationToken);
        var normalizedEnergyDirection = energyDirection is null ? null : NormalizeEnergyDirection(energyDirection);

        if (description is not null)
        {
            meteringPoint.Description = NormalizeNullable(description);
        }

        if (normalizedEnergyDirection is not null)
        {
            meteringPoint.EnergyDirection = normalizedEnergyDirection;
        }

        if (customers is not null)
        {
            meteringPoint.Customers.Clear();
            foreach (var customer in customers)
            {
                meteringPoint.Customers.Add(customer);
            }
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

    private async Task<List<Customer>> LoadCustomersAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(customerIds);
        var ids = customerIds.Distinct().ToList();
        if (ids.Count == 0 || ids.Contains(Guid.Empty))
        {
            throw new ArgumentException("At least one non-empty customer ID is required.", nameof(customerIds));
        }

        var customers = await unitOfWork.Customers.Query()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var missingIds = ids.Except(customers.Select(x => x.Id)).ToArray();
        if (missingIds.Length != 0)
        {
            throw new KeyNotFoundException($"Customers not found: {string.Join(", ", missingIds)}.");
        }

        return customers;
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
