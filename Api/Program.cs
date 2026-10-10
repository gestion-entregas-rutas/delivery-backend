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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(opciones => opciones.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddSignalR()
    .AddJsonProtocol(opciones => opciones.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Reemplaza al NotificadorNulo que registra Infrastructure por defecto.
builder.Services.AddSingleton<INotificadorEventos, NotificadorSignalR>();

builder.Services.AddExceptionHandler<ManejadorExcepciones>();
builder.Services.AddProblemDetails();

// Web (Angular) y móvil (Flutter). SignalR necesita orígenes explícitos para permitir credenciales.
var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(opciones => opciones.AddPolicy(PoliticaCors, politica =>
    politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapSwaggerUi();
}

app.UseHttpsRedirection();
app.UseCors(PoliticaCors);
app.UseAuthorization();

app.MapControllers();
app.MapHub<SeguimientoHub>("/hubs/seguimiento");

app.Run();

// Permite que las pruebas de integración (WebApplicationFactory<Program>) arranquen la Api en memoria.
public partial class Program;
