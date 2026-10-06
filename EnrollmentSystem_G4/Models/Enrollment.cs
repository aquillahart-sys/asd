using System;
using System.Collections.Generic;
using System.Linq;

namespace EnrollmentSystem_G4.Models
{
    public class Enrollment
    {
        public decimal RemainingBalance => GetRemainingBalance();

        // Calculated Property for Total Units
        public int TotalUnits => GetTotalUnits();

        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public string AcademicYear { get; set; } = "2026-2027";
        public string Semester { get; set; } = "1st Semester";
        public decimal TotalAssessment { get; set; }

        // Renamed property
        public decimal TotalPayment { get; set; }

        public string Status { get; set; } = "Pending";
        public DateTime EnrollmentDate { get; set; } = DateTime.Now;

        // Association: One Enrollment contains multiple Subjects (1-to-Many)
        public List<Subject> EnrolledSubjects { get; set; } = new List<Subject>();

        // Association: One Enrollment contains multiple Payment receipts (1-to-Many)
        public List<Payment> PaymentHistory { get; set; } = new List<Payment>();

        // Association: One Enrollment has multiple scheduled installments (1-to-Many)
        public List<PaymentSchedule> PaymentSchedules { get; set; } = new List<PaymentSchedule>();

        // Domain Method: Calculate Total Units
        public int GetTotalUnits()
        {
            return EnrolledSubjects.Sum(s => s.Units);
        }

        // Domain Method: Dynamically calculate Total Fee Assessment
        public decimal CalculateTotalAssessment(decimal ratePerUnit = 500.00m)
        {
            return EnrolledSubjects.Sum(s => s.CalculateSubjectFee(ratePerUnit));
        }

        // Domain Method: Dynamic Balance Calculation
        public decimal GetRemainingBalance()
        {
            return TotalAssessment - TotalPayment;
        }

        // Domain Method: Update status based on payment thresholds
        public void EvaluateStatus()
        {
            if (GetRemainingBalance() <= 0 && TotalAssessment > 0)
            {
                Status = "Fully Paid";
            }
            else if (TotalPayment > 0)
            {
                Status = "Enrolled";
            }
            else
            {
                Status = "Pending";
            }
        }

        // Domain Method: Generate standard 3-part installment schedule
        public List<PaymentSchedule> GenerateDefaultSchedules(int enrollmentId)
        {
            decimal installmentAmount = TotalAssessment / 3m;

            return new List<PaymentSchedule>
            {
                new PaymentSchedule
                {
                    EnrollmentId = enrollmentId,
                    InstallmentName = "Downpayment",
                    AmountDue = Math.Round(installmentAmount, 2),
                    DueDate = DateTime.Now.AddDays(7),
                    Status = "Unpaid"
                },
                new PaymentSchedule
                {
                    EnrollmentId = enrollmentId,
                    InstallmentName = "Midterm Installment",
                    AmountDue = Math.Round(installmentAmount, 2),
                    DueDate = DateTime.Now.AddDays(45),
                    Status = "Unpaid"
                },
                new PaymentSchedule
                {
                    EnrollmentId = enrollmentId,
                    InstallmentName = "Final Installment",
                    AmountDue = Math.Round(installmentAmount, 2),
                    DueDate = DateTime.Now.AddDays(90),
                    Status = "Unpaid"
                }
            };
        }
    }
}