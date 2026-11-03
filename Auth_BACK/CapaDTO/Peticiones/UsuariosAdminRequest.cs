using System.ComponentModel.DataAnnotations;

namespace CapaDto.Peticiones
{
    public class FiltroUsuariosRequest
    {
        [Range(1, 100000)]
        public int pagina { get; set; } = 1;

        [Range(1, 100)]
        public int tamano { get; set; } = 20;

        [MaxLength(100)]
        public string? buscar { get; set; }

        [MaxLength(50)]
        public string? rol { get; set; }

        public bool? activo { get; set; }
    }

    public class CrearUsuarioRequest
    {
        [Required, MaxLength(100)]
        public string nombre { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string apellido { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(254)]
        public string email { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(128)]
        public string password { get; set; } = string.Empty;

        [Required, MinLength(1, ErrorMessage = "Asigne al menos un rol.")]
        public List<string> roles { get; set; } = new();
    }

    public class CambiarRolesRequest
    {
        [Required, MinLength(1, ErrorMessage = "Asigne al menos un rol.")]
        public List<string> roles { get; set; } = new();
    }

    public class CambiarEstadoUsuarioRequest
    {
        [Required]
        public bool? activo { get; set; }
    }

    // Quién hace el cambio: queda en la auditoría del usuario afectado.
    public record ContextoAdministrador(Guid UserId, string Email, string? Ip, string? UserAgent);
}
