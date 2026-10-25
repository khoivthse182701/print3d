namespace TheBoys.Application.Common.Interfaces;

public interface IPrint3dTelemetry
{
    void RecordOrderCreated(decimal totalAmountVnd);
    void RecordOrderPaid(decimal totalAmountVnd);
    void RecordRefundSucceeded(decimal amountVnd);
    void RecordOutboxDispatched(int count);
    void RecordAiJobCompleted(string provider, double durationSeconds);
}

