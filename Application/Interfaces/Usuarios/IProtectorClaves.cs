namespace Application.Interfaces;

public interface IProtectorClaves
{
    string Proteger(string clave);
    bool Verificar(string hash, string clave);
}
