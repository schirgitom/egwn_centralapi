using System.Text.Json;
using EGWNCentral.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace EGWNCentral.Tests.Infrastructure;

public class OpenApiServerTests
{
    [Fact]
    public async Task SwaggerDocumentUsesSameOriginBehindProxy()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddOpenApi(options =>
        {
            options.AddSameOriginServer();
            options.AddBearerSecurity();
        });

        await using var app = builder.Build();
        app.MapOpenApi("/swagger/v1/swagger.json");
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/swagger/v1/swagger.json");
        request.Headers.Host = "admin.example.com";
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("/", document.RootElement.GetProperty("servers")[0].GetProperty("url").GetString());
        Assert.True(document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _));
        Assert.True(document.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("InternalBearer", out _));
    }
}
