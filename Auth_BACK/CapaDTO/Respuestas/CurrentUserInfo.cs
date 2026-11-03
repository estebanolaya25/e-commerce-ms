namespace CapaDto.Respuestas
{
    public class CurrentUserInfo
    {
        public Guid IdUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailVerificado { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
