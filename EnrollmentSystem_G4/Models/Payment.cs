using System.ComponentModel.DataAnnotations;

namespace EnrollmentSystem_G4.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Select an existing enrollment.")]
        public int EnrollmentId { get; set; }
        public int? ScheduleId { get; set; }
        public int StudentId { get; set; }
        [Required(ErrorMessage = "Receipt/reference number is required.")]
        [StringLength(50, ErrorMessage = "Receipt/reference number cannot exceed 50 characters.")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "Enter a payment amount greater than zero and within the supported limit.")]
        public decimal AmountPaid { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;
        [Required(ErrorMessage = "Select a payment type.")]
        [StringLength(30)]
        public string FeeType { get; set; } = "Full Tuition Fee"; // e.g., Full Tuition Fee, Downpayment, Partial Tuition

        [Required(ErrorMessage = "Select a payment method.")]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Cash"; // Cash, GCash, Bank Transfer, Check
        public int? ProcessedBy { get; set; }

        // Student Info for Dashboard View
        public string StudentName { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;
        public string EnrollmentStatus { get; set; } = string.Empty;

        // Transferred Financial Context Properties
        public decimal TotalAssessment { get; set; }
        public decimal TotalPayment { get; set; }

        // Domain Expression Property: Dynamic Remaining Balance Calculation
        public decimal RemainingBalance => GetRemainingBalance();

        // Domain Method: Dynamic Balance Calculation
        public decimal GetRemainingBalance()
        {
            return TotalAssessment - TotalPayment;
        }

        // Domain Method: Validate if payment amount is valid against current balance
        public bool IsValidPayment()
        {
            return AmountPaid > 0 && AmountPaid <= RemainingBalance;
        }
    }
}