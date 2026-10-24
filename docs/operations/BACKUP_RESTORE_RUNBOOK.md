# HƯỚNG DẪN VẬN HÀNH SAO LƯU, PHỤC HỒI & ĐỐI SOÁT (BACKUP & RESTORE RUNBOOK)
**Dự án:** PRINT3D / THE BOYS (.NET 10 & SQL Server)  
**Tác giả:** System Architect (Winston) & Master Test Architect (Murat - BMad TEA)  
**Phiên bản:** v1.0-REL-B09  
**Ngày áp dụng:** 2026-10-25  

---

## 1. MỤC TIÊU PHỤC HỒI THẢM HỌA (DR OBJECTIVES)

| Tiêu chí | Mục tiêu cam kết (SLA) | Chiến lược thực thi |
| :--- | :--- | :--- |
| **RPO (Recovery Point Objective)** | **≤ 15 phút** | Full backup hàng ngày lúc 01:00 UTC + Transaction Log backup mỗi 15 phút liên tục. |
| **RTO (Recovery Time Objective)** | **≤ 60 phút** | Tự động hóa khôi phục qua PowerShell / SQLCMD script, dự phòng nóng cold standby tại secondary region. |
| **Retention Policy** | **30 ngày** (Daily) + **12 tháng** (Monthly archive) | Lưu trữ mã hóa AES-256 trên Object Storage (S3 / Azure Blob) với immutability policy (WORM). |

---

## 2. QUY TRÌNH SAO LƯU (BACKUP PROCEDURES)

### 2.1. Sao lưu Cơ sở dữ liệu SQL Server

#### Lịch thực hiện:
- **Full Backup:** 01:00 UTC mỗi ngày.
- **Differential Backup:** Mỗi 6 giờ (06:00, 12:00, 18:00 UTC).
- **Transaction Log Backup:** Mỗi 15 phút.

#### Kịch bản T-SQL Full Backup có nén & mã hóa:
```sql
BACKUP DATABASE [TheBoysDb]
TO DISK = N'/var/opt/mssql/backups/TheBoysDb_Full_$(ESCAPE_SQUOTE(DATE)).bak'
WITH FORMAT, INIT, COMPRESSION, CHECKSUM,
     NAME = N'TheBoysDb-Full Database Backup',
     STATS = 10;
GO
```

#### Kịch bản Transaction Log Backup:
```sql
BACKUP LOG [TheBoysDb]
TO DISK = N'/var/opt/mssql/backups/TheBoysDb_Log_$(ESCAPE_SQUOTE(DATE)).trn'
WITH NOFORMAT, NOINIT, COMPRESSION, CHECKSUM,
     NAME = N'TheBoysDb-Log Backup',
     STATS = 5;
GO
```

### 2.2. Sao lưu Object Storage (3D Model Assets, QC Evidence, Defect Photos)

- **Sync Policy:** Sử dụng RClone / AWS CLI S3 sync định kỳ mỗi 15 phút từ local volume sang S3 bucket thứ cấp:
  ```bash
  aws s3 sync /data/models s3://theboys-cold-backup/models/ --sse aws:kms
  ```
- **Versioning:** Bucket được bật S3 Versioning và Object Lock (Retention 90 ngày chống xóa nhầm).

---

## 3. QUY TRÌNH PHỤC HỒI THẢM HỌA (RESTORE PROCEDURES)

### 3.1. Kịch bản phục hồi Point-In-Time (PITR)
Khi xảy ra sự cố dữ liệu tại thời điểm `2026-10-25 14:32:00 UTC`:

1. **Ngắt kết nối người dùng & chuyển API sang chế độ bảo trì (503 Service Unavailable):**
   ```bash
   touch /app/maintenance.lock
   ```

2. **Restore bản Full Backup gần nhất kèm NORECOVERY:**
   ```sql
   RESTORE DATABASE [TheBoysDb]
   FROM DISK = N'/var/opt/mssql/backups/TheBoysDb_Full_20261025.bak'
   WITH NORECOVERY, REPLACE;
   GO
   ```

3. **Restore bản Differential gần nhất (nếu có) kèm NORECOVERY:**
   ```sql
   RESTORE DATABASE [TheBoysDb]
   FROM DISK = N'/var/opt/mssql/backups/TheBoysDb_Diff_20261025_1200.bak'
   WITH NORECOVERY;
   GO
   ```

4. **Restore chuỗi Transaction Log đến thời điểm trước sự cố (STOPAT):**
   ```sql
   RESTORE LOG [TheBoysDb]
   FROM DISK = N'/var/opt/mssql/backups/TheBoysDb_Log_20261025_1415.trn'
   WITH NORECOVERY;

   RESTORE LOG [TheBoysDb]
   FROM DISK = N'/var/opt/mssql/backups/TheBoysDb_Log_20261025_1430.trn'
   WITH RECOVERY, STOPAT = '2026-10-25 14:31:59.000';
   GO
   ```

5. **Xác minh Schema & Integrity qua DBCC CHECKDB:**
   ```sql
   DBCC CHECKDB ([TheBoysDb]) WITH NO_INFOMSGS, ALL_ERRORMSGS;
   GO
   ```

---

## 4. QUY TRÌNH XỬ LÝ SỰ CỐ VẬN HÀNH (OPERATIONAL INCIDENT PLAYBOOKS)

### 4.1. Xử lý Outbox Messages bị Dead-Letter (`EVT-B08`)

Bản ghi `OutboxMessages` chuyển sang `DeadLetteredAtUtc != null` khi thất bại liên tục qua 5 lần retry (exponential backoff 15s → 30s → 60s → 120s).

#### Bước 1: Truy vấn danh sách Dead-Letter Messages:
```sql
SELECT Id, EventType, AggregateId, AttemptCount, DeadLetteredAtUtc, PayloadJson
FROM vnext.OutboxMessages
WHERE DeadLetteredAtUtc IS NOT NULL
ORDER BY DeadLetteredAtUtc DESC;
```

#### Bước 2: Phân tích nguyên nhân:
- Kiểm tra `AuditLogs` và application log tìm Exception tương ứng với `MessageId`.
- Nếu lỗi do payload sai định dạng: chỉnh sửa payload JSON hợp lệ.
- Nếu lỗi do dịch vụ ngoài (Email/Webhook timeout): kiểm tra lại kết nối mạng.

#### Bước 3: Tái kích hoạt (Reprocess):
```sql
UPDATE vnext.OutboxMessages
SET AttemptCount = 0,
    DeadLetteredAtUtc = NULL,
    AvailableAtUtc = SYSUTCDATETIME(),
    LeaseOwner = NULL,
    LeaseExpiresAtUtc = NULL
WHERE Id = 'CẦN_RETRY_GUID';
```

#### Bước 4: Kích hoạt quét ngay qua API:
```bash
curl -X POST https://api.theboys.vn/api/v2/admin/outbox/dispatch \
  -H "Authorization: Bearer $STAFF_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"batchSize": 50}'
```

---

### 4.2. Quy trình Đối soát Giao dịch Treo `NeedsAttention` (`PAY-B04`, `PAY-B05`, `OBS-A09`)

Giao dịch chuyển sang `NeedsAttention` khi cổng thanh toán (SePay/MoMo/Bank) phản hồi timeout, không xác định hoặc webhook sai chữ ký.

#### Bước 1: Giám sát thời gian thực qua API Observability:
```bash
curl -s -X GET https://api.theboys.vn/api/v2/admin/observability \
  -H "Authorization: Bearer $STAFF_TOKEN" | jq '.commerce'
```
*Cảnh báo kích hoạt nếu `PaymentAttemptsNeedsAttention > 0` hoặc `RefundsNeedsAttention > 0`.*

#### Bước 2: Đối soát thủ công qua Cổng thanh toán:
1. Lấy thông tin giao dịch treo:
   ```sql
   SELECT Id, OrderId, MerchantReference, ProviderTransactionId, AmountVnd, Status, CreatedAtUtc
   FROM vnext.PaymentAttempts
   WHERE Status = 'NeedsAttention';

   SELECT Id, PaymentAttemptId, OperationKey, ProviderReference, AmountVnd, Status, CreatedAtUtc
   FROM vnext.Refunds
   WHERE Status = 'NeedsAttention';
   ```
2. Đăng nhập cổng quản trị cổng thanh toán (SePay Merchant Portal / Bank Portal), tra cứu mã `MerchantReference` hoặc `OperationKey`.
3. Đối chiếu trạng thái tiền thực tế trong tài khoản thụ hưởng.

#### Bước 3: Cập nhật kết quả đối soát:
- Nếu giao dịch thanh toán thành công: gọi API đối soát staff xác nhận `Succeeded`.
- Nếu giao dịch thanh toán thất bại: hủy `PaymentAttempt`, cho phép khách hàng tạo attempt mới.
- Mọi thao tác đều tự động ghi vết vào bảng `vnext.AuditLogs` với redaction bảo mật tuyệt đối.

