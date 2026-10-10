using Application.Interfaces;
using Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IPedidoService, PedidoService>();
        services.AddScoped<IZonaService, ZonaService>();
        services.AddScoped<IRepartidorService, RepartidorService>();
        services.AddScoped<IRutaService, RutaService>();
        services.AddScoped<ISeguimientoService, SeguimientoService>();
        services.AddScoped<IAsignadorUltimoMomento, AsignadorUltimoMomento>();

        return services;
    }
}
