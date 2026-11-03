namespace CapaDto.Respuestas
{
    public class RefreshTokenDto
    {
        public Guid RefreshTokenId { get; set; }
        public Guid UserId { get; set; }
        public Guid TokenFamilyId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public Guid? ReplacedByTokenId { get; set; }
    }
}
