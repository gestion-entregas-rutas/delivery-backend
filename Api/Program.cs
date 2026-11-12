using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json.Serialization;
using Api.Extensions;
using Api.Hubs;
using Api.Middleware;
using Api.Services;
using Application;
using Application.Interfaces;
using Infrastructure;

const string PoliticaCors = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAutenticacion(builder.Configuration);
builder.Services.AddSingleton<SesionesSignalR>();
builder.Services.AddSingleton<IRevocadorSesiones>(sp => sp.GetRequiredService<SesionesSignalR>());
builder.Services.AddScoped<AccesoRecursos>();
builder.Services.AddScoped<PermisosRecursosFilter>();
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("Login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(o => o.Filters.AddService<PermisosRecursosFilter>())
    .AddJsonOptions(opciones => opciones.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<SeguridadOpenApiTransformer>());

builder.Services.AddSignalR(o => o.AddFilter<PermisosHubFilter>())
    .AddJsonProtocol(opciones => opciones.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Reemplaza al NotificadorNulo que registra Infrastructure por defecto.
builder.Services.AddSingleton<INotificadorEventos, NotificadorSignalR>();

builder.Services.AddExceptionHandler<ManejadorExcepciones>();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);

// En desarrollo se aceptan los origenes del equipo frontend, incluido SignalR.
// AllowAnyOrigin no se combina con AllowCredentials: se refleja el origen autorizado.
var corsAbiertoParaPruebas = builder.Environment.IsDevelopment() &&
    (builder.Configuration.GetValue<bool?>("Cors:PermitirCualquierOrigenEnDesarrollo") ?? true);
var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(opciones => opciones.AddPolicy(PoliticaCors, politica =>
{
    if (corsAbiertoParaPruebas)
        politica.SetIsOriginAllowed(_ => true);
    else
        politica.WithOrigins(origenes);

    politica.AllowAnyHeader().AllowAnyMethod().AllowCredentials()
        .WithExposedHeaders("Location");
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapSwaggerUi();
}

// El perfil HTTP permite probar Angular sin certificados locales ni redirecciones CORS.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors(PoliticaCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Controladores protegidos por la política predeterminada; login permite acceso anónimo.
app.MapControllers();
app.MapHub<SeguimientoHub>("/hubs/seguimiento", o => o.CloseOnAuthenticationExpiration = true).RequireAuthorization("Completo");

if (args.Contains("--crear-admin"))
{
    await Api.Services.AdministradorInicial.CrearAsync(app.Services, builder.Configuration);
    return;
}
app.Run();

// Permite que las pruebas de integración (WebApplicationFactory<Program>) arranquen la Api en memoria.
public partial class Program;
