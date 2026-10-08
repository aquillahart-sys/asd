using System.ComponentModel.DataAnnotations;

namespace EnrollmentSystem_G4.Models
{
    public class SubjectOffering
    {
        public int OfferingId { get; set; }

        [Range(1, int.MaxValue)]
        public int SubjectId { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression("^\\d{4}-\\d{4}$")]
        public string AcademicYear { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [RegularExpression("^(1st Semester|2nd Semester|Summer)$")]
        public string Semester { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string Section { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Schedule { get; set; }

        [StringLength(50)]
        public string? Room { get; set; }

        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectDescription { get; set; } = string.Empty;
        public int Units { get; set; }
    }
}
