using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Defects.Commands.CreateDefectReport;
using TheBoys.Application.Defects.Commands.ResolveDefectReport;
using TheBoys.Application.Fulfillment.Commands.AssignOrderToPartner;
using TheBoys.Application.Fulfillment.Commands.CreateShipment;
using TheBoys.Application.Fulfillment.Commands.RecordProductionRunQc;
using TheBoys.Application.Fulfillment.Commands.RecordShipmentEvent;
using TheBoys.Application.Fulfillment.Commands.RespondPartnerAssignment;
using TheBoys.Application.Fulfillment.Commands.StartProductionRun;
using TheBoys.Application.Fulfillment.Commands.UpdateProductionRunProgress;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests.Unit;

public class OpsB06AndB07Tests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private (IServiceScope scope, ApplicationDbContext db, Order order, PartnerLab lab1, PartnerLab lab2, IClock clock) CreateOpsContext(
        string fulfillmentStatus = "AwaitingAssignment",
        string paymentStatus = "Paid",
        decimal totalAmount = 390000m)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var lab1 = new PartnerLab
        {
            Id = Guid.NewGuid(),
            LabName = "Partner Lab Alpha (Thu Duc)",
            ContactPerson = "Alpha Manager",
            PhoneNumber = "0901112233",
            District = "Thu Duc",
            Address = "100 Vo Van Ngan",
            CapacityPerDay = 30,
            IsActive = true,
            CreatedAtUtc = clock.UtcNow
        };

        var lab2 = new PartnerLab
        {
            Id = Guid.NewGuid(),
            LabName = "Partner Lab Beta (District 7)",
            ContactPerson = "Beta Manager",
            PhoneNumber = "0904445566",
            District = "District 7",
            Address = "200 Nguyen Van Linh",
            CapacityPerDay = 25,
            IsActive = true,
            CreatedAtUtc = clock.UtcNow
        };

        db.PartnerLabs.AddRange(lab1, lab2);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderCode = $"TB-OPS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}",
            UserId = factory.CustomerId,
            QuoteId = null,
            PricingPackageId = DatabaseInitializer.DemoPackageId,
            Status = string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ? OrderStatus.Paid : OrderStatus.PendingPayment,
            FulfillmentStatus = fulfillmentStatus,
            OrderPaymentStatus = paymentStatus,
            Currency = "VND",
            Quantity = 1,
            UnitPriceSnapshot = totalAmount,
            PartnerCostSnapshot = 120000m,
            SubTotalVnd = totalAmount,
            ShippingFeeRaw = 0,
            FinalShippingFee = 0,
            TotalAmount = totalAmount,
            ReceiverName = "Nguyen Van Test",
            ReceiverPhone = "0901234567",
            StreetAddress = "456 Test Street",
            District = "District 1",
            City = "Ho Chi Minh",
            CreatedAtUtc = clock.UtcNow
        };

        db.Orders.Add(order);
        db.SaveChanges();

        return (scope, db, order, lab1, lab2, clock);
    }

    [Fact]
    public async Task Invariant1_AssignOrder_CannotAssignIfOrderNotPaidOrNotAwaitingAssignment()
    {
        // 1. Order chưa Paid -> Từ chối OrderNotEligibleForAssignment
        var (scope1, db1, unpaidOrder, labA1, _, clock1) = CreateOpsContext("PendingPayment", "Unpaid");
        using var _1 = scope1;
        var handler1 = new AssignOrderToPartnerCommandHandler(db1, clock1);
        var cmd1 = new AssignOrderToPartnerCommand(unpaidOrder.Id, labA1.Id, factory.StaffId);
        var res1 = await handler1.Handle(cmd1, CancellationToken.None);
        Assert.True(res1.IsFailure);
        Assert.Equal("OrderNotEligibleForAssignment", res1.Error.Code);

        // 2. Order đã Paid nhưng không ở AwaitingAssignment (ví dụ đang Printing) -> Bị từ chối
        var (scope2, db2, printingOrder, labA2, _, clock2) = CreateOpsContext("Printing", "Paid");
        using var _2 = scope2;
        var handler2 = new AssignOrderToPartnerCommandHandler(db2, clock2);
        var cmd2 = new AssignOrderToPartnerCommand(printingOrder.Id, labA2.Id, factory.StaffId);
        var res2 = await handler2.Handle(cmd2, CancellationToken.None);
        Assert.True(res2.IsFailure);
        Assert.Equal("OrderNotEligibleForAssignment", res2.Error.Code);

        // 3. Order hợp lệ (Paid + AwaitingAssignment) -> Thành công, chuyển sang Assigned
        var (scope3, db3, validOrder, labA3, _, clock3) = CreateOpsContext("AwaitingAssignment", "Paid");
        using var _3 = scope3;
        var handler3 = new AssignOrderToPartnerCommandHandler(db3, clock3);
        var cmd3 = new AssignOrderToPartnerCommand(validOrder.Id, labA3.Id, factory.StaffId, 125000m, clock3.UtcNow.AddDays(2), "Phân công test");
        var res3 = await handler3.Handle(cmd3, CancellationToken.None);
        Assert.True(res3.IsSuccess);
        Assert.Equal("Offered", res3.Value.Status);
        Assert.True(res3.Value.IsActive);
        Assert.Equal(125000m, res3.Value.PartnerCostSnapshotVnd);

        var refreshedOrder = await db3.Orders.FindAsync(validOrder.Id);
        Assert.Equal("Assigned", refreshedOrder!.FulfillmentStatus);
        Assert.Equal(labA3.Id, refreshedOrder.PartnerLabId);
    }

    [Fact]
    public async Task Invariant2_PartnerReject_SetsActiveFalse_RevertsOrderToAwaitingAssignment_AllowsSecondPartner()
    {
        var (scope, db, order, labAlpha, labBeta, clock) = CreateOpsContext();
        using var _ = scope;

        var assignHandler = new AssignOrderToPartnerCommandHandler(db, clock);

        // 1. Phân công cho Lab Alpha
        var assignAlpha = await assignHandler.Handle(new AssignOrderToPartnerCommand(order.Id, labAlpha.Id, factory.StaffId), CancellationToken.None);
        Assert.True(assignAlpha.IsSuccess);

        // 1b. Customer (non-management) cố phản hồi -> Bị chặn với Forbidden
        var respondHandler = new RespondPartnerAssignmentCommandHandler(db, clock);
        var unauthRespond = await respondHandler.Handle(new RespondPartnerAssignmentCommand(assignAlpha.Value.Id, factory.CustomerId, false, "Customer thử reject", IsManagement: false), CancellationToken.None);
        Assert.True(unauthRespond.IsFailure);
        Assert.Equal("Forbidden", unauthRespond.Error.Code);

        // 2. Lab Alpha từ chối (Reject) bởi Staff/Management
        var rejectAlpha = await respondHandler.Handle(new RespondPartnerAssignmentCommand(assignAlpha.Value.Id, factory.StaffId, false, "Alpha quá tải máy in"), CancellationToken.None);
        Assert.True(rejectAlpha.IsSuccess);
        Assert.Equal("Rejected", rejectAlpha.Value.Status);
        Assert.False(rejectAlpha.Value.IsActive);

        // Đơn hàng tự động quay về AwaitingAssignment và PartnerLabId = null
        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("AwaitingAssignment", refreshedOrder!.FulfillmentStatus);
        Assert.Null(refreshedOrder.PartnerLabId);

        // 3. Phân công tiếp cho Lab Beta -> Thành công
        var assignBeta = await assignHandler.Handle(new AssignOrderToPartnerCommand(order.Id, labBeta.Id, factory.StaffId), CancellationToken.None);
        Assert.True(assignBeta.IsSuccess);
        Assert.Equal("Offered", assignBeta.Value.Status);
        Assert.True(assignBeta.Value.IsActive);

        // 4. Lab Beta chấp thuận (Accept)
        var acceptBeta = await respondHandler.Handle(new RespondPartnerAssignmentCommand(assignBeta.Value.Id, factory.StaffId, true, "Beta nhận in ngay"), CancellationToken.None);
        Assert.True(acceptBeta.IsSuccess);
        Assert.Equal("Accepted", acceptBeta.Value.Status);
        Assert.True(acceptBeta.Value.IsActive);

        // DB có đủ 2 bản ghi lịch sử phân công, trong đó đúng 1 bản ghi đang IsActive = true
        var assignments = await db.PartnerAssignments.Where(a => a.OrderId == order.Id).ToListAsync();
        Assert.Equal(2, assignments.Count);
        Assert.Single(assignments, a => a.IsActive);
    }

    [Fact]
    public async Task Invariant3_StartProductionRun_CannotStartIfPartnerNotAcceptedOrOrderNotPaid()
    {
        var (scope, db, order, lab, _, clock) = CreateOpsContext();
        using var _ = scope;

        var assignHandler = new AssignOrderToPartnerCommandHandler(db, clock);
        var assignRes = await assignHandler.Handle(new AssignOrderToPartnerCommand(order.Id, lab.Id, factory.StaffId), CancellationToken.None);

        var runHandler = new StartProductionRunCommandHandler(db, clock);

        // Khi assignment mới ở Offered (chưa Accepted) -> Bị chặn với PartnerNotAcceptedYet
        var prematureRun = await runHandler.Handle(new StartProductionRunCommand(assignRes.Value.Id, factory.StaffId), CancellationToken.None);
        Assert.True(prematureRun.IsFailure);
        Assert.Equal("PartnerNotAcceptedYet", prematureRun.Error.Code);

        // Sau khi xưởng Accept -> Khởi tạo lượt in thành công (RunNo = 1, Status = Printing)
        var respondHandler = new RespondPartnerAssignmentCommandHandler(db, clock);
        await respondHandler.Handle(new RespondPartnerAssignmentCommand(assignRes.Value.Id, factory.StaffId, true), CancellationToken.None);

        var validRun = await runHandler.Handle(new StartProductionRunCommand(assignRes.Value.Id, factory.StaffId, 1, 120000m), CancellationToken.None);
        Assert.True(validRun.IsSuccess);
        Assert.Equal(1, validRun.Value.RunNo);
        Assert.Equal("Printing", validRun.Value.Status);

        // Cố tình khởi tạo thêm lượt in khi lượt in 1 vẫn đang Printing -> Bị chặn với ActiveProductionRunExists
        var conflictRun = await runHandler.Handle(new StartProductionRunCommand(assignRes.Value.Id, factory.StaffId, 1), CancellationToken.None);
        Assert.True(conflictRun.IsFailure);
        Assert.Equal("ActiveProductionRunExists", conflictRun.Error.Code);

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("Printing", refreshedOrder!.FulfillmentStatus);
        Assert.NotNull(refreshedOrder.PrintingStartedAtUtc);
    }

    [Fact]
    public async Task Invariant4_ProductionRun_AdvanceProgress_ToPostProcessingAndQualityCheck()
    {
        var (scope, db, order, lab, _, clock) = CreateOpsContext();
        using var _ = scope;

        var assignHandler = new AssignOrderToPartnerCommandHandler(db, clock);
        var assignRes = await assignHandler.Handle(new AssignOrderToPartnerCommand(order.Id, lab.Id, factory.StaffId), CancellationToken.None);
        var respondHandler = new RespondPartnerAssignmentCommandHandler(db, clock);
        await respondHandler.Handle(new RespondPartnerAssignmentCommand(assignRes.Value.Id, factory.StaffId, true), CancellationToken.None);

        var startHandler = new StartProductionRunCommandHandler(db, clock);
        var runRes = await startHandler.Handle(new StartProductionRunCommand(assignRes.Value.Id, factory.StaffId), CancellationToken.None);

        var progressHandler = new UpdateProductionRunProgressCommandHandler(db, clock);

        // 1. Chuyển sang PostProcessing
        var postRes = await progressHandler.Handle(new UpdateProductionRunProgressCommand(runRes.Value.Id, factory.StaffId, ProductionRunStatus.PostProcessing), CancellationToken.None);
        Assert.True(postRes.IsSuccess);
        Assert.Equal("PostProcessing", postRes.Value.Status);

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("PostProcessing", refreshedOrder!.FulfillmentStatus);

        // 2. Chuyển sang QualityCheck
        var qcRes = await progressHandler.Handle(new UpdateProductionRunProgressCommand(runRes.Value.Id, factory.StaffId, ProductionRunStatus.QualityCheck), CancellationToken.None);
        Assert.True(qcRes.IsSuccess);
        Assert.Equal("QualityCheck", qcRes.Value.Status);

        await db.Entry(refreshedOrder).ReloadAsync();
        Assert.Equal("QualityCheck", refreshedOrder.FulfillmentStatus);
    }

    [Fact]
    public async Task Invariant5_RecordQc_CannotPassWithoutEvidenceAndNote()
    {
        var (scope, db, order, lab, _, clock) = CreateOpsContext();
        using var _ = scope;

        var assignHandler = new AssignOrderToPartnerCommandHandler(db, clock);
        var assignRes = await assignHandler.Handle(new AssignOrderToPartnerCommand(order.Id, lab.Id, factory.StaffId), CancellationToken.None);
        var respondHandler = new RespondPartnerAssignmentCommandHandler(db, clock);
        await respondHandler.Handle(new RespondPartnerAssignmentCommand(assignRes.Value.Id, factory.StaffId, true), CancellationToken.None);
        var startHandler = new StartProductionRunCommandHandler(db, clock);
        var runRes = await startHandler.Handle(new StartProductionRunCommand(assignRes.Value.Id, factory.StaffId), CancellationToken.None);

        var qcHandler = new RecordProductionRunQcCommandHandler(db, clock);

        // 1. QC Pass nhưng thiếu Evidence hoặc Note -> Bị chặn với QcEvidenceRequired
        var invalidPass = await qcHandler.Handle(new RecordProductionRunQcCommand(runRes.Value.Id, factory.StaffId, true, null, ""), CancellationToken.None);
        Assert.True(invalidPass.IsFailure);
        Assert.Equal("QcEvidenceRequired", invalidPass.Error.Code);

        // 2. QC Pass có đầy đủ ảnh và ghi chú -> Thành công, chuyển sang Packaged và Assignment kết thúc
        var validPass = await qcHandler.Handle(new RecordProductionRunQcCommand(runRes.Value.Id, factory.StaffId, true, "s3://qc/run-1-pass.jpg", "Chi tiết sắc nét, kích thước chuẩn 102mm"), CancellationToken.None);
        Assert.True(validPass.IsSuccess);
        Assert.Equal("Passed", validPass.Value.Status);
        Assert.NotNull(validPass.Value.CompletedAtUtc);

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("Packaged", refreshedOrder!.FulfillmentStatus);
        Assert.NotNull(refreshedOrder.PackagedAtUtc);

        var refreshedAssignment = await db.PartnerAssignments.FindAsync(assignRes.Value.Id);
        Assert.Equal(PartnerAssignmentStatus.Completed, refreshedAssignment!.Status);
        Assert.False(refreshedAssignment.IsActive);
    }

    [Fact]
    public async Task Invariant6_RecordQc_WhenFailedWithTriggerReprint_PreservesOldRun_CreatesNewRun()
    {
        var (scope, db, order, lab, _, clock) = CreateOpsContext();
        using var _ = scope;

        var assignHandler = new AssignOrderToPartnerCommandHandler(db, clock);
        var assignRes = await assignHandler.Handle(new AssignOrderToPartnerCommand(order.Id, lab.Id, factory.StaffId), CancellationToken.None);
        var respondHandler = new RespondPartnerAssignmentCommandHandler(db, clock);
        await respondHandler.Handle(new RespondPartnerAssignmentCommand(assignRes.Value.Id, factory.StaffId, true), CancellationToken.None);
        var startHandler = new StartProductionRunCommandHandler(db, clock);
        var runRes = await startHandler.Handle(new StartProductionRunCommand(assignRes.Value.Id, factory.StaffId), CancellationToken.None);

        var qcHandler = new RecordProductionRunQcCommandHandler(db, clock);

        // QC Thất bại và kích hoạt TriggerReprint = true
        var failQc = await qcHandler.Handle(new RecordProductionRunQcCommand(
            RunId: runRes.Value.Id,
            ActorUserId: factory.StaffId,
            Passed: false,
            QcEvidenceStorageKey: "s3://qc/run-1-fail.jpg",
            QcNote: "Bị cong vênh đế 2mm",
            TriggerReprint: true
        ), CancellationToken.None);

        Assert.True(failQc.IsSuccess);
        Assert.Equal("Failed", failQc.Value.Status);

        // Lượt in cũ RunNo = 1 giữ nguyên Status = Failed
        var run1 = await db.ProductionRuns.FindAsync(runRes.Value.Id);
        Assert.Equal(ProductionRunStatus.Failed, run1!.Status);
        Assert.Equal(1, run1.RunNo);

        // Hệ thống tự động sinh RunNo = 2 ở trạng thái Printing
        var runs = await db.ProductionRuns.Where(r => r.AssignmentId == assignRes.Value.Id).OrderBy(r => r.RunNo).ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.Equal(2, runs[1].RunNo);
        Assert.Equal(ProductionRunStatus.Printing, runs[1].Status);

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("Printing", refreshedOrder!.FulfillmentStatus);
    }

    [Fact]
    public async Task Invariant7_CreateShipment_CannotCreateIfNotPackagedOrDeliveryFailed()
    {
        // 1. Đơn đang ở AwaitingAssignment -> Bị từ chối OrderNotPackaged
        var (scope1, db1, order1, _, _, clock1) = CreateOpsContext("AwaitingAssignment", "Paid");
        using var _1 = scope1;
        var handler1 = new CreateShipmentCommandHandler(db1, clock1);
        var res1 = await handler1.Handle(new CreateShipmentCommand(order1.Id, factory.StaffId, "GHTK", "GHTK123456"), CancellationToken.None);
        Assert.True(res1.IsFailure);
        Assert.Equal("OrderNotPackaged", res1.Error.Code);

        // 2. Đơn ở Packaged -> Tạo vận đơn thành công
        var (scope2, db2, order2, _, _, clock2) = CreateOpsContext("Packaged", "Paid");
        using var _2 = scope2;
        var handler2 = new CreateShipmentCommandHandler(db2, clock2);
        var res2 = await handler2.Handle(new CreateShipmentCommand(order2.Id, factory.StaffId, "GHN", "GHN987654", 32000m), CancellationToken.None);
        Assert.True(res2.IsSuccess);
        Assert.Equal("Created", res2.Value.Status);
        Assert.Equal("GHN987654", res2.Value.TrackingNumber);
        Assert.Single(res2.Value.Events); // Initial event
    }

    [Fact]
    public async Task Invariant8_RecordShipmentEvent_CannotDeliverWithoutEvidence_IdempotentOnEventKey()
    {
        var (scope, db, order, _, _, clock) = CreateOpsContext("Packaged", "Paid");
        using var _ = scope;

        var shipmentHandler = new CreateShipmentCommandHandler(db, clock);
        var shipmentRes = await shipmentHandler.Handle(new CreateShipmentCommand(order.Id, factory.StaffId, "ViettelPost", "VT001122"), CancellationToken.None);

        var eventHandler = new RecordShipmentEventCommandHandler(db, clock);

        // 1. Chuyển sang Shipping
        var shippingRes = await eventHandler.Handle(new RecordShipmentEventCommand(shipmentRes.Value.Id, "EVT-01", "Shipping", "Rời kho"), CancellationToken.None);
        Assert.True(shippingRes.IsSuccess);
        Assert.False(shippingRes.Value.IsReplay);
        Assert.Equal("Shipping", shippingRes.Value.Shipment.Status);

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("Shipping", refreshedOrder!.FulfillmentStatus);

        // 2. Chuyển sang Delivered mà thiếu bằng chứng ảnh -> Bị chặn DeliveryEvidenceRequired
        var invalidDelivered = await eventHandler.Handle(new RecordShipmentEventCommand(shipmentRes.Value.Id, "EVT-02", "Delivered", "Đã nhận", DeliveryEvidenceStorageKey: null), CancellationToken.None);
        Assert.True(invalidDelivered.IsFailure);
        Assert.Equal("DeliveryEvidenceRequired", invalidDelivered.Error.Code);

        // 3. Giao hàng thành công có bằng chứng -> Thành công
        var validDelivered = await eventHandler.Handle(new RecordShipmentEventCommand(shipmentRes.Value.Id, "EVT-02", "Delivered", "Khách ký nhận", DeliveryEvidenceStorageKey: "s3://proof/signature.jpg"), CancellationToken.None);
        Assert.True(validDelivered.IsSuccess);
        Assert.False(validDelivered.Value.IsReplay);
        Assert.Equal("Delivered", validDelivered.Value.Shipment.Status);

        await db.Entry(refreshedOrder).ReloadAsync();
        Assert.Equal("Delivered", refreshedOrder.FulfillmentStatus);
        Assert.NotNull(refreshedOrder.DeliveredAtUtc);

        // 4. Replay cùng EventKey "EVT-02" -> Trả về IsReplay = true, không nhân đôi sự kiện
        var replay = await eventHandler.Handle(new RecordShipmentEventCommand(shipmentRes.Value.Id, "EVT-02", "Delivered", "Trùng sự kiện"), CancellationToken.None);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IsReplay);

        var events = await db.ShipmentEvents.Where(e => e.ShipmentId == shipmentRes.Value.Id).ToListAsync();
        Assert.Equal(3, events.Count); // Initial + Shipping + Delivered
    }

    [Fact]
    public async Task Invariant9_CreateDefectReport_OnlyAllowedWhenDeliveredOrShipping_IdempotentOnClientRequestId()
    {
        // 1. Đơn mới ở AwaitingAssignment -> Bị chặn với OrderNotDeliveredYet
        var (scope1, db1, order1, _, _, clock1) = CreateOpsContext("AwaitingAssignment", "Paid");
        using var _1 = scope1;
        var handler1 = new CreateDefectReportCommandHandler(db1, clock1);
        var res1 = await handler1.Handle(new CreateDefectReportCommand(order1.Id, factory.CustomerId, Guid.NewGuid(), "Gãy tay", null), CancellationToken.None);
        Assert.True(res1.IsFailure);
        Assert.Equal("OrderNotDeliveredYet", res1.Error.Code);

        // 2. Đơn ở Delivered -> Tạo khiếu nại thành công kèm bằng chứng SHA256
        var (scope2, db2, order2, _, _, clock2) = CreateOpsContext("Delivered", "Paid");
        using var _2 = scope2;
        var handler2 = new CreateDefectReportCommandHandler(db2, clock2);
        var reqId = Guid.NewGuid();
        var evidenceList = new List<DefectEvidenceInput>
        {
            new("s3://defects/photo1.jpg", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")
        };
        var res2 = await handler2.Handle(new CreateDefectReportCommand(order2.Id, factory.CustomerId, reqId, "Sản phẩm bị nứt thân", evidenceList), CancellationToken.None);
        Assert.True(res2.IsSuccess);
        Assert.False(res2.Value.IsReplay);
        Assert.Equal("Open", res2.Value.Report.Status);
        Assert.Single(res2.Value.Report.Evidence);

        // 3. Replay cùng ClientRequestId -> Trả về kết quả cũ với IsReplay = true
        var replay = await handler2.Handle(new CreateDefectReportCommand(order2.Id, factory.CustomerId, reqId, "Gửi lại cùng mã"), CancellationToken.None);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IsReplay);
        Assert.Equal(res2.Value.Report.Id, replay.Value.Report.Id);
    }

    [Fact]
    public async Task Invariant10_ResolveDefectReport_ReprintApproved_EnforcesSingleResolution_CreatesNewRun()
    {
        var (scope, db, order, lab, _, clock) = CreateOpsContext("Delivered", "Paid");
        using var _ = scope;

        // Tạo assignment và production run trước đó
        var assignment = PartnerAssignment.Create(order.Id, lab.Id, factory.StaffId, 120000m, clock.UtcNow.AddDays(2), "Assignment goc", clock.UtcNow);
        assignment.Accept();
        db.PartnerAssignments.Add(assignment);
        var run1 = ProductionRun.Start(assignment.Id, 1, 1, clock.UtcNow);
        run1.MarkPassed("s3://qc/run1.jpg", "Passed", clock.UtcNow);
        assignment.Complete();
        db.ProductionRuns.Add(run1);

        var report = DefectReport.Create(order.Id, factory.CustomerId, Guid.NewGuid(), "Nứt đế sau khi nhận", clock.UtcNow);
        db.DefectReports.Add(report);
        await db.SaveChangesAsync();

        var resolveHandler = new ResolveDefectReportCommandHandler(db, clock);

        // Staff duyệt in lại
        var resolveRes = await resolveHandler.Handle(new ResolveDefectReportCommand(report.Id, factory.StaffId, DefectResolutionType.ReprintApproved, "Duyệt bảo hành in lại miễn phí"), CancellationToken.None);
        Assert.True(resolveRes.IsSuccess);
        Assert.Equal("ReprintApproved", resolveRes.Value.Status);
        Assert.NotNull(resolveRes.Value.ReprintRunId);
        Assert.Null(resolveRes.Value.RefundId); // Bất biến SingleResolution

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("Printing", refreshedOrder!.FulfillmentStatus);
        Assert.True(refreshedOrder.ReprintApproved);

        // Kiểm tra Run mới sinh ra có RunNo = 2
        var newRun = await db.ProductionRuns.FindAsync(resolveRes.Value.ReprintRunId!.Value);
        Assert.NotNull(newRun);
        Assert.Equal(2, newRun.RunNo);
        Assert.Equal(ProductionRunStatus.Printing, newRun.Status);
    }

    [Fact]
    public async Task Invariant11_ResolveDefectReport_RefundApproved_SetsRefundPending_EnforcesSingleResolution()
    {
        var (scope, db, order, _, _, clock) = CreateOpsContext("Delivered", "Paid");
        using var _ = scope;

        var report = DefectReport.Create(order.Id, factory.CustomerId, Guid.NewGuid(), "Màu sắc sai lệch hoàn toàn", clock.UtcNow);
        db.DefectReports.Add(report);
        await db.SaveChangesAsync();

        var resolveHandler = new ResolveDefectReportCommandHandler(db, clock);

        // Staff duyệt hoàn tiền
        var resolveRes = await resolveHandler.Handle(new ResolveDefectReportCommand(report.Id, factory.StaffId, DefectResolutionType.RefundApproved, "Chấp nhận hoàn tiền 100%"), CancellationToken.None);
        Assert.True(resolveRes.IsSuccess);
        Assert.Equal("RefundApproved", resolveRes.Value.Status);
        Assert.Null(resolveRes.Value.ReprintRunId); // Bất biến SingleResolution

        var refreshedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal("RefundPending", refreshedOrder!.OrderPaymentStatus);
    }

    [Fact]
    public async Task Invariant12_ResolveDefectReport_Rejected_SetsRejected()
    {
        var (scope, db, order, _, _, clock) = CreateOpsContext("Delivered", "Paid");
        using var _ = scope;

        var report = DefectReport.Create(order.Id, factory.CustomerId, Guid.NewGuid(), "Yêu cầu hoàn vì không thích", clock.UtcNow);
        db.DefectReports.Add(report);
        await db.SaveChangesAsync();

        var resolveHandler = new ResolveDefectReportCommandHandler(db, clock);

        var resolveRes = await resolveHandler.Handle(new ResolveDefectReportCommand(report.Id, factory.StaffId, DefectResolutionType.Rejected, "Không thuộc phạm vi bảo hành lỗi kỹ thuật"), CancellationToken.None);
        Assert.True(resolveRes.IsSuccess);
        Assert.Equal("Rejected", resolveRes.Value.Status);
    }
}
