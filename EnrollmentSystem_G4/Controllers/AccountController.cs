using System.Data;
using System.Security.Claims;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnrollmentSystem_G4.Controllers
{
    public class AccountController : Controller
    {
        private static readonly HashSet<string> SupportedRoles =
            new(StringComparer.Ordinal) { "Administrator", "Registrar", "Cashier" };

        private readonly DatabaseHelper _db;
        private readonly PasswordService _passwords;
        private readonly AuditLogger _audit;

        public AccountController(DatabaseHelper db, PasswordService passwords, AuditLogger audit)
        {
            _db = db;
            _passwords = passwords;
            _audit = audit;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string username = model.Username.Trim();
            DataTable users = _db.ExecuteQuery(
                @"SELECT user_id, username, password_hash, password_salt, role
                  FROM users
                  WHERE username = @Username AND is_active = 1
                  LIMIT 1",
                new Dictionary<string, object> { { "@Username", username } });

            User? user = null;
            if (users.Rows.Count > 0)
            {
                DataRow row = users.Rows[0];
                user = new User
                {
                    UserId = Convert.ToInt32(row["user_id"]),
                    Username = Convert.ToString(row["username"]) ?? string.Empty,
                    PasswordHash = Convert.ToString(row["password_hash"]) ?? string.Empty,
                    PasswordSalt = Convert.ToString(row["password_salt"]) ?? string.Empty,
                    Role = NormalizeRole(Convert.ToString(row["role"]) ?? string.Empty)
                };
            }

            if (user == null ||
                !_passwords.VerifyPassword(user, model.Password) ||
                !SupportedRoles.Contains(user.Role))
            {
                _audit.Log(null, "LOGIN_FAILURE", "User", username, "Invalid login attempt.");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };
            var identity = new ClaimsIdentity(claims, "EnrollmentCookie");
            await HttpContext.SignInAsync("EnrollmentCookie", new ClaimsPrincipal(identity));

            _audit.Log(
                user.UserId,
                "LOGIN_SUCCESS",
                "User",
                user.UserId.ToString(),
                $"User {user.Username} logged in.");

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return user.Role == "Cashier"
                ? RedirectToAction("Index", "Payment")
                : RedirectToAction("Index", "Student");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int id)
                ? id
                : null;
            string username = User.Identity?.Name ?? "Unknown";

            try
            {
                _audit.Log(userId, "LOGOUT", "User", userId?.ToString(), $"User {username} logged out.");
            }
            finally
            {
                await HttpContext.SignOutAsync("EnrollmentCookie");
            }

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }

        private static string NormalizeRole(string role)
        {
            return role == "Admin" ? "Administrator" : role;
        }
    }
}
