using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;
namespace UnitTests.Domain;

public sealed class EdicionPedidoTests
{
    [Fact]
    public void EditarPendiente_ConservaIdentidadYRegistro_YSepaRemitenteDeDestinatario()
    {
        var zona = DomainFixtures.CrearZona(); var p = DomainFixtures.CrearPedido(zona);
        var id = p.Id; var registro = p.RegistradoEn;
        var detalles = InformacionPedido.Crear("Remitente", "0991111111", "origen@example.com", "destino@example.com",
            "No doblar", TipoCarga.Documento, 0.5m, true, PrioridadPedido.Alta);
        p.Actualizar("Destinatario", "0992222222", "Otra calle", new(0, 0), zona,
            DomainFixtures.Hoy.AddDays(1), new(14, 0), DomainFixtures.Hoy, detalles);
        Assert.Equal(id, p.Id); Assert.Equal(registro, p.RegistradoEn);
        Assert.Equal("Destinatario", p.ClienteNombre); Assert.Equal("Remitente", p.Detalles!.RemitenteNombre);
        Assert.Null(p.RutaId); Assert.Equal(EstadoPedido.Pendiente, p.Estado);
    }

    [Fact]
    public void EditarAsignado_EsRechazadoSinAlterarDatos()
    {
        var zona = DomainFixtures.CrearZona(); var p = DomainFixtures.CrearPedido(zona); p.AsignarARuta(Guid.NewGuid());
        Assert.Throws<DomainException>(() => p.Actualizar("Nuevo", "09", "Otra", new(0, 0), zona,
            DomainFixtures.Hoy, new(10, 0), DomainFixtures.Hoy, null));
        Assert.Equal("Luis", p.ClienteNombre);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10001)]
    public void PesoInvalido_SeRechaza(decimal peso) => Assert.Throws<DomainException>(() =>
        InformacionPedido.Crear(null, null, null, null, null, null, peso, null, null));

    [Fact]
    public void RemitenteIncompletoOCorreoInvalido_SeRechaza()
    {
        Assert.Throws<DomainException>(() => InformacionPedido.Crear("Solo nombre", null, null, null, null, null, null, null, null));
        Assert.Throws<DomainException>(() => InformacionPedido.Crear(null, null, "correo-invalido", null, null, null, null, null, null));
    }
}
