using System.Data;
using System.Security.Claims;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace EnrollmentSystem_G4.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UserController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly PasswordService _passwords;
        private readonly AuditLogger _audit;

        public UserController(DatabaseHelper db, PasswordService passwords, AuditLogger audit)
        {
            _db = db;
            _passwords = passwords;
            _audit = audit;
        }

        public IActionResult Index()
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT user_id, username, first_name, last_name, email, role, is_active, created_at
                  FROM users
                  ORDER BY username");
            var users = new List<User>();
            foreach (DataRow row in rows.Rows)
            {
                users.Add(new User
                {
                    UserId = Convert.ToInt32(row["user_id"]),
                    Username = Convert.ToString(row["username"]) ?? string.Empty,
                    FirstName = Convert.ToString(row["first_name"]) ?? string.Empty,
                    LastName = Convert.ToString(row["last_name"]) ?? string.Empty,
                    Email = Convert.ToString(row["email"]) ?? string.Empty,
                    Role = NormalizeRole(Convert.ToString(row["role"]) ?? string.Empty),
                    IsActive = Convert.ToBoolean(row["is_active"]),
                    CreatedAt = Convert.ToDateTime(row["created_at"])
                });
            }

            return View(users);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string username = model.Username.Trim();
            var newUser = new User { Username = username, Role = model.Role };
            _passwords.HashPassword(newUser, model.Password);

            try
            {
                int actorId = GetCurrentUserId();
                _db.ExecuteInTransaction((connection, transaction) =>
                {
                    _db.ExecuteNonQuery(
                        connection,
                        transaction,
                        @"INSERT INTO users
                            (username, password_hash, password_salt, first_name, last_name, email,
                             role, status, is_active)
                          VALUES
                            (@Username, @PasswordHash, @PasswordSalt, @FirstName, @LastName, @Email,
                             @Role, 'Active', 1)",
                        new Dictionary<string, object>
                        {
                            { "@Username", newUser.Username },
                            { "@PasswordHash", newUser.PasswordHash },
                            { "@PasswordSalt", newUser.PasswordSalt },
                            { "@FirstName", model.FirstName.Trim() },
                            { "@LastName", model.LastName.Trim() },
                            { "@Email", model.Email.Trim() },
                            { "@Role", newUser.Role }
                        });

                    int createdId = Convert.ToInt32(_db.ExecuteScalar(
                        connection,
                        transaction,
                        "SELECT LAST_INSERT_ID()"));
                    _audit.Log(
                        connection,
                        transaction,
                        actorId,
                        "USER_CREATE",
                        "User",
                        createdId.ToString(),
                        $"Created {newUser.Role} account '{newUser.Username}'.");
                    return createdId;
                });
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                ModelState.AddModelError(string.Empty, "The username or email address is already in use.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Account '{username}' was created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT user_id, username, first_name, last_name, email, role
                  FROM users
                  WHERE user_id = @Id",
                new Dictionary<string, object> { { "@Id", id } });
            if (rows.Rows.Count == 0)
            {
                return NotFound();
            }

            DataRow row = rows.Rows[0];
            return View(new EditUserViewModel
            {
                UserId = Convert.ToInt32(row["user_id"]),
                Username = Convert.ToString(row["username"]) ?? string.Empty,
                FirstName = Convert.ToString(row["first_name"]) ?? string.Empty,
                LastName = Convert.ToString(row["last_name"]) ?? string.Empty,
                Email = Convert.ToString(row["email"]) ?? string.Empty,
                Role = NormalizeRole(Convert.ToString(row["role"]) ?? string.Empty)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, EditUserViewModel model)
        {
            if (id != model.UserId)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            int actorId = GetCurrentUserId();
            string username = model.Username.Trim();
            string firstName = model.FirstName.Trim();
            string lastName = model.LastName.Trim();
            string email = model.Email.Trim();

            try
            {
                string result = _db.ExecuteInTransaction((connection, transaction) =>
                {
                    DataTable activeAdministrators = model.Role != "Administrator"
                        ? LockActiveAdministrators(connection, transaction)
                        : new DataTable();
                    DataTable currentRows = _db.ExecuteQuery(
                        connection,
                        transaction,
                        @"SELECT username, first_name, last_name, email, role, is_active
                          FROM users
                          WHERE user_id = @Id
                          FOR UPDATE",
                        new Dictionary<string, object> { { "@Id", id } });
                    if (currentRows.Rows.Count == 0)
                    {
                        return "NOT_FOUND";
                    }

                    DataRow current = currentRows.Rows[0];
                    string currentRole = NormalizeRole(Convert.ToString(current["role"]) ?? string.Empty);
                    bool isActive = Convert.ToBoolean(current["is_active"]);

                    if (actorId == id && model.Role != currentRole)
                    {
                        return "CANNOT_CHANGE_OWN_ROLE";
                    }

                    if (isActive && currentRole == "Administrator" && model.Role != "Administrator")
                    {
                        if (activeAdministrators.Rows.Count <= 1)
                        {
                            return "LAST_ADMINISTRATOR";
                        }
                    }

                    var changedFields = new List<string>();
                    AddChangedField(changedFields, "username", Convert.ToString(current["username"]), username);
                    AddChangedField(changedFields, "first name", Convert.ToString(current["first_name"]), firstName);
                    AddChangedField(changedFields, "last name", Convert.ToString(current["last_name"]), lastName);
                    AddChangedField(changedFields, "email", Convert.ToString(current["email"]), email);
                    if (currentRole != model.Role)
                    {
                        changedFields.Add($"role ({currentRole} to {model.Role})");
                    }

                    int updated = _db.ExecuteNonQuery(
                        connection,
                        transaction,
                        @"UPDATE users
                          SET username = @Username, first_name = @FirstName, last_name = @LastName,
                              email = @Email, role = @Role
                          WHERE user_id = @Id",
                        new Dictionary<string, object>
                        {
                            { "@Username", username },
                            { "@FirstName", firstName },
                            { "@LastName", lastName },
                            { "@Email", email },
                            { "@Role", model.Role },
                            { "@Id", id }
                        });

                    if (updated > 0 && changedFields.Count > 0)
                    {
                        _audit.Log(
                            connection,
                            transaction,
                            actorId,
                            "USER_UPDATE",
                            "User",
                            id.ToString(),
                            $"Updated account {id}; changed {string.Join(", ", changedFields)}.");
                    }

                    return "UPDATED";
                });

                if (result == "NOT_FOUND")
                {
                    return NotFound();
                }
                if (result == "CANNOT_CHANGE_OWN_ROLE")
                {
                    ModelState.AddModelError(nameof(model.Role), "You cannot change your own Administrator role.");
                    return View(model);
                }
                if (result == "LAST_ADMINISTRATOR")
                {
                    ModelState.AddModelError(nameof(model.Role), "At least one active Administrator account must remain.");
                    return View(model);
                }
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                ModelState.AddModelError(string.Empty, "The username or email address is already in use.");
                return View(model);
            }

            TempData["SuccessMessage"] = "User account updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Disable(int id)
        {
            int actorId = GetCurrentUserId();
            if (actorId == id)
            {
                TempData["ErrorMessage"] = "You cannot disable your own account.";
                return RedirectToAction(nameof(Index));
            }

            string result = _db.ExecuteInTransaction((connection, transaction) =>
            {
                DataTable activeAdministrators = LockActiveAdministrators(connection, transaction);
                DataTable targetRows = _db.ExecuteQuery(
                    connection,
                    transaction,
                    "SELECT role, is_active FROM users WHERE user_id = @Id FOR UPDATE",
                    new Dictionary<string, object> { { "@Id", id } });
                if (targetRows.Rows.Count == 0 || !Convert.ToBoolean(targetRows.Rows[0]["is_active"]))
                {
                    return "NOT_FOUND";
                }

                string targetRole = NormalizeRole(Convert.ToString(targetRows.Rows[0]["role"]) ?? string.Empty);
                if (targetRole == "Administrator")
                {
                    if (activeAdministrators.Rows.Count <= 1)
                    {
                        return "LAST_ADMINISTRATOR";
                    }
                }

                int updated = _db.ExecuteNonQuery(
                    connection,
                    transaction,
                    "UPDATE users SET is_active = 0, status = 'Inactive' WHERE user_id = @Id AND is_active = 1",
                    new Dictionary<string, object> { { "@Id", id } });
                if (updated > 0)
                {
                    _audit.Log(
                        connection,
                        transaction,
                        actorId,
                        "USER_DISABLE",
                        "User",
                        id.ToString(),
                        $"Disabled account {id}.");
                }

                return updated > 0 ? "DISABLED" : "NOT_FOUND";
            });

            TempData[result == "DISABLED" ? "SuccessMessage" : "ErrorMessage"] = result switch
            {
                "DISABLED" => "User account disabled.",
                "LAST_ADMINISTRATOR" => "The last active Administrator account cannot be disabled.",
                _ => "Account not found or already disabled."
            };
            return RedirectToAction(nameof(Index));
        }

        private DataTable LockActiveAdministrators(MySqlConnection connection, MySqlTransaction transaction)
        {
            return _db.ExecuteQuery(
                connection,
                transaction,
                @"SELECT user_id
                  FROM users
                  WHERE is_active = 1 AND role IN ('Administrator', 'Admin')
                  ORDER BY user_id
                  FOR UPDATE");
        }

        private static void AddChangedField(List<string> changedFields, string field, string? oldValue, string? newValue)
        {
            if (!string.Equals(oldValue?.Trim(), newValue?.Trim(), StringComparison.Ordinal))
            {
                changedFields.Add(field);
            }
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                ? id
                : throw new InvalidOperationException("The authenticated user ID is missing.");
        }

        private static string NormalizeRole(string role)
        {
            return role == "Admin" ? "Administrator" : role;
        }
    }
}
