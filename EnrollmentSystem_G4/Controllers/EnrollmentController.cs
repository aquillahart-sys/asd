using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;

namespace EnrollmentSystem_G4.Controllers
{
    public class EnrollmentController : Controller
    {
        private readonly DatabaseHelper _db;

        public EnrollmentController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /Enrollment/Assess/5
        public IActionResult Assess(int id)
        {
            // 1. Fetch Student Details
            string studentQuery = "SELECT student_id, student_number, first_name, last_name, email FROM students WHERE student_id = @Id";
            var studentParams = new Dictionary<string, object> { { "@Id", id } };
            DataTable studentDt = _db.ExecuteQuery(studentQuery, studentParams);

            if (studentDt.Rows.Count == 0)
            {
                return NotFound();
            }

            DataRow row = studentDt.Rows[0];
            var student = new Student
            {
                StudentId = Convert.ToInt32(row["student_id"]),
                StudentNumber = row["student_number"].ToString() ?? string.Empty,
                FirstName = row["first_name"].ToString() ?? string.Empty,
                LastName = row["last_name"].ToString() ?? string.Empty,
                Email = row["email"].ToString() ?? string.Empty
            };

            ViewBag.Student = student;

            // 2. Fetch Available Subjects
            string subjectQuery = "SELECT subject_id, subject_code, subject_description, units FROM subjects";
            DataTable subjectDt = _db.ExecuteQuery(subjectQuery);

            var subjects = new List<Subject>();
            foreach (DataRow sRow in subjectDt.Rows)
            {
                subjects.Add(new Subject
                {
                    SubjectId = Convert.ToInt32(sRow["subject_id"]),
                    SubjectCode = sRow["subject_code"].ToString() ?? string.Empty,
                    SubjectDescription = sRow["subject_description"].ToString() ?? string.Empty,
                    Units = Convert.ToInt32(sRow["units"])
                });
            }

            return View(subjects);
        }

        // POST: /Enrollment/Confirm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Confirm(int studentId, int[] selectedSubjects, decimal costPerUnit = 500.00m)
        {
            if (selectedSubjects == null || selectedSubjects.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select at least one subject to assess.";
                return RedirectToAction("Assess", new { id = studentId });
            }

            int totalUnits = 0;
            foreach (int subjectId in selectedSubjects)
            {
                string unitQuery = "SELECT units FROM subjects WHERE subject_id = @SubId";
                var unitParams = new Dictionary<string, object> { { "@SubId", subjectId } };
                object? result = _db.ExecuteScalar(unitQuery, unitParams);
                if (result != null)
                {
                    totalUnits += Convert.ToInt32(result);
                }
            }

            decimal totalAssessment = totalUnits * costPerUnit;

            string insertQuery = @"INSERT INTO enrollments (student_id, academic_year, semester, total_assessment, status) 
                                   VALUES (@StudentId, '2026-2027', '1st Semester', @TotalAssessment, 'Pending');
                                   SELECT LAST_INSERT_ID();";

            var insertParams = new Dictionary<string, object>
            {
                { "@StudentId", studentId },
                { "@TotalAssessment", totalAssessment }
            };

            object? newId = _db.ExecuteScalar(insertQuery, insertParams);

            TempData["SuccessMessage"] = $"Enrollment assessed successfully! Total Assessment: ₱{totalAssessment:N2}";
            return RedirectToAction("Index", "Student");
        }
    }
}