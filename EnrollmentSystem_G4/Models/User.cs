namespace EnrollmentSystem_G4.Models
{
    public class User : Person
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // 'Admin', 'Registrar', 'Cashier'
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
