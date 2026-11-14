namespace Application.Exceptions;

/// <summary>Conflicto de estado/concurrencia; puede incluir un código de negocio.</summary>
public class ConflictoException : Exception
{
    public string? Codigo { get; }
    public IReadOnlyDictionary<string, object?>? Datos { get; }

    public ConflictoException(string mensaje, string? codigo = null, IReadOnlyDictionary<string, object?>? datos = null) : base(mensaje)
    {
        Codigo = codigo;
        Datos = datos;
    }
}
