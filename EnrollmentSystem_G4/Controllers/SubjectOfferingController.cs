using System.Data;
using System.Security.Claims;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnrollmentSystem_G4.Controllers
{
    [Authorize(Roles = "Administrator,Registrar")]
    public class SubjectOfferingController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly AuditLogger _audit;

        public SubjectOfferingController(DatabaseHelper db, AuditLogger audit)
        {
            _db = db;
            _audit = audit;
        }

        public IActionResult Index()
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT o.offering_id, o.subject_id, o.academic_year, o.semester,
                         o.section, o.schedule, o.room, s.subject_code,
                         s.subject_description, s.units
                  FROM subject_offerings o
                  JOIN subjects s ON s.subject_id = o.subject_id
                  ORDER BY o.academic_year DESC, o.semester, s.subject_code, o.section");
            return View(MapOfferings(rows));
        }

        [HttpGet]
        public IActionResult Create()
        {
            LoadSubjects();
            return View(new SubjectOffering());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SubjectOffering offering)
        {
            if (!ModelState.IsValid)
            {
                LoadSubjects();
                return View(offering);
            }

            string duplicateQuery = @"SELECT COUNT(*) FROM subject_offerings
                                      WHERE subject_id = @SubjectId
                                        AND academic_year = @AcademicYear
                                        AND semester = @Semester
                                        AND section = @Section";
            int duplicates = Convert.ToInt32(_db.ExecuteScalar(
                duplicateQuery,
                new Dictionary<string, object>
                {
                    { "@SubjectId", offering.SubjectId },
                    { "@AcademicYear", offering.AcademicYear.Trim() },
                    { "@Semester", offering.Semester.Trim() },
                    { "@Section", offering.Section.Trim() }
                }) ?? 0);
            if (duplicates > 0)
            {
                ModelState.AddModelError(nameof(offering.Section), "This subject offering already exists.");
                LoadSubjects();
                return View(offering);
            }

            int actorId = GetCurrentUserId();
            int offeringId = _db.ExecuteInTransaction((connection, transaction) =>
            {
                _db.ExecuteNonQuery(
                    connection,
                    transaction,
                    @"INSERT INTO subject_offerings
                        (subject_id, academic_year, semester, section, schedule, room, status)
                      VALUES (@SubjectId, @AcademicYear, @Semester, @Section, @Schedule, @Room, 'Active')",
                    new Dictionary<string, object>
                    {
                        { "@SubjectId", offering.SubjectId },
                        { "@AcademicYear", offering.AcademicYear.Trim() },
                        { "@Semester", offering.Semester.Trim() },
                        { "@Section", offering.Section.Trim() },
                        { "@Schedule", (object?)offering.Schedule ?? DBNull.Value },
                        { "@Room", (object?)offering.Room ?? DBNull.Value }
                    });

                int newId = Convert.ToInt32(_db.ExecuteScalar(
                    connection,
                    transaction,
                    "SELECT LAST_INSERT_ID()"));
                _audit.Log(
                    connection,
                    transaction,
                    actorId,
                    "OFFERING_CREATE",
                    "SubjectOffering",
                    newId.ToString(),
                    $"Created offering for subject {offering.SubjectId}, {offering.AcademicYear} {offering.Semester}, section {offering.Section}.");
                return newId;
            });

            TempData["SuccessMessage"] = $"Subject offering {offeringId} created.";
            return RedirectToAction(nameof(Index));
        }

        private void LoadSubjects()
        {
            var subjects = new List<Subject>();
            DataTable rows = _db.ExecuteQuery(
                "SELECT subject_id, subject_code, subject_description, units FROM subjects ORDER BY subject_code");
            foreach (DataRow row in rows.Rows)
            {
                subjects.Add(new Subject
                {
                    SubjectId = Convert.ToInt32(row["subject_id"]),
                    SubjectCode = Convert.ToString(row["subject_code"]) ?? string.Empty,
                    SubjectDescription = Convert.ToString(row["subject_description"]) ?? string.Empty,
                    Units = Convert.ToInt32(row["units"])
                });
            }

            ViewBag.Subjects = subjects;
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
    }
}
