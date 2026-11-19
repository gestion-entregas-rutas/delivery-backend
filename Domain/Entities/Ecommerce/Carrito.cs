using Domain.Common;

namespace Domain.Entities;

public sealed class Carrito : Entity
{
    private readonly List<CarritoItem> _items = [];
    private Carrito() { }
    public Guid? UsuarioId { get; private set; }
    public string? TokenInvitado { get; private set; }
    public DateTimeOffset ActualizadoEn { get; private set; }
    public IReadOnlyCollection<CarritoItem> Items => _items;

    public static Carrito CrearUsuario(Guid usuarioId, DateTimeOffset ahora) => new() { UsuarioId = usuarioId, ActualizadoEn = ahora };
    public static Carrito CrearInvitado(string token, DateTimeOffset ahora) => new() { TokenInvitado = token, ActualizadoEn = ahora };

    public void Tocar(DateTimeOffset ahora) => ActualizadoEn = ahora;
    public void AsignarUsuario(Guid usuarioId) { UsuarioId = usuarioId; TokenInvitado = null; }
}
