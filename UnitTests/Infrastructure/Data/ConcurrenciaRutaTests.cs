using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UnitTests.Domain;

namespace UnitTests.Infrastructure;

public class ConcurrenciaRutaTests
{
    [Fact]
    public async Task CambiarEstadoDePedido_ActualizaVersionDeLaRuta()
    {
        var zona = DomainFixtures.CrearZona();
        var repartidor = DomainFixtures.CrearRepartidor();
        var ruta = Ruta.Crear(zona, repartidor, DomainFixtures.Hoy, DomainFixtures.Ahora);
        var pedido = DomainFixtures.CrearPedido(zona);
        ruta.AgregarPedido(pedido);
        ruta.Iniciar(repartidor, DomainFixtures.Ahora);
        var interceptor = new InspeccionarGuardado(ruta);
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=pruebas_sin_conexion", n => n.UseNetTopologySuite())
            .AddInterceptors(interceptor).Options;
        await using var db = new AppDbContext(opciones);
        db.Attach(ruta);

        ruta.MarcarEnCamino(pedido.Id);
        await db.SaveChangesAsync();

        Assert.True(interceptor.Inspeccionado);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CambiarParadas_ActualizaLaRutaParaComprobarSuVersion(bool agregar)
    {
        var zona = DomainFixtures.CrearZona();
        var ruta = Ruta.Crear(zona, DomainFixtures.CrearRepartidor(),
            DomainFixtures.Hoy, DomainFixtures.Ahora);
        var pedido = DomainFixtures.CrearPedido(zona);
        ruta.AgregarPedido(pedido);

        var interceptor = new InspeccionarGuardado(ruta);
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=pruebas_sin_conexion", n => n.UseNetTopologySuite())
            .AddInterceptors(interceptor).Options;
        await using var db = new AppDbContext(opciones);
        db.Attach(ruta);

        if (agregar)
        {
            var nuevo = DomainFixtures.CrearPedido(zona);
            db.Pedidos.Add(nuevo);
            ruta.AgregarPedido(nuevo);
        }
        else
        {
            ruta.QuitarPedido(pedido.Id);
        }

        await db.SaveChangesAsync();
        Assert.True(interceptor.Inspeccionado);
    }

    // Inspecciona el comando pendiente de EF antes de acceder a la base de datos.
    // La carrera real de xmin requiere ademas pruebas contra PostgreSQL.
    private sealed class InspeccionarGuardado(Ruta ruta) : SaveChangesInterceptor
    {
        public bool Inspeccionado { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var entrada = eventData.Context!.Entry(ruta);
            Assert.Equal(EntityState.Modified, entrada.State);
            Assert.True(entrada.Property(r => r.Capacidad).IsModified);
            Assert.True(entrada.Property(r => r.Version).Metadata.IsConcurrencyToken);
            Inspeccionado = true;
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
        }
    }
}
