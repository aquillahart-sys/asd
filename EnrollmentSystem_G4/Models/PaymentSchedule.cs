using System;

namespace EnrollmentSystem_G4.Models
{
    public class PaymentSchedule
    {
        public int ScheduleId { get; set; }
        public int EnrollmentId { get; set; }
        public string InstallmentName { get; set; } = string.Empty;
        public decimal ExpectedAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Unpaid";

        public bool IsOverdue()
        {
            return Status != "Paid" && DateTime.Today > DueDate.Date;
        }
    }
}