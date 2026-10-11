using System.ComponentModel.DataAnnotations;

namespace EnrollmentSystem_G4.Models
{
    public class Subject
    {
        public int SubjectId { get; set; }
        [Required(ErrorMessage = "Subject code is required.")]
        [StringLength(30, ErrorMessage = "Subject code cannot exceed 30 characters.")]
        public string SubjectCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Subject description is required.")]
        [StringLength(150, ErrorMessage = "Subject description cannot exceed 150 characters.")]
        public string SubjectDescription { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Units must be greater than zero.")]
        public int Units { get; set; }
        public string Section { get; set; } = string.Empty;
        public string? Schedule { get; set; }

        // Domain Method: Calculate fee for this specific subject
        public decimal CalculateSubjectFee(decimal ratePerUnit = 500.00m)
        {
            return Units * ratePerUnit;
        }
    }
}