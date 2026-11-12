namespace Application.Interfaces;

public interface IRevocadorSesiones
{
    void Revocar(Guid usuarioId);
}
