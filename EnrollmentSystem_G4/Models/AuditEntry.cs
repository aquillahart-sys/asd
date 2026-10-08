namespace EnrollmentSystem_G4.Models
{
    public class AuditEntry
    {
        public long AuditId { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string? EntityType { get; set; }
        public string? EntityId { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
