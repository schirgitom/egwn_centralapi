using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Abstractions.Persistence;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Persistence.Repositories;
using EGWNCentral.Infrastructure.Services;
using EGWNCentral.Infrastructure.Messaging;
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

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => options.DispatchIntervalSeconds > 0 && options.BatchSize > 0,
                "RabbitMQ dispatch interval and batch size must be positive.")
            .Validate(options => !options.Enabled ||
                (!string.IsNullOrWhiteSpace(options.HostName) &&
                 options.Port is > 0 and <= 65535 &&
                 !string.IsNullOrWhiteSpace(options.VirtualHost) &&
                 !string.IsNullOrWhiteSpace(options.UserName) &&
                 !string.IsNullOrWhiteSpace(options.Password) &&
                 !string.IsNullOrWhiteSpace(options.Exchange) &&
                 !string.IsNullOrWhiteSpace(options.RoutingKey) &&
                 !string.IsNullOrWhiteSpace(options.Queue)),
                "Enabled RabbitMQ requires host, valid port, virtual host, credentials, exchange, routing key and queue.")
            .ValidateOnStart();
        services.AddScoped<ICustomerEventPublisher, RabbitMqCustomerEventPublisher>();
        services.AddScoped<CustomerOutboxDispatcher>();
        services.AddHostedService<CustomerOutboxWorker>();

        return services;
    }
}
