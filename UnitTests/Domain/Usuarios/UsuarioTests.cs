using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.ExternalServices;

namespace UnitTests.Domain;

public sealed class UsuarioTests
{
    [Theory]
    [InlineData(RolUsuario.Conductor, false)]
    [InlineData(RolUsuario.Administrador, true)]
    public void Crear_RechazaVinculoIncompatibleConRol(RolUsuario rol, bool tieneRepartidor) =>
        Assert.Throws<DomainException>(() => Usuario.Crear("usuario", "hash", rol, tieneRepartidor ? Guid.NewGuid() : null));

    [Fact]
    public void CambioPropio_LevantaObligacionYRenuevaSello()
    {
        var u = Usuario.Crear("CHOFER", "hash-temporal", RolUsuario.Conductor, Guid.NewGuid());
        var sello = u.SelloSeguridad;
        Assert.True(u.DebeCambiarCredenciales);
        u.CambiarCredenciales("  Chofer.Nuevo  ", "hash-nuevo");
        Assert.Equal("CHOFER.NUEVO", u.NombreNormalizado);
        Assert.False(u.DebeCambiarCredenciales); Assert.NotEqual(sello, u.SelloSeguridad);
        sello = u.SelloSeguridad; u.RestablecerClave("otra-temporal");
        Assert.True(u.DebeCambiarCredenciales); Assert.NotEqual(sello, u.SelloSeguridad);
    }

    [Fact]
    public void CincoFallos_BloqueanQuinceMinutos_YRestablecerDesbloquea()
    {
        var u = Usuario.Crear("chofer", "hash", RolUsuario.Conductor, Guid.NewGuid());
        var ahora = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++) u.RegistrarFallo(ahora);
        Assert.Equal(ahora.AddMinutes(15), u.BloqueadoHasta);
        u.RestablecerClave("nuevo"); Assert.Null(u.BloqueadoHasta);
    }

    [Fact]
    public void Hashes_UsanSalesDistintas_YVerificanSoloLaClaveCorrecta()
    {
        var p = new ProtectorClaves(); var hash = p.Proteger("Clave-Larga2026!Pruebas");
        Assert.NotEqual(hash, p.Proteger("Clave-Larga2026!Pruebas"));
        Assert.True(p.Verificar(hash, "Clave-Larga2026!Pruebas"));
        Assert.False(p.Verificar(hash, "Clave-Incorrecta2026!")); Assert.False(p.Verificar("invalido", "clave"));
    }
}
