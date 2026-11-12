using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
namespace Api.Extensions;

public sealed class SeguridadOpenApiTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument doc, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        doc.Info.Version = "1.1.0";
        doc.Components ??= new();
        doc.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Pegue el accessToken obtenido en login." };
        foreach (var description in context.DescriptionGroups.SelectMany(g => g.Items))
        {
            if (description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) continue;
            var path = "/" + description.RelativePath?.Split('?')[0];
            if (!doc.Paths.TryGetValue(path, out var item) || item.Operations is null) continue;
            foreach (var operation in item.Operations.Where(o => o.Key.Method.Equals(description.HttpMethod, StringComparison.OrdinalIgnoreCase)).Select(o => o.Value))
                operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", doc)] = [] }];
        }
        return Task.CompletedTask;
    }
}
