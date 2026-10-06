using System.Collections.Generic;
namespace EnrollmentSystem_G4.Models
{
    public class Program
    {
        public int ProgramId { get; set; }
        public string ProgramCode { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;

        public List<Student> EnrolledStudents { get; set; } = new List<Student>();

        public string GetFullProgramDisplay()
        {
            return $"{ProgramCode} - {ProgramName}";
        }
    }
}