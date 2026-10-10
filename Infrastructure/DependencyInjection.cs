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

        var segundos = int.TryParse(config["Planificador:SegundosLimite"], out var s) && s > 0 ? s : 2;
        services.AddSingleton<IPlanificadorRutas>(new PlanificadorRutasOrTools(segundos));

        services.TryAddSingleton<INotificadorEventos, NotificadorNulo>();

        return services;
    }
}
