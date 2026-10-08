using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;
using System.Security.Claims;

namespace EnrollmentSystem_G4.Controllers
{
    [Authorize(Roles = "Administrator,Registrar")]
    public class ProgramController : Controller
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly AuditLogger _audit;

        public ProgramController(DatabaseHelper dbHelper, AuditLogger audit)
        {
            _dbHelper = dbHelper;
            _audit = audit;
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
                string code = model.ProgramCode.Trim();
                string name = model.ProgramName.Trim();
                int actorId = GetCurrentUserId();
                int createdId = _dbHelper.ExecuteInTransaction((connection, transaction) =>
                {
                    _dbHelper.ExecuteNonQuery(
                        connection,
                        transaction,
                        "INSERT INTO programs (program_code, program_name, description, status) VALUES (@Code, @Name, @Desc, @Status)",
                        new Dictionary<string, object>
                        {
                            { "@Code", code },
                            { "@Name", name },
                            { "@Desc", (object?)model.Description?.Trim() ?? DBNull.Value },
                            { "@Status", model.Status }
                        });
                    int id = Convert.ToInt32(_dbHelper.ExecuteScalar(connection, transaction, "SELECT LAST_INSERT_ID()"));
                    _audit.Log(connection, transaction, actorId, "PROGRAM_CREATE", "Program", id.ToString(),
                        $"Created program '{code} - {name}'.");
                    return id;
                });

                if (createdId > 0)
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
                string code = model.ProgramCode.Trim();
                string name = model.ProgramName.Trim();
                string result = _dbHelper.ExecuteInTransaction((connection, transaction) =>
                {
                    DataTable currentRows = _dbHelper.ExecuteQuery(
                        connection,
                        transaction,
                        "SELECT program_code, program_name, description, status FROM programs WHERE program_id = @Id FOR UPDATE",
                        new Dictionary<string, object> { { "@Id", id } });
                    if (currentRows.Rows.Count == 0)
                    {
                        return "NOT_FOUND";
                    }

                    DataRow current = currentRows.Rows[0];
                    int rowsAffected = _dbHelper.ExecuteNonQuery(
                        connection,
                        transaction,
                        @"UPDATE programs
                          SET program_code = @Code, program_name = @Name, description = @Desc, status = @Status
                          WHERE program_id = @Id",
                        new Dictionary<string, object>
                        {
                            { "@Code", code },
                            { "@Name", name },
                            { "@Desc", (object?)model.Description?.Trim() ?? DBNull.Value },
                            { "@Status", model.Status },
                            { "@Id", id }
                        });
                    if (rowsAffected == 0)
                    {
                        return "UNCHANGED";
                    }

                    string oldDescription = current["description"] == DBNull.Value ? string.Empty : Convert.ToString(current["description"]) ?? string.Empty;
                    string newDescription = model.Description?.Trim() ?? string.Empty;
                    var changedFields = new List<string>();
                    if (!string.Equals(Convert.ToString(current["program_code"]), code, StringComparison.Ordinal))
                    {
                        changedFields.Add("code");
                    }
                    if (!string.Equals(Convert.ToString(current["program_name"]), name, StringComparison.Ordinal))
                    {
                        changedFields.Add("name");
                    }
                    if (!string.Equals(oldDescription, newDescription, StringComparison.Ordinal))
                    {
                        changedFields.Add("description");
                    }
                    if (!string.Equals(Convert.ToString(current["status"]), model.Status, StringComparison.Ordinal))
                    {
                        changedFields.Add("status");
                    }

                    if (changedFields.Count > 0)
                    {
                        int actorId = GetCurrentUserId();
                        _audit.Log(connection, transaction, actorId, "PROGRAM_UPDATE", "Program", id.ToString(),
                            $"Updated program '{code} - {name}'; changed {string.Join(", ", changedFields)}.");
                    }
                    return "UPDATED";
                });
                if (result == "UPDATED" || result == "UNCHANGED")
                {
                    TempData["SuccessMessage"] = "Program updated successfully!";
                    return RedirectToAction(nameof(Index));
                }

                return NotFound();
            }

            return View(model);
        }

        // POST: Program/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            try
            {
                bool deleted = _dbHelper.ExecuteInTransaction((connection, transaction) =>
                {
                    DataTable rows = _dbHelper.ExecuteQuery(
                        connection,
                        transaction,
                        "SELECT program_code, program_name FROM programs WHERE program_id = @Id FOR UPDATE",
                        new Dictionary<string, object> { { "@Id", id } });
                    if (rows.Rows.Count == 0)
                    {
                        return false;
                    }

                    string code = Convert.ToString(rows.Rows[0]["program_code"]) ?? string.Empty;
                    string name = Convert.ToString(rows.Rows[0]["program_name"]) ?? string.Empty;
                    int affected = _dbHelper.ExecuteNonQuery(
                        connection,
                        transaction,
                        "DELETE FROM programs WHERE program_id = @Id",
                        new Dictionary<string, object> { { "@Id", id } });
                    if (affected > 0)
                    {
                        _audit.Log(connection, transaction, GetCurrentUserId(), "PROGRAM_DELETE", "Program", id.ToString(),
                            $"Deleted program '{code} - {name}'.");
                    }
                    return affected > 0;
                });
                if (deleted)
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

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                ? id
                : throw new InvalidOperationException("The authenticated user ID is missing.");
        }
    }
}