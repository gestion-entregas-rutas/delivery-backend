using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
namespace UnitTests.Domain;

public sealed class AjustesRutaTests
{
    [Fact]
    public void Transferencia_DestinoLleno_NoCambiaElOrigen()
    {
        var zona = DomainFixtures.CrearZona(1);
        var origen = Ruta.Crear(zona, DomainFixtures.CrearRepartidor(), DomainFixtures.Hoy, DomainFixtures.Ahora);
        var destino = Ruta.Crear(zona, DomainFixtures.CrearRepartidor(), DomainFixtures.Hoy, DomainFixtures.Ahora);
        var p = DomainFixtures.CrearPedido(zona); origen.AgregarPedido(p); destino.AgregarPedido(DomainFixtures.CrearPedido(zona));
        Assert.Throws<DomainException>(() => origen.TransferirPedidoA(destino, p.Id, null));
        Assert.Single(origen.Paradas); Assert.Equal(origen.Id, p.RutaId); Assert.Equal(EstadoPedido.AsignadoARuta, p.Estado);
    }
    [Fact]
    public void Transferencia_Valida_MueveUnaSolaVezYReordena()
    {
        var zona = DomainFixtures.CrearZona();
        var a = Ruta.Crear(zona, DomainFixtures.CrearRepartidor(), DomainFixtures.Hoy, DomainFixtures.Ahora);
        var b = Ruta.Crear(zona, DomainFixtures.CrearRepartidor(), DomainFixtures.Hoy, DomainFixtures.Ahora);
        var p = DomainFixtures.CrearPedido(zona); var otro = DomainFixtures.CrearPedido(zona);
        a.AgregarPedido(p); b.AgregarPedido(otro); a.TransferirPedidoA(b, p.Id, 0);
        Assert.Empty(a.Paradas); Assert.Equal(b.Id, p.RutaId);
        Assert.Equal(new[] { p.Id, otro.Id }, b.Paradas.Select(x => x.PedidoId));
        Assert.Equal(new[] { 1, 2 }, b.Paradas.Select(x => x.Orden));
    }
    [Fact]
    public void Reordenamiento_NoPermiteDuplicadosNiMoverElTramoIniciado()
    {
        var zona = DomainFixtures.CrearZona(); var rep = DomainFixtures.CrearRepartidor();
        var r = Ruta.Crear(zona, rep, DomainFixtures.Hoy, DomainFixtures.Ahora);
        var a = DomainFixtures.CrearPedido(zona); var b = DomainFixtures.CrearPedido(zona); var c = DomainFixtures.CrearPedido(zona);
        r.AgregarPedido(a); r.AgregarPedido(b); r.AgregarPedido(c);
        Assert.Throws<DomainException>(() => r.ReordenarParadas([a.Id, a.Id, c.Id]));
        r.Iniciar(rep, DomainFixtures.Ahora); r.MarcarEnCamino(a.Id);
        Assert.Throws<DomainException>(() => r.ReordenarParadas([b.Id, a.Id, c.Id]));
        r.ReordenarParadas([a.Id, c.Id, b.Id]); Assert.Equal(c.Id, r.Paradas[1].PedidoId);
    }
}
