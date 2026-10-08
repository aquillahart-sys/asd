using System.ComponentModel.DataAnnotations;

namespace EnrollmentSystem_G4.Models
{
    public class EditUserViewModel
    {
        public int UserId { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Administrator|Registrar|Cashier)$")]
        public string Role { get; set; } = string.Empty;
    }
}
