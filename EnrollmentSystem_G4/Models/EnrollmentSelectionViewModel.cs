using System.ComponentModel.DataAnnotations;

namespace EnrollmentSystem_G4.Models
{
    public class EnrollmentSelectionViewModel
    {
        [Range(1, int.MaxValue)]
        public int StudentId { get; set; }
        public Student Student { get; set; } = new();

        [Required]
        [StringLength(20)]
        [RegularExpression("^\\d{4}-\\d{4}$", ErrorMessage = "Enter an academic year in YYYY-YYYY format.")]
        public string AcademicYear { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(1st Semester|2nd Semester|Summer)$")]
        public string Semester { get; set; } = string.Empty;

        public List<int> SelectedOfferingIds { get; set; } = new();
        public List<SubjectOffering> AvailableOfferings { get; set; } = new();
    }
}
