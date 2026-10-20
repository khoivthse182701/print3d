-- THE BOYS target P0 schema, 2026-10-19. NOT an EF migration.
-- Execute only in an empty scratch database created by scripts/Verify-Design.ps1.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME() NOT LIKE N'TheBoys_DesignCheck[_]%'
    THROW 51000, 'Use a dedicated TheBoys_DesignCheck_* scratch database.', 1;
IF SCHEMA_ID(N'vnext') IS NOT NULL
    THROW 51001, 'vnext already exists; do not overwrite a design database.', 1;
BEGIN TRANSACTION;
EXEC(N'CREATE SCHEMA vnext');

CREATE TABLE vnext.Users (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    NormalizedEmail nvarchar(255) NOT NULL UNIQUE,
    PasswordHash nvarchar(1000) NOT NULL,
    FullName nvarchar(150) NOT NULL,
    Role varchar(20) NOT NULL CHECK (Role IN ('Customer','Staff','Admin')),
    AccountStatus varchar(20) NOT NULL CHECK (AccountStatus IN ('Unverified','Active','Locked','Anonymized')),
    EmailVerifiedAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL
);
CREATE TABLE vnext.RefreshSessions (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    FamilyId uniqueidentifier NOT NULL,
    TokenHash binary(32) NOT NULL UNIQUE,
    ExpiresAtUtc datetime2(3) NOT NULL,
    RevokedAtUtc datetime2(3) NULL,
    ReplacedById uniqueidentifier NULL REFERENCES vnext.RefreshSessions(Id),
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CHECK (ExpiresAtUtc > CreatedAtUtc)
);
CREATE INDEX IX_Sessions_UserFamily ON vnext.RefreshSessions(UserId,FamilyId);
CREATE TABLE vnext.IdentityTokens (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    Purpose varchar(20) NOT NULL CHECK (Purpose IN ('VerifyEmail','ResetPassword')),
    TokenHash binary(32) NOT NULL UNIQUE,
    ExpiresAtUtc datetime2(3) NOT NULL,
    ConsumedAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CHECK (ExpiresAtUtc > CreatedAtUtc)
);
CREATE TABLE vnext.ConsentRecords (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    Purpose varchar(40) NOT NULL,
    PolicyVersion varchar(40) NOT NULL,
    Granted bit NOT NULL,
    RecordedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE vnext.CreditAccounts (
    UserId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES vnext.Users(Id),
    TrialBalance int NOT NULL DEFAULT 0 CHECK (TrialBalance >= 0),
    PurchasedBalance int NOT NULL DEFAULT 0 CHECK (PurchasedBalance >= 0),
    RowVersion rowversion NOT NULL
);
CREATE TABLE vnext.ModelProjects (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    Name nvarchar(200) NOT NULL,
    ArchivedAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (Id,UserId)
);
CREATE TABLE vnext.GenerationJobs (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    ProjectId uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    Operation varchar(30) NOT NULL CHECK (Operation IN ('ImageTo3D','TextTo3D','Resize','Repair')),
    Provider varchar(30) NOT NULL,
    ProviderModel varchar(100) NOT NULL,
    PolicyVersion varchar(40) NOT NULL,
    Prompt nvarchar(4000) NULL,
    InputStorageKey nvarchar(500) NULL,
    InputHash binary(32) NOT NULL,
    InputConsentId uniqueidentifier NOT NULL REFERENCES vnext.ConsentRecords(Id),
    RequestedHeightMm decimal(10,3) NOT NULL CHECK (RequestedHeightMm > 0),
    ShouldTexture bit NOT NULL DEFAULT 0,
    Status varchar(30) NOT NULL CHECK (Status IN ('Queued','Processing','Succeeded','Failed','NeedsAttention','Cancelled')),
    ClientRequestId uniqueidentifier NOT NULL,
    RequestFingerprint binary(32) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    FOREIGN KEY (ProjectId,UserId) REFERENCES vnext.ModelProjects(Id,UserId),
    UNIQUE (UserId,ClientRequestId),
    UNIQUE (Id,ProjectId),
    CHECK (CompletedAtUtc IS NULL OR CompletedAtUtc >= CreatedAtUtc),
    CHECK ((Operation = 'TextTo3D' AND Prompt IS NOT NULL) OR Operation <> 'TextTo3D')
);
CREATE TABLE vnext.GenerationStages (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    JobId uniqueidentifier NOT NULL REFERENCES vnext.GenerationJobs(Id),
    Stage varchar(20) NOT NULL CHECK (Stage IN ('Generate','Resize','Convert','Analyze','Repair')),
    AttemptNo int NOT NULL CHECK (AttemptNo > 0),
    Provider varchar(30) NOT NULL,
    ProviderAccountScope varchar(100) NOT NULL,
    ProviderTaskId varchar(150) NULL,
    Status varchar(30) NOT NULL CHECK (Status IN ('Queued','Submitting','Running','Succeeded','Failed','NeedsAttention')),
    IsActive bit NOT NULL DEFAULT 1,
    SubmissionPending bit NOT NULL DEFAULT 0,
    PollFailureCount int NOT NULL DEFAULT 0 CHECK (PollFailureCount >= 0),
    NextPollAtUtc datetime2(3) NULL,
    LeaseOwner varchar(100) NULL,
    LeaseExpiresAtUtc datetime2(3) NULL,
    ProviderCredits decimal(18,4) NULL CHECK (ProviderCredits IS NULL OR ProviderCredits >= 0),
    ActualCostVnd decimal(18,2) NULL CHECK (ActualCostVnd IS NULL OR ActualCostVnd >= 0),
    ErrorCode varchar(100) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    UNIQUE (JobId,Stage,AttemptNo)
);
CREATE UNIQUE INDEX UX_Stages_Active ON vnext.GenerationStages(JobId,Stage) WHERE IsActive = 1;
CREATE UNIQUE INDEX UX_Stages_ProviderTask ON vnext.GenerationStages(Provider,ProviderAccountScope,ProviderTaskId) WHERE ProviderTaskId IS NOT NULL;
CREATE INDEX IX_Stages_Queue ON vnext.GenerationStages(Status,NextPollAtUtc) INCLUDE(JobId,LeaseExpiresAtUtc);
CREATE TABLE vnext.CreditLedger (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.CreditAccounts(UserId),
    JobId uniqueidentifier NULL REFERENCES vnext.GenerationJobs(Id),
    Bucket varchar(20) NOT NULL CHECK (Bucket IN ('Trial','Purchased')),
    Delta int NOT NULL CHECK (Delta <> 0),
    Kind varchar(30) NOT NULL CHECK (Kind IN ('OpeningBalance','TrialGrant','GenerationDebit','GenerationRefund','AdminAdjustment')),
    ReversesEntryId uniqueidentifier NULL REFERENCES vnext.CreditLedger(Id),
    OperationKey varchar(150) NOT NULL,
    ActorUserId uniqueidentifier NULL REFERENCES vnext.Users(Id),
    Reason nvarchar(500) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (UserId,OperationKey),
    CHECK ((Kind = 'GenerationDebit' AND Delta = -1 AND JobId IS NOT NULL)
        OR (Kind = 'GenerationRefund' AND Delta = 1 AND JobId IS NOT NULL AND ReversesEntryId IS NOT NULL)
        OR Kind IN ('OpeningBalance','TrialGrant','AdminAdjustment'))
);
CREATE UNIQUE INDEX UX_Ledger_RefundOnce ON vnext.CreditLedger(ReversesEntryId) WHERE ReversesEntryId IS NOT NULL;
CREATE UNIQUE INDEX UX_Ledger_JobDebit ON vnext.CreditLedger(JobId) WHERE Kind = 'GenerationDebit';
CREATE INDEX IX_Ledger_UserTime ON vnext.CreditLedger(UserId,CreatedAtUtc);
CREATE TABLE vnext.ModelVersions (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    ProjectId uniqueidentifier NOT NULL REFERENCES vnext.ModelProjects(Id),
    SourceJobId uniqueidentifier NULL,
    ParentVersionId uniqueidentifier NULL,
    VersionNo int NOT NULL CHECK (VersionNo > 0),
    GeometryHash binary(32) NOT NULL,
    WidthMm decimal(10,3) NOT NULL CHECK (WidthMm > 0),
    DepthMm decimal(10,3) NOT NULL CHECK (DepthMm > 0),
    HeightMm decimal(10,3) NOT NULL CHECK (HeightMm > 0),
    VolumeCm3 decimal(18,4) NULL CHECK (VolumeCm3 IS NULL OR VolumeCm3 > 0),
    AnalysisCoverage varchar(40) NOT NULL CHECK (AnalysisCoverage IN ('GeometryOnly','GeometryAndThickness','ManualSlicer')),
    AnalysisJson nvarchar(max) NULL CHECK (AnalysisJson IS NULL OR ISJSON(AnalysisJson) = 1),
    PublishedAtUtc datetime2(3) NOT NULL,
    UNIQUE (ProjectId,VersionNo),
    UNIQUE (Id,ProjectId),
    FOREIGN KEY (SourceJobId,ProjectId) REFERENCES vnext.GenerationJobs(Id,ProjectId)
);
ALTER TABLE vnext.ModelVersions ADD CONSTRAINT FK_Version_Parent FOREIGN KEY (ParentVersionId,ProjectId) REFERENCES vnext.ModelVersions(Id,ProjectId);
ALTER TABLE vnext.GenerationJobs ADD SourceVersionId uniqueidentifier NULL;
ALTER TABLE vnext.GenerationJobs ADD CONSTRAINT FK_Job_SourceVersion FOREIGN KEY (SourceVersionId,ProjectId) REFERENCES vnext.ModelVersions(Id,ProjectId);
CREATE TABLE vnext.ModelAssets (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    VersionId uniqueidentifier NOT NULL REFERENCES vnext.ModelVersions(Id),
    Format varchar(10) NOT NULL CHECK (Format IN ('GLB','STL','PNG','JPEG','WEBP','3MF')),
    Purpose varchar(20) NOT NULL CHECK (Purpose IN ('Preview','Print','Thumbnail')),
    StorageKey nvarchar(500) NOT NULL UNIQUE,
    Sha256 binary(32) NOT NULL,
    ByteLength bigint NOT NULL CHECK (ByteLength > 0),
    CoordinateUnit varchar(10) NOT NULL CHECK (CoordinateUnit IN ('mm','m','pixel')),
    UpAxis varchar(10) NULL CHECK (UpAxis IS NULL OR UpAxis IN ('X','Y','Z')),
    PurgedAtUtc datetime2(3) NULL,
    RetainUntilUtc datetime2(3) NULL,
    LegalHold bit NOT NULL DEFAULT 0,
    UNIQUE (VersionId,Format,Purpose)
);
CREATE TABLE vnext.PrintReviews (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    VersionId uniqueidentifier NOT NULL REFERENCES vnext.ModelVersions(Id),
    ConfigurationHash binary(32) NOT NULL,
    ReviewerUserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    Decision varchar(20) NOT NULL CHECK (Decision IN ('Approved','Rejected')),
    ChecklistJson nvarchar(max) NOT NULL CHECK (ISJSON(ChecklistJson) = 1),
    EvidenceStorageKey nvarchar(500) NULL,
    Note nvarchar(1000) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (Id,VersionId,ConfigurationHash)
);
CREATE TABLE vnext.CustomerApprovals (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    VersionId uniqueidentifier NOT NULL REFERENCES vnext.ModelVersions(Id),
    ConfigurationHash binary(32) NOT NULL,
    ConfigurationJson nvarchar(max) NOT NULL CHECK (ISJSON(ConfigurationJson) = 1),
    PolicyVersion varchar(40) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (Id,UserId,VersionId,ConfigurationHash)
);
CREATE TABLE vnext.PricingPackages (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    Code varchar(50) NOT NULL,
    VersionNo int NOT NULL CHECK (VersionNo > 0),
    Name nvarchar(150) NOT NULL,
    Material nvarchar(100) NOT NULL,
    FinishDescription nvarchar(500) NOT NULL,
    UnitPriceVnd decimal(18,2) NOT NULL CHECK (UnitPriceVnd > 0),
    PartnerUnitBudgetVnd decimal(18,2) NOT NULL CHECK (PartnerUnitBudgetVnd >= 0),
    MinHeightMm decimal(10,3) NOT NULL CHECK (MinHeightMm > 0),
    MaxHeightMm decimal(10,3) NOT NULL,
    IsActive bit NOT NULL,
    UNIQUE (Code,VersionNo),
    CHECK (MaxHeightMm >= MinHeightMm)
);
CREATE TABLE vnext.Quotes (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    VersionId uniqueidentifier NOT NULL REFERENCES vnext.ModelVersions(Id),
    ConfigurationHash binary(32) NOT NULL,
    ApprovalId uniqueidentifier NOT NULL,
    ReviewId uniqueidentifier NOT NULL,
    PricingPackageId uniqueidentifier NOT NULL REFERENCES vnext.PricingPackages(Id),
    Quantity int NOT NULL CHECK (Quantity BETWEEN 1 AND 10),
    UnitPriceVnd decimal(18,2) NOT NULL CHECK (UnitPriceVnd > 0),
    ShippingFeeVnd decimal(18,2) NOT NULL CHECK (ShippingFeeVnd >= 0),
    TotalVnd decimal(18,2) NOT NULL,
    AddressJson nvarchar(max) NOT NULL CHECK (ISJSON(AddressJson) = 1),
    SnapshotJson nvarchar(max) NOT NULL CHECK (ISJSON(SnapshotJson) = 1),
    ExpiresAtUtc datetime2(3) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (ApprovalId,UserId,VersionId,ConfigurationHash) REFERENCES vnext.CustomerApprovals(Id,UserId,VersionId,ConfigurationHash),
    FOREIGN KEY (ReviewId,VersionId,ConfigurationHash) REFERENCES vnext.PrintReviews(Id,VersionId,ConfigurationHash),
    UNIQUE (Id,UserId),
    CHECK (TotalVnd = UnitPriceVnd * Quantity + ShippingFeeVnd),
    CHECK (ExpiresAtUtc > CreatedAtUtc)
);
CREATE TABLE vnext.Orders (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderCode varchar(50) NOT NULL UNIQUE,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    QuoteId uniqueidentifier NOT NULL UNIQUE,
    ClientRequestId uniqueidentifier NOT NULL,
    RequestFingerprint binary(32) NOT NULL,
    FulfillmentStatus varchar(30) NOT NULL CHECK (FulfillmentStatus IN ('PendingPayment','AwaitingAssignment','Assigned','Printing','PostProcessing','QualityCheck','Packaged','Shipping','DeliveryFailed','Delivered','Cancelled','Returned')),
    PaymentStatus varchar(30) NOT NULL CHECK (PaymentStatus IN ('Unpaid','Paid','RefundPending','PartiallyRefunded','Refunded','NeedsAttention')),
    SubtotalVnd decimal(18,2) NOT NULL CHECK (SubtotalVnd > 0),
    ShippingFeeVnd decimal(18,2) NOT NULL CHECK (ShippingFeeVnd >= 0),
    TotalVnd decimal(18,2) NOT NULL,
    Currency char(3) NOT NULL DEFAULT 'VND' CHECK (Currency = 'VND'),
    AddressSnapshotJson nvarchar(max) NOT NULL CHECK (ISJSON(AddressSnapshotJson) = 1),
    PolicyVersion varchar(40) NOT NULL,
    CancellationRequestedAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    UNIQUE (UserId,ClientRequestId),
    FOREIGN KEY (QuoteId,UserId) REFERENCES vnext.Quotes(Id,UserId),
    CHECK (TotalVnd = SubtotalVnd + ShippingFeeVnd)
);
CREATE INDEX IX_Orders_OwnerTime ON vnext.Orders(UserId,CreatedAtUtc) INCLUDE(OrderCode,FulfillmentStatus,TotalVnd);
CREATE INDEX IX_Orders_Queue ON vnext.Orders(FulfillmentStatus,CreatedAtUtc);
CREATE TABLE vnext.OrderItems (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES vnext.Orders(Id),
    LineNumber int NOT NULL CHECK (LineNumber > 0),
    VersionId uniqueidentifier NOT NULL REFERENCES vnext.ModelVersions(Id),
    ConfigurationHash binary(32) NOT NULL,
    AssetHash binary(32) NOT NULL,
    Quantity int NOT NULL CHECK (Quantity BETWEEN 1 AND 10),
    UnitPriceVnd decimal(18,2) NOT NULL CHECK (UnitPriceVnd > 0),
    MaterialSnapshot nvarchar(100) NOT NULL,
    HeightMm decimal(10,3) NOT NULL CHECK (HeightMm > 0),
    EngravingText nvarchar(150) NULL,
    PartnerUnitBudgetVnd decimal(18,2) NOT NULL CHECK (PartnerUnitBudgetVnd >= 0),
    UNIQUE (OrderId,LineNumber)
);
CREATE TABLE vnext.OrderStatusHistory (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES vnext.Orders(Id),
    Axis varchar(20) NOT NULL CHECK (Axis IN ('Fulfillment','Payment')),
    FromStatus varchar(30) NULL,
    ToStatus varchar(30) NOT NULL,
    ActorUserId uniqueidentifier NULL REFERENCES vnext.Users(Id),
    Source varchar(30) NOT NULL,
    Reason nvarchar(500) NOT NULL,
    OperationKey varchar(150) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (OrderId,OperationKey)
);
CREATE TABLE vnext.PaymentAttempts (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES vnext.Orders(Id),
    Gateway varchar(30) NOT NULL,
    MerchantReference varchar(150) NOT NULL,
    ProviderTransactionId varchar(150) NULL,
    Status varchar(30) NOT NULL CHECK (Status IN ('Pending','Succeeded','Failed','Expired','NeedsAttention')),
    AmountVnd decimal(18,2) NOT NULL CHECK (AmountVnd > 0),
    Currency char(3) NOT NULL CHECK (Currency = 'VND'),
    GatewayFeeActualVnd decimal(18,2) NULL CHECK (GatewayFeeActualVnd IS NULL OR GatewayFeeActualVnd >= 0),
    ExpiresAtUtc datetime2(3) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CapturedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    UNIQUE (Gateway,MerchantReference)
);
CREATE UNIQUE INDEX UX_Payment_ProviderTransaction ON vnext.PaymentAttempts(Gateway,ProviderTransactionId) WHERE ProviderTransactionId IS NOT NULL;
CREATE TABLE vnext.PaymentEvents (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    PaymentAttemptId uniqueidentifier NOT NULL REFERENCES vnext.PaymentAttempts(Id),
    Gateway varchar(30) NOT NULL,
    EventId varchar(150) NOT NULL,
    PayloadHash binary(32) NOT NULL,
    VerifiedAtUtc datetime2(3) NOT NULL,
    ProcessedAtUtc datetime2(3) NULL,
    ProcessingResult varchar(30) NOT NULL CHECK (ProcessingResult IN ('Pending','Applied','Ignored','NeedsAttention')),
    ReceivedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (Gateway,EventId)
);
CREATE TABLE vnext.Refunds (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    PaymentAttemptId uniqueidentifier NOT NULL REFERENCES vnext.PaymentAttempts(Id),
    RequestedByUserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    ApprovedByUserId uniqueidentifier NULL REFERENCES vnext.Users(Id),
    OperationKey uniqueidentifier NOT NULL UNIQUE,
    ProviderReference varchar(150) NULL,
    AmountVnd decimal(18,2) NOT NULL CHECK (AmountVnd > 0),
    Status varchar(30) NOT NULL CHECK (Status IN ('Requested','Reserved','Submitted','Succeeded','Failed','NeedsAttention','Rejected')),
    Reason nvarchar(1000) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL
);
CREATE INDEX IX_Refunds_PaymentStatus ON vnext.Refunds(PaymentAttemptId,Status) INCLUDE(AmountVnd);
CREATE TABLE vnext.PartnerLabs (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    LabName nvarchar(200) NOT NULL,
    ContactJson nvarchar(max) NOT NULL CHECK (ISJSON(ContactJson) = 1),
    CapabilitiesJson nvarchar(max) NOT NULL CHECK (ISJSON(CapabilitiesJson) = 1),
    CapacityPerDay int NOT NULL CHECK (CapacityPerDay > 0),
    IsActive bit NOT NULL,
    RowVersion rowversion NOT NULL
);
CREATE TABLE vnext.PartnerAssignments (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES vnext.Orders(Id),
    PartnerLabId uniqueidentifier NOT NULL REFERENCES vnext.PartnerLabs(Id),
    AssignedByUserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    Status varchar(20) NOT NULL CHECK (Status IN ('Offered','Accepted','Rejected','Expired','Completed','Cancelled')),
    IsActive bit NOT NULL,
    PartnerCostSnapshotVnd decimal(18,2) NOT NULL CHECK (PartnerCostSnapshotVnd >= 0),
    DueAtUtc datetime2(3) NOT NULL,
    DecisionNote nvarchar(500) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CHECK ((Status IN ('Offered','Accepted') AND IsActive = 1) OR (Status NOT IN ('Offered','Accepted') AND IsActive = 0))
);
CREATE UNIQUE INDEX UX_Assignment_ActiveOrder ON vnext.PartnerAssignments(OrderId) WHERE IsActive = 1;
CREATE TABLE vnext.ProductionRuns (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    AssignmentId uniqueidentifier NOT NULL REFERENCES vnext.PartnerAssignments(Id),
    RunNo int NOT NULL CHECK (RunNo > 0),
    Status varchar(30) NOT NULL CHECK (Status IN ('Printing','PostProcessing','QualityCheck','Passed','Failed','Cancelled')),
    Quantity int NOT NULL CHECK (Quantity > 0),
    ActualCostVnd decimal(18,2) NULL CHECK (ActualCostVnd IS NULL OR ActualCostVnd >= 0),
    QcEvidenceStorageKey nvarchar(500) NULL,
    QcNote nvarchar(1000) NULL,
    StartedAtUtc datetime2(3) NOT NULL,
    CompletedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    UNIQUE (AssignmentId,RunNo),
    CHECK (Status <> 'Passed' OR (QcEvidenceStorageKey IS NOT NULL AND QcNote IS NOT NULL AND CompletedAtUtc IS NOT NULL))
);
CREATE TABLE vnext.Shipments (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES vnext.Orders(Id),
    Carrier varchar(50) NOT NULL,
    TrackingNumber varchar(100) NOT NULL,
    Status varchar(30) NOT NULL CHECK (Status IN ('Created','Shipping','DeliveryFailed','Delivered','Returned','Cancelled')),
    ShippingActualVnd decimal(18,2) NULL CHECK (ShippingActualVnd IS NULL OR ShippingActualVnd >= 0),
    DeliveredAtUtc datetime2(3) NULL,
    DeliveryEvidenceStorageKey nvarchar(500) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (Carrier,TrackingNumber),
    CHECK (Status <> 'Delivered' OR (DeliveredAtUtc IS NOT NULL AND DeliveryEvidenceStorageKey IS NOT NULL))
);
CREATE TABLE vnext.ShipmentEvents (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    ShipmentId uniqueidentifier NOT NULL REFERENCES vnext.Shipments(Id),
    EventKey varchar(150) NOT NULL,
    Status varchar(30) NOT NULL,
    Note nvarchar(500) NULL,
    Source varchar(30) NOT NULL,
    ActorUserId uniqueidentifier NULL REFERENCES vnext.Users(Id),
    OccurredAtUtc datetime2(3) NOT NULL,
    ReceivedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (ShipmentId,EventKey)
);
CREATE TABLE vnext.DefectReports (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES vnext.Orders(Id),
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    ClientRequestId uniqueidentifier NOT NULL,
    Reason nvarchar(1000) NOT NULL,
    Status varchar(30) NOT NULL CHECK (Status IN ('Open','AwaitingEvidence','Investigating','ReprintApproved','RefundApproved','Rejected','Resolved')),
    ResolutionNote nvarchar(1000) NULL,
    ResolvedByUserId uniqueidentifier NULL REFERENCES vnext.Users(Id),
    RefundId uniqueidentifier NULL REFERENCES vnext.Refunds(Id),
    ReprintRunId uniqueidentifier NULL REFERENCES vnext.ProductionRuns(Id),
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    ResolvedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    UNIQUE (UserId,ClientRequestId),
    CHECK (RefundId IS NULL OR ReprintRunId IS NULL)
);
CREATE TABLE vnext.DefectEvidence (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    ReportId uniqueidentifier NOT NULL REFERENCES vnext.DefectReports(Id),
    StorageKey nvarchar(500) NOT NULL UNIQUE,
    Sha256 binary(32) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE vnext.AuditLogs (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    ActorUserId uniqueidentifier NULL REFERENCES vnext.Users(Id),
    Action varchar(100) NOT NULL,
    AggregateType varchar(50) NOT NULL,
    AggregateId uniqueidentifier NOT NULL,
    TraceId varchar(64) NOT NULL,
    RedactedChangesJson nvarchar(max) NOT NULL CHECK (ISJSON(RedactedChangesJson) = 1),
    Reason nvarchar(1000) NOT NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Audit_AggregateTime ON vnext.AuditLogs(AggregateType,AggregateId,CreatedAtUtc);
CREATE TABLE vnext.OutboxMessages (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    EventType varchar(100) NOT NULL,
    AggregateId uniqueidentifier NOT NULL,
    DeduplicationKey varchar(150) NOT NULL UNIQUE,
    PayloadJson nvarchar(max) NOT NULL CHECK (ISJSON(PayloadJson) = 1),
    AttemptCount int NOT NULL DEFAULT 0 CHECK (AttemptCount >= 0),
    AvailableAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    LeaseOwner varchar(100) NULL,
    LeaseExpiresAtUtc datetime2(3) NULL,
    ProcessedAtUtc datetime2(3) NULL,
    DeadLetteredAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Outbox_Pending ON vnext.OutboxMessages(AvailableAtUtc) INCLUDE(LeaseExpiresAtUtc) WHERE ProcessedAtUtc IS NULL AND DeadLetteredAtUtc IS NULL;
CREATE TABLE vnext.Notifications (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    OutboxMessageId uniqueidentifier NOT NULL REFERENCES vnext.OutboxMessages(Id),
    Channel varchar(20) NOT NULL CHECK (Channel IN ('InApp','Email')),
    Title nvarchar(200) NOT NULL,
    Body nvarchar(1000) NOT NULL,
    DeliveredAtUtc datetime2(3) NULL,
    ReadAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    UNIQUE (UserId,OutboxMessageId,Channel)
);
CREATE TABLE vnext.IdempotencyRequests (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    Operation varchar(100) NOT NULL,
    ClientRequestId uniqueidentifier NOT NULL,
    RequestFingerprint binary(32) NOT NULL,
    Status varchar(20) NOT NULL CHECK (Status IN ('Processing','Completed','NeedsAttention')),
    ResourceType varchar(50) NULL,
    ResourceId uniqueidentifier NULL,
    ResponseStatus int NULL CHECK (ResponseStatus IS NULL OR ResponseStatus BETWEEN 200 AND 599),
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    UNIQUE (UserId,Operation,ClientRequestId),
    CHECK (Status <> 'Completed' OR (ResponseStatus IS NOT NULL AND CompletedAtUtc IS NOT NULL))
);
CREATE TABLE vnext.OrderCancellationRequests (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES vnext.Orders(Id),
    RequestedByUserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    Reason nvarchar(1000) NOT NULL,
    Status varchar(20) NOT NULL CHECK (Status IN ('Pending','Approved','Rejected','Completed')),
    DecidedByUserId uniqueidentifier NULL REFERENCES vnext.Users(Id),
    DecisionNote nvarchar(1000) NULL,
    RefundId uniqueidentifier NULL REFERENCES vnext.Refunds(Id),
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    DecidedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL
);
CREATE UNIQUE INDEX UX_Cancellation_Pending ON vnext.OrderCancellationRequests(OrderId) WHERE Status = 'Pending';
CREATE TABLE vnext.PrivacyRequests (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES vnext.Users(Id),
    RequestType varchar(20) NOT NULL CHECK (RequestType IN ('DeleteAssets','AnonymizeAccount')),
    Status varchar(20) NOT NULL CHECK (Status IN ('Pending','OnHold','Processing','Completed','Rejected')),
    Reason nvarchar(1000) NOT NULL,
    DecisionNote nvarchar(1000) NULL,
    HandledByUserId uniqueidentifier NULL REFERENCES vnext.Users(Id),
    CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAtUtc datetime2(3) NULL,
    RowVersion rowversion NOT NULL,
    CHECK (Status <> 'Completed' OR CompletedAtUtc IS NOT NULL)
);
COMMIT;
