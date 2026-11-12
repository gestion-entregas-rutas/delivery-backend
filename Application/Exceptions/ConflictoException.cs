namespace Application.Exceptions;

/// <summary>Otra operación modificó el mismo registro al mismo tiempo; el cliente puede reintentar.</summary>
public class ConflictoException(string mensaje) : Exception(mensaje);
