using System.ComponentModel.DataAnnotations;

namespace EnrollmentSystem_G4.Models
{
    public class LoginViewModel
    {
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(128, ErrorMessage = "Password cannot exceed 128 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
