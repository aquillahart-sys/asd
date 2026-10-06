namespace EnrollmentSystem_G4.Models
{
    public class Subject
    {
        public int SubjectId { get; set; }
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectDescription { get; set; } = string.Empty;
        public int Units { get; set; }

        // Domain Method: Calculate fee for this specific subject
        public decimal CalculateSubjectFee(decimal ratePerUnit = 500.00m)
        {
            return Units * ratePerUnit;
        }
    }
}