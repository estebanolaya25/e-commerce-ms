namespace CapaDto.Externos
{
    // Respuesta de GET /api/inventario/disponibilidad (microservicio Inventario).
    public class DisponibilidadInventarioDto
    {
        public string Sku { get; set; } = string.Empty;
        public bool Existe { get; set; }
        public int Disponible { get; set; }
        public bool Agotado { get; set; }
        public bool PocasUnidades { get; set; }
    }
}
