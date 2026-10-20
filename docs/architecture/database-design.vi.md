# Thiết kế database vNext

## 1. Hiện trạng và lựa chọn

Runtime hiện có 6 bảng nghiệp vụ: Users, PricingPackages, PartnerLabs, GenerationTasks, Orders, Payments. GenerationTasks gộp orchestration, assets và review; Orders gộp item, địa chỉ, defect và milestones; Users gộp balance. Cách này phù hợp demo nhưng mất lịch sử khi có nhiều lần sửa mẫu/in/hoàn tiền.

Thiết kế đích có **34 bảng** trong [DDL](../../database/design/vnext.sql), schema `vnext` để review/kiểm chứng. Đây là **schema thiết kế P0 và một số cấu trúc dự phòng P1**, không phải migration thay thế DB hiện tại. Có thể triển khai từng nhóm; 34 bảng không đồng nghĩa 34 module hoặc 34 màn hình. Ba bảng workflow cuối lưu idempotency chung, yêu cầu hủy có xét duyệt và yêu cầu xử lý dữ liệu riêng tư.

Mục tiêu: 3NF cho entity có lifecycle riêng; denormalize có chủ đích cho quote/order snapshot và metadata provider. Không dùng JSON để giấu trường hay filter/sum thường xuyên. JSON chỉ cho cấu hình/checklist/report có schema version; tiền/trạng thái/khóa luôn là cột typed.

## 2. Data dictionary

`Id` mặc định Guid do application tạo, PK; UTC dùng datetime2(3); VND decimal(18,2), dimensions decimal(10,3), volume decimal(18,4), SHA-256 binary(32). Cột NULL có nghĩa chưa biết/chưa xảy ra, không thay bằng 0. Nullable/length/check chi tiết được quy định trực tiếp trong DDL.

| Bảng | Mục đích, cột quan trọng | Khóa và lifecycle |
|---|---|---|
| Users | normalizedEmail, hash, role, accountStatus, verifiedAt | Email unique; Customer/Staff/Admin; rowversion |
| RefreshSessions | user, family, tokenHash, expiry, revoked, replacement | Hash unique; không lưu bearer token; revoke family |
| IdentityTokens | verify/reset purpose, tokenHash, expiry/consumed | Hash unique; consume một lần trong transaction |
| ConsentRecords | user, purpose, policyVersion, granted, recorded | Append quyết định grant/revoke; không overwrite lịch sử |
| CreditAccounts | user PK, trialBalance, purchasedBalance | Balance ≥0 + rowversion; cache số dư đối soát từ ledger |
| CreditLedger | user/job/bucket/delta/kind/reversal/key/actor/reason | Unique user+operation; một debit/job; một reversal/debit |
| ModelProjects | owner, name, archive | Composite Id+UserId bảo vệ ownership của jobs |
| GenerationJobs | project/owner, operation, provider/model, input hash/key/consent, quota policy | Unique owner+clientRequestId; rowversion; source version cho P1 |
| GenerationStages | job, stage, attemptNo, provider/account/taskId, submission, poll/lease, cost | Một active stage/job+stage; unique provider+scope+taskId; hàng đợi indexed |
| ModelVersions | project, parent, source job, versionNo, geometryHash, dimensions, analysis coverage/report | Unique project+versionNo; parent/source cùng project; published immutable |
| ModelAssets | version, purpose/format, key/hash/bytes/unit/upAxis, retention/hold | Key unique; mỗi version+format+purpose một file; không lưu binary trong SQL |
| PrintReviews | version/configHash, reviewer, decision, checklist, evidence/note | Append-only; composite key cho quote bind đúng cấu hình |
| CustomerApprovals | user/version/configHash/configJSON/policy | Append-only; phân biệt approval kỹ thuật |
| PricingPackages | code/version, material/finish, unit price/partner budget, height range, active | Unique code+version; bản đang dùng immutable ngoại trừ active |
| Quotes | owner, version/config, approval/review/package, qty/price/shipping/total, expiry | Composite FK buộc đúng version/config/owner của approval; total check |
| Orders | owner/quote/key/fingerprint, fulfillment/payment states, totals/address/policy | Unique quote; unique owner+key; rowversion; trạng thái tiền tách giao hàng |
| OrderItems | order/lineNumber/version/configHash/assetHash, qty/price/material/height/engraving/budget | Một item P0 do application enforce; nhiều item P2 cần đổi quote model |
| OrderStatusHistory | order, axis, from/to, actor/source/reason/operation/time | Append-only; unique order+operation; giữ milestones qua nhiều lần chuyển |
| PaymentAttempts | order/gateway/merchantRef/providerTxn, amount/currency/status, actual fee, expiry/capture | Unique gateway+ref và gateway+providerTxn; không giới hạn một attempt |
| PaymentEvents | attempt, gateway/eventId, payloadHash, verified/processed/result | Inbox unique gateway+eventId; chỉ event đã xác minh mới lưu tại đây |
| Refunds | attempt/requester/approver/operation/providerRef, amount/status/reason | Operation unique, reserve theo transaction; rowversion; không thể CHECK tổng cross-row |
| PartnerLabs | contact/capabilities, capacity, active | Quyền login không gộp vào lab; P1 thêm memberships |
| PartnerAssignments | order/lab/actor/status/active, cost/due/decision | Một active assignment/order; từ chối giữ lịch sử |
| ProductionRuns | assignment/runNo/status/quantity/actual cost/QC evidence | Unique assignment+runNo; Passed phải có evidence/note/completed |
| Shipments | order/carrier/tracking/status/actual cost/delivery evidence | Carrier+tracking unique; Delivered phải có evidence/time |
| ShipmentEvents | shipment/eventKey/status/source/actor/occurred/received | Unique shipment+eventKey; không xóa event đến sai thứ tự |
| DefectReports | order/customer/key/reason/status/resolution/refund hoặc reprint | Nhiều report/order; rowversion; tối đa một loại resolution trực tiếp/case |
| DefectEvidence | report/storageKey/hash | Nhiều evidence/report; key unique |
| AuditLogs | actor/action/aggregate/trace/redactedChanges/reason/time | Append-only; aggregate index; không FK polymorphic aggregate |
| OutboxMessages | event/aggregate/dedupe/payload, retry/lease/processed/deadletter | Dedupe unique; index pending; commit với nghiệp vụ |
| Notifications | user/event/channel/title/body/delivered/read | Unique user+event+channel; in-app P0, email P1 |
| IdempotencyRequests | user/operation/key/fingerprint/status/resource/result | Unique theo user+operation+key cho approval/payment/refund/claim/admin mutation; job/order vẫn có unique riêng phòng vệ |
| OrderCancellationRequests | order/requester/reason/status/decision/refund | Một pending/order; trạng thái xét hủy không gộp vào trạng thái sản xuất |
| PrivacyRequests | user/type/status/reason/handler/decision/completed | Theo dõi hold và kết quả xóa/anonymize; không suy hoàn thành chỉ từ audit entry |

DDL không định nghĩa role portal xưởng, credit purchase/subscription, address book, public share/gallery hay B2B. Chúng là extension P1/P2, cần migration riêng và không được coi sẵn sàng vì có JSON mở rộng.

## 3. Invariant: DB cưỡng chế gì, application phải làm gì

| Quy tắc | DB đảm bảo | Application/transaction phải đảm bảo |
|---|---|---|
| Quota | Không âm, unique debit/job, one reversal | Debit + balance + ledger atomic; bucket/owner/reversal khớp; opening balance không bịa lịch sử |
| Job/provider | Snapshot columns và unique external ID | Resolve adapter theo job, stage provider/account đúng; không double POST; verify consent owner/granted |
| Version | Parent/source cùng project, unique số version | Assets ≥1 khi published; file immutable, hashes verified; user sở hữu approval/version |
| Quote | Total đúng, expiry sau created; approval/review cùng version/config | Decision Approved và chưa superseded; package/height/material/owner hợp lệ; expiry chưa trôi; configJSON hash canonical |
| Checkout | Unique quote, owner+key; total = subtotal+ship | Items khớp quote/version/assets; subtotal = sum item; một item P0; owner approval đúng project |
| Payment | Unique gateway event/reference | Signature/amount/currency validation; state monotonic; khóa order khi apply; không coi email/redirect là paid |
| Refund | Amount dương, operation unique | Lock payment; SUM reserved/submitted/unknown/succeeded ≤ captured; role Admin và callback evidence |
| Production | Một assignment active, QC evidence khi Passed | BR-08, đúng file version/qty, active lab năng lực; tổng run quantities không vượt yêu cầu ngoài reprint policy |
| Support | FK đến order/report/refund/run | Report owner là order owner; refund/reprint thuộc cùng order; deadline policy; changes có audit |
| Append-only | PK/FK và restrictive deletes | Runtime DB role không UPDATE/DELETE ledger/review/history/audit; migration role riêng; không cấp db_owner |

Không dùng cascade delete cho tài chính/history. User/ModelProject archive/anonymize theo policy. Các FK phụ có thể thiếu index tùy workload; trước production kiểm execution plan cho query thực tế thay vì index mọi cột.

## 4. Transaction và concurrency

1. **Create generation:** kiểm idempotency → lưu ảnh tạm → transaction account rowversion/UPDLOCK + debit ledger + job/outbox → commit → worker. Nếu commit fail, cleanup file orphan; job không tham chiếu file chưa bền vững.
2. **Advance job:** claim bằng lease hoặc SQL applock; ghi Submitting rồi commit trước HTTP. Nhận taskId ghi bền vững. Crash ở khoảng giữa chuyển NeedsAttention; không giả định provider hỗ trợ idempotency khi chưa có tài liệu.
3. **Checkout:** khóa quote, validate review/customer approval và expiry; insert order/item/history atomically. Unique QuoteId ngăn hai checkout khác key dùng cùng quote.
4. **Capture:** verify event ngoài transaction; BEGIN, unique inbox, lock order/attempt, map trạng thái, history/outbox, COMMIT rồi ack. Unknown callback không có attempt gửi log quarantine đã redacted, đối soát thủ công.
5. **Refund:** lock payment → kiểm đã captured + available → reserve row → commit → gọi gateway với reference ổn định → confirm/query. Unknown giữ reservation; total constraint không phải CHECK thông thường.
6. **Start/cancel:** khóa cùng order và active assignment. Một transition thắng; cancel không chạy đồng thời start in. Mọi transition dùng expected RowVersion.

## 5. Đơn vị, giá và snapshot

- CoordinateUnit của GLB có thể `m`, STL chuẩn `mm`; UpAxis phải xác định, không mặc định “cạnh dài nhất = chiều cao”. Review phát hiện Tripo local đang dùng cạnh dài nhất để suy chiều cao: cần lựa orientation và kiểm actual Z/up-axis trước hứa kích thước.
- GeometryHash nhận diện hình học, AssetHash nhận diện byte file; ConfigurationHash được tạo từ canonical JSON có version cho material/height/finish/engraving. Không hash JSON serialize tùy thứ tự rồi coi là canonical.
- Quote SnapshotJson gồm package code/version, finish, full dimensions, partner unit budget, AI budget per order, defect budget, shipping subsidy, pricing policy và thuế nếu áp dụng. Các khoản cần báo cáo thường xuyên phải promote thành columns ở migration nghiệp vụ, không suy ngân sách thành actual.
- PartnerUnitBudget × quantity khác ProductionRuns.ActualCostVnd theo từng lần in. Cộng chi phí AI từng stage actual, gateway fee và shipment actual; `NULL` = chưa có bằng chứng, không phải miễn phí.
- Record P&L quản trị cần phân biệt cash captured, refunds, delivered revenue theo policy kế toán được nhóm chốt; schema không tự xác nhận doanh thu pháp lý.

## 6. Kiểm chứng và giới hạn

Chạy `powershell -File scripts/Verify-Design.ps1`. Script chỉ tạo/xóa tên DB tự sinh `TheBoys_DesignCheck_<32 hex>`, dùng integrated SQL auth, không đọc connection ứng dụng. Kiểm compile, 34 tables, fixture hợp lệ và các ràng buộc trọng yếu. [Kết quả](../review/validation.vi.md).

IdempotencyRequests được tạo/hoàn tất trong cùng transaction với mutation DB. ResourceId là tham chiếu đa hình do service kiểm, không có FK tổng quát; chỉ trả DTO dựng lại theo quyền hiện tại, không lưu response chứa PII. Với HTTP external, lưu trạng thái Processing/NeedsAttention trước và giữ reference ổn định; expiry/cleanup key không được diễn ra khi còn nghiệp vụ/financial retry. Pending cancel/privacy phải có handler/queue và audit, không chỉ insert bảng.

Đây không phải migration/data backfill đã áp dụng; không có EF entities/handlers vNext runtime trong đợt này. Trước cutover phải triển khai invariants ở application, quyền DB append-only, tests concurrency, migrations và kiểm restore theo [migration plan](../../database/migrations/README.md).
