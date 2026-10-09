using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;
using MySql.Data.MySqlClient;

using AcademicProgram = EnrollmentSystem_G4.Models.Program;

namespace EnrollmentSystem_G4.Controllers
{
    public class StudentController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly AuditLogger _audit;

        public StudentController(DatabaseHelper db, AuditLogger audit)
        {
            _db = db;
            _audit = audit;
        }

        // GET: /Student/Index
        public IActionResult Index()
        {
            var studentList = new List<Student>();

            string query = @"SELECT s.student_id, s.student_number, s.first_name, s.last_name, s.email, 
                                    s.program_id, s.year_level, p.program_code, p.program_name
                             FROM students s
                             LEFT JOIN programs p ON s.program_id = p.program_id
                             ORDER BY s.student_id DESC";

            DataTable dt = _db.ExecuteQuery(query);

            foreach (DataRow row in dt.Rows)
            {
                var student = new Student
                {
                    StudentId = Convert.ToInt32(row["student_id"]),
                    StudentNumber = row["student_number"].ToString() ?? string.Empty,
                    FirstName = row["first_name"].ToString() ?? string.Empty,
                    LastName = row["last_name"].ToString() ?? string.Empty,
                    Email = row["email"].ToString() ?? string.Empty,
                    ProgramId = row["program_id"] != DBNull.Value ? Convert.ToInt32(row["program_id"]) : 0,
                    YearLevel = row["year_level"] != DBNull.Value ? row["year_level"].ToString() ?? "1st Year" : "1st Year"
                };

                if (row["program_id"] != DBNull.Value)
                {
                    student.AssignedProgram = new AcademicProgram
                    {
                        ProgramId = Convert.ToInt32(row["program_id"]),
                        ProgramCode = row["program_code"].ToString() ?? string.Empty,
                        ProgramName = row["program_name"].ToString() ?? string.Empty
                    };
                }

                studentList.Add(student);
            }

            return View(studentList);
        }

        // GET: /Student/Create
        [Authorize(Roles = "Administrator,Registrar")]
        public IActionResult Create()
        {
            PopulatePrograms();
            return View();
        }

        // POST: /Student/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Registrar")]
        public IActionResult Create(Student student)
        {
            student.StudentNumber = (student.StudentNumber ?? string.Empty).Trim();
            student.FirstName = (student.FirstName ?? string.Empty).Trim();
            student.LastName = (student.LastName ?? string.Empty).Trim();
            student.Email = (student.Email ?? string.Empty).Trim();
            student.YearLevel = (student.YearLevel ?? string.Empty).Trim();

            if (ModelState.IsValid)
            {
                if (student.ProgramId > 0 && !ProgramExists(student.ProgramId))
                {
                    ModelState.AddModelError(nameof(student.ProgramId), "Select an available academic program.");
                    PopulatePrograms(student.ProgramId);
                    return View(student);
                }

                int actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                    ? id
                    : throw new InvalidOperationException("The authenticated user ID is missing.");
                try
                {
                    int studentId = _db.ExecuteInTransaction((connection, transaction) =>
                    {
                        _db.ExecuteNonQuery(
                            connection,
                            transaction,
                            @"INSERT INTO students
                                (student_number, first_name, last_name, email, program_id, year_level)
                              VALUES
                                (@StudentNumber, @FirstName, @LastName, @Email, @ProgramId, @YearLevel)",
                            new Dictionary<string, object>
                            {
                                { "@StudentNumber", student.StudentNumber.Trim() },
                                { "@FirstName", student.FirstName.Trim() },
                                { "@LastName", student.LastName.Trim() },
                                { "@Email", student.Email.Trim() },
                                { "@ProgramId", student.ProgramId > 0 ? student.ProgramId : DBNull.Value },
                                { "@YearLevel", string.IsNullOrEmpty(student.YearLevel) ? "1st Year" : student.YearLevel }
                            });

                        int newId = Convert.ToInt32(_db.ExecuteScalar(
                            connection,
                            transaction,
                            "SELECT LAST_INSERT_ID()"));
                        _audit.Log(
                            connection,
                            transaction,
                            actorId,
                            "STUDENT_CREATE",
                            "Student",
                            newId.ToString(),
                            $"Created student record {student.StudentNumber}.");
                        return newId;
                    });
                }
                catch (MySqlException ex) when (ex.Number == 1062)
                {
                    ModelState.AddModelError(nameof(student.StudentNumber), "This student number is already registered. Enter a unique student number.");
                    PopulatePrograms(student.ProgramId);
                    return View(student);
                }
                catch (MySqlException ex) when (ex.Number == 1452)
                {
                    ModelState.AddModelError(nameof(student.ProgramId), "The selected academic program is no longer available.");
                    PopulatePrograms(student.ProgramId);
                    return View(student);
                }

                TempData["SuccessMessage"] = $"Student {student.StudentNumber} registered successfully.";
                return RedirectToAction(nameof(Index));
            }

            PopulatePrograms(student.ProgramId);
            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Registrar")]
        public IActionResult Delete(int studentId)
        {
            int actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                ? id
                : throw new InvalidOperationException("The authenticated user ID is missing.");

            StudentDeleteResult result;
            try
            {
                result = _db.ExecuteInTransaction((connection, transaction) =>
                {
                    DataTable studentRows = _db.ExecuteQuery(
                        connection,
                        transaction,
                        "SELECT student_number FROM students WHERE student_id = @StudentId FOR UPDATE",
                        new Dictionary<string, object> { { "@StudentId", studentId } });

                    if (studentRows.Rows.Count == 0)
                    {
                        _audit.Log(
                            connection,
                            transaction,
                            actorId,
                            "STUDENT_DELETE_DENIED",
                            "Student",
                            studentId.ToString(),
                            "Delete denied because the student record was not found.");
                        return StudentDeleteResult.NotFound;
                    }

                    string studentNumber = Convert.ToString(studentRows.Rows[0]["student_number"]) ?? string.Empty;
                    int enrollmentCount = Convert.ToInt32(_db.ExecuteScalar(
                        connection,
                        transaction,
                        "SELECT COUNT(*) FROM enrollments WHERE student_id = @StudentId",
                        new Dictionary<string, object> { { "@StudentId", studentId } }) ?? 0);

                    if (enrollmentCount > 0)
                    {
                        _audit.Log(
                            connection,
                            transaction,
                            actorId,
                            "STUDENT_DELETE_DENIED",
                            "Student",
                            studentId.ToString(),
                            $"Delete denied for student {studentNumber}: {enrollmentCount} related enrollment record(s) exist.");
                        return StudentDeleteResult.HasHistory;
                    }

                    _db.ExecuteNonQuery(
                        connection,
                        transaction,
                        "DELETE FROM students WHERE student_id = @StudentId",
                        new Dictionary<string, object> { { "@StudentId", studentId } });
                    _audit.Log(
                        connection,
                        transaction,
                        actorId,
                        "STUDENT_DELETE",
                        "Student",
                        studentId.ToString(),
                        $"Deleted student record {studentNumber}.");
                    return StudentDeleteResult.Deleted;
                });
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                _audit.Log(
                    actorId,
                    "STUDENT_DELETE_DENIED",
                    "Student",
                    studentId.ToString(),
                    "Delete denied because related academic or financial history is linked to the student.");
                result = StudentDeleteResult.HasHistory;
            }

            switch (result)
            {
                case StudentDeleteResult.Deleted:
                    TempData["SuccessMessage"] = "Student deleted successfully.";
                    break;
                case StudentDeleteResult.HasHistory:
                    TempData["ErrorMessage"] = "This student cannot be deleted because related enrollment, payment, or academic history exists. No records were deleted.";
                    break;
                default:
                    TempData["ErrorMessage"] = "The student was not found or has already been deleted.";
                    break;
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ProgramExists(int programId)
        {
            object? result = _db.ExecuteScalar(
                "SELECT COUNT(*) FROM programs WHERE program_id = @ProgramId",
                new Dictionary<string, object> { { "@ProgramId", programId } });
            return Convert.ToInt32(result ?? 0) > 0;
        }

        private void PopulatePrograms(int selectedProgramId = 0)
        {
            ViewBag.Programs = GetProgramsList()
                .Select(program => new SelectListItem
                {
                    Text = $"{program.ProgramCode} - {program.ProgramName}",
                    Value = program.ProgramId.ToString(),
                    Selected = program.ProgramId == selectedProgramId
                })
                .ToList();
        }

        private List<AcademicProgram> GetProgramsList()
        {
            var programs = new List<AcademicProgram>();
            string query = "SELECT program_id, program_code, program_name FROM programs ORDER BY program_code ASC";

            DataTable dt = _db.ExecuteQuery(query);
            foreach (DataRow row in dt.Rows)
            {
                programs.Add(new AcademicProgram
                {
                    ProgramId = Convert.ToInt32(row["program_id"]),
                    ProgramCode = row["program_code"].ToString() ?? string.Empty,
                    ProgramName = row["program_name"].ToString() ?? string.Empty
                });
            }

            return programs;
        }

        private enum StudentDeleteResult
        {
            Deleted,
            NotFound,
            HasHistory
        }
    }
}