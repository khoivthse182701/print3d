using System.Diagnostics.Metrics;
using TheBoys.Application.Common.Interfaces;

namespace TheBoys.Infrastructure.Services;

public sealed class Print3dTelemetry : IPrint3dTelemetry, IDisposable
{
    public const string MeterName = "TheBoys.Print3d";
    private readonly Meter _meter;

    private readonly Counter<long> _orderCreatedCounter;
    private readonly Counter<long> _orderPaidCounter;
    private readonly Counter<long> _refundSucceededCounter;
    private readonly Counter<long> _outboxDispatchedCounter;
    private readonly Histogram<double> _aiJobDurationHistogram;

    public Print3dTelemetry()
    {
        _meter = new Meter(MeterName, "1.0.0");
        _orderCreatedCounter = _meter.CreateCounter<long>("print3d.orders.created", description: "Số đơn hàng được tạo");
        _orderPaidCounter = _meter.CreateCounter<long>("print3d.orders.paid", description: "Số đơn hàng thanh toán thành công");
        _refundSucceededCounter = _meter.CreateCounter<long>("print3d.refunds.succeeded", description: "Số lệnh hoàn tiền thành công");
        _outboxDispatchedCounter = _meter.CreateCounter<long>("print3d.outbox.dispatched", description: "Số Outbox messages đã phát");
        _aiJobDurationHistogram = _meter.CreateHistogram<double>("print3d.ai.job_duration_seconds", unit: "s", description: "Thời gian hoàn thành tác vụ AI generation");
    }

    public void RecordOrderCreated(decimal totalAmountVnd) => _orderCreatedCounter.Add(1);
    public void RecordOrderPaid(decimal totalAmountVnd) => _orderPaidCounter.Add(1);
    public void RecordRefundSucceeded(decimal amountVnd) => _refundSucceededCounter.Add(1);
    public void RecordOutboxDispatched(int count) => _outboxDispatchedCounter.Add(count);
    public void RecordAiJobCompleted(string provider, double durationSeconds) => _aiJobDurationHistogram.Record(durationSeconds, new KeyValuePair<string, object?>("provider", provider));

    public void Dispose()
    {
        _meter.Dispose();
    }
}

