using System.Reflection;
using Domain.Entities;

namespace UnitTests.Domain;

/// <summary>Una ruta cargada desde la BD puede traer sus paradas en cualquier orden.</summary>
public class RutaCargadaTests
{
    private static void DesordenarParadas(Ruta ruta)
    {
        var campo = typeof(Ruta).GetField("_paradas", BindingFlags.NonPublic | BindingFlags.Instance)!;
        ((List<ParadaRuta>)campo.GetValue(ruta)!).Reverse();
    }

    private static (Zona zona, Ruta ruta, Pedido[] pedidos) RutaConTresParadas()
    {
        var zona = DomainFixtures.CrearZona();
        var ruta = Ruta.Crear(zona, DomainFixtures.CrearRepartidor(), DomainFixtures.Hoy, DomainFixtures.Ahora);
        var pedidos = Enumerable.Range(0, 3).Select(_ => DomainFixtures.CrearPedido(zona)).ToArray();
        foreach (var p in pedidos) ruta.AgregarPedido(p);
        return (zona, ruta, pedidos);
    }

    [Fact]
    public void Paradas_SiempreSeExponenOrdenadasPorOrden()
    {
        var (_, ruta, pedidos) = RutaConTresParadas();
        DesordenarParadas(ruta);

        Assert.Equal(pedidos.Select(p => p.Id), ruta.Paradas.Select(p => p.PedidoId));
    }

    [Fact]
    public void AgregarPedido_ConParadasDesordenadas_InsertaEnLaPosicionPedida()
    {
        var (zona, ruta, pedidos) = RutaConTresParadas();
        DesordenarParadas(ruta);
        var nuevo = DomainFixtures.CrearPedido(zona);

        ruta.AgregarPedido(nuevo, posicion: 1);

        Assert.Equal([pedidos[0].Id, nuevo.Id, pedidos[1].Id, pedidos[2].Id], ruta.Paradas.Select(p => p.PedidoId));
        Assert.Equal([1, 2, 3, 4], ruta.Paradas.Select(p => p.Orden));
    }

    [Fact]
    public void QuitarPedido_ConParadasDesordenadas_MantieneElOrdenDeLasRestantes()
    {
        var (_, ruta, pedidos) = RutaConTresParadas();
        DesordenarParadas(ruta);

        ruta.QuitarPedido(pedidos[0].Id);

        Assert.Equal([pedidos[1].Id, pedidos[2].Id], ruta.Paradas.Select(p => p.PedidoId));
        Assert.Equal([1, 2], ruta.Paradas.Select(p => p.Orden));
    }
}
