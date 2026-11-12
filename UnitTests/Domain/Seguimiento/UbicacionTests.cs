using Domain.Entities;
using Domain.Exceptions;
namespace UnitTests.Domain;

public sealed class UbicacionTests
{
    [Fact]
    public void MuestraAnteriorORepetida_NoReemplazaLaMasReciente()
    {
        var u = UltimaUbicacionRepartidor.Crear(Guid.NewGuid()); var ahora = DateTimeOffset.UtcNow;
        var ruta = Guid.NewGuid();
        Assert.True(u.Actualizar(ruta, new(0, 0), 10, ahora, ahora));
        Assert.False(u.Actualizar(ruta, new(1, 1), 20, ahora.AddSeconds(-10), ahora));
        Assert.False(u.Actualizar(ruta, new(2, 2), 20, ahora, ahora));
        Assert.Equal(0, u.Punto.Latitud); Assert.Equal(10, u.PrecisionMetros);
    }

    [Theory]
    [InlineData(-6)]
    [InlineData(2)]
    public void MuestraCaducadaOFutura_EsRechazada(int minutos)
    {
        var u = UltimaUbicacionRepartidor.Crear(Guid.NewGuid()); var ahora = DateTimeOffset.UtcNow;
        Assert.Throws<DomainException>(() => u.Actualizar(Guid.NewGuid(), new(0, 0), 10, ahora.AddMinutes(minutos), ahora));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(double.NaN)]
    public void PrecisionInvalida_EsRechazada(double precision)
    {
        var ahora = DateTimeOffset.UtcNow;
        Assert.Throws<DomainException>(() => UltimaUbicacionRepartidor.Crear(Guid.NewGuid())
            .Actualizar(Guid.NewGuid(), new(0, 0), precision, ahora, ahora));
    }
}
