using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
namespace Api.Services;

public sealed class PermisosHubFilter : IHubFilter
{
    public async Task OnConnectedAsync(HubLifetimeContext ctx, Func<HubLifetimeContext, Task> next)
    {
        var id = Guid.Parse(ctx.Context.User!.FindFirst("sub")!.Value);
        var sesiones = ctx.ServiceProvider.GetRequiredService<SesionesSignalR>();
        sesiones.Registrar(id, ctx.Context);
        try { await next(ctx); }
        catch { sesiones.Quitar(ctx.Context.ConnectionId); throw; }
    }
    public async Task OnDisconnectedAsync(HubLifetimeContext ctx, Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        ctx.ServiceProvider.GetRequiredService<SesionesSignalR>().Quitar(ctx.Context.ConnectionId);
        await next(ctx, exception);
    }
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext ctx, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = ctx.Context.User!;
        var db = ctx.ServiceProvider.GetRequiredService<IAppDbContext>();
        var id = Guid.Parse(user.FindFirst("sub")!.Value);
        var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (u is null || !u.Activo || u.DebeCambiarCredenciales || u.SelloSeguridad.ToString() != user.FindFirst("sello")?.Value)
        { ctx.Context.Abort(); throw new HubException("Sesión revocada."); }
        if (!user.IsInRole("Administrador"))
        {
            try
            {
                switch (ctx.HubMethodName)
                {
                    case "SuscribirseARepartidor":
                        if ((Guid)ctx.HubMethodArguments[0]! != AccesoRecursos.Repartidor(user)) throw new Application.Exceptions.AccesoDenegadoException();
                        break;
                    // Los pedidos pueden reasignarse. El grupo estable del conductor es
                    // su repartidor; RutaActualizada ya contiene sus estados y paradas.
                    // Reservar los grupos por pedido al administrador evita suscripciones
                    // residuales a pedidos que luego pasan a otro conductor.
                    default: throw new Application.Exceptions.AccesoDenegadoException();
                }
            }
            catch (Application.Exceptions.AccesoDenegadoException) { throw new HubException("Acceso denegado."); }
        }
        return await next(ctx);
    }
}
