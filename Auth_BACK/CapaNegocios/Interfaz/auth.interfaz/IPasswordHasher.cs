namespace CapaNegocios.Interfaz.auth.interfaz
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verificar(string password, string? passwordHash);
    }
}
