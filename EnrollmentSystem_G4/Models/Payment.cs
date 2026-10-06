using System;

namespace EnrollmentSystem_G4.Models
{
    public class Payment
    {
        private decimal _amountPaid;

        public int PaymentId { get; set; }
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;

        public decimal AmountPaid
        {
            get => _amountPaid;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Payment amount must be greater than zero.");
                }
                _amountPaid = value;
            }
        }

        public DateTime PaymentDate { get; set; } = DateTime.Now;
        public string FeeType { get; set; } = "Full Tuition Fee"; // e.g., Full Tuition Fee, Downpayment, Partial Tuition
        public string PaymentMethod { get; set; } = "Cash"; // Cash, GCash, Bank Transfer, Check

        // Student Info for Dashboard View
        public string StudentName { get; set; } = string.Empty;
        public string StudentNumber { get; set; } = string.Empty;

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