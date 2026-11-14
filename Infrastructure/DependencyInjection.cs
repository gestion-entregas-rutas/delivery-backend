using Application.Interfaces;
using Domain.Interfaces;
using Infrastructure.Data;
using Infrastructure.ExternalServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opciones =>
            opciones.UseNpgsql(config.GetConnectionString("DefaultConnection"), n => n.UseNetTopologySuite()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IPlanificadorRutas, PlanificadorRutasPropio>();

        services.AddSingleton<IProtectorClaves, ProtectorClaves>();
        services.TryAddSingleton<INotificadorEventos, NotificadorNulo>();
        services.TryAddSingleton<IPagoService, PagoSimuladoService>();

        return services;
    }
}
