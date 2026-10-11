using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EnrollmentSystem_G4.Models
{
    public class Student : Person
    {
        public int StudentId { get; set; }

        [Required(ErrorMessage = "Student number is required.")]
        [StringLength(30, ErrorMessage = "Student number cannot exceed 30 characters.")]
        [Display(Name = "Student Number")]
        public string StudentNumber { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "Select a valid academic program.")]
        public int ProgramId { get; set; }

        [Required(ErrorMessage = "Year level is required.")]
        [StringLength(20)]
        [RegularExpression("^(1st Year|2nd Year|3rd Year|4th Year)$", ErrorMessage = "Select a valid year level.")]
        public string YearLevel { get; set; } = "1st Year";
        public string FullName => $"{FirstName} {LastName}";
        // Domain Associations
        public Program? AssignedProgram { get; set; }
        public List<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}