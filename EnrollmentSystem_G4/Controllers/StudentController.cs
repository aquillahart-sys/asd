using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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
            ViewBag.Programs = GetProgramsList();
            return View();
        }

        // POST: /Student/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Registrar")]
        public IActionResult Create(Student student)
        {
            if (ModelState.IsValid)
            {
                int actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                    ? id
                    : throw new InvalidOperationException("The authenticated user ID is missing.");
                int studentId;
                try
                {
                    studentId = _db.ExecuteInTransaction((connection, transaction) =>
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
                    ModelState.AddModelError(string.Empty, "The student number or email is already registered.");
                    ViewBag.Programs = GetProgramsList();
                    return View(student);
                }

                TempData["SuccessMessage"] = $"Student {student.StudentNumber} registered successfully.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Programs = GetProgramsList();
            return View(student);
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
    }
}