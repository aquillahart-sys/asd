using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;

namespace EnrollmentSystem_G4.Controllers
{
    public class SubjectController : Controller
    {
        private readonly DatabaseHelper _db;

        public SubjectController(DatabaseHelper db)
        {
            _db = db;
        }

        // GET: /Subject/Index
        public IActionResult Index()
        {
            var subjects = new List<Subject>();
            string query = "SELECT subject_id, subject_code, subject_description, units FROM subjects ORDER BY subject_code ASC";

            DataTable dt = _db.ExecuteQuery(query);

            foreach (DataRow row in dt.Rows)
            {
                subjects.Add(new Subject
                {
                    SubjectId = Convert.ToInt32(row["subject_id"]),
                    SubjectCode = row["subject_code"].ToString() ?? string.Empty,
                    SubjectDescription = row["subject_description"].ToString() ?? string.Empty,
                    Units = Convert.ToInt32(row["units"])
                });
            }

            return View(subjects);
        }

        // GET: /Subject/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Subject/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Subject subject)
        {
            if (ModelState.IsValid)
            {
                string query = "INSERT INTO subjects (subject_code, subject_description, units) VALUES (@Code, @Desc, @Units)";
                var parameters = new Dictionary<string, object>
                {
                    { "@Code", subject.SubjectCode.Trim() },
                    { "@Desc", subject.SubjectDescription.Trim() },
                    { "@Units", subject.Units }
                };

                int rows = _db.ExecuteNonQuery(query, parameters);
                if (rows > 0)
                {
                    TempData["SuccessMessage"] = "Subject created successfully!";
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("", "Failed to save subject.");
            }

            return View(subject);
        }
    }
}