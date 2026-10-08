namespace EnrollmentSystem_G4.Models
{
    public class EnrollmentListItem
    {
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public int TotalUnits { get; set; }
        public decimal TotalAssessment { get; set; }
        public decimal RequiredDownpayment { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
