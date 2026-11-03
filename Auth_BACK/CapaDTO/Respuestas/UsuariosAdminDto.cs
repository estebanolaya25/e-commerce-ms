namespace CapaDto.Respuestas
{
    public class UsuarioResumenDto
    {
        public Guid UserId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public List<string> Roles { get; set; } = new();
        public DateTime? BloqueadoHasta { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UltimoAcceso { get; set; }
    }

    public class UsuarioDetalleDto : UsuarioResumenDto
    {
        public int IntentosFallidos { get; set; }
        public List<EventoAuditoriaDto> Actividad { get; set; } = new();
    }

    public class EventoAuditoriaDto
    {
        public string Tipo { get; set; } = string.Empty;
        public bool Exitoso { get; set; }
        public string? Ip { get; set; }
        public string? Detalle { get; set; }
        public DateTime Fecha { get; set; }
    }

    public class RolDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Usuarios { get; set; }
    }

    public class PaginaDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Pagina { get; set; }
        public int Tamano { get; set; }
        public int Total { get; set; }
        public int TotalPaginas => Tamano == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);
    }
}
