IF OBJECT_ID('dbo.RequestHistory', 'U') IS NULL
BEGIN
 CREATE TABLE dbo.RequestHistory (
  Id bigint IDENTITY PRIMARY KEY,
  RequestId bigint NOT NULL,
  FromStage nvarchar(50) NULL,
  ToStage nvarchar(50) NOT NULL,
  Actor nvarchar(256) NOT NULL,
  Note nvarchar(1000) NULL,
  OccurredAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
 );
 CREATE INDEX IX_RequestHistory_RequestId ON dbo.RequestHistory(RequestId, Id);
END;
IF OBJECT_ID('dbo.StockAllocation', 'U') IS NULL
BEGIN
 CREATE TABLE dbo.StockAllocation (
  Id bigint IDENTITY PRIMARY KEY,
  RequestId bigint NOT NULL REFERENCES dbo.Request(Id),
  ResourceId bigint NOT NULL REFERENCES dbo.Resource(id),
  Quantity bigint NOT NULL CHECK (Quantity > 0),
  AllocatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
  CONSTRAINT UQ_StockAllocation UNIQUE (RequestId, ResourceId)
 );
END;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_Resource_AvailableStock')
 ALTER TABLE dbo.Resource ADD CONSTRAINT CK_Resource_AvailableStock CHECK (Quantity >= 0);
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_ResourceRequest_Positive')
 ALTER TABLE dbo.ResourceRequest ADD CONSTRAINT CK_ResourceRequest_Positive CHECK ([Count] > 0);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Request_Stage_Date')
 CREATE INDEX IX_Request_Stage_Date ON dbo.Request(StageId, DateRequested);
