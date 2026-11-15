
using System.Text;
using EGWNCentral.Api.Options;
using EGWNCentral.Api.Services;
using EGWNCentral.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Serilog;
using Winton.Extensions.Configuration.Consul;

namespace EGWNCentral.Api;

public class Program
{
    public static void Main(string[] args)
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
        builder.Services.AddOptions<InternalRegistrationOptions>()
            .Bind(builder.Configuration.GetSection(InternalRegistrationOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Token) &&
                                 !options.Token.Any(char.IsControl) &&
                                 Encoding.UTF8.GetByteCount(options.Token) >= 32,
                "InternalRegistration:Token must contain at least 32 UTF-8 bytes.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TicketSigningKey) &&
                                 Encoding.UTF8.GetByteCount(options.TicketSigningKey) >= 32,
                "InternalRegistration:TicketSigningKey must contain at least 32 UTF-8 bytes.")
            .ValidateOnStart();

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
        builder.Services.AddSingleton<RegistrationTicketService>();

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
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, InternalBearerAuthenticationHandler>(
                InternalBearerDefaults.AuthenticationScheme, _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();


        var app = builder.Build();

        var databaseConnection = new NpgsqlConnectionStringBuilder(
            app.Configuration.GetConnectionString("DefaultConnection"));
        app.Logger.LogInformation(
            "Using database server {DatabaseHost}:{DatabasePort}, database {DatabaseName}, user {DatabaseUser}",
            databaseConnection.Host,
            databaseConnection.Port,
            databaseConnection.Database,
            databaseConnection.Username);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi("/swagger/v1/swagger.json");
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "EGWN Central API");
            });
        }

        app.UseSerilogRequestLogging();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.Run();
    }
}
