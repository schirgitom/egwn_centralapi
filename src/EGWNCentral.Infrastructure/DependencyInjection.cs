using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Abstractions.Persistence;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Persistence.Repositories;
using EGWNCentral.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EGWNCentral.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        }

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IMeteringPointService, MeteringPointService>();
        services.AddScoped<IEnergyCommunityService, EnergyCommunityService>();

        return services;
    }
}
