using System.Data;
using System.Security.Claims;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

using AcademicProgram = EnrollmentSystem_G4.Models.Program;

namespace EnrollmentSystem_G4.Controllers
{
    [Authorize(Roles = "Administrator,Registrar,Cashier")]
    public class PaymentController : Controller
    {
        private static readonly HashSet<string> PaymentTypes =
            new(StringComparer.Ordinal) { "Downpayment", "Monthly Payment" };
        private static readonly HashSet<string> PaymentMethods =
            new(StringComparer.Ordinal) { "Cash", "GCash", "Bank Transfer", "Check" };

        private readonly DatabaseHelper _db;
        private readonly AuditLogger _audit;

        public PaymentController(DatabaseHelper db, AuditLogger audit)
        {
            _db = db;
            _audit = audit;
        }

        public IActionResult Index()
        {
            DataTable payments = _db.ExecuteQuery(
                @"SELECT p.payment_id, p.enrollment_id, p.receipt_number, p.amount_paid AS amount,
                         p.payment_date, p.payment_type, p.payment_method,
                         s.first_name, s.last_name, s.student_number, e.status
                  FROM payments p
                  JOIN enrollments e ON e.enrollment_id = p.enrollment_id
                  JOIN students s ON s.student_id = e.student_id
                  ORDER BY p.payment_date DESC, p.payment_id DESC
                  LIMIT 50");
            var recentPayments = new List<Payment>();
            foreach (DataRow row in payments.Rows)
            {
                recentPayments.Add(new Payment
                {
                    PaymentId = Convert.ToInt32(row["payment_id"]),
                    EnrollmentId = Convert.ToInt32(row["enrollment_id"]),
                    ReceiptNumber = Convert.ToString(row["receipt_number"]) ?? string.Empty,
                    AmountPaid = Convert.ToDecimal(row["amount"]),
                    PaymentDate = Convert.ToDateTime(row["payment_date"]),
                    FeeType = Convert.ToString(row["payment_type"]) ?? string.Empty,
                    PaymentMethod = Convert.ToString(row["payment_method"]) ?? string.Empty,
                    StudentName = $"{row["first_name"]} {row["last_name"]}",
                    StudentNumber = Convert.ToString(row["student_number"]) ?? string.Empty,
                    EnrollmentStatus = NormalizeStatus(Convert.ToString(row["status"]) ?? string.Empty)
                });
            }

            ViewBag.TotalCollections = Convert.ToDecimal(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(amount_paid), 0) FROM payments") ?? 0);
            ViewBag.IssuedReceiptsCount = Convert.ToInt32(_db.ExecuteScalar(
                "SELECT COUNT(*) FROM payments") ?? 0);
            ViewBag.OutstandingBalances = Convert.ToDecimal(_db.ExecuteScalar(
                @"SELECT COALESCE(SUM(GREATEST(e.total_assessment - COALESCE(p.total_paid, 0), 0)), 0)
                  FROM enrollments e
                  LEFT JOIN (
                      SELECT enrollment_id, SUM(amount_paid) AS total_paid
                      FROM payments
                      GROUP BY enrollment_id
                  ) p ON p.enrollment_id = e.enrollment_id
                  WHERE UPPER(e.status) IN ('ASSESSED', 'CONFIRMED')") ?? 0);
            ViewBag.OverdueInstallments = Convert.ToInt32(_db.ExecuteScalar(
                @"SELECT COUNT(*)
                  FROM payment_schedules
                  WHERE due_date < CURRENT_DATE
                    AND amount_paid < expected_amount") ?? 0);
            return View(recentPayments);
        }

        [HttpGet]
        [Authorize(Roles = "Administrator,Cashier")]
        public IActionResult Create(int? enrollmentId = null)
        {
            var payment = new Payment
            {
                EnrollmentId = enrollmentId ?? 0,
                PaymentDate = DateTime.Today,
                FeeType = enrollmentId.HasValue ? "Monthly Payment" : "Downpayment"
            };
            LoadPaymentOptions(payment.EnrollmentId);
            return View(payment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Cashier")]
        public IActionResult Create(Payment payment)
        {
            payment.ReceiptNumber = payment.ReceiptNumber?.Trim() ?? string.Empty;
            payment.FeeType ??= string.Empty;
            payment.PaymentMethod ??= string.Empty;
            if (payment.FeeType == null || !PaymentTypes.Contains(payment.FeeType))
            {
                ModelState.AddModelError(nameof(payment.FeeType), "Select a valid payment type.");
            }

            if (payment.PaymentMethod == null || !PaymentMethods.Contains(payment.PaymentMethod))
            {
                ModelState.AddModelError(nameof(payment.PaymentMethod), "Select a valid payment method.");
            }

            if (string.IsNullOrWhiteSpace(payment.ReceiptNumber) || payment.ReceiptNumber.Length > 50)
            {
                ModelState.AddModelError(nameof(payment.ReceiptNumber), "Enter a receipt/reference number up to 50 characters.");
            }

            if (payment.PaymentDate == default || payment.PaymentDate.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(payment.PaymentDate), "Enter a valid payment date that is not in the future.");
            }

            if (!ModelState.IsValid)
            {
                LoadPaymentOptions(payment.EnrollmentId);
                return View(payment);
            }

            int actorId = GetCurrentUserId();
            try
            {
                (int PaymentId, decimal RemainingBalance) result = _db.ExecuteInTransaction((connection, transaction) =>
                {
                    DataTable enrollments = _db.ExecuteQuery(
                        connection,
                        transaction,
                        @"SELECT total_assessment, required_downpayment, status
                          FROM enrollments
                          WHERE enrollment_id = @Id
                          FOR UPDATE",
                        new Dictionary<string, object> { { "@Id", payment.EnrollmentId } });
                    if (enrollments.Rows.Count == 0)
                    {
                        throw new PaymentValidationException("Select an existing enrollment.");
                    }

                    DataRow enrollmentRow = enrollments.Rows[0];
                    string status = NormalizeStatus(Convert.ToString(enrollmentRow["status"]) ?? string.Empty);
                    decimal assessment = Convert.ToDecimal(enrollmentRow["total_assessment"]);
                    decimal requiredDownpayment = Convert.ToDecimal(enrollmentRow["required_downpayment"]);
                    decimal totalPaid = Convert.ToDecimal(_db.ExecuteScalar(
                        connection,
                        transaction,
                        "SELECT COALESCE(SUM(amount_paid), 0) FROM payments WHERE enrollment_id = @Id",
                        new Dictionary<string, object> { { "@Id", payment.EnrollmentId } }) ?? 0);

                    if (status == "ASSESSED" && payment.FeeType != "Downpayment")
                    {
                        throw new PaymentValidationException("Only downpayments can be recorded before enrollment confirmation.");
                    }

                    if (status == "ASSESSED" && payment.ScheduleId.HasValue)
                    {
                        throw new PaymentValidationException("A downpayment cannot be linked to a monthly installment.");
                    }

                    if (status == "CONFIRMED" && payment.FeeType != "Monthly Payment")
                    {
                        throw new PaymentValidationException("Confirmed enrollments accept monthly payments only.");
                    }

                    if (status != "ASSESSED" && status != "CONFIRMED")
                    {
                        throw new PaymentValidationException("Payments can only be recorded for assessed or confirmed enrollments.");
                    }

                    decimal balance = assessment - totalPaid;
                    if (payment.AmountPaid <= 0 || payment.AmountPaid > balance)
                    {
                        throw new PaymentValidationException($"Payment must be greater than zero and no more than the remaining balance of ₱{Math.Max(0, balance):N2}.");
                    }

                    if (status == "ASSESSED" && totalPaid + payment.AmountPaid > assessment)
                    {
                        throw new PaymentValidationException("The payment cannot exceed the remaining enrollment balance.");
                    }

                    if (status == "ASSESSED" && totalPaid + payment.AmountPaid > requiredDownpayment)
                    {
                        throw new PaymentValidationException(
                            $"Downpayments cannot exceed the required amount of ₱{requiredDownpayment:N2}.");
                    }

                    decimal schedulePaid = 0;
                    decimal scheduleExpected = 0;
                    if (status == "CONFIRMED")
                    {
                        if (!payment.ScheduleId.HasValue)
                        {
                            throw new PaymentValidationException("Select the monthly installment receiving this payment.");
                        }

                        DataTable schedules = _db.ExecuteQuery(
                            connection,
                            transaction,
                            @"SELECT expected_amount, amount_paid
                              FROM payment_schedules
                              WHERE schedule_id = @ScheduleId
                                AND enrollment_id = @EnrollmentId
                              FOR UPDATE",
                            new Dictionary<string, object>
                            {
                                { "@ScheduleId", payment.ScheduleId.Value },
                                { "@EnrollmentId", payment.EnrollmentId }
                            });
                        if (schedules.Rows.Count == 0)
                        {
                            throw new PaymentValidationException("The selected schedule does not belong to this enrollment.");
                        }

                        scheduleExpected = Convert.ToDecimal(schedules.Rows[0]["expected_amount"]);
                        schedulePaid = Convert.ToDecimal(schedules.Rows[0]["amount_paid"]);
                        if (payment.AmountPaid > scheduleExpected - schedulePaid)
                        {
                            throw new PaymentValidationException($"Payment exceeds this installment's remaining amount of ₱{Math.Max(0, scheduleExpected - schedulePaid):N2}.");
                        }
                    }

                    _db.ExecuteNonQuery(
                        connection,
                        transaction,
                        @"INSERT INTO payments
                            (enrollment_id, schedule_id, receipt_number, payment_date, amount_paid,
                             payment_type, payment_method, processed_by)
                          VALUES
                            (@EnrollmentId, @ScheduleId, @ReceiptNumber, @PaymentDate, @Amount,
                             @PaymentType, @PaymentMethod, @ProcessedBy)",
                        new Dictionary<string, object>
                        {
                            { "@EnrollmentId", payment.EnrollmentId },
                            { "@ScheduleId", (object?)payment.ScheduleId ?? DBNull.Value },
                            { "@ReceiptNumber", payment.ReceiptNumber },
                            { "@PaymentDate", payment.PaymentDate.Date },
                            { "@Amount", payment.AmountPaid },
                            { "@PaymentType", payment.FeeType ?? string.Empty },
                            { "@PaymentMethod", payment.PaymentMethod ?? string.Empty },
                            { "@ProcessedBy", actorId }
                        });
                    int newPaymentId = Convert.ToInt32(_db.ExecuteScalar(
                        connection,
                        transaction,
                        "SELECT LAST_INSERT_ID()"));

                    if (payment.ScheduleId.HasValue)
                    {
                        decimal newSchedulePaid = schedulePaid + payment.AmountPaid;
                        string scheduleStatus = newSchedulePaid >= scheduleExpected ? "Paid" : "Partially Paid";
                        _db.ExecuteNonQuery(
                            connection,
                            transaction,
                            @"UPDATE payment_schedules
                              SET amount_paid = @AmountPaid, schedule_status = @Status
                              WHERE schedule_id = @ScheduleId
                                AND enrollment_id = @EnrollmentId",
                            new Dictionary<string, object>
                            {
                                { "@AmountPaid", newSchedulePaid },
                                { "@Status", scheduleStatus },
                                { "@ScheduleId", payment.ScheduleId.Value },
                                { "@EnrollmentId", payment.EnrollmentId }
                            });
                    }

                    _audit.Log(
                        connection,
                        transaction,
                        actorId,
                        "PAYMENT_RECORD",
                        "Payment",
                        newPaymentId.ToString(),
                        $"Recorded {payment.FeeType} receipt {payment.ReceiptNumber} for enrollment {payment.EnrollmentId}: {payment.AmountPaid:N2}; total paid {totalPaid + payment.AmountPaid:N2}; remaining balance {assessment - totalPaid - payment.AmountPaid:N2}.");
                    return (
                        newPaymentId,
                        Math.Max(0, assessment - totalPaid - payment.AmountPaid));
                });

                TempData["SuccessMessage"] = $"Receipt {payment.ReceiptNumber} recorded. Remaining balance: ₱{result.RemainingBalance:N2}.";
                return RedirectToAction(nameof(Index));
            }
            catch (PaymentValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                ModelState.AddModelError(nameof(payment.ReceiptNumber), "That receipt/reference number has already been used.");
            }

            LoadPaymentOptions(payment.EnrollmentId);
            return View(payment);
        }

        [HttpGet]
        public IActionResult GenerateReport(string type, int enrollmentId)
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT e.enrollment_id, e.academic_year, e.semester, e.total_units,
                         e.tuition_fee, e.miscellaneous_fee, e.other_fees, e.total_assessment,
                         e.required_downpayment, e.status, s.student_id,
                         s.student_number, s.first_name, s.last_name, s.email,
                         s.year_level, p.program_code
                  FROM enrollments e
                  JOIN students s ON s.student_id = e.student_id
                  LEFT JOIN programs p ON p.program_id = s.program_id
                  WHERE e.enrollment_id = @Id",
                new Dictionary<string, object> { { "@Id", enrollmentId } });
            if (rows.Rows.Count == 0)
            {
                return NotFound();
            }

            DataRow row = rows.Rows[0];
            string enrollmentStatus = NormalizeStatus(Convert.ToString(row["status"]) ?? string.Empty);
            if (type == "COR" && enrollmentStatus != "CONFIRMED")
            {
                return Forbid();
            }

            var student = new Student
            {
                StudentId = Convert.ToInt32(row["student_id"]),
                StudentNumber = Convert.ToString(row["student_number"]) ?? string.Empty,
                FirstName = Convert.ToString(row["first_name"]) ?? string.Empty,
                LastName = Convert.ToString(row["last_name"]) ?? string.Empty,
                Email = Convert.ToString(row["email"]) ?? string.Empty,
                YearLevel = Convert.ToString(row["year_level"]) ?? string.Empty,
                AssignedProgram = new AcademicProgram
                {
                    ProgramCode = Convert.ToString(row["program_code"]) ?? string.Empty
                }
            };
            var enrollment = new Enrollment
            {
                EnrollmentId = Convert.ToInt32(row["enrollment_id"]),
                AcademicYear = Convert.ToString(row["academic_year"]) ?? string.Empty,
                Semester = Convert.ToString(row["semester"]) ?? string.Empty,
                TotalAssessment = Convert.ToDecimal(row["total_assessment"]),
                RequiredDownpayment = Convert.ToDecimal(row["required_downpayment"]),
                Status = enrollmentStatus
            };

            IReportGenerator reportGenerator;
            decimal totalPaid = Convert.ToDecimal(_db.ExecuteScalar(
                "SELECT COALESCE(SUM(amount_paid), 0) FROM payments WHERE enrollment_id = @Id",
                new Dictionary<string, object> { { "@Id", enrollmentId } }) ?? 0);
            if (type == "Balance")
            {
                reportGenerator = new BalanceReportGenerator(student, enrollment.TotalAssessment, totalPaid);
            }
            else if (type == "History")
            {
                DataTable historyRows = _db.ExecuteQuery(
                    @"SELECT receipt_number, amount_paid AS amount, payment_method, payment_date
                      FROM payments
                      WHERE enrollment_id = @Id
                      ORDER BY payment_date, payment_id",
                    new Dictionary<string, object> { { "@Id", enrollmentId } });
                var history = new List<Payment>();
                foreach (DataRow paymentRow in historyRows.Rows)
                {
                    history.Add(new Payment
                    {
                        ReceiptNumber = Convert.ToString(paymentRow["receipt_number"]) ?? string.Empty,
                        AmountPaid = Convert.ToDecimal(paymentRow["amount"]),
                        PaymentMethod = Convert.ToString(paymentRow["payment_method"]) ?? string.Empty,
                        PaymentDate = Convert.ToDateTime(paymentRow["payment_date"])
                    });
                }

                reportGenerator = new PaymentHistoryReportGenerator(student, history);
            }
            else if (type == "COR")
            {
                enrollment.EnrolledSubjects = LoadEnrollmentSubjects(enrollmentId);
                reportGenerator = new CorReportGenerator(student, enrollment, totalPaid);
            }
            else
            {
                return BadRequest("Select a valid report type.");
            }

            ViewBag.ReportText = reportGenerator.Generate();
            ViewBag.ReportType = type switch
            {
                "Balance" => "Statement of Account",
                "History" => "Payment History",
                _ => "Certificate of Registration"
            };
            return View("ReportPreview");
        }

        private void LoadPaymentOptions(int selectedEnrollmentId)
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT e.enrollment_id, e.total_assessment, e.required_downpayment,
                         e.status, COALESCE(SUM(p.amount_paid), 0) AS total_paid,
                         s.first_name, s.last_name, s.student_number, pr.program_code
                  FROM enrollments e
                  JOIN students s ON s.student_id = e.student_id
                  LEFT JOIN programs pr ON pr.program_id = s.program_id
                  LEFT JOIN payments p ON p.enrollment_id = e.enrollment_id
                  WHERE e.status IN ('ASSESSED', 'CONFIRMED')
                  GROUP BY e.enrollment_id, e.total_assessment, e.required_downpayment,
                           e.status, s.first_name, s.last_name, s.student_number, pr.program_code
                  ORDER BY s.last_name, s.first_name");
            var enrollments = new List<dynamic>();
            foreach (DataRow row in rows.Rows)
            {
                decimal assessment = Convert.ToDecimal(row["total_assessment"]);
                decimal paid = Convert.ToDecimal(row["total_paid"]);
                string status = NormalizeStatus(Convert.ToString(row["status"]) ?? string.Empty);
                int id = Convert.ToInt32(row["enrollment_id"]);
                enrollments.Add(new
                {
                    EnrollmentId = id,
                    StudentName = $"{row["first_name"]} {row["last_name"]}",
                    StudentNumber = Convert.ToString(row["student_number"]) ?? string.Empty,
                    ProgramCode = Convert.ToString(row["program_code"]) ?? string.Empty,
                    TotalAssessment = assessment,
                    AmountPaid = paid,
                    Balance = Math.Max(0, assessment - paid),
                    RequiredDownpayment = Convert.ToDecimal(row["required_downpayment"]),
                    Status = status,
                    DisplayName = $"{row["first_name"]} {row["last_name"]} ({row["student_number"]}) - {status} [Balance: ₱{Math.Max(0, assessment - paid):N2}]"
                });
            }

            DataTable scheduleRows = _db.ExecuteQuery(
                @"SELECT schedule_id, enrollment_id, due_date, expected_amount,
                         amount_paid, schedule_status
                  FROM payment_schedules
                  WHERE enrollment_id = @Id
                    AND amount_paid < expected_amount
                  ORDER BY due_date, schedule_id",
                new Dictionary<string, object> { { "@Id", selectedEnrollmentId } });
            var schedules = new List<PaymentSchedule>();
            foreach (DataRow row in scheduleRows.Rows)
            {
                schedules.Add(new PaymentSchedule
                {
                    ScheduleId = Convert.ToInt32(row["schedule_id"]),
                    EnrollmentId = Convert.ToInt32(row["enrollment_id"]),
                    DueDate = Convert.ToDateTime(row["due_date"]),
                    ExpectedAmount = Convert.ToDecimal(row["expected_amount"]),
                    AmountPaid = Convert.ToDecimal(row["amount_paid"]),
                    Status = Convert.ToString(row["schedule_status"]) ?? string.Empty
                });
            }

            ViewBag.Enrollments = enrollments;
            ViewBag.Schedules = schedules;
            ViewBag.SelectedEnrollmentId = selectedEnrollmentId;
        }

        private List<Subject> LoadEnrollmentSubjects(int enrollmentId)
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT s.subject_id, s.subject_code, s.subject_description, d.units,
                         o.section, o.schedule
                  FROM enrollment_details d
                  JOIN subject_offerings o ON o.offering_id = d.subject_offering_id
                  JOIN subjects s ON s.subject_id = o.subject_id
                  WHERE d.enrollment_id = @Id
                  ORDER BY s.subject_code, o.section",
                new Dictionary<string, object> { { "@Id", enrollmentId } });
            var subjects = new List<Subject>();
            foreach (DataRow row in rows.Rows)
            {
                subjects.Add(new Subject
                {
                    SubjectId = Convert.ToInt32(row["subject_id"]),
                    SubjectCode = Convert.ToString(row["subject_code"]) ?? string.Empty,
                    SubjectDescription = Convert.ToString(row["subject_description"]) ?? string.Empty,
                    Units = Convert.ToInt32(row["units"]),
                    Section = Convert.ToString(row["section"]) ?? string.Empty,
                    Schedule = row["schedule"] == DBNull.Value ? null : Convert.ToString(row["schedule"])
                });
            }

            return subjects;
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                ? id
                : throw new InvalidOperationException("The authenticated user ID is missing.");
        }

        private static string NormalizeStatus(string status)
        {
            return status.Trim().ToUpperInvariant();
        }

        private sealed class PaymentValidationException : Exception
        {
            public PaymentValidationException(string message) : base(message)
            {
            }
        }
    }
}
