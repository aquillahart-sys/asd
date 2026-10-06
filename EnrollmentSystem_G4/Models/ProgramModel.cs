using System.ComponentModel.DataAnnotations;

namespace EnrollmentSystem_G4.Models
{
    public class ProgramModel
    {
        public int ProgramId { get; set; }

        [Required(ErrorMessage = "Program Code is required.")]
        [StringLength(20, ErrorMessage = "Program Code cannot exceed 20 characters.")]
        [Display(Name = "Program Code")]
        public string ProgramCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Program Name is required.")]
        [StringLength(100, ErrorMessage = "Program Name cannot exceed 100 characters.")]
        [Display(Name = "Program Name")]
        public string ProgramName { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
        public string? Description { get; set; }

        public string Status { get; set; } = "Active";
    }
}