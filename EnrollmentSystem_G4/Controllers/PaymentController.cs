using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;

using AcademicProgram = EnrollmentSystem_G4.Models.Program;

namespace EnrollmentSystem_G4.Controllers
{
    public class PaymentController : Controller
    {
        private readonly DatabaseHelper _db;

        public PaymentController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /Payment/Index
        public IActionResult Index()
        {
            var recentPayments = new List<Payment>();

            string paymentsQuery = @"SELECT p.payment_id, p.enrollment_id, p.receipt_number, p.amount_paid, 
                                           p.payment_date, p.payment_type, p.payment_method,
                                           s.first_name, s.last_name, s.student_number
                                    FROM payments p
                                    JOIN enrollments e ON p.enrollment_id = e.enrollment_id
                                    JOIN students s ON e.student_id = s.student_id
                                    ORDER BY p.payment_date DESC
                                    LIMIT 10";

            DataTable dtPayments = _db.ExecuteQuery(paymentsQuery);

            foreach (DataRow row in dtPayments.Rows)
            {
                recentPayments.Add(new Payment
                {
                    PaymentId = Convert.ToInt32(row["payment_id"]),
                    EnrollmentId = Convert.ToInt32(row["enrollment_id"]),
                    ReceiptNumber = row["receipt_number"].ToString() ?? string.Empty,
                    AmountPaid = Convert.ToDecimal(row["amount_paid"]),
                    PaymentDate = Convert.ToDateTime(row["payment_date"]),
                    FeeType = row.Table.Columns.Contains("payment_type") ? row["payment_type"].ToString() ?? "Full Tuition Fee" : "Full Tuition Fee",
                    PaymentMethod = row.Table.Columns.Contains("payment_method") ? row["payment_method"].ToString() ?? "Cash" : "Cash",
                    StudentName = $"{row["first_name"]} {row["last_name"]}",
                    StudentNumber = row["student_number"].ToString() ?? string.Empty
                });
            }

            string collectionsQuery = "SELECT IFNULL(SUM(amount_paid), 0) FROM payments";
            string issuedCountQuery = "SELECT COUNT(*) FROM payments";
            string outstandingQuery = @"SELECT IFNULL(SUM(e.total_assessment - IFNULL(p.total_paid, 0)), 0)
                                        FROM enrollments e
                                        LEFT JOIN (
                                            SELECT enrollment_id, SUM(amount_paid) AS total_paid 
                                            FROM payments 
                                            GROUP BY enrollment_id
                                        ) p ON e.enrollment_id = p.enrollment_id";

            ViewBag.TotalCollections = Convert.ToDecimal(_db.ExecuteScalar(collectionsQuery) ?? 0);
            ViewBag.IssuedReceiptsCount = Convert.ToInt32(_db.ExecuteScalar(issuedCountQuery) ?? 0);

            decimal outstanding = Convert.ToDecimal(_db.ExecuteScalar(outstandingQuery) ?? 0);
            ViewBag.OutstandingBalances = outstanding < 0 ? 0 : outstanding;

            return View(recentPayments);
        }

        // GET: /Payment/Create
        [HttpGet]
        public IActionResult Create()
        {
            string studentQuery = @"SELECT e.enrollment_id, e.total_assessment, 
                                   IFNULL(SUM(p.amount_paid), 0) AS total_paid,
                                   s.first_name, s.last_name, s.student_number,
                                   IFNULL(pr.program_code, 'BSIT') AS program_code
                            FROM enrollments e
                            JOIN students s ON e.student_id = s.student_id
                            LEFT JOIN programs pr ON s.program_id = pr.program_id
                            WHERE e.status != 'Fully Paid'
                            GROUP BY e.enrollment_id, e.total_assessment, 
                                     s.first_name, s.last_name, s.student_number, pr.program_code";

            DataTable dtStudents = _db.ExecuteQuery(studentQuery);
            var studentList = new List<dynamic>();

            foreach (DataRow row in dtStudents.Rows)
            {
                decimal assessment = Convert.ToDecimal(row["total_assessment"]);
                decimal paid = Convert.ToDecimal(row["total_paid"]);
                decimal balance = Math.Max(0, assessment - paid);
                string programCode = row["program_code"].ToString() ?? "BSIT";

                studentList.Add(new
                {
                    EnrollmentId = Convert.ToInt32(row["enrollment_id"]),
                    StudentName = $"{row["first_name"]} {row["last_name"]}",
                    StudentNumber = row["student_number"].ToString(),
                    ProgramCode = programCode,
                    TotalAssessment = assessment,
                    AmountPaid = paid,
                    Balance = balance,
                    DisplayName = $"{row["first_name"]} {row["last_name"]} ({row["student_number"]}) - {programCode} [Balance: ₱{balance:N0}]"
                });
            }

            ViewBag.Students = studentList;
            return View();
        }

        // POST: /Payment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(int enrollmentId, decimal amountPaid, string feeType, string paymentMethod)
        {
            if (enrollmentId <= 0 || amountPaid <= 0)
            {
                ModelState.AddModelError("", "Please select a valid student and enter a payment amount.");
                return Create();
            }

            string balanceQuery = @"SELECT total_assessment, IFNULL(SUM(p.amount_paid), 0) AS total_paid 
                                   FROM enrollments e 
                                   LEFT JOIN payments p ON e.enrollment_id = p.enrollment_id 
                                   WHERE e.enrollment_id = @Id 
                                   GROUP BY e.enrollment_id, e.total_assessment";

            DataTable dt = _db.ExecuteQuery(balanceQuery, new Dictionary<string, object> { { "@Id", enrollmentId } });

            if (dt.Rows.Count == 0)
            {
                return NotFound();
            }

            decimal totalAssessment = Convert.ToDecimal(dt.Rows[0]["total_assessment"]);
            decimal totalPayment = Convert.ToDecimal(dt.Rows[0]["total_paid"]);

            var payment = new Payment
            {
                EnrollmentId = enrollmentId,
                ReceiptNumber = "OR-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                AmountPaid = amountPaid,
                FeeType = string.IsNullOrEmpty(feeType) ? "Full Tuition Fee" : feeType,
                PaymentMethod = string.IsNullOrEmpty(paymentMethod) ? "Cash" : paymentMethod,
                TotalAssessment = totalAssessment,
                TotalPayment = totalPayment
            };

            if (!payment.IsValidPayment())
            {
                ModelState.AddModelError("AmountPaid", $"Payment must be between ₱1.00 and remaining balance (₱{payment.RemainingBalance:N2}).");
                return Create();
            }

            try
            {
                string insertQuery = @"INSERT INTO payments (enrollment_id, receipt_number, amount_paid, payment_type, payment_method)
                                       VALUES (@EnrollmentId, @ReceiptNumber, @AmountPaid, @PaymentType, @PaymentMethod)";

                var parameters = new Dictionary<string, object>
                {
                    { "@EnrollmentId", payment.EnrollmentId },
                    { "@ReceiptNumber", payment.ReceiptNumber },
                    { "@AmountPaid", payment.AmountPaid },
                    { "@PaymentType", payment.FeeType },
                    { "@PaymentMethod", payment.PaymentMethod }
                };

                _db.ExecuteNonQuery(insertQuery, parameters);

                var enrollment = new Enrollment
                {
                    TotalAssessment = payment.TotalAssessment,
                    TotalPayment = payment.TotalPayment + payment.AmountPaid
                };
                enrollment.EvaluateStatus();

                string updateStatusQuery = "UPDATE enrollments SET status = @Status WHERE enrollment_id = @Id";
                _db.ExecuteNonQuery(updateStatusQuery, new Dictionary<string, object>
                {
                    { "@Status", enrollment.Status },
                    { "@Id", payment.EnrollmentId }
                });

                TempData["SuccessMessage"] = $"Payment of ₱{payment.AmountPaid:N2} recorded via {payment.PaymentMethod}!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Payment processing error: {ex.Message}");
                return Create();
            }
        }

        // GET: /Payment/GenerateReport
        public IActionResult GenerateReport(string type, int enrollmentId)
        {
            string query = @"SELECT e.enrollment_id, e.academic_year, e.semester, e.total_assessment, e.status,
                                    s.student_id, s.student_number, s.first_name, s.last_name, s.email,
                                    IFNULL(p.program_code, 'BSIT') AS program_code
                             FROM enrollments e
                             JOIN students s ON e.student_id = s.student_id
                             LEFT JOIN programs p ON s.program_id = p.program_id
                             WHERE e.enrollment_id = @Id";

            DataTable dt = _db.ExecuteQuery(query, new Dictionary<string, object> { { "@Id", enrollmentId } });

            if (dt.Rows.Count == 0) return NotFound();

            DataRow row = dt.Rows[0];
            var student = new Student
            {
                StudentId = Convert.ToInt32(row["student_id"]),
                StudentNumber = row["student_number"].ToString() ?? "",
                FirstName = row["first_name"].ToString() ?? "",
                LastName = row["last_name"].ToString() ?? "",
                AssignedProgram = new AcademicProgram { ProgramCode = row["program_code"].ToString() ?? "BSIT" }
            };

            var enrollment = new Enrollment
            {
                EnrollmentId = Convert.ToInt32(row["enrollment_id"]),
                AcademicYear = row["academic_year"].ToString() ?? "2026-2027",
                Semester = row["semester"].ToString() ?? "1st Semester",
                TotalAssessment = Convert.ToDecimal(row["total_assessment"]),
                Status = row["status"].ToString() ?? "Pending"
            };

            IReportGenerator reportGenerator;

            if (type == "Balance")
            {
                string paidQuery = "SELECT IFNULL(SUM(amount_paid), 0) FROM payments WHERE enrollment_id = @Id";
                decimal totalPaid = Convert.ToDecimal(_db.ExecuteScalar(paidQuery, new Dictionary<string, object> { { "@Id", enrollmentId } }) ?? 0);

                reportGenerator = new BalanceReportGenerator(student, enrollment.TotalAssessment, totalPaid);
            }
            else if (type == "History")
            {
                string historyQuery = "SELECT receipt_number, amount_paid, payment_method, payment_date FROM payments WHERE enrollment_id = @Id";
                DataTable dtPayments = _db.ExecuteQuery(historyQuery, new Dictionary<string, object> { { "@Id", enrollmentId } });

                var payments = new List<Payment>();
                foreach (DataRow pRow in dtPayments.Rows)
                {
                    payments.Add(new Payment
                    {
                        ReceiptNumber = pRow["receipt_number"].ToString() ?? "",
                        AmountPaid = Convert.ToDecimal(pRow["amount_paid"]),
                        PaymentMethod = pRow["payment_method"].ToString() ?? "Cash",
                        PaymentDate = Convert.ToDateTime(pRow["payment_date"])
                    });
                }

                reportGenerator = new PaymentHistoryReportGenerator(student, payments);
            }
            else
            {
                reportGenerator = new CorReportGenerator(student, enrollment);
            }

            ViewBag.ReportText = reportGenerator.Generate();
            ViewBag.ReportType = type == "Balance" ? "Statement of Account" : (type == "History" ? "Payment History Trail" : "Certificate of Registration");

            return View("ReportPreview");
        }
    }
}