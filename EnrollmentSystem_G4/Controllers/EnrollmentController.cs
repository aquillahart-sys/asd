using System.Data;
using System.Security.Claims;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace EnrollmentSystem_G4.Controllers
{
    [Authorize(Roles = "Administrator,Registrar,Cashier")]
    public class EnrollmentController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly AuditLogger _audit;
        private readonly IConfiguration _configuration;

        public EnrollmentController(DatabaseHelper db, AuditLogger audit, IConfiguration configuration)
        {
            _db = db;
            _audit = audit;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT e.enrollment_id, e.student_id, s.first_name, s.last_name,
                         s.student_number, e.academic_year, e.semester, e.total_units,
                         e.total_assessment, e.required_downpayment, e.status,
                         COALESCE(SUM(p.amount_paid), 0) AS total_paid
                  FROM enrollments e
                  JOIN students s ON s.student_id = e.student_id
                  LEFT JOIN payments p ON p.enrollment_id = e.enrollment_id
                  GROUP BY e.enrollment_id, e.student_id, s.first_name, s.last_name,
                           s.student_number, e.academic_year, e.semester, e.total_units,
                           e.total_assessment, e.required_downpayment, e.status
                  ORDER BY e.enrollment_id DESC");
            var enrollments = new List<EnrollmentListItem>();
            foreach (DataRow row in rows.Rows)
            {
                decimal assessment = Convert.ToDecimal(row["total_assessment"]);
                decimal paid = Convert.ToDecimal(row["total_paid"]);
                enrollments.Add(new EnrollmentListItem
                {
                    EnrollmentId = Convert.ToInt32(row["enrollment_id"]),
                    StudentId = Convert.ToInt32(row["student_id"]),
                    StudentName = $"{row["first_name"]} {row["last_name"]}",
                    StudentNumber = Convert.ToString(row["student_number"]) ?? string.Empty,
                    AcademicYear = Convert.ToString(row["academic_year"]) ?? string.Empty,
                    Semester = Convert.ToString(row["semester"]) ?? string.Empty,
                    TotalUnits = Convert.ToInt32(row["total_units"]),
                    TotalAssessment = assessment,
                    RequiredDownpayment = Convert.ToDecimal(row["required_downpayment"]),
                    TotalPaid = paid,
                    RemainingBalance = Math.Max(0, assessment - paid),
                    Status = NormalizeStatus(Convert.ToString(row["status"]) ?? string.Empty)
                });
            }

            return View(enrollments);
        }

        [HttpGet]
        [Authorize(Roles = "Administrator,Registrar")]
        public IActionResult Create(int studentId, string? academicYear = null, string? semester = null)
        {
            Student? student = FindStudent(studentId);
            if (student == null)
            {
                return NotFound();
            }

            var model = new EnrollmentSelectionViewModel
            {
                StudentId = studentId,
                Student = student,
                AcademicYear = academicYear ?? _configuration["Enrollment:AcademicYear"] ?? "2026-2027",
                Semester = semester ?? _configuration["Enrollment:Semester"] ?? "1st Semester"
            };
            LoadOfferings(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Registrar")]
        public IActionResult Create(EnrollmentSelectionViewModel model)
        {
            model.Student = FindStudent(model.StudentId) ?? new Student();
            if (model.Student.StudentId == 0)
            {
                return NotFound();
            }

            if (model.SelectedOfferingIds.Count == 0 ||
                model.SelectedOfferingIds.Any(id => id <= 0) ||
                model.SelectedOfferingIds.Distinct().Count() != model.SelectedOfferingIds.Count)
            {
                ModelState.AddModelError(nameof(model.SelectedOfferingIds), "Select one or more distinct subject offerings.");
                LoadOfferings(model);
                return View(model);
            }

            List<SubjectOffering> selectedOfferings = FindOfferings(
                model.SelectedOfferingIds,
                model.AcademicYear,
                model.Semester);
            if (selectedOfferings.Count != model.SelectedOfferingIds.Count)
            {
                ModelState.AddModelError(nameof(model.SelectedOfferingIds), "One or more offerings are invalid for the selected term.");
                LoadOfferings(model);
                return View(model);
            }

            int actorId = GetCurrentUserId();
            try
            {
                int enrollmentId = _db.ExecuteInTransaction((connection, transaction) =>
                {
                    object? lockedStudent = _db.ExecuteScalar(
                        connection,
                        transaction,
                        "SELECT student_id FROM students WHERE student_id = @StudentId FOR UPDATE",
                        new Dictionary<string, object> { { "@StudentId", model.StudentId } });
                    if (lockedStudent == null || lockedStudent == DBNull.Value)
                    {
                        throw new InvalidOperationException("The selected student no longer exists.");
                    }

                    int duplicate = Convert.ToInt32(_db.ExecuteScalar(
                        connection,
                        transaction,
                        @"SELECT COUNT(*) FROM enrollments
                          WHERE student_id = @StudentId
                            AND academic_year = @AcademicYear
                            AND semester = @Semester
                            AND status <> 'CANCELLED'",
                        new Dictionary<string, object>
                        {
                            { "@StudentId", model.StudentId },
                            { "@AcademicYear", model.AcademicYear.Trim() },
                            { "@Semester", model.Semester.Trim() }
                        }) ?? 0);
                    if (duplicate > 0)
                    {
                        throw new InvalidOperationException("This student already has an enrollment for that term.");
                    }

                    _db.ExecuteNonQuery(
                        connection,
                        transaction,
                        @"INSERT INTO enrollments
                            (student_id, academic_year, semester, enrollment_date, total_units,
                             tuition_fee, miscellaneous_fee, other_fees, total_assessment,
                             required_downpayment, status)
                          VALUES
                            (@StudentId, @AcademicYear, @Semester, @EnrollmentDate, @TotalUnits,
                             0, 0, 0, 0, 0, 'PENDING')",
                        new Dictionary<string, object>
                        {
                            { "@StudentId", model.StudentId },
                            { "@AcademicYear", model.AcademicYear.Trim() },
                            { "@Semester", model.Semester.Trim() },
                            { "@EnrollmentDate", DateTime.Today },
                            { "@TotalUnits", selectedOfferings.Sum(item => item.Units) }
                        });

                    int newId = Convert.ToInt32(_db.ExecuteScalar(
                        connection,
                        transaction,
                        "SELECT LAST_INSERT_ID()"));
                    decimal ratePerUnit = _configuration.GetValue<decimal>("Enrollment:RatePerUnit", 500m);
                    foreach (SubjectOffering offering in selectedOfferings)
                    {
                        _db.ExecuteNonQuery(
                            connection,
                            transaction,
                            @"INSERT INTO enrollment_details
                                (enrollment_id, subject_offering_id, subject_id, units, amount)
                              VALUES (@EnrollmentId, @OfferingId, @SubjectId, @Units, @Amount)",
                            new Dictionary<string, object>
                            {
                                { "@EnrollmentId", newId },
                                { "@OfferingId", offering.OfferingId },
                                { "@SubjectId", offering.SubjectId },
                                { "@Units", offering.Units },
                                { "@Amount", decimal.Round(offering.Units * ratePerUnit, 2) }
                            });
                    }

                    _audit.Log(
                        connection,
                        transaction,
                        actorId,
                        "ENROLLMENT_CREATE",
                        "Enrollment",
                        newId.ToString(),
                        $"Created pending enrollment for student {model.Student.StudentNumber} with {selectedOfferings.Count} offerings.");
                    return newId;
                });

                TempData["SuccessMessage"] = "Pending enrollment and selected offerings were saved.";
                return RedirectToAction(nameof(Assess), new { id = enrollmentId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                LoadOfferings(model);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Assess(int id)
        {
            EnrollmentAssessmentViewModel? model = LoadAssessment(id);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Registrar")]
        public IActionResult SaveAssessment(int enrollmentId)
        {
            decimal ratePerUnit = _configuration.GetValue<decimal>("Enrollment:RatePerUnit", 500m);
            decimal downpaymentRate = _configuration.GetValue<decimal>("Enrollment:DownpaymentRate", 0.20m);
            decimal miscellaneousFee = _configuration.GetValue<decimal>("Enrollment:MiscellaneousFee", 0m);
            decimal otherFees = _configuration.GetValue<decimal>("Enrollment:OtherFees", 0m);
            if (ratePerUnit <= 0 || downpaymentRate <= 0 || downpaymentRate > 1 ||
                miscellaneousFee < 0 || otherFees < 0)
            {
                throw new InvalidOperationException("Enrollment fee and downpayment settings are invalid.");
            }

            int actorId = GetCurrentUserId();
            string status = _db.ExecuteInTransaction((connection, transaction) =>
            {
                DataTable rows = _db.ExecuteQuery(
                    connection,
                    transaction,
                    @"SELECT status
                      FROM enrollments
                      WHERE enrollment_id = @Id
                      FOR UPDATE",
                    new Dictionary<string, object> { { "@Id", enrollmentId } });
                if (rows.Rows.Count == 0)
                {
                    return "NOT_FOUND";
                }

                string currentStatus = NormalizeStatus(Convert.ToString(rows.Rows[0]["status"]) ?? string.Empty);
                if (currentStatus != "PENDING")
                {
                    return currentStatus;
                }

                int totalUnits = Convert.ToInt32(_db.ExecuteScalar(
                    connection,
                    transaction,
                    @"SELECT COALESCE(SUM(units), 0)
                      FROM enrollment_details
                      WHERE enrollment_id = @Id",
                    new Dictionary<string, object> { { "@Id", enrollmentId } }) ?? 0);
                if (totalUnits <= 0)
                {
                    return "NO_SUBJECTS";
                }

                decimal tuitionFee = decimal.Round(totalUnits * ratePerUnit, 2);
                decimal totalAssessment = decimal.Round(tuitionFee + miscellaneousFee + otherFees, 2);
                decimal requiredDownpayment = decimal.Round(totalAssessment * downpaymentRate, 2);
                _db.ExecuteNonQuery(
                    connection,
                    transaction,
                    @"UPDATE enrollments
                      SET tuition_fee = @TuitionFee,
                          miscellaneous_fee = @MiscellaneousFee,
                          other_fees = @OtherFees,
                          total_assessment = @TotalAssessment,
                          required_downpayment = @RequiredDownpayment,
                          status = 'ASSESSED'
                      WHERE enrollment_id = @Id",
                    new Dictionary<string, object>
                    {
                        { "@TuitionFee", tuitionFee },
                        { "@MiscellaneousFee", miscellaneousFee },
                        { "@OtherFees", otherFees },
                        { "@TotalAssessment", totalAssessment },
                        { "@RequiredDownpayment", requiredDownpayment },
                        { "@Id", enrollmentId }
                    });
                _audit.Log(
                    connection,
                    transaction,
                    actorId,
                    "ASSESSMENT_CREATE",
                    "Enrollment",
                    enrollmentId.ToString(),
                    $"Assessed {totalUnits} units at {ratePerUnit:N2} per unit; total assessment {totalAssessment:N2}; required downpayment {requiredDownpayment:N2}.");
                return "ASSESSED";
            });

            if (status == "NOT_FOUND")
            {
                return NotFound();
            }

            if (status == "NO_SUBJECTS")
            {
                TempData["ErrorMessage"] = "An enrollment must have at least one valid subject offering before assessment.";
            }
            else if (status == "ASSESSED")
            {
                TempData["SuccessMessage"] = "Assessment saved. Record the required downpayment before confirming enrollment.";
            }
            else if (status != "ASSESSED")
            {
                TempData["ErrorMessage"] = "Only a pending enrollment can be assessed.";
            }

            return RedirectToAction(nameof(Assess), new { id = enrollmentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Registrar")]
        public IActionResult Confirm(int enrollmentId)
        {
            int installmentCount = _configuration.GetValue<int>("Enrollment:InstallmentCount", 4);
            if (installmentCount <= 0)
            {
                throw new InvalidOperationException("Enrollment installment count must be greater than zero.");
            }

            int actorId = GetCurrentUserId();
            string result = _db.ExecuteInTransaction((connection, transaction) =>
            {
                DataTable rows = _db.ExecuteQuery(
                    connection,
                    transaction,
                    @"SELECT student_id, academic_year, semester, total_assessment,
                             required_downpayment, status
                      FROM enrollments
                      WHERE enrollment_id = @Id
                      FOR UPDATE",
                    new Dictionary<string, object> { { "@Id", enrollmentId } });
                if (rows.Rows.Count == 0)
                {
                    return "NOT_FOUND";
                }

                DataRow row = rows.Rows[0];
                if (NormalizeStatus(Convert.ToString(row["status"]) ?? string.Empty) != "ASSESSED")
                {
                    return NormalizeStatus(Convert.ToString(row["status"]) ?? "INVALID_STATUS");
                }

                decimal totalAssessment = Convert.ToDecimal(row["total_assessment"]);
                decimal requiredDownpayment = Convert.ToDecimal(row["required_downpayment"]);
                decimal totalPaid = Convert.ToDecimal(_db.ExecuteScalar(
                    connection,
                    transaction,
                    "SELECT COALESCE(SUM(amount_paid), 0) FROM payments WHERE enrollment_id = @Id",
                    new Dictionary<string, object> { { "@Id", enrollmentId } }) ?? 0);
                var enrollment = new Enrollment
                {
                    TotalAssessment = totalAssessment,
                    RequiredDownpayment = requiredDownpayment,
                    AcademicYear = Convert.ToString(row["academic_year"]) ?? string.Empty,
                    Semester = Convert.ToString(row["semester"]) ?? string.Empty
                };
                if (!enrollment.CanConfirm(totalPaid))
                {
                    return "DOWNPAYMENT_REQUIRED";
                }

                _db.ExecuteNonQuery(
                    connection,
                    transaction,
                    "UPDATE enrollments SET status = 'CONFIRMED' WHERE enrollment_id = @Id",
                    new Dictionary<string, object> { { "@Id", enrollmentId } });

                decimal balance = Math.Max(0, totalAssessment - totalPaid);
                List<PaymentSchedule> schedules = balance > 0
                    ? enrollment.GenerateMonthlySchedules(
                        enrollmentId,
                        balance,
                        installmentCount,
                        DateTime.Today.AddMonths(1))
                    : new List<PaymentSchedule>();
                foreach (PaymentSchedule schedule in schedules)
                {
                    _db.ExecuteNonQuery(
                        connection,
                        transaction,
                        @"INSERT INTO payment_schedules
                            (enrollment_id, installment_name, due_date, expected_amount, amount_paid, schedule_status)
                          VALUES (@EnrollmentId, @InstallmentName, @DueDate, @ExpectedAmount, 0, 'Unpaid')",
                        new Dictionary<string, object>
                        {
                            { "@EnrollmentId", schedule.EnrollmentId },
                            { "@InstallmentName", schedule.InstallmentName },
                            { "@DueDate", schedule.DueDate.Date },
                            { "@ExpectedAmount", schedule.ExpectedAmount }
                        });
                }

                _audit.Log(
                    connection,
                    transaction,
                    actorId,
                    "ENROLLMENT_CONFIRM",
                    "Enrollment",
                    enrollmentId.ToString(),
                    $"Confirmed enrollment after {totalPaid:N2} in payments satisfied the {requiredDownpayment:N2} downpayment; created {schedules.Count} monthly installments.");
                return "CONFIRMED";
            });

            if (result == "NOT_FOUND")
            {
                return NotFound();
            }

            TempData[result == "CONFIRMED" ? "SuccessMessage" : "ErrorMessage"] = result switch
            {
                "CONFIRMED" => "Enrollment confirmed and monthly payment schedule created.",
                "DOWNPAYMENT_REQUIRED" => "The required downpayment has not been satisfied; enrollment remains assessed.",
                _ => "Only an assessed enrollment can be confirmed."
            };
            return RedirectToAction(nameof(Assess), new { id = enrollmentId });
        }

        private EnrollmentAssessmentViewModel? LoadAssessment(int enrollmentId)
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT e.enrollment_id, e.student_id, e.academic_year, e.semester,
                         e.total_units, e.tuition_fee, e.miscellaneous_fee, e.other_fees,
                         e.total_assessment,
                         e.required_downpayment, e.status,
                         s.student_number, s.first_name, s.last_name, s.email,
                         s.program_id, s.year_level,
                         COALESCE(SUM(p.amount_paid), 0) AS total_paid
                  FROM enrollments e
                  JOIN students s ON s.student_id = e.student_id
                  LEFT JOIN payments p ON p.enrollment_id = e.enrollment_id
                  WHERE e.enrollment_id = @Id
                  GROUP BY e.enrollment_id, e.student_id, e.academic_year, e.semester,
                           e.total_units, e.tuition_fee, e.total_assessment,
                           e.required_downpayment, e.status, s.student_number,
                           s.first_name, s.last_name, s.email, s.program_id, s.year_level",
                new Dictionary<string, object> { { "@Id", enrollmentId } });
            if (rows.Rows.Count == 0)
            {
                return null;
            }

            DataRow row = rows.Rows[0];
            decimal ratePerUnit = _configuration.GetValue<decimal>("Enrollment:RatePerUnit", 500m);
            decimal tuitionFee = Convert.ToDecimal(row["tuition_fee"]);
            decimal miscellaneousFee = Convert.ToDecimal(row["miscellaneous_fee"]);
            decimal otherFees = Convert.ToDecimal(row["other_fees"]);
            decimal assessment = Convert.ToDecimal(row["total_assessment"]);
            int totalUnits = Convert.ToInt32(row["total_units"]);
            if (assessment == 0)
            {
                tuitionFee = decimal.Round(totalUnits * ratePerUnit, 2);
                miscellaneousFee = _configuration.GetValue<decimal>("Enrollment:MiscellaneousFee", 0m);
                otherFees = _configuration.GetValue<decimal>("Enrollment:OtherFees", 0m);
                assessment = decimal.Round(tuitionFee + miscellaneousFee + otherFees, 2);
            }

            decimal paid = Convert.ToDecimal(row["total_paid"]);
            decimal downpaymentRate = _configuration.GetValue<decimal>("Enrollment:DownpaymentRate", 0.20m);
            decimal requiredDownpayment = Convert.ToDecimal(row["required_downpayment"]);
            if (requiredDownpayment == 0 && assessment > 0)
            {
                requiredDownpayment = decimal.Round(assessment * downpaymentRate, 2);
            }
            var enrollment = new Enrollment
            {
                EnrollmentId = Convert.ToInt32(row["enrollment_id"]),
                StudentId = Convert.ToInt32(row["student_id"]),
                AcademicYear = Convert.ToString(row["academic_year"]) ?? string.Empty,
                Semester = Convert.ToString(row["semester"]) ?? string.Empty,
                TotalAssessment = assessment,
                RequiredDownpayment = requiredDownpayment,
                TotalPayment = paid,
                Status = NormalizeStatus(Convert.ToString(row["status"]) ?? string.Empty)
            };
            return new EnrollmentAssessmentViewModel
            {
                Enrollment = enrollment,
                Student = new Student
                {
                    StudentId = enrollment.StudentId,
                    StudentNumber = Convert.ToString(row["student_number"]) ?? string.Empty,
                    FirstName = Convert.ToString(row["first_name"]) ?? string.Empty,
                    LastName = Convert.ToString(row["last_name"]) ?? string.Empty,
                    Email = Convert.ToString(row["email"]) ?? string.Empty,
                    ProgramId = row["program_id"] == DBNull.Value ? 0 : Convert.ToInt32(row["program_id"]),
                    YearLevel = Convert.ToString(row["year_level"]) ?? string.Empty
                },
                Offerings = FindEnrollmentOfferings(enrollmentId),
                TotalUnits = totalUnits,
                RatePerUnit = ratePerUnit,
                TuitionFee = tuitionFee,
                MiscellaneousFee = miscellaneousFee,
                OtherFees = otherFees,
                TotalAssessment = assessment,
                RequiredDownpayment = requiredDownpayment,
                TotalPaid = paid,
                RemainingBalance = Math.Max(0, assessment - paid),
                CanConfirm = enrollment.Status == "ASSESSED" && enrollment.CanConfirm(paid)
            };
        }

        private Student? FindStudent(int studentId)
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT student_id, student_number, first_name, last_name, email
                  FROM students
                  WHERE student_id = @Id",
                new Dictionary<string, object> { { "@Id", studentId } });
            if (rows.Rows.Count == 0)
            {
                return null;
            }

            DataRow row = rows.Rows[0];
            return new Student
            {
                StudentId = Convert.ToInt32(row["student_id"]),
                StudentNumber = Convert.ToString(row["student_number"]) ?? string.Empty,
                FirstName = Convert.ToString(row["first_name"]) ?? string.Empty,
                LastName = Convert.ToString(row["last_name"]) ?? string.Empty,
                Email = Convert.ToString(row["email"]) ?? string.Empty
            };
        }

        private List<SubjectOffering> FindOfferings(IEnumerable<int> offeringIds, string academicYear, string semester)
        {
            int[] ids = offeringIds.Distinct().ToArray();
            if (ids.Length == 0)
            {
                return new List<SubjectOffering>();
            }

            var parameters = new Dictionary<string, object>
            {
                { "@AcademicYear", academicYear.Trim() },
                { "@Semester", semester.Trim() }
            };
            string[] placeholders = ids.Select((id, index) =>
            {
                string parameter = $"@Offering{index}";
                parameters.Add(parameter, id);
                return parameter;
            }).ToArray();

            DataTable rows = _db.ExecuteQuery(
                $@"SELECT o.offering_id, o.subject_id, o.academic_year, o.semester,
                          o.section, o.schedule, o.room, s.subject_code,
                          s.subject_description, s.units
                   FROM subject_offerings o
                   JOIN subjects s ON s.subject_id = o.subject_id
                   WHERE o.academic_year = @AcademicYear
                     AND o.semester = @Semester
                     AND o.status = 'Active'
                     AND o.offering_id IN ({string.Join(", ", placeholders)})
                   ORDER BY s.subject_code, o.section",
                parameters);
            return MapOfferings(rows);
        }

        private List<SubjectOffering> FindEnrollmentOfferings(int enrollmentId)
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT o.offering_id, o.subject_id, o.academic_year, o.semester,
                         o.section, o.schedule, o.room, s.subject_code,
                         s.subject_description, d.units
                  FROM enrollment_details d
                  JOIN subject_offerings o ON o.offering_id = d.subject_offering_id
                  JOIN subjects s ON s.subject_id = o.subject_id
                  WHERE d.enrollment_id = @Id
                  ORDER BY s.subject_code, o.section",
                new Dictionary<string, object> { { "@Id", enrollmentId } });
            return MapOfferings(rows);
        }

        private void LoadOfferings(EnrollmentSelectionViewModel model)
        {
            model.AvailableOfferings = FindOfferings(
                model.AcademicYear,
                model.Semester);
        }

        private List<SubjectOffering> FindOfferings(string academicYear, string semester)
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT o.offering_id, o.subject_id, o.academic_year, o.semester,
                         o.section, o.schedule, o.room, s.subject_code,
                         s.subject_description, s.units
                  FROM subject_offerings o
                  JOIN subjects s ON s.subject_id = o.subject_id
                  WHERE o.academic_year = @AcademicYear
                    AND o.semester = @Semester
                    AND o.status = 'Active'
                  ORDER BY s.subject_code, o.section",
                new Dictionary<string, object>
                {
                    { "@AcademicYear", academicYear.Trim() },
                    { "@Semester", semester.Trim() }
                });
            return MapOfferings(rows);
        }

        private static List<SubjectOffering> MapOfferings(DataTable rows)
        {
            var offerings = new List<SubjectOffering>();
            foreach (DataRow row in rows.Rows)
            {
                offerings.Add(new SubjectOffering
                {
                    OfferingId = Convert.ToInt32(row["offering_id"]),
                    SubjectId = Convert.ToInt32(row["subject_id"]),
                    AcademicYear = Convert.ToString(row["academic_year"]) ?? string.Empty,
                    Semester = Convert.ToString(row["semester"]) ?? string.Empty,
                    Section = Convert.ToString(row["section"]) ?? string.Empty,
                    Schedule = row["schedule"] == DBNull.Value ? null : Convert.ToString(row["schedule"]),
                    Room = row["room"] == DBNull.Value ? null : Convert.ToString(row["room"]),
                    SubjectCode = Convert.ToString(row["subject_code"]) ?? string.Empty,
                    SubjectDescription = Convert.ToString(row["subject_description"]) ?? string.Empty,
                    Units = Convert.ToInt32(row["units"])
                });
            }

            return offerings;
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
    }
}
