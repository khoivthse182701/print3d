namespace TheBoys.Domain.Enums;

public enum UserRole
{
    Customer = 0,
    // Value 1 was PartnerLab. The migration maps legacy accounts to Staff.
    Staff = 2,
    Admin = 3,
    Manager = 4
}

public enum TaskType
{
    TextTo3D = 0,
    ImageTo3D = 1
}

public enum GenerationStatus
{
    Queued = 0,
    Processing = 1,
    MeshOptimizing = 2,
    Succeeded = 3,
    Failed = 4,
    Expired = 5,
    NeedsAttention = 6
}

public enum GenerationStage { Generate, Resize, Convert, Analyze, Complete }
public enum PrintReviewStatus { Pending, Approved, Rejected }

public enum OrderStatus
{
    Draft = 0,
    PendingPayment = 1,
    Paid = 2,
    SentToPartner = 3,
    Printing = 4,
    Reprinting = 5,
    PostProcessing = 6,
    Packaged = 7,
    Shipping = 8,
    Delivered = 9,
    Cancelled = 10,
    Refunded = 11
}

public enum PaymentStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Expired = 3
}
