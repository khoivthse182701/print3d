using TheBoys.Domain.Common;
using TheBoys.Domain.Enums;

namespace TheBoys.Domain.Entities;

public sealed class Order : BaseEntity
{
    public string OrderCode { get; set; } = default!; // 'TB-ORD-20261019-0001'
    public Guid UserId { get; set; }
    public Guid GenerationTaskId { get; set; }
    public Guid PricingPackageId { get; set; }
    public Guid? PartnerLabId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    // Strategic Financial Snapshots (Bảo toàn số liệu P&L theo nguyên tắc CP3)
    public decimal UnitPriceSnapshot { get; set; } // 390.000 hoặc 300.000 VNĐ
    public decimal PartnerCostSnapshot { get; set; } // 120.000 VNĐ
    public decimal AiCostBudgetSnapshot { get; set; } // 10.000 VNĐ
    public decimal DefectBudgetSnapshot { get; set; } // 6.000 VNĐ (5% của giá xưởng)
    public decimal ShippingSubsidySnapshot { get; set; } // 10.000 VNĐ trợ giá nền tảng
    public decimal PaymentGatewayFeeSnapshot { get; set; } // 1% giá trị đơn

    public int Quantity { get; set; } = 1;
    public decimal SubTotalVnd { get; set; }
    public decimal ShippingFeeRaw { get; set; }
    public decimal FinalShippingFee { get; set; }
    public decimal TotalAmount { get; set; }

    // Thông tin giao hàng & khắc tên cá nhân hóa
    public string? CustomEngravingText { get; set; }
    public string ReceiverName { get; set; } = default!;
    public string ReceiverPhone { get; set; } = default!;
    public string StreetAddress { get; set; } = default!;
    public string District { get; set; } = default!;
    public string City { get; set; } = "Thành phố Hồ Chí Minh";
    public string? CustomerNote { get; set; }
    public string? TrackingNumber { get; set; }

    // Gộp từ OrderDefectReports (Quản lý ngân sách lỗi 5% theo CP3)
    public string? DefectReason { get; set; }
    public string? EvidencePhotoUrl { get; set; }
    public decimal? EstimatedDefectCostVnd { get; set; } = 6000.00m;
    public bool ReprintApproved { get; set; }

    // Gộp từ OrderStatusHistories (Timeline Milestones)
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? SentToPartnerAtUtc { get; set; }
    public DateTime? PrintingStartedAtUtc { get; set; }
    public DateTime? PackagedAtUtc { get; set; }
    public DateTime? ShippedAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? StatusNote { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public User User { get; set; } = default!;
    public GenerationTask GenerationTask { get; set; } = default!;
    public PricingPackage PricingPackage { get; set; } = default!;
    public PartnerLab? PartnerLab { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public sealed class Payment : BaseEntity
{
    public Guid OrderId { get; set; }
    public string PaymentMethod { get; set; } = default!; // 'MoMoQR', 'VNPay', 'VietQR_SePay'
    public string TransactionReference { get; set; } = default!;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? GatewayResponseJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }

    // Navigation
    public Order Order { get; set; } = default!;
}
