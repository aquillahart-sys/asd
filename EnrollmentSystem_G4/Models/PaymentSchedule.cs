using System;

namespace EnrollmentSystem_G4.Models
{
    public class PaymentSchedule
    {
        public int ScheduleId { get; set; }
        public int EnrollmentId { get; set; }
        public string InstallmentName { get; set; } = string.Empty; // e.g., "Downpayment"
        public decimal AmountDue { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Unpaid";

        // Domain Method: Check if the installment is overdue
        public bool IsOverdue()
        {
            return Status != "Paid" && DateTime.Now.Date > DueDate.Date;
        }
    }
}