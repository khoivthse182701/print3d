-- Destructive ONLY to transient test transactions in a dedicated scratch DB.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME() NOT LIKE N'TheBoys_DesignCheck[_]%'
    THROW 51100, 'Scratch database required', 1;
DECLARE @tables int = (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('vnext'));
IF @tables <> 34 THROW 51101, 'Expected 34 target tables', 1;
DECLARE @user uniqueidentifier = NEWID(), @project uniqueidentifier = NEWID(), @job uniqueidentifier = NEWID(), @consent uniqueidentifier = NEWID();
DECLARE @hash binary(32) = HASHBYTES('SHA2_256','fixture'), @key uniqueidentifier = NEWID();
INSERT vnext.Users(Id,NormalizedEmail,PasswordHash,FullName,Role,AccountStatus)
VALUES(@user,'design@example.test','not-a-real-password','Fixture','Customer','Active');
INSERT vnext.CreditAccounts(UserId,TrialBalance) VALUES(@user,2);
INSERT vnext.ConsentRecords(Id,UserId,Purpose,PolicyVersion,Granted) VALUES(@consent,@user,'Generate','test-v1',1);
INSERT vnext.ModelProjects(Id,UserId,Name) VALUES(@project,@user,'Fixture');
INSERT vnext.GenerationJobs(Id,ProjectId,UserId,Operation,Provider,ProviderModel,PolicyVersion,InputHash,InputConsentId,RequestedHeightMm,Status,ClientRequestId,RequestFingerprint)
VALUES(@job,@project,@user,'ImageTo3D','Fake','v1','test-v1',@hash,@consent,100,'Queued',@key,@hash);

BEGIN TRY
    UPDATE vnext.CreditAccounts SET TrialBalance=-1 WHERE UserId=@user;
    THROW 51102, 'Negative balance accepted', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
BEGIN TRY
    INSERT vnext.GenerationJobs(Id,ProjectId,UserId,Operation,Provider,ProviderModel,PolicyVersion,InputHash,InputConsentId,RequestedHeightMm,Status,ClientRequestId,RequestFingerprint)
    VALUES(NEWID(),@project,@user,'ImageTo3D','Fake','v1','test-v1',@hash,@consent,100,'Queued',@key,@hash);
    THROW 51103, 'Duplicate idempotency key accepted', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
DECLARE @debit uniqueidentifier=NEWID();
INSERT vnext.CreditLedger(Id,UserId,JobId,Bucket,Delta,Kind,OperationKey,Reason)
VALUES(@debit,@user,@job,'Trial',-1,'GenerationDebit','debit:fixture','fixture');
INSERT vnext.CreditLedger(Id,UserId,JobId,Bucket,Delta,Kind,ReversesEntryId,OperationKey,Reason)
VALUES(NEWID(),@user,@job,'Trial',1,'GenerationRefund',@debit,'refund:fixture','fixture');
BEGIN TRY
    INSERT vnext.CreditLedger(Id,UserId,JobId,Bucket,Delta,Kind,ReversesEntryId,OperationKey,Reason)
    VALUES(NEWID(),@user,@job,'Trial',1,'GenerationRefund',@debit,'refund:again','fixture');
    THROW 51104, 'Double refund entry accepted', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() NOT IN (2601,2627) THROW;
END CATCH;
BEGIN TRY
    INSERT vnext.ModelVersions(Id,ProjectId,VersionNo,GeometryHash,WidthMm,DepthMm,HeightMm,AnalysisCoverage,AnalysisJson,PublishedAtUtc)
    VALUES(NEWID(),@project,1,@hash,10,20,100,'GeometryOnly','not-json',SYSUTCDATETIME());
    THROW 51105, 'Invalid JSON accepted', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
DECLARE @version uniqueidentifier=NEWID(), @review uniqueidentifier=NEWID(), @approval uniqueidentifier=NEWID(), @package uniqueidentifier=NEWID(), @quote uniqueidentifier=NEWID();
INSERT vnext.ModelVersions(Id,ProjectId,VersionNo,GeometryHash,WidthMm,DepthMm,HeightMm,AnalysisCoverage,PublishedAtUtc)
VALUES(@version,@project,1,@hash,10,20,100,'ManualSlicer',SYSUTCDATETIME());
INSERT vnext.PrintReviews(Id,VersionId,ConfigurationHash,ReviewerUserId,Decision,ChecklistJson,Note)
VALUES(@review,@version,@hash,@user,'Approved','{}','fixture only');
INSERT vnext.CustomerApprovals(Id,UserId,VersionId,ConfigurationHash,ConfigurationJson,PolicyVersion)
VALUES(@approval,@user,@version,@hash,'{}','test-v1');
INSERT vnext.PricingPackages(Id,Code,VersionNo,Name,Material,FinishDescription,UnitPriceVnd,PartnerUnitBudgetVnd,MinHeightMm,MaxHeightMm,IsActive)
VALUES(@package,'TEST',1,'Fixture','Resin','Basic',390000,120000,100,120,1);
BEGIN TRY
    INSERT vnext.Quotes(Id,UserId,VersionId,ConfigurationHash,ApprovalId,ReviewId,PricingPackageId,Quantity,UnitPriceVnd,ShippingFeeVnd,TotalVnd,AddressJson,SnapshotJson,ExpiresAtUtc)
    VALUES(@quote,@user,@version,@hash,@approval,@review,@package,2,390000,0,390000,'{}','{}',DATEADD(minute,30,SYSUTCDATETIME()));
    THROW 51106, 'Wrong quote arithmetic accepted', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
BEGIN TRY
    INSERT vnext.Quotes(Id,UserId,VersionId,ConfigurationHash,ApprovalId,ReviewId,PricingPackageId,Quantity,UnitPriceVnd,ShippingFeeVnd,TotalVnd,AddressJson,SnapshotJson,ExpiresAtUtc)
    VALUES(@quote,@user,@version,HASHBYTES('SHA2_256','different'),@approval,@review,@package,1,390000,0,390000,'{}','{}',DATEADD(minute,30,SYSUTCDATETIME()));
    THROW 51107, 'Mismatched approval configuration accepted', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 547 THROW;
END CATCH;
INSERT vnext.Quotes(Id,UserId,VersionId,ConfigurationHash,ApprovalId,ReviewId,PricingPackageId,Quantity,UnitPriceVnd,ShippingFeeVnd,TotalVnd,AddressJson,SnapshotJson,ExpiresAtUtc)
VALUES(@quote,@user,@version,@hash,@approval,@review,@package,1,390000,0,390000,'{}','{}',DATEADD(minute,30,SYSUTCDATETIME()));
PRINT 'PASS: 34 tables, valid fixture, nonnegative balance, unique job key, one refund per debit, valid JSON, quote arithmetic, approval configuration binding';
