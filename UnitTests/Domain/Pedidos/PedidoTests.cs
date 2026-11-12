using Domain.Enums;
using Domain.Exceptions;

namespace UnitTests.Domain;

public class PedidoTests
{
    [Fact]
    public void Registrar_ConDatosValidos_QuedaPendiente()
    {
        var pedido = DomainFixtures.CrearPedido(DomainFixtures.CrearZona());
        Assert.Equal(EstadoPedido.Pendiente, pedido.Estado);
    }

    [Fact]
    public void Registrar_ConFechaPasada_Falla()
    {
        var zona = DomainFixtures.CrearZona();
        Assert.Throws<DomainException>(() =>
            DomainFixtures.CrearPedido(zona, fecha: DomainFixtures.Hoy.AddDays(-1)));
    }

    [Fact]
    public void Registrar_ConAnticipacion_NoEsDeUltimoMomento()
    {
        var pedido = DomainFixtures.CrearPedido(DomainFixtures.CrearZona(), fecha: DomainFixtures.Hoy.AddDays(10));
        Assert.False(pedido.EsDeUltimoMomento(DomainFixtures.Hoy));
    }

    [Fact]
    public void Registrar_FueraDeLaZona_Falla()
    {
        var zona = DomainFixtures.CrearZona();
        Assert.Throws<DomainException>(() => DomainFixtures.CrearPedido(zona, lat: 5, lon: 5));
    }

    [Fact]
    public void AsignarARuta_DosVeces_Falla()
    {
        var pedido = DomainFixtures.CrearPedido(DomainFixtures.CrearZona());
        pedido.AsignarARuta(Guid.NewGuid());
        Assert.Throws<TransicionInvalidaException>(() => pedido.AsignarARuta(Guid.NewGuid()));
    }

    [Fact]
    public void Entregar_SinHaberSalido_Falla()
    {
        var pedido = DomainFixtures.CrearPedido(DomainFixtures.CrearZona());
        pedido.AsignarARuta(Guid.NewGuid());
        Assert.Throws<TransicionInvalidaException>(() => pedido.MarcarEntregado(DomainFixtures.Ahora));
    }
}
