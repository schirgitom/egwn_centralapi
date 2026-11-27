using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EGWNCentral.Api.Services;

public static class OpenApiSecurityExtensions
{
    public static void AddSameOriginServer(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Servers = [new OpenApiServer { Url = "/" }];
            return Task.CompletedTask;
        });
    }

    public static void AddBearerSecurity(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT access token from POST /api/auth/token. Enter the token without the Bearer prefix."
            };
            document.Components.SecuritySchemes["InternalBearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                Description = "Internal registration token for Keycloak endpoints. Enter the token without the Bearer prefix."
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAllowAnonymous>().Any())
            {
                return Task.CompletedTask;
            }

            var authorization = metadata.OfType<IAuthorizeData>().ToArray();
            if (authorization.Length == 0)
            {
                return Task.CompletedTask;
            }

            var isInternal = authorization
                .SelectMany(item => (item.AuthenticationSchemes ?? string.Empty).Split(',', StringSplitOptions.TrimEntries))
                .Contains(InternalBearerDefaults.AuthenticationScheme, StringComparer.Ordinal);
            var scheme = isInternal ? "InternalBearer" : "Bearer";
            operation.Security = [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(scheme, context.Document)] = []
                }
            ];
            return Task.CompletedTask;
        });
    }
}
