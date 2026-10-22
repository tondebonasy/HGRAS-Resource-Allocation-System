using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Hgras.Network
{
    public sealed class NetworkWorkspace
    {
        public long GroupId { get; set; }
        public string GroupName { get; set; }
        public string Role { get; set; }
        public long? FacilityId { get; set; }
        public DataTable Groups { get; set; }
        public DataTable Facilities { get; set; }
        public DataTable Departments { get; set; }
        public DataTable Stock { get; set; }
        public DataTable Transfers { get; set; }
        public DataTable Audit { get; set; }
        public DataTable Operations { get; set; }
        public DataTable Members { get; set; }
        public DataTable Notices { get; set; }
        public DateTime RetrievedAtUtc { get; set; }
    }

    // This service is the tenant and inventory boundary for the network workspace.
    // Its tables are deliberately separate from the original organisation request workflow.
    public sealed class NetworkService
    {
        private readonly string connectionString;
        private readonly string userId;
        private readonly string actor;
        public NetworkService(string connectionString, string userId, string actor)
        { this.connectionString = connectionString; this.userId = userId; this.actor = actor; }

        private SqlConnection Open() { var c = new SqlConnection(connectionString); c.Open(); return c; }
        private static SqlCommand Command(SqlConnection c, SqlTransaction t, string sql, params object[] values)
        {
            var cmd = new SqlCommand(sql, c, t);
            for (int i = 0; i < values.Length; i += 2) cmd.Parameters.AddWithValue((string)values[i], values[i + 1] ?? DBNull.Value);
            return cmd;
        }
        private static DataTable Read(SqlConnection c, SqlTransaction t, string sql, params object[] values)
        { using (var cmd = Command(c,t,sql,values)) using (var r = cmd.ExecuteReader()) { var table = new DataTable(); table.Load(r); return table; } }
        private static void Execute(SqlConnection c, SqlTransaction t, string sql, params object[] values)
        { using (var cmd = Command(c,t,sql,values)) cmd.ExecuteNonQuery(); }
        private static long Insert(SqlConnection c, SqlTransaction t, string sql, params object[] values)
        { using (var cmd = Command(c,t,sql + "; SELECT CAST(SCOPE_IDENTITY() AS bigint)",values)) return (long)cmd.ExecuteScalar(); }
        private sealed class Access { public string Role; public long? Facility; }
        private Access Authorize(SqlConnection c, SqlTransaction t, long group)
        {
            var rows = Read(c,t,"SELECT Role,FacilityId FROM dbo.HgrasMember WITH(HOLDLOCK) WHERE GroupId=@g AND UserId=@u", "@g",group,"@u",userId);
            if (rows.Rows.Count != 1) throw new UnauthorizedAccessException("You are not a member of this hospital group.");
            var row=rows.Rows[0]; return new Access { Role=(string)row["Role"], Facility=row.IsNull("FacilityId")?(long?)null:(long)row["FacilityId"] };
        }
        private static void Permit(Access access, long? facility, params string[] roles)
        {
            if (access.Role == "GROUP_ADMIN") return;
            if (!roles.Contains(access.Role) || !facility.HasValue || access.Facility != facility)
                throw new UnauthorizedAccessException("Your hospital assignment and role do not permit this action.");
        }
        private static void Facility(SqlConnection c, SqlTransaction t, long group, long facility)
        { if (Read(c,t,"SELECT Id FROM dbo.HgrasFacility WHERE GroupId=@g AND Id=@f","@g",group,"@f",facility).Rows.Count!=1) throw new InvalidOperationException("Choose a facility in this group."); }
        private void Audit(SqlConnection c, SqlTransaction t, long group, long? facility, long? transfer, string action, string note)
        {
            Execute(c,t,"INSERT dbo.HgrasAudit(GroupId,FacilityId,TransferId,Actor,Action,Note) VALUES(@g,@f,@r,@a,@action,@n)","@g",group,"@f",facility,"@r",transfer,"@a",actor,"@action",action,"@n",note);
            if(transfer.HasValue) Execute(c,t,"INSERT dbo.HgrasNotice(GroupId,FacilityId,TransferId,Subject,Message) SELECT @g,FromFacilityId,Id,@subject,@n FROM dbo.HgrasTransfer WHERE GroupId=@g AND Id=@id UNION ALL SELECT @g,ToFacilityId,Id,@subject,@n FROM dbo.HgrasTransfer WHERE GroupId=@g AND Id=@id","@g",group,"@id",transfer,"@subject","Transfer #"+transfer+": "+action,"@n",note);
        }
        private static string Text(string value, string field, int length)
        { if (String.IsNullOrWhiteSpace(value) || value.Trim().Length>length) throw new InvalidOperationException(field+" is required and must be within "+length+" characters."); return value.Trim(); }

        public NetworkWorkspace Load(long? groupId)
        {
            using (var c=Open())
            {
                var groups=Read(c,null,"SELECT g.Id,g.Name FROM dbo.HgrasGroup g JOIN dbo.HgrasMember m ON m.GroupId=g.Id WHERE m.UserId=@u ORDER BY g.Name","@u",userId);
                var result=new NetworkWorkspace {Groups=groups,RetrievedAtUtc=DateTime.UtcNow};
                if (groups.Rows.Count==0) return result;
                var group=groupId??(long)groups.Rows[0]["Id"]; var access=Authorize(c,null,group);
                result.GroupId=group; result.GroupName=(string)groups.Select("Id="+group)[0]["Name"]; result.Role=access.Role;result.FacilityId=access.Facility;
                result.Facilities=Read(c,null,"SELECT Id,Name,Location,Officer FROM dbo.HgrasFacility WHERE GroupId=@g ORDER BY Name","@g",group);
                // Group members see eligible stock to request sharing. Audit and personnel records remain scoped.
                result.Stock=Read(c,null,"SELECT s.*,f.Name FacilityName,CASE WHEN (ExpiresOn IS NULL OR ExpiresOn>CAST(SYSUTCDATETIME() AS date)) AND Quantity-Reserved-MinimumReserve>0 THEN Quantity-Reserved-MinimumReserve ELSE 0 END Shareable FROM dbo.HgrasStock s JOIN dbo.HgrasFacility f ON f.Id=s.FacilityId AND f.GroupId=s.GroupId WHERE s.GroupId=@g ORDER BY s.Name,f.Name","@g",group);
                result.Departments=Read(c,null,"SELECT d.*,f.Name FacilityName FROM dbo.HgrasDepartment d JOIN dbo.HgrasFacility f ON f.Id=d.FacilityId WHERE d.GroupId=@g AND (@f IS NULL OR d.FacilityId=@f)","@g",group,"@f",access.Facility);
                result.Transfers=Read(c,null,"SELECT t.*,s.Name ResourceName,s.Unit,ff.Name FromName,tf.Name ToName FROM dbo.HgrasTransfer t JOIN dbo.HgrasStock s ON s.Id=t.StockId JOIN dbo.HgrasFacility ff ON ff.Id=t.FromFacilityId JOIN dbo.HgrasFacility tf ON tf.Id=t.ToFacilityId WHERE t.GroupId=@g AND (@f IS NULL OR t.FromFacilityId=@f OR t.ToFacilityId=@f) ORDER BY CASE Priority WHEN 'Critical' THEN 0 WHEN 'Urgent' THEN 1 ELSE 2 END,t.CreatedAtUtc DESC","@g",group,"@f",access.Facility);
                result.Audit=Read(c,null,"SELECT TOP 100 * FROM dbo.HgrasAudit WHERE GroupId=@g AND (@f IS NULL OR FacilityId=@f OR TransferId IN (SELECT Id FROM dbo.HgrasTransfer WHERE GroupId=@g AND (FromFacilityId=@f OR ToFacilityId=@f))) ORDER BY Id DESC","@g",group,"@f",access.Facility);
                result.Operations=Read(c,null,"SELECT o.*,f.Name FacilityName FROM dbo.HgrasOperation o JOIN dbo.HgrasFacility f ON f.Id=o.FacilityId WHERE o.GroupId=@g AND (@f IS NULL OR o.FacilityId=@f) ORDER BY o.Kind,o.Name","@g",group,"@f",access.Facility);
                result.Members=access.Role=="GROUP_ADMIN"?Read(c,null,"SELECT m.Role,m.FacilityId,u.Email,f.Name FacilityName FROM dbo.HgrasMember m JOIN dbo.AspNetUsers u ON u.Id=m.UserId LEFT JOIN dbo.HgrasFacility f ON f.Id=m.FacilityId WHERE m.GroupId=@g","@g",group):new DataTable();
                result.Notices=Read(c,null,"SELECT TOP 30 n.* FROM dbo.HgrasNotice n WHERE n.GroupId=@g AND (@f IS NULL OR n.FacilityId=@f) AND NOT EXISTS(SELECT 1 FROM dbo.HgrasNoticeRead r WHERE r.NoticeId=n.Id AND r.UserId=@u) ORDER BY n.Id DESC","@g",group,"@f",access.Facility,"@u",userId);
                return result;
            }
        }
        public void Acknowledge(long group,long notice)
        {
            using(var c=Open()) using(var t=c.BeginTransaction(IsolationLevel.Serializable))
            {
                var access=Authorize(c,t,group);
                var rows=Read(c,t,"SELECT Id FROM dbo.HgrasNotice WHERE GroupId=@g AND Id=@id AND (@f IS NULL OR FacilityId=@f)","@g",group,"@id",notice,"@f",access.Facility);
                if(rows.Rows.Count!=1) throw new UnauthorizedAccessException("This notification is outside your hospital scope.");
                Execute(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.HgrasNoticeRead WHERE NoticeId=@id AND UserId=@u) INSERT dbo.HgrasNoticeRead(NoticeId,UserId) VALUES(@id,@u)","@id",notice,"@u",userId);t.Commit();
            }
        }

        public long CreateGroup(string name)
        {
            name=Text(name,"Group name",160);
            using(var c=Open()) using(var t=c.BeginTransaction())
            {
                var id=Insert(c,t,"INSERT dbo.HgrasGroup(Name) VALUES(@n)","@n",name);
                Execute(c,t,"INSERT dbo.HgrasMember(GroupId,UserId,Role) VALUES(@g,@u,'GROUP_ADMIN')","@g",id,"@u",userId);
                Audit(c,t,id,null,null,"GROUP_CREATED",name);t.Commit();return id;
            }
        }
        public void AddFacility(long group,string name,string location,string officer)
        {
            name=Text(name,"Facility name",160);location=Text(location,"Location",200);officer=Text(officer,"Responsible officer",160);
            using(var c=Open()) using(var t=c.BeginTransaction()) {Permit(Authorize(c,t,group),null);var id=Insert(c,t,"INSERT dbo.HgrasFacility(GroupId,Name,Location,Officer) VALUES(@g,@n,@l,@o)","@g",group,"@n",name,"@l",location,"@o",officer);Audit(c,t,group,id,null,"FACILITY_CREATED",name);t.Commit();}
        }
        public void AddDepartment(long group,long facility,string name,string officer)
        {
            name=Text(name,"Department",160);officer=Text(officer,"Officer",160);
            using(var c=Open()) using(var t=c.BeginTransaction()) {Permit(Authorize(c,t,group),facility,"HOSPITAL_ADMIN");Facility(c,t,group,facility);Execute(c,t,"INSERT dbo.HgrasDepartment(GroupId,FacilityId,Name,Officer) VALUES(@g,@f,@n,@o)","@g",group,"@f",facility,"@n",name,"@o",officer);Audit(c,t,group,facility,null,"DEPARTMENT_CREATED",name);t.Commit();}
        }
        public void AssignMember(long group,string email,long? facility,string role)
        {
            var roles=new[]{"GROUP_ADMIN","HOSPITAL_ADMIN","DEPARTMENT_MANAGER","STORES","CLINICAL","EQUIPMENT","HR","PROCUREMENT","AUDITOR"};
            if (!roles.Contains(role)) throw new InvalidOperationException("Choose a valid network role.");
            using(var c=Open()) using(var t=c.BeginTransaction(IsolationLevel.Serializable))
            {
                Permit(Authorize(c,t,group),null);
                var users=Read(c,t,"SELECT Id FROM dbo.AspNetUsers WHERE Email=@e","@e",Text(email,"Existing account email",256));
                if(users.Rows.Count!=1) throw new InvalidOperationException("Use the email of one existing registered account.");
                var target=(string)users.Rows[0]["Id"];
                if(target==userId) throw new InvalidOperationException("Ask another group administrator to change your own membership.");
                if(role=="GROUP_ADMIN") facility=null;else if(!facility.HasValue) throw new InvalidOperationException("Assign a hospital for this role.");
                if(facility.HasValue) Facility(c,t,group,facility.Value);
                Execute(c,t,"UPDATE dbo.HgrasMember SET Role=@r,FacilityId=@f WHERE GroupId=@g AND UserId=@u; IF @@ROWCOUNT=0 INSERT dbo.HgrasMember(GroupId,UserId,FacilityId,Role) VALUES(@g,@u,@f,@r)","@g",group,"@u",target,"@f",facility,"@r",role);
                Execute(c,t,"UPDATE dbo.AspNetUsers SET SecurityStamp=@s WHERE Id=@u","@s",Guid.NewGuid().ToString(),"@u",target);
                Audit(c,t,group,facility,null,"MEMBERSHIP_ASSIGNED",email+" / "+role);t.Commit();
            }
        }

        public void AddStock(long group,long facility,string name,string category,string unit,string batch,DateTime? expires,string handling,long quantity,long minimum,long reorder)
        {
            name=Text(name,"Resource name",160);unit=Text(unit,"Unit",32);
            if(!new[]{"Medicine","Supply","Equipment","Consumable"}.Contains(category)) throw new InvalidOperationException("Choose a resource category.");
            if(quantity<0||minimum<0||reorder<0||minimum>quantity) throw new InvalidOperationException("Stock and thresholds must be nonnegative; reserve cannot exceed initial stock.");
            if((batch??"").Length>80||(handling??"").Length>200) throw new InvalidOperationException("Batch or handling instructions are too long.");
            if(category=="Medicine" && (String.IsNullOrWhiteSpace(batch)||!expires.HasValue||String.IsNullOrWhiteSpace(handling))) throw new InvalidOperationException("Medicine stock requires a batch, expiry date and storage instructions.");
            if(expires.HasValue&&expires.Value.Date<=DateTime.UtcNow.Date) throw new InvalidOperationException("New stock must have a future expiry date.");
            using(var c=Open()) using(var t=c.BeginTransaction())
            {
                Permit(Authorize(c,t,group),facility,"HOSPITAL_ADMIN","STORES");Facility(c,t,group,facility);
                Execute(c,t,"INSERT dbo.HgrasStock(GroupId,FacilityId,Name,Category,Unit,Batch,ExpiresOn,Handling,Quantity,MinimumReserve,ReorderLevel) VALUES(@g,@f,@n,@cat,@u,@b,@e,@h,@q,@m,@r)","@g",group,"@f",facility,"@n",name,"@cat",category,"@u",unit,"@b",batch??"","@e",expires,"@h",handling??"","@q",quantity,"@m",minimum,"@r",reorder);
                Audit(c,t,group,facility,null,"STOCK_ADDED",name+": "+quantity+" "+unit);t.Commit();
            }
        }

        public void RequestTransfer(long group,long stock,long destination,long quantity,string priority,string reason)
        {
            reason=Text(reason,"Reason and suitability requirements",1000);
            if(quantity<=0||!new[]{"Routine","Urgent","Critical"}.Contains(priority)) throw new InvalidOperationException("Use a positive quantity and valid priority.");
            using(var c=Open()) using(var t=c.BeginTransaction(IsolationLevel.Serializable))
            {
                Permit(Authorize(c,t,group),destination,"HOSPITAL_ADMIN","STORES","DEPARTMENT_MANAGER","CLINICAL");Facility(c,t,group,destination);
                var rows=Read(c,t,"SELECT * FROM dbo.HgrasStock WITH(UPDLOCK,HOLDLOCK) WHERE GroupId=@g AND Id=@s","@g",group,"@s",stock);
                if(rows.Rows.Count!=1) throw new InvalidOperationException("Stock was not found in this group.");var row=rows.Rows[0];
                var source=(long)row["FacilityId"];if(source==destination) throw new InvalidOperationException("Choose a different receiving hospital.");
                if(!row.IsNull("ExpiresOn")&&((DateTime)row["ExpiresOn"]).Date<=DateTime.UtcNow.Date) throw new InvalidOperationException("Expired stock cannot be requested.");
                if((long)row["Quantity"]-(long)row["Reserved"]-(long)row["MinimumReserve"]<quantity) throw new InvalidOperationException("The supplying hospital cannot safely release this quantity.");
                var id=Insert(c,t,"INSERT dbo.HgrasTransfer(GroupId,StockId,FromFacilityId,ToFacilityId,Quantity,Priority,Reason,CreatedBy) VALUES(@g,@s,@from,@to,@q,@p,@r,@u)","@g",group,"@s",stock,"@from",source,"@to",destination,"@q",quantity,"@p",priority,"@r",reason,"@u",userId);
                Audit(c,t,group,destination,id,"REQUESTED",reason);t.Commit();
            }
        }

        public void ReconcileStock(long group,long stock,long expected,long quantity,long minimum,long reorder,string note)
        {
            note=Text(note,"Stock reconciliation reason",500);
            if(quantity<0||minimum<0||reorder<0) throw new InvalidOperationException("Counts and thresholds must be nonnegative.");
            using(var c=Open()) using(var t=c.BeginTransaction(IsolationLevel.Serializable))
            {
                var rows=Read(c,t,"SELECT FacilityId FROM dbo.HgrasStock WITH(UPDLOCK,HOLDLOCK) WHERE GroupId=@g AND Id=@s","@g",group,"@s",stock);
                if(rows.Rows.Count!=1) throw new InvalidOperationException("Stock not found in this hospital group.");
                var facility=(long)rows.Rows[0]["FacilityId"];Permit(Authorize(c,t,group),facility,"HOSPITAL_ADMIN","STORES");
                using(var cmd=Command(c,t,"UPDATE dbo.HgrasStock SET Quantity=@q,MinimumReserve=@m,ReorderLevel=@r,UpdatedAtUtc=SYSUTCDATETIME() WHERE GroupId=@g AND Id=@s AND Quantity=@expected AND Reserved<=@q","@g",group,"@s",stock,"@expected",expected,"@q",quantity,"@m",minimum,"@r",reorder))
                    if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("Stock changed or the new count would invalidate a reservation. Reload and resolve reservations before reconciling.");
                Audit(c,t,group,facility,null,"STOCK_RECONCILED","Lot #"+stock+": "+expected+" → "+quantity+". "+note);t.Commit();
            }
        }

        public void Transition(long group,long transfer,string expected,string target,string note,long? receivedQuantity=null)
        {
            note=Text(note,"Approval, transport or receipt note",500);
            using(var c=Open()) using(var t=c.BeginTransaction(IsolationLevel.Serializable))
            {
                var access=Authorize(c,t,group);
                var rows=Read(c,t,"SELECT * FROM dbo.HgrasTransfer WITH(UPDLOCK,HOLDLOCK) WHERE GroupId=@g AND Id=@id","@g",group,"@id",transfer);
                if(rows.Rows.Count!=1) throw new InvalidOperationException("Transfer not found.");var row=rows.Rows[0];
                var status=(string)row["Status"];var source=(long)row["FromFacilityId"];var destination=(long)row["ToFacilityId"];var quantity=(long)row["Quantity"];var stock=(long)row["StockId"];
                if(status!=expected) throw new InvalidOperationException("This transfer changed. Reload before continuing.");
                if(status=="REQUESTED"&&(target=="APPROVED"||target=="REJECTED")) Permit(access,source,"HOSPITAL_ADMIN","STORES");
                else if(status=="APPROVED"&&(target=="DISPATCHED"||target=="REJECTED")) Permit(access,source,"HOSPITAL_ADMIN","STORES");
                else if(status=="DISPATCHED"&&target=="RECEIVED") Permit(access,destination,"HOSPITAL_ADMIN","STORES");
                else throw new InvalidOperationException("This stage transition is not permitted.");
                if(target=="APPROVED")
                {
                    using(var cmd=Command(c,t,"UPDATE dbo.HgrasStock SET Reserved=Reserved+@q,UpdatedAtUtc=SYSUTCDATETIME() WHERE GroupId=@g AND Id=@s AND Quantity-Reserved-MinimumReserve>=@q AND (ExpiresOn IS NULL OR ExpiresOn>CAST(SYSUTCDATETIME() AS date))","@g",group,"@s",stock,"@q",quantity))
                        if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("Stock is no longer safely available. No allocation was made.");
                }
                if(status=="APPROVED"&&target=="REJECTED")
                {
                    using(var cmd=Command(c,t,"UPDATE dbo.HgrasStock SET Reserved=Reserved-@q,UpdatedAtUtc=SYSUTCDATETIME() WHERE GroupId=@g AND Id=@s AND Reserved>=@q","@g",group,"@s",stock,"@q",quantity))
                        if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("The reservation could not be released. No changes were saved.");
                }
                if(target=="DISPATCHED")
                {
                    using(var cmd=Command(c,t,"UPDATE dbo.HgrasStock SET Quantity=Quantity-@q,Reserved=Reserved-@q,UpdatedAtUtc=SYSUTCDATETIME() WHERE GroupId=@g AND Id=@s AND Reserved>=@q AND Quantity>=@q AND (ExpiresOn IS NULL OR ExpiresOn>CAST(SYSUTCDATETIME() AS date))","@g",group,"@s",stock,"@q",quantity))
                        if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("Reserved stock is unavailable or expired. Resolve this before dispatch.");
                }
                if(target=="RECEIVED")
                {
                    if(!receivedQuantity.HasValue||receivedQuantity<0||receivedQuantity>quantity) throw new InvalidOperationException("Confirm the usable quantity actually received, between zero and the dispatched quantity.");
                    // Each receipt creates a traceable stock lot; no name-only matching of medicines or equipment.
                    Execute(c,t,"INSERT dbo.HgrasStock(GroupId,FacilityId,Name,Category,Unit,Batch,ExpiresOn,Handling,Quantity) SELECT GroupId,@to,Name,Category,Unit,Batch,ExpiresOn,Handling,@q FROM dbo.HgrasStock WHERE GroupId=@g AND Id=@s","@g",group,"@s",stock,"@to",destination,"@q",receivedQuantity.Value);
                    note="Received "+receivedQuantity.Value+" of "+quantity+". "+note;
                }
                Execute(c,t,"UPDATE dbo.HgrasTransfer SET Status=@status,ReceivedQuantity=CASE WHEN @status='RECEIVED' THEN @received ELSE ReceivedQuantity END,Transport=CASE WHEN @status='DISPATCHED' THEN @note ELSE Transport END,UpdatedAtUtc=SYSUTCDATETIME() WHERE GroupId=@g AND Id=@id","@g",group,"@id",transfer,"@status",target,"@note",note,"@received",receivedQuantity);
                Audit(c,t,group,target=="RECEIVED"?destination:source,transfer,target,note);t.Commit();
            }
        }

        public void SaveOperation(long group,long facility,long? id,string kind,string name,string status,int total,int available,DateTime? due,string notes)
        {
            name=Text(name,"Record name",160);status=Text(status,"Status",32);notes=Text(notes,"Details",1000);
            var role=kind=="Staff"?"HR":kind=="Equipment"?"EQUIPMENT":kind=="Procurement"?"PROCUREMENT":kind=="Capacity"?"CLINICAL":null;
            if(role==null||available<0||total<available) throw new InvalidOperationException("Choose a valid module and capacity counts.");
            using(var c=Open()) using(var t=c.BeginTransaction())
            {
                Permit(Authorize(c,t,group),facility,"HOSPITAL_ADMIN",role);Facility(c,t,group,facility);
                if(id.HasValue)
                {
                    using(var cmd=Command(c,t,"UPDATE dbo.HgrasOperation SET Name=@n,Status=@s,Total=@total,Available=@a,DueOn=@d,Notes=@notes,UpdatedAtUtc=SYSUTCDATETIME() WHERE GroupId=@g AND FacilityId=@f AND Id=@id AND Kind=@k","@g",group,"@f",facility,"@id",id,"@k",kind,"@n",name,"@s",status,"@total",total,"@a",available,"@d",due,"@notes",notes)) if(cmd.ExecuteNonQuery()!=1) throw new InvalidOperationException("The operational record was not found in your hospital.");
                }
                else Execute(c,t,"INSERT dbo.HgrasOperation(GroupId,FacilityId,Kind,Name,Status,Total,Available,DueOn,Notes) VALUES(@g,@f,@k,@n,@s,@total,@a,@d,@notes)","@g",group,"@f",facility,"@k",kind,"@n",name,"@s",status,"@total",total,"@a",available,"@d",due,"@notes",notes);
                Audit(c,t,group,facility,null,kind.ToUpperInvariant()+"_RECORDED",name+": "+status);t.Commit();
            }
        }
    }
}
