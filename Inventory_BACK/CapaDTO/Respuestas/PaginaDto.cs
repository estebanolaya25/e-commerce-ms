namespace CapaDto.Respuestas
{
    public class PaginaDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Pagina { get; set; }
        public int Tamano { get; set; }
        public int Total { get; set; }
        public int TotalPaginas => Tamano == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);
    }
}
