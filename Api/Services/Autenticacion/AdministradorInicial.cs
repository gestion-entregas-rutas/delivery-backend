using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Api.Services;

public static class AdministradorInicial
{
    public static async Task RestablecerLocalAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var administradores = await db.Usuarios.Where(u => u.Rol == RolUsuario.Administrador).Take(2).ToListAsync();
        if (administradores.Count != 1)
            throw new InvalidOperationException("Se requiere exactamente un administrador existente para restablecerlo.");
        var usuario = administradores[0];
        if (!usuario.Activo)
            throw new InvalidOperationException("El administrador esta desactivado; revisa la cuenta antes de restablecerla.");
        var clave = config["Bootstrap:Clave"] ?? throw new InvalidOperationException("Configure Bootstrap:Clave.");
        UsuarioService.ValidarClave(clave);
        usuario.RestablecerClave(scope.ServiceProvider.GetRequiredService<IProtectorClaves>().Proteger(clave));
        await db.SaveChangesAsync();
        Console.WriteLine($"Clave temporal restablecida para el administrador: {usuario.NombreUsuario}. Debe cambiarla al ingresar.");
    }
    // Operación local explícita, sin endpoint público ni credenciales predeterminadas.
    public static async Task CrearAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        if (await db.Usuarios.AnyAsync(u => u.Rol == RolUsuario.Administrador))
            throw new InvalidOperationException("Ya existe un administrador. Use la gestión de usuarios autenticada.");
        var nombre = config["Bootstrap:Usuario"] ?? throw new InvalidOperationException("Configure Bootstrap:Usuario.");
        var clave = config["Bootstrap:Clave"] ?? throw new InvalidOperationException("Configure Bootstrap:Clave.");
        UsuarioService.ValidarClave(clave);
        var protector = scope.ServiceProvider.GetRequiredService<IProtectorClaves>();
        db.Usuarios.Add(Usuario.Crear(nombre, protector.Proteger(clave), RolUsuario.Administrador, null));
        await db.SaveChangesAsync();
        Console.WriteLine("Administrador inicial creado. Debe cambiar sus credenciales al ingresar.");
    }
}
