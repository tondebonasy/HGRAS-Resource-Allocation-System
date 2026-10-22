using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Principal;

namespace Pgh.Workflow
{
    public sealed class RequestHistoryEntry
    {
        public long Id { get; set; }
        public long RequestId { get; set; }
        public string FromStage { get; set; }
        public string ToStage { get; set; }
        public string Actor { get; set; }
        public string Note { get; set; }
        public DateTime OccurredAtUtc { get; set; }
    }

    public sealed class RequestedItem
    {
        public long ResourceId { get; set; }
        public long Quantity { get; set; }
    }

    // Shared by the two portals. The database transaction is the allocation boundary.
    public static class RequestWorkflow
    {
        public static string[] NextStages(string stage, IPrincipal actor)
        {
            if (actor == null || actor.Identity == null || !actor.Identity.IsAuthenticated) return new string[0];
            var admin = actor.IsInRole("ADMIN");
            switch ((stage ?? "").ToUpperInvariant())
            {
                case "INITIAL": return admin || actor.IsInRole("APPROVER") ? new[] { "APPROVED", "REJECTED" } : new string[0];
                case "APPROVED": return admin || actor.IsInRole("RELEASER") ? new[] { "RELEASED" } : new string[0];
                case "RELEASED": return admin || actor.IsInRole("DISPATCHER") ? new[] { "DISPATCHED" } : new string[0];
                case "DISPATCHED": return admin || actor.IsInRole("DISPATCHER") ? new[] { "COMPLETED" } : new string[0];
                default: return new string[0];
            }
        }

        private static SqlCommand Command(SqlConnection connection, SqlTransaction transaction, string sql, params object[] pairs)
        {
            var command = new SqlCommand(sql, connection, transaction);
            for (var i = 0; i < pairs.Length; i += 2) command.Parameters.AddWithValue((string)pairs[i], pairs[i + 1] ?? DBNull.Value);
            return command;
        }

        private sealed class LockedRequest
        {
            public long StageId;
            public long? CompanyId;
            public string Stage;
        }

        private static LockedRequest Lock(SqlConnection connection, SqlTransaction transaction, long id)
        {
            using (var command = Command(connection, transaction,
                "SELECT r.StageId,r.CompanyId,s.Name FROM dbo.Request r WITH(UPDLOCK,HOLDLOCK) JOIN dbo.RequestStage s ON s.id=r.StageId WHERE r.Id=@id", "@id", id))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) throw new InvalidOperationException("This request no longer exists or has no valid stage.");
                return new LockedRequest { StageId = reader.GetInt64(0), CompanyId = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1), Stage = reader.GetString(2) };
            }
        }

        private static void History(SqlConnection connection, SqlTransaction transaction, long id, string from, string to, string actor, string note)
        {
            using (var command = Command(connection, transaction,
                "INSERT dbo.RequestHistory(RequestId,FromStage,ToStage,Actor,Note) VALUES(@id,@from,@to,@actor,@note)",
                "@id", id, "@from", from, "@to", to, "@actor", actor, "@note", note)) command.ExecuteNonQuery();
        }

        public static void Transition(string connectionString, long id, long expectedStageId, long targetStageId, IPrincipal actor, string note)
        {
            if (actor == null || actor.Identity == null || !actor.Identity.IsAuthenticated) throw new UnauthorizedAccessException("Sign in to continue.");
            if ((note ?? "").Length > 1000) throw new InvalidOperationException("Keep the review note within 1,000 characters.");
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var request = Lock(connection, transaction, id);
                    if (request.StageId != expectedStageId) throw new InvalidOperationException("Another user has updated this request. Reload it before continuing.");
                    string target;
                    using (var command = Command(connection, transaction, "SELECT Name FROM dbo.RequestStage WHERE id=@id", "@id", targetStageId)) target = command.ExecuteScalar() as string;
                    if (target == null || !NextStages(request.Stage, actor).Contains(target, StringComparer.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("Your role cannot make that transition from the current stage.");
                    if (target == "REJECTED" && String.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("Give a reason when rejecting a request.");
                    if (target == "APPROVED")
                    {
                        var items = new List<RequestedItem>();
                        using (var command = Command(connection, transaction, "SELECT resourceId,SUM([Count]) FROM dbo.ResourceRequest WHERE requestId=@id GROUP BY resourceId ORDER BY resourceId", "@id", id))
                        using (var reader = command.ExecuteReader()) while (reader.Read()) items.Add(new RequestedItem { ResourceId = reader.GetInt64(0), Quantity = reader.GetInt64(1) });
                        if (items.Count == 0) throw new InvalidOperationException("A request must contain resources before it can be approved.");
                        foreach (var item in items)
                        {
                            if (item.Quantity <= 0) throw new InvalidOperationException("Resource quantities must be positive.");
                            using (var command = Command(connection, transaction,
                                "UPDATE dbo.Resource SET Quantity=Quantity-@quantity WHERE id=@resource AND Quantity>=@quantity", "@quantity", item.Quantity, "@resource", item.ResourceId))
                                if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Insufficient available stock. No items were allocated; review the resource catalogue.");
                            using (var command = Command(connection, transaction,
                                "INSERT dbo.StockAllocation(RequestId,ResourceId,Quantity) VALUES(@id,@resource,@quantity)", "@id", id, "@resource", item.ResourceId, "@quantity", item.Quantity)) command.ExecuteNonQuery();
                        }
                    }
                    using (var command = Command(connection, transaction, "UPDATE dbo.Request SET StageId=@stage WHERE Id=@id", "@stage", targetStageId, "@id", id)) command.ExecuteNonQuery();
                    History(connection, transaction, id, request.Stage, target, actor.Identity.Name, note);
                    transaction.Commit();
                }
            }
        }

        public static void UpdateInitial(string connectionString, long id, long companyId, string actor, string description, string additionalInfo, IList<RequestedItem> items)
        {
            if (String.IsNullOrWhiteSpace(description) || description.Length > 500 || (additionalInfo ?? "").Length > 250)
                throw new InvalidOperationException("Provide a description within 500 characters and additional information within 250 characters.");
            if (items == null || items.Count == 0 || items.Any(i => i.Quantity <= 0) || items.Select(i => i.ResourceId).Distinct().Count() != items.Count)
                throw new InvalidOperationException("Select resources with positive quantities.");
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var request = Lock(connection, transaction, id);
                    if (request.CompanyId != companyId) throw new UnauthorizedAccessException("This request belongs to another organisation.");
                    if (request.Stage != "INITIAL") throw new InvalidOperationException("Only initial requests can be edited.");
                    using (var command = Command(connection, transaction, "DELETE dbo.ResourceRequest WHERE requestId=@id", "@id", id)) command.ExecuteNonQuery();
                    foreach (var item in items)
                        using (var command = Command(connection, transaction, "INSERT dbo.ResourceRequest(requestId,resourceId,[Count]) VALUES(@id,@resource,@quantity)", "@id", id, "@resource", item.ResourceId, "@quantity", item.Quantity)) command.ExecuteNonQuery();
                    using (var command = Command(connection, transaction, "UPDATE dbo.Request SET Description=@description,additionalInfo=@info WHERE Id=@id", "@id", id, "@description", description, "@info", additionalInfo)) command.ExecuteNonQuery();
                    History(connection, transaction, id, "INITIAL", "INITIAL", actor, "Request details updated.");
                    transaction.Commit();
                }
            }
        }

        public static void DeleteInitial(string connectionString, long id, long? companyId, IPrincipal actor)
        {
            if (actor == null || actor.Identity == null || !actor.Identity.IsAuthenticated) throw new UnauthorizedAccessException("Sign in to continue.");
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var request = Lock(connection, transaction, id);
                    if (!companyId.HasValue ? !actor.IsInRole("ADMIN") : request.CompanyId != companyId) throw new UnauthorizedAccessException("You cannot delete this request.");
                    if (request.Stage != "INITIAL") throw new InvalidOperationException("Only initial requests can be deleted. Allocated stock and audit history are protected.");
                    History(connection, transaction, id, request.Stage, "DELETED", actor.Identity.Name, "Initial request deleted.");
                    using (var command = Command(connection, transaction, "DELETE dbo.ResourceRequest WHERE requestId=@id; DELETE dbo.Request WHERE Id=@id", "@id", id)) command.ExecuteNonQuery();
                    transaction.Commit();
                }
            }
        }
    }
}
