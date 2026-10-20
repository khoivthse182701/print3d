# Kế hoạch chuyển đổi DB không mất lịch sử

Thiết kế đích xem `../design/vnext.sql`. EF runtime hiện ở `src/TheBoys.Infrastructure/Persistence/Migrations`. **Không sửa ID/Up/Down migration đã phát hành**, không tự chèn `__EFMigrationsHistory`. File InitialCreate chỉ được chuyển vị trí vật lý, giữ namespace/type/ID/nội dung để tương thích database các nhánh cũ.

## Giai đoạn triển khai

1. **M0 — inventory và backup:** chốt schema/model snapshot/runtime DB version, counts theo trạng thái, duplicates, lengths, nulls; backup SQL + file store và diễn tập restore. Xác minh các baseline InitialCreate/InitialSchema/MeshyPrintingDemo/MergeMainSchemaAlignment.
2. **M1 — expand lịch sử:** migration thêm credit ledger/account, order/review history, audit/outbox, chưa drop cột cũ. Deploy code ghi đồng thời trong một transaction; mở ledger bằng balance tại cutover. Không tạo lịch sử giả cho giao dịch đã mất dấu.
3. **M2 — model/version:** tạo project/version/assets từ từng GenerationTask Succeeded; chỉ publish file tồn tại, checksum/units kiểm được. Job chưa xong giữ worker legacy cho đến drain; không đoán task ID/provider. Order pin version tương ứng. Quarantine missing file/dimension.
4. **M3 — quote/checkout/payment:** thêm quote/approval/item/payment events/refund. Chỉ đơn mới dùng checkout mới qua `/api/v2`; đơn demo cũ giữ policy `legacy-demo-offline`, không backfill `Paid` khi không có bằng chứng thu tiền.
5. **M4 — fulfillment/support:** assignments/runs/shipments/claims. Timestamps cũ tạo event `ImportedMilestone` có source import; không bịa actor/evidence. Active shipment thiếu tracking vào reconciliation.
6. **M5 — cutover/contract:** so số dư, tổng tiền, row counts, FK và file hash; chạy shadow reads, feature flag cho đơn mới; dừng dual-write chỉ khi consistency đạt. Drop cột cũ bằng migration riêng sau retention/rollback window đã được nhóm chốt.

## Mapping và dữ liệu chưa biết

| Cũ | Mới | Quy tắc |
|---|---|---|
| Users.FreeTrialRemaining/PurchasedRemaining | CreditAccounts + OpeningBalance ledger | Snapshot tại cutover, một entry/bucket nếu balance khác 0; không hồi dựng từng lần dùng |
| Users.TotalUsed | LegacyUsage metadata/report | Không cộng lại thành ledger debit giả |
| GenerationTask input/provider/status/stage | GenerationJobs/Stages | Giữ provider/model/task IDs và unknown state; model thông tin thiếu phải review |
| GenerationTask file/geometry | ModelProject/Version/Asset | Một project/version đầu/task cũ; file SHA-256 mới; không coi GLB và STL có cùng byte hash |
| GenerationTask review fields | PrintReview imported | Giữ reviewer/note/time nếu có; chưa đủ checklist thì legacy evidence, không auto qualify quote mới |
| Order model/qty/price/material/address | Order/OrderItem snapshot | Bảo toàn tiền/địa chỉ; không recompute theo giá gói hiện tại |
| Order StatusNote/timestamps | OrderStatusHistory imported | Source=Migration, Actor nullable; thiếu milestones ghi unknown |
| Payment rows | PaymentAttempts | Không gán Succeeded chỉ từ PaidAt; đối chiếu trạng thái/reference/receipt thực tế |
| Order defect fields | DefectReport/Evidence | Nếu có dữ liệu mới tạo; không tự đánh dấu refund/reprint đã hoàn tất |
| PartnerLab.UserId | Legacy reference / future membership | Không phục hồi role 1 thành portal Staff toàn quyền; mapping quyền phải review |

## Acceptance và rollback

- Mỗi migration test trên DB rỗng và clone đã ẩn PII từ tất cả baseline hỗ trợ. So sánh counts, amounts, balances, unique/FK, orphan assets và `HasPendingModelChanges`.
- Viết preflight báo lỗi dữ liệu vượt length/invalid enums/duplicate reference; sửa có audit trước migrate. Không cắt chuỗi/ép số âm tự động.
- Giai đoạn expand có thể rollback code bằng feature flag và giữ bảng mới. Một khi có tiền thực hoặc ghi vNext-only, không dùng Down drop bảng để rollback: restore/forward-fix theo bản backup và đối soát gateway.
- Freeze writes ngắn cho switch authority, drain worker, chốt outbox/inbox offsets; không có thời điểm hai worker khác version submit cùng job.
- DDL thiết kế dùng schema `vnext`; schema triển khai thực tế (`dbo` hay modular schema) phải được ADR/migration chọn rõ. Không copy SQL thiết kế vào startup.
