namespace Api.Extensions;

public static class SwaggerUiExtensions
{
    // Versión fija de la interfaz oficial de Swagger UI, servida desde CDN.
    private const string VersionSwaggerUi = "5.17.14";

    /// <summary>
    /// Publica Swagger UI en <paramref name="ruta"/> leyendo el documento OpenAPI nativo de ASP.NET Core.
    /// Se usa la UI oficial en lugar de Swashbuckle, que no es compatible con los tipos OpenAPI de .NET 9+.
    /// </summary>
    public static IEndpointRouteBuilder MapSwaggerUi(
        this IEndpointRouteBuilder app, string ruta = "/swagger", string documento = "/openapi/v1.json")
    {
        var html = $$"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Delivery API - Swagger</title>
              <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@{{VersionSwaggerUi}}/swagger-ui.css" />
            </head>
            <body>
              <div id="swagger-ui"></div>
              <script src="https://unpkg.com/swagger-ui-dist@{{VersionSwaggerUi}}/swagger-ui-bundle.js"></script>
              <script>
                window.ui = SwaggerUIBundle({
                  url: "{{documento}}",
                  dom_id: "#swagger-ui",
                  deepLinking: true,
                  displayRequestDuration: true,
                  tryItOutEnabled: true
                });
              </script>
            </body>
            </html>
            """;

        app.MapGet(ruta, () => Results.Content(html, "text/html; charset=utf-8")).ExcludeFromDescription();
        return app;
    }
}
