
using System.Text;
using EGWNCentral.Api.Options;
using EGWNCentral.Api.Services;
using EGWNCentral.Infrastructure;
using EGWNCentral.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Winton.Extensions.Configuration.Consul;

namespace EGWNCentral.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var preConfiguration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var builder = WebApplication.CreateBuilder(args);

        if (preConfiguration.GetValue<bool>("Consul:Enabled"))
        {
            var consulAddress = preConfiguration["Consul:Address"] ?? "http://localhost:8500";
            var consulKey = preConfiguration["Consul:Key"] ?? "egwncentral/appsettings";

            builder.Configuration.AddConsul(consulKey, options =>
            {
                options.Optional = true;
                options.ReloadOnChange = false;
                options.ConsulConfigurationOptions = configuration => { configuration.Address = new Uri(consulAddress); };
            });
        }

        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext();

            var seqUrl = context.Configuration["Seq:Url"];
            if (string.IsNullOrWhiteSpace(seqUrl))
            {
                loggerConfiguration.WriteTo.Console();
            }
            else
            {
                loggerConfiguration.WriteTo.Seq(seqUrl);
            }
        });

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is required.");

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) ||
            string.IsNullOrWhiteSpace(jwtOptions.Audience) ||
            string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("Jwt:Issuer, Jwt:Audience and Jwt:SigningKey are required.");
        }

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<JwtTokenService>();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();


        var app = builder.Build();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            SeedData.Seed(db);
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi("/swagger/v1/swagger.json");
        }

        app.UseSerilogRequestLogging();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.Run();
    }
}
