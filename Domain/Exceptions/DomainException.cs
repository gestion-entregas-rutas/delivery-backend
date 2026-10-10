namespace Domain.Exceptions;

/// <summary>Se lanza cuando una operación viola una regla de negocio.</summary>
public class DomainException(string message) : Exception(message);
