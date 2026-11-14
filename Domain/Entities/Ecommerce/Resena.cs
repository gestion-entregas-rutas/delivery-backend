using Domain.Common;
using Domain.Exceptions;

namespace Domain.Entities;

public sealed class Resena : Entity
{
    private Resena() { }
    public Guid ProductoId { get; private set; }
    public Guid ClienteId { get; private set; }
    public int Calificacion { get; private set; }
    public string? Comentario { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }

    public static Resena Crear(Guid productoId, Guid clienteId, int calificacion, string? comentario, DateTimeOffset ahora)
    {
        if (productoId == Guid.Empty || clienteId == Guid.Empty) throw new DomainException("La reseña requiere producto y cliente.");
        if (calificacion is < 1 or > 5) throw new DomainException("La calificación debe estar entre 1 y 5.");
        return new Resena
        {
            ProductoId = productoId,
            ClienteId = clienteId,
            Calificacion = calificacion,
            Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(),
            CreadaEn = ahora
        };
    }
}
