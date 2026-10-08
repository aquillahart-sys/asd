using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;

namespace EnrollmentSystem_G4.Controllers
{
    [Authorize(Roles = "Administrator,Registrar")]
    public class SubjectController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly AuditLogger _audit;

        public SubjectController(DatabaseHelper db, AuditLogger audit)
        {
            _db = db;
            _audit = audit;
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
                string code = subject.SubjectCode.Trim();
                int createdId = _db.ExecuteInTransaction((connection, transaction) =>
                {
                    _db.ExecuteNonQuery(
                        connection,
                        transaction,
                        "INSERT INTO subjects (subject_code, subject_description, units) VALUES (@Code, @Desc, @Units)",
                        new Dictionary<string, object>
                        {
                            { "@Code", code },
                            { "@Desc", subject.SubjectDescription.Trim() },
                            { "@Units", subject.Units }
                        });
                    int id = Convert.ToInt32(_db.ExecuteScalar(connection, transaction, "SELECT LAST_INSERT_ID()"));
                    int actorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int value)
                        ? value
                        : throw new InvalidOperationException("The authenticated user ID is missing.");
                    _audit.Log(connection, transaction, actorId, "SUBJECT_CREATE", "Subject", id.ToString(),
                        $"Created subject '{code}'.");
                    return id;
                });
                if (createdId > 0)
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