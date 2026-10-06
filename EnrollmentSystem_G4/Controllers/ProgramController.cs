using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;

namespace EnrollmentSystem_G4.Controllers
{
    public class ProgramController : Controller
    {
        private readonly DatabaseHelper _dbHelper;

        public ProgramController(DatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // GET: Program/Index
        public IActionResult Index()
        {
            List<ProgramModel> programs = new List<ProgramModel>();
            string query = "SELECT program_id, program_code, program_name, description, status FROM programs ORDER BY program_code ASC";

            DataTable dt = _dbHelper.ExecuteQuery(query);

            foreach (DataRow row in dt.Rows)
            {
                programs.Add(new ProgramModel
                {
                    ProgramId = Convert.ToInt32(row["program_id"]),
                    ProgramCode = row["program_code"].ToString()!,
                    ProgramName = row["program_name"].ToString()!,
                    Description = row["description"] != DBNull.Value ? row["description"].ToString() : null,
                    Status = row["status"] != DBNull.Value ? row["status"].ToString()! : "Active"
                });
            }

            return View(programs);
        }

        // GET: Program/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Program/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ProgramModel model)
        {
            if (ModelState.IsValid)
            {
                string query = "INSERT INTO programs (program_code, program_name, description, status) VALUES (@Code, @Name, @Desc, @Status)";
                var parameters = new Dictionary<string, object>
                {
                    { "@Code", model.ProgramCode.Trim() },
                    { "@Name", model.ProgramName.Trim() },
                    { "@Desc", (object?)model.Description ?? DBNull.Value },
                    { "@Status", model.Status }
                };

                int rowsAffected = _dbHelper.ExecuteNonQuery(query, parameters);
                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Program created successfully!";
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("", "Failed to insert program into database.");
            }

            return View(model);
        }

        // GET: Program/Edit/5
        public IActionResult Edit(int id)
        {
            string query = "SELECT program_id, program_code, program_name, description, status FROM programs WHERE program_id = @Id";
            var parameters = new Dictionary<string, object> { { "@Id", id } };

            DataTable dt = _dbHelper.ExecuteQuery(query, parameters);

            if (dt.Rows.Count == 0)
            {
                return NotFound();
            }

            DataRow row = dt.Rows[0];
            ProgramModel model = new ProgramModel
            {
                ProgramId = Convert.ToInt32(row["program_id"]),
                ProgramCode = row["program_code"].ToString()!,
                ProgramName = row["program_name"].ToString()!,
                Description = row["description"] != DBNull.Value ? row["description"].ToString() : null,
                Status = row["status"] != DBNull.Value ? row["status"].ToString()! : "Active"
            };

            return View(model);
        }

        // POST: Program/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, ProgramModel model)
        {
            if (id != model.ProgramId)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                string query = "UPDATE programs SET program_code = @Code, program_name = @Name, description = @Desc, status = @Status WHERE program_id = @Id";
                var parameters = new Dictionary<string, object>
                {
                    { "@Code", model.ProgramCode.Trim() },
                    { "@Name", model.ProgramName.Trim() },
                    { "@Desc", (object?)model.Description ?? DBNull.Value },
                    { "@Status", model.Status },
                    { "@Id", id }
                };

                int rowsAffected = _dbHelper.ExecuteNonQuery(query, parameters);
                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Program updated successfully!";
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("", "Failed to update program record.");
            }

            return View(model);
        }

        // POST: Program/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            string query = "DELETE FROM programs WHERE program_id = @Id";
            var parameters = new Dictionary<string, object> { { "@Id", id } };

            try
            {
                int rowsAffected = _dbHelper.ExecuteNonQuery(query, parameters);
                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Program deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Program not found or could not be deleted.";
                }
            }
            catch (MySqlException)
            {
                TempData["ErrorMessage"] = "Cannot delete this program because students or subjects are linked to it.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}