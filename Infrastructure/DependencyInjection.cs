using Application.Interfaces;
using Domain.Interfaces;
using Infrastructure.Data;
using Infrastructure.ExternalServices;
using Infrastructure.ExternalServices.Pagos;
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
        services.Configure<PayPalOptions>(config.GetSection(PayPalOptions.Section));
        services.AddHttpClient<IPayPalService, PayPalCheckoutService>(client =>
            client.BaseAddress = new Uri("https://api-m.sandbox.paypal.com"));

        return services;
    }
}
