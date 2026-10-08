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

        public bool CanConfirm(decimal totalPaid)
        {
            return TotalAssessment > 0 && totalPaid >= RequiredDownpayment;
        }

        public decimal RequiredDownpayment { get; set; }

        public List<PaymentSchedule> GenerateMonthlySchedules(
            int enrollmentId,
            decimal balance,
            int installmentCount,
            DateTime firstDueDate)
        {
            if (installmentCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(installmentCount));
            }

            var schedules = new List<PaymentSchedule>();
            decimal regularInstallment = Math.Round(balance / installmentCount, 2);
            decimal scheduledTotal = 0;

            for (int index = 0; index < installmentCount; index++)
            {
                decimal installment = index == installmentCount - 1
                    ? balance - scheduledTotal
                    : regularInstallment;
                scheduledTotal += installment;
                schedules.Add(new PaymentSchedule
                {
                    EnrollmentId = enrollmentId,
                    InstallmentName = $"Monthly Installment {index + 1}",
                    ExpectedAmount = installment,
                    DueDate = firstDueDate.AddMonths(index),
                    Status = "Unpaid"
                });
            }

            return schedules;
        }
    }
}