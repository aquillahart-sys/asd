using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EnrollmentSystem_G4.Data
{
    public class UserStatusCookieEvents : CookieAuthenticationEvents
    {
        private readonly DatabaseHelper _db;

        public UserStatusCookieEvents(DatabaseHelper db)
        {
            _db = db;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            string? userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out int id))
            {
                await RejectPrincipal(context);
                return;
            }

            DataTable users = _db.ExecuteQuery(
                "SELECT username, role, is_active FROM users WHERE user_id = @Id",
                new Dictionary<string, object> { { "@Id", id } });
            if (users.Rows.Count == 0)
            {
                await RejectPrincipal(context);
                return;
            }

            DataRow user = users.Rows[0];
            string currentUsername = Convert.ToString(user["username"]) ?? string.Empty;
            string currentRole = NormalizeRole(Convert.ToString(user["role"]) ?? string.Empty);
            string? claimUsername = context.Principal?.Identity?.Name;
            string? claimRole = context.Principal?.FindFirstValue(ClaimTypes.Role);
            if (!Convert.ToBoolean(user["is_active"]) ||
                !string.Equals(currentUsername, claimUsername, StringComparison.Ordinal) ||
                !string.Equals(currentRole, claimRole, StringComparison.Ordinal))
            {
                await RejectPrincipal(context);
            }
        }

        private static string NormalizeRole(string role)
        {
            return role == "Admin" ? "Administrator" : role;
        }

        private static async Task RejectPrincipal(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync("EnrollmentCookie");
        }
    }
}
