using CapaNegocios.Interfaz.auth.interfaz;
using System.Security.Cryptography;

namespace CapaNegocios.Implementacion.auth.Implementacion
{
    public class clsPasswordHasherPbkdf2 : IPasswordHasher
    {
        private const string Algoritmo = "PBKDF2-SHA256";
        private const int Iteraciones = 600000;
        private const int TamanoSalt = 16;
        private const int TamanoHash = 32;

        private static readonly string HashFicticio =
            $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(new byte[TamanoSalt])}${Convert.ToBase64String(new byte[TamanoHash])}";

        public string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(TamanoSalt);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
            return $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public bool Verificar(string password, string? passwordHash)
        {
            bool hashReal = !string.IsNullOrEmpty(passwordHash);
            string[] partes = (hashReal ? passwordHash! : HashFicticio).Split('$');

            if (partes.Length != 4 || partes[0] != Algoritmo || !int.TryParse(partes[1], out int iteraciones) || iteraciones <= 0)
                return false;

            byte[] salt;
            byte[] esperado;
            try
            {
                salt = Convert.FromBase64String(partes[2]);
                esperado = Convert.FromBase64String(partes[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(password, salt, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return hashReal && CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
    }
}
