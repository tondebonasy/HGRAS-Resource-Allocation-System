-- Additive HGRAS network schema. Existing organisation requests remain unchanged.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @schemaLock int;
EXEC @schemaLock=sys.sp_getapplock @Resource='HGRAS.Schema.003',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
IF @schemaLock<0 THROW 50001, 'Network schema update is busy. Retry shortly.', 1;
IF OBJECT_ID('dbo.HgrasGroup') IS NULL
 CREATE TABLE dbo.HgrasGroup(Id bigint IDENTITY PRIMARY KEY, Name nvarchar(160) NOT NULL, CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF OBJECT_ID('dbo.HgrasFacility') IS NULL
 CREATE TABLE dbo.HgrasFacility(Id bigint IDENTITY PRIMARY KEY, GroupId bigint NOT NULL REFERENCES dbo.HgrasGroup(Id), Name nvarchar(160) NOT NULL, Location nvarchar(200) NOT NULL, Officer nvarchar(160) NOT NULL, UNIQUE(GroupId,Id));
IF OBJECT_ID('dbo.HgrasMember') IS NULL
 CREATE TABLE dbo.HgrasMember(GroupId bigint NOT NULL REFERENCES dbo.HgrasGroup(Id), UserId nvarchar(128) NOT NULL REFERENCES dbo.AspNetUsers(Id), FacilityId bigint NULL, Role nvarchar(32) NOT NULL, PRIMARY KEY(GroupId,UserId), FOREIGN KEY(GroupId,FacilityId) REFERENCES dbo.HgrasFacility(GroupId,Id), CHECK(Role IN ('GROUP_ADMIN','HOSPITAL_ADMIN','DEPARTMENT_MANAGER','STORES','CLINICAL','EQUIPMENT','HR','PROCUREMENT','AUDITOR')), CHECK(Role='GROUP_ADMIN' OR FacilityId IS NOT NULL));
IF OBJECT_ID('dbo.HgrasDepartment') IS NULL
 CREATE TABLE dbo.HgrasDepartment(Id bigint IDENTITY PRIMARY KEY, GroupId bigint NOT NULL, FacilityId bigint NOT NULL, Name nvarchar(160) NOT NULL, Officer nvarchar(160) NOT NULL, FOREIGN KEY(GroupId,FacilityId) REFERENCES dbo.HgrasFacility(GroupId,Id));
IF OBJECT_ID('dbo.HgrasStock') IS NULL
 CREATE TABLE dbo.HgrasStock(Id bigint IDENTITY PRIMARY KEY, GroupId bigint NOT NULL, FacilityId bigint NOT NULL, Name nvarchar(160) NOT NULL, Category nvarchar(24) NOT NULL, Unit nvarchar(32) NOT NULL, Batch nvarchar(80) NOT NULL DEFAULT '', ExpiresOn date NULL, Handling nvarchar(200) NOT NULL DEFAULT '', Quantity bigint NOT NULL CHECK(Quantity>=0), Reserved bigint NOT NULL DEFAULT 0 CHECK(Reserved>=0), MinimumReserve bigint NOT NULL DEFAULT 0 CHECK(MinimumReserve>=0), ReorderLevel bigint NOT NULL DEFAULT 0 CHECK(ReorderLevel>=0), UpdatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), FOREIGN KEY(GroupId,FacilityId) REFERENCES dbo.HgrasFacility(GroupId,Id), UNIQUE(GroupId,Id), CHECK(Reserved<=Quantity), CHECK(Category IN ('Medicine','Supply','Equipment','Consumable')));
IF OBJECT_ID('dbo.HgrasTransfer') IS NULL
 CREATE TABLE dbo.HgrasTransfer(Id bigint IDENTITY PRIMARY KEY, GroupId bigint NOT NULL, StockId bigint NOT NULL, FromFacilityId bigint NOT NULL, ToFacilityId bigint NOT NULL, Quantity bigint NOT NULL CHECK(Quantity>0), Priority nvarchar(16) NOT NULL CHECK(Priority IN ('Routine','Urgent','Critical')), Status nvarchar(16) NOT NULL DEFAULT 'REQUESTED' CHECK(Status IN ('REQUESTED','APPROVED','DISPATCHED','RECEIVED','REJECTED')), Reason nvarchar(1000) NOT NULL, Transport nvarchar(500) NULL, CreatedBy nvarchar(128) NOT NULL, CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), FOREIGN KEY(GroupId,StockId) REFERENCES dbo.HgrasStock(GroupId,Id), FOREIGN KEY(GroupId,FromFacilityId) REFERENCES dbo.HgrasFacility(GroupId,Id), FOREIGN KEY(GroupId,ToFacilityId) REFERENCES dbo.HgrasFacility(GroupId,Id), CHECK(FromFacilityId<>ToFacilityId));
IF OBJECT_ID('dbo.HgrasAudit') IS NULL
 CREATE TABLE dbo.HgrasAudit(Id bigint IDENTITY PRIMARY KEY, GroupId bigint NOT NULL REFERENCES dbo.HgrasGroup(Id), FacilityId bigint NULL, TransferId bigint NULL, Actor nvarchar(256) NOT NULL, Action nvarchar(80) NOT NULL, Note nvarchar(1000) NOT NULL, OccurredAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), FOREIGN KEY(GroupId,FacilityId) REFERENCES dbo.HgrasFacility(GroupId,Id));
IF OBJECT_ID('dbo.HgrasOperation') IS NULL
 CREATE TABLE dbo.HgrasOperation(Id bigint IDENTITY PRIMARY KEY, GroupId bigint NOT NULL, FacilityId bigint NOT NULL, Kind nvarchar(24) NOT NULL CHECK(Kind IN ('Equipment','Staff','Capacity','Procurement')), Name nvarchar(160) NOT NULL, Status nvarchar(32) NOT NULL, Total int NOT NULL CHECK(Total>=0), Available int NOT NULL CHECK(Available>=0), DueOn date NULL, Notes nvarchar(1000) NOT NULL, UpdatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), FOREIGN KEY(GroupId,FacilityId) REFERENCES dbo.HgrasFacility(GroupId,Id), CHECK(Available<=Total));
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.HgrasStock') AND name='IX_HgrasStock_GroupFacility')
 CREATE INDEX IX_HgrasStock_GroupFacility ON dbo.HgrasStock(GroupId,FacilityId,Name);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.HgrasTransfer') AND name='IX_HgrasTransfer_GroupStatus')
 CREATE INDEX IX_HgrasTransfer_GroupStatus ON dbo.HgrasTransfer(GroupId,Status,CreatedAtUtc);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.HgrasAudit') AND name='IX_HgrasAudit_GroupTime')
 CREATE INDEX IX_HgrasAudit_GroupTime ON dbo.HgrasAudit(GroupId,OccurredAtUtc);
IF COL_LENGTH('dbo.HgrasTransfer','ReceivedQuantity') IS NULL
 ALTER TABLE dbo.HgrasTransfer ADD ReceivedQuantity bigint NULL CHECK(ReceivedQuantity>=0);
IF OBJECT_ID('dbo.HgrasNotice') IS NULL
 CREATE TABLE dbo.HgrasNotice(Id bigint IDENTITY PRIMARY KEY, GroupId bigint NOT NULL, FacilityId bigint NOT NULL, TransferId bigint NOT NULL REFERENCES dbo.HgrasTransfer(Id), Subject nvarchar(160) NOT NULL, Message nvarchar(1000) NOT NULL, CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), FOREIGN KEY(GroupId,FacilityId) REFERENCES dbo.HgrasFacility(GroupId,Id));
IF OBJECT_ID('dbo.HgrasNoticeRead') IS NULL
 CREATE TABLE dbo.HgrasNoticeRead(NoticeId bigint NOT NULL REFERENCES dbo.HgrasNotice(Id), UserId nvarchar(128) NOT NULL REFERENCES dbo.AspNetUsers(Id), PRIMARY KEY(NoticeId,UserId));
COMMIT;
