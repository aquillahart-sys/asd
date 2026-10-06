using System.Collections.Generic;

namespace EnrollmentSystem_G4.Models
{
    public class Student : Person
    {
        public int StudentId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public int ProgramId { get; set; }
        public string YearLevel { get; set; } = "1st Year";
        public string FullName => $"{FirstName} {LastName}";
        // Domain Associations
        public Program? AssignedProgram { get; set; }
        public List<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}