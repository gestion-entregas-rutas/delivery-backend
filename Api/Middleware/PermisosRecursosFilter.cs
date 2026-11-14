using Api.Services;
using Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
namespace Api.Middleware;

public sealed class PermisosRecursosFilter(AccesoRecursos acceso) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var user = ctx.HttpContext.User;
        var a = (ControllerActionDescriptor)ctx.ActionDescriptor;
        if (a.EndpointMetadata.OfType<IAllowAnonymous>().Any() ||
            a.ControllerName is "Autenticacion" or "Usuarios" || user.IsInRole("Administrador")) { await next(); return; }
        var ct = ctx.HttpContext.RequestAborted;
        switch (a.ControllerName, a.ActionName)
        {
            case ("Productos", "CrearResena") when user.IsInRole("Cliente"):
                break;
            case ("Ubicaciones", "Registrar"):
                AccesoRecursos.Repartidor(user); break;
            case ("Ubicaciones", "Obtener"):
                if ((Guid)ctx.ActionArguments["id"]! != AccesoRecursos.Repartidor(user)) throw new AccesoDenegadoException();
                break;
            case ("Rutas", "Obtener" or "Iniciar"):
                await acceso.ExigirRuta(user, (Guid)ctx.ActionArguments["id"]!, ct); break;
            case ("Pedidos", "Obtener"):
                await acceso.ExigirPedido(user, (Guid)ctx.ActionArguments["id"]!, ct); break;
            case ("Seguimiento", _):
                await acceso.ExigirPedido(user, (Guid)ctx.ActionArguments["pedidoId"]!, ct); break;
            case ("Repartidores", "ObtenerRuta"):
                if ((Guid)ctx.ActionArguments["id"]! != AccesoRecursos.Repartidor(user)) throw new AccesoDenegadoException();
                break;
            default: throw new AccesoDenegadoException();
        }
        await next();
    }
}
