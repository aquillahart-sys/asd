using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace EnrollmentSystem_G4.Data
{
    public class AuditLogger
    {
        private readonly DatabaseHelper _db;

        public AuditLogger(DatabaseHelper db)
        {
            _db = db;
        }

        public void Log(
            int? userId,
            string actionType,
            string? entityType,
            string? entityId,
            string details)
        {
            _db.ExecuteNonQuery(
                @"INSERT INTO audit_logs
                    (user_id, action_type, entity_type, entity_id, details)
                  VALUES (@UserId, @ActionType, @EntityType, @EntityId, @Details)",
                CreateParameters(userId, actionType, entityType, entityId, details));
        }

        public void Log(
            MySqlConnection connection,
            MySqlTransaction transaction,
            int? userId,
            string actionType,
            string? entityType,
            string? entityId,
            string details)
        {
            _db.ExecuteNonQuery(
                connection,
                transaction,
                @"INSERT INTO audit_logs
                    (user_id, action_type, entity_type, entity_id, details)
                  VALUES (@UserId, @ActionType, @EntityType, @EntityId, @Details)",
                CreateParameters(userId, actionType, entityType, entityId, details));
        }

        private static Dictionary<string, object> CreateParameters(
            int? userId,
            string actionType,
            string? entityType,
            string? entityId,
            string details)
        {
            return new Dictionary<string, object>
            {
                { "@UserId", userId.HasValue ? userId.Value : DBNull.Value },
                { "@ActionType", actionType },
                { "@EntityType", (object?)entityType ?? DBNull.Value },
                { "@EntityId", (object?)entityId ?? DBNull.Value },
                { "@Details", details }
            };
        }
    }
}
