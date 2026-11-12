using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Data;

/// <summary>
/// Solo para las herramientas de migraciones (dotnet ef). Lee la misma configuración que la Api:
/// appsettings.json, appsettings.Development.json, user-secrets y variables de entorno
/// (ConnectionStrings__DefaultConnection).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuracion = new ConfigurationBuilder()
            .SetBasePath(BuscarCarpetaApi())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(AppDbContextFactory).Assembly, optional: true)
            .AddUserSecrets("585582b7-3335-4d40-b6b0-881bedeaadcd", reloadOnChange: false) // UserSecretsId de Api
            .AddEnvironmentVariables()
            .Build();

        var conexion = configuracion.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection.");

        if (conexion.Contains("TU_USUARIO") || conexion.Contains("TU_PASSWORD"))
            throw new InvalidOperationException(
                "La cadena de conexión aún tiene los valores de ejemplo. " +
                "Reemplaza TU_USUARIO y TU_PASSWORD en Api/appsettings.Development.json.");

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexion, n => n.UseNetTopologySuite())
            .Options;

        return new AppDbContext(opciones);
    }

    private static string BuscarCarpetaApi()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var api = Path.Combine(dir.FullName, "Api");
            if (File.Exists(Path.Combine(api, "appsettings.json"))) return api;
        }

        throw new DirectoryNotFoundException("No se encontró la carpeta Api con appsettings.json.");
    }
}
