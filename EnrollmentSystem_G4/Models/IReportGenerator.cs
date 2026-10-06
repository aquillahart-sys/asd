using System.Collections.Generic;

namespace EnrollmentSystem_G4.Models
{
    // Interface contract requiring a string output for reports
    public interface IReportGenerator
    {
        string Generate();
    }

    // Certificate of Registration Report
    public class CorReportGenerator : IReportGenerator
    {
        public Student Student { get; set; }
        public Enrollment Enrollment { get; set; }

        public CorReportGenerator(Student student, Enrollment enrollment)
        {
            Student = student;
            Enrollment = enrollment;
        }

        public string Generate()
        {
            return $"CERTIFICATE OF REGISTRATION\n" +
                   $"Student: {Student.GetFullName()} ({Student.StudentNumber})\n" +
                   $"Program: {Student.AssignedProgram?.ProgramCode ?? "BSIT"}\n" +
                   $"Academic Year: {Enrollment.AcademicYear} ({Enrollment.Semester})\n" +
                   $"Total Units: {Enrollment.TotalUnits}\n" +
                   $"Total Assessment: ₱{Enrollment.TotalAssessment:N2}\n" +
                   $"Status: {Enrollment.Status}";
        }
    }

    // Outstanding Balance Statement Report
    public class BalanceReportGenerator : IReportGenerator
    {
        public Student Student { get; set; }
        public decimal TotalAssessment { get; set; }
        public decimal TotalPayment { get; set; }

        public BalanceReportGenerator(Student student, decimal totalAssessment, decimal totalPayment)
        {
            Student = student;
            TotalAssessment = totalAssessment;
            TotalPayment = totalPayment;
        }

        public string Generate()
        {
            decimal balance = TotalAssessment - TotalPayment;
            return $"STATEMENT OF ACCOUNT\n" +
                   $"Student: {Student.GetFullName()} ({Student.StudentNumber})\n" +
                   $"Total Fees Assessed: ₱{TotalAssessment:N2}\n" +
                   $"Total Paid: ₱{TotalPayment:N2}\n" +
                   $"Remaining Balance: ₱{balance:N2}";
        }
    }

    // Payment Audit Trail Report
    public class PaymentHistoryReportGenerator : IReportGenerator
    {
        public Student Student { get; set; }
        public List<Payment> Payments { get; set; }

        public PaymentHistoryReportGenerator(Student student, List<Payment> payments)
        {
            Student = student;
            Payments = payments ?? new List<Payment>();
        }

        public string Generate()
        {
            string summary = $"PAYMENT AUDIT TRAIL\nStudent: {Student.GetFullName()} ({Student.StudentNumber})\nTotal Official Receipts Issued: {Payments.Count}\n";
            foreach (var p in Payments)
            {
                summary += $"• OR #{p.ReceiptNumber} | ₱{p.AmountPaid:N2} | {p.PaymentMethod} | {p.PaymentDate:yyyy-MM-dd}\n";
            }
            return summary;
        }
    }
}