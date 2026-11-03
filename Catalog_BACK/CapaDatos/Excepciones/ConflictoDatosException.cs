namespace CapaDatos.Excepciones
{
    public class ConflictoDatosException : Exception
    {
        public string? Restriccion { get; }

        public ConflictoDatosException(string? restriccion, Exception inner)
            : base($"Violación de la restricción única {restriccion}.", inner)
        {
            Restriccion = restriccion;
        }
    }
}
