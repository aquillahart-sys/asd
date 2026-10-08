namespace EnrollmentSystem_G4.Models
{
    public class EnrollmentAssessmentViewModel
    {
        public Enrollment Enrollment { get; set; } = new();
        public Student Student { get; set; } = new();
        public List<SubjectOffering> Offerings { get; set; } = new();
        public int TotalUnits { get; set; }
        public decimal RatePerUnit { get; set; }
        public decimal TuitionFee { get; set; }
        public decimal MiscellaneousFee { get; set; }
        public decimal OtherFees { get; set; }
        public decimal TotalAssessment { get; set; }
        public decimal RequiredDownpayment { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public bool CanConfirm { get; set; }
    }
}
