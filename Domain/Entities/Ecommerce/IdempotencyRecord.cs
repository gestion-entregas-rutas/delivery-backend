using Domain.Common;

namespace Domain.Entities;

public sealed class IdempotencyRecord : Entity
{
    private IdempotencyRecord() { }
    public string Key { get; private set; } = null!;
    public Guid? UsuarioId { get; private set; }
    public string Operacion { get; private set; } = null!;
    public Guid? OrdenId { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public string? ResultadoJson { get; private set; }

    public static IdempotencyRecord Crear(string key, Guid? usuarioId, string operacion, Guid? ordenId, DateTimeOffset ahora, string? resultadoJson) =>
        new() { Key = key, UsuarioId = usuarioId, Operacion = operacion, OrdenId = ordenId, CreadoEn = ahora, ResultadoJson = resultadoJson };
}
