using System.Data;
using EnrollmentSystem_G4.Data;
using EnrollmentSystem_G4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnrollmentSystem_G4.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AuditController : Controller
    {
        private readonly DatabaseHelper _db;

        public AuditController(DatabaseHelper db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            DataTable rows = _db.ExecuteQuery(
                @"SELECT a.audit_id, a.user_id, u.username, a.action_type, a.entity_type,
                         a.entity_id, a.details, a.created_at
                  FROM audit_logs a
                  LEFT JOIN users u ON a.user_id = u.user_id
                  ORDER BY a.created_at DESC, a.audit_id DESC
                  LIMIT 500");

            var entries = new List<AuditEntry>();
            foreach (DataRow row in rows.Rows)
            {
                entries.Add(new AuditEntry
                {
                    AuditId = Convert.ToInt64(row["audit_id"]),
                    UserId = row["user_id"] == DBNull.Value ? null : Convert.ToInt32(row["user_id"]),
                    Username = Convert.ToString(row["username"]) ?? "Anonymous",
                    ActionType = Convert.ToString(row["action_type"]) ?? string.Empty,
                    EntityType = row["entity_type"] == DBNull.Value ? null : Convert.ToString(row["entity_type"]),
                    EntityId = row["entity_id"] == DBNull.Value ? null : Convert.ToString(row["entity_id"]),
                    Details = row["details"] == DBNull.Value ? null : Convert.ToString(row["details"]),
                    CreatedAt = Convert.ToDateTime(row["created_at"])
                });
            }

            return View(entries);
        }
    }
}
