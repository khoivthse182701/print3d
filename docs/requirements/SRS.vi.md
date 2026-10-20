# Software Requirements Specification — THE BOYS Backend

**SRS 1.0 · 19/10/2026 · Draft for team review.** Chủ sở hữu nghiệp vụ: nhóm THE BOYS (chưa chỉ định cá nhân). Người duyệt: product owner và giảng viên/nhóm SWD. Mọi thay đổi đã duyệt phải ghi version, FR/BR/UC bị ảnh hưởng và migration/API tương ứng trong PR. Đây là baseline yêu cầu để triển khai, không phải giấy chứng nhận tính năng đã hoàn thành.

## 1. Mục đích, phạm vi và tài liệu liên quan

Hệ thống giúp khách tạo model từ ảnh, xem và chốt mẫu, đặt thành phẩm cá nhân hóa, trả tiền, theo dõi sản xuất qua xưởng đối tác và nhận hỗ trợ sau giao. THE BOYS điều phối, không được giả định sở hữu máy in. Pilot hướng tới khách B2C tại TP.HCM theo CP3.

Backend là repository và deployment riêng. Frontend là client qua HTTPS API; FE không truy cập SQL, không giữ API key provider và không tự quyết giá/quyền/trạng thái đơn. Chưa review FE; viewer, form và thông báo FE là yêu cầu giao tiếp cần triển khai ở repo FE.

Tài liệu cấu thành SRS: [use cases](use-cases.vi.md), [traceability](traceability.vi.md), [API](../api/backend-contract.vi.md), [diagram](../architecture/README.md), [DB](../architecture/database-design.vi.md), [nghiên cứu](../research/competitor-study.vi.md), [kiểm kê hiện trạng](../review/flow-inventory.vi.md). Nguồn nghiệp vụ: [CP3](../business/CP3_THE_BOYS_DaChinhSua.txt). Slide Ch06 trang 5–6 và 17 yêu cầu FR/NFR có thể kiểm chứng, truy vết và use case có tiền/hậu điều kiện, main/alternative sequences.

### 1.1 Thuật ngữ

| Thuật ngữ | Nghĩa |
|---|---|
| GenerationJob | Lần xử lý AI, gồm các stage và provider task IDs; không đồng nghĩa model |
| ModelVersion | Một phiên bản hình học/cấu hình có định danh bất biến |
| Asset | File riêng có storage key, checksum, format, đơn vị và quyền sở hữu |
| PrintReview | Xác nhận kỹ thuật của Staff cho version tại cấu hình in xác định |
| CustomerApproval | Khách chốt hình dáng, kích thước, vật liệu/cá nhân hóa của version |
| Quote | Báo giá đã lưu, có snapshot và hạn hiệu lực |
| Credit | Lượt nội bộ theo policy; khác credit provider và tiền VND |
| Idempotency | Gửi lại cùng thao tác/key không tạo side effect lần hai |
| Reconciliation | Đối soát kết quả bên ngoài chưa biết chắc, không tự thử lại có phí |
| P0/P1/P2 | P0 bắt buộc trước pilot thu tiền; P1 tiếp theo; P2 mở rộng |

### 1.2 Phạm vi phát hành

- **P0:** image generation, private library/version, kỹ thuật in và chốt mẫu, quote/checkout/payment, điều phối xưởng qua Staff, QC/giao hàng, khiếu nại/refund tối thiểu, ledger/audit, bảo mật và vận hành.
- **P1:** text-to-3D, resize/repair tái sử dụng model, mua lượt nếu được phê duyệt, báo cáo đầy đủ, portal đối tác có quyền giới hạn, tự động giao vận.
- **P2:** multi-view, public gallery, 3MF/màu, đơn nhiều mẫu/nhóm và B2B; không nằm điều kiện nghiệm thu P0.
- Ngoài phạm vi: tự huấn luyện foundation model, hệ điều khiển máy in, marketplace người bán độc lập, animation/scene/video, ứng dụng mobile native.

## 2. Actor, quyền và môi trường

| Actor | Trách nhiệm / giới hạn |
|---|---|
| Guest | Xem gói, đăng ký/đăng nhập; không tạo AI ẩn danh |
| Customer | Quản lý tài khoản, model/đơn của mình; chốt mẫu, trả tiền, gửi khiếu nại |
| Staff | Review, đối soát job, điều phối xưởng, ghi nhận QC/giao hàng, xử lý hỗ trợ; không đổi role/giá/refund tiền tùy ý |
| Admin | Quyền Staff + quản lý role/policy/gói, duyệt refund/cấp credit có audit; không được bỏ qua invariant |
| AI provider | Xử lý job ngoài hệ thống; không được tin vô điều kiện về units/trạng thái/URL |
| Payment gateway | Tạo phiên thanh toán, event xác thực, query giao dịch/refund |
| Carrier | Bổ sung P1; P0 Staff ghi nhận tracking/bằng chứng |
| Partner lab | Đối tác nghiệp vụ ngoài hệ thống; P0 giao tiếp qua Staff, chưa phải tài khoản được xem tất cả đơn |
| Scheduler/worker | Khởi động poll, outbox, retry và cảnh báo theo lịch |

Ma trận RBAC: mọi tài nguyên Customer bị lọc owner tại server; Staff chỉ đọc dữ liệu cần vận hành; Admin cũng phải có lý do cho điều chỉnh. Partner portal nếu bổ sung chỉ đọc assignment của chính xưởng, không kế thừa Staff. Actor business và role đăng nhập là hai khái niệm khác nhau.

Stack hiện có: ASP.NET Core/.NET 10, EF Core/SQL Server, MediatR, JWT, provider Meshy/Tripo. Mục tiêu dùng API + worker trong cùng codebase, storage riêng; cho phép tách worker process khi có tải. Mọi thời gian lưu UTC, hiển thị theo Asia/Ho_Chi_Minh; VND dùng decimal, dimensions mm, thể tích cm³ có nhãn rõ.

## 3. Yêu cầu chức năng

Từ “phải” quy định tiêu chí mục tiêu. Hiện trạng chi tiết ở inventory. Mỗi FR có tiêu chí tối thiểu; kế hoạch test và liên kết UC ở traceability.

| ID | Ưu tiên | Yêu cầu và tiêu chí nghiệm thu |
|---|---|---|
| FR-01 | P0 | Đăng ký chỉ Customer, email normalized duy nhất, hash mật khẩu, login không lộ email tồn tại; payload role lạ bị từ chối. Hai request đăng ký trùng không tạo hai user. |
| FR-02 | P0 | Verify email, reset mật khẩu, refresh/logout/revoke và khóa tài khoản. Token reset một lần có expiry; refresh reuse thu hồi session family; khóa/revoke chặn thao tác nhạy cảm ngay. |
| FR-03 | P0 | Tạo job ảnh PNG/JPEG/WEBP ≤15 MiB, decode và giới hạn 40 megapixel đề xuất, height 100–120 mm, credit hợp lệ, consent policy. Key UUID trùng/cùng payload trả cùng job; khác payload 409; sai ảnh không trừ lượt. |
| FR-04 | P0 | Worker lưu checkpoint/stage trước side effect, dùng adapter theo provider/version của job. POST timeout → NeedsAttention; GET lỗi retry backoff; không phát sinh generation mới khi resume. |
| FR-05 | P0 | Lưu/đối soát quota bằng ledger và bucket Trial/Purchased; trừ một lần/job; hoàn một lần nếu generate thất bại xác định. Lỗi stage sau không tự hoàn. Admin cấp phải có lý do, actor và key. |
| FR-06 | P0 | Liệt kê/xem/tải model riêng tư có phân trang; file qua owner check và storage key server. User khác nhận 404; GLB không được nhúng token trong query URL. |
| FR-07 | P0 | Tách project/job/version/asset; mọi thay đổi hình học/kích thước sinh version mới. Order pin version/hash; không overwrite file đã được chốt. P0 version initial + replacement thủ công; công cụ resize/repair tự động P1. |
| FR-08 | P0 | Staff xem dimensions, coverage analysis và checklist slicer; duyệt/từ chối có lý do, người, thời điểm, bằng chứng. Review append-only; warning phải có xử lý rõ; không tự duyệt vì provider succeeded. |
| FR-09 | P0 | Khách xác nhận version/configuration hash, chiều cao, vật liệu, engraving và phạm vi hoàn thiện. Đổi bất kỳ mục nào làm approval cũ không dùng cho quote mới. |
| FR-10 | P0 | Hiển thị gói active; quote server chứa version/configuration, giá, quantity, shipping, policy version và expiry 30 phút đề xuất. Quote hết hạn/đổi nội dung trả 409 và quote mới, không tự nâng giá. |
| FR-11 | P0 | Checkout nguyên tử từ quote hợp lệ + approval: tạo PendingPayment order, item/snapshot địa chỉ và số tiền. Retry key không tạo đơn mới. P0 một item/đơn, quantity 1–10. |
| FR-12 | P0 | Tạo payment attempt ở gateway, tiền và currency do BE quyết. Chỉ event/query đã xác thực, đúng order/amount/currency, chưa consume mới cập nhật paid. Redirect FE không xác nhận thanh toán. |
| FR-13 | P0 | Nhận duplicate/out-of-order/late payment; inbox event duy nhất theo gateway/event ID. Callback trùng trả ack không tăng tiền/đơn hai lần; sai tiền đưa đối soát; payment muộn không tự mở lại đơn hủy. |
| FR-14 | P0 | Khách hủy PendingPayment; đã paid chỉ tạo cancel request để Staff/Admin xử lý trước sản xuất. Refund tiền chỉ Admin duyệt; phần tổng refund reserved/succeeded không vượt capture; retry không hoàn hai lần. |
| FR-15 | P0 | Staff chọn xưởng active/có năng lực, ghi offer/accept/reject/expiry, đúng version và production quantity. Mỗi order chỉ một assignment active trong MVP; từ chối cho phân công lại có history. |
| FR-16 | P0 | ProductionRun đi Printing → PostProcessing → QC. Bắt đầu khi paid + approved + assignment accepted. QC pass mới Packaged; fail tạo reprint run hoặc hỗ trợ, lưu actual cost và evidence. |
| FR-17 | P0 | Shipping cần carrier/tracking hoặc hand-delivery evidence. Ghi event giao thất bại/giao lại/đã giao; chỉ Delivered với bằng chứng. Staff P0 nhập thủ công có audit. |
| FR-18 | P0 | Customer gửi defect report của đơn mình kèm ≥1 bằng chứng; Staff đánh giá, Admin duyệt refund hoặc Staff chốt reprint theo policy. Một complaint có nhiều evidence/resolution, không ghi đè lịch sử. |
| FR-19 | P0 | Thông báo in-app cho job hoàn tất/cần hỗ trợ, paid, review rejected, shipped, refund; outbox commit cùng nghiệp vụ; lỗi gửi không rollback giao dịch; delivery key chống trùng. Email P1. |
| FR-20 | P0 | Admin quản lý user role, khóa account, gói/price version và credit policy; không tự hạ role hoặc làm mất admin cuối; mọi mutation có audit before/after đã loại PII/secret. |
| FR-21 | P0 | Staff/Admin tìm/filter queue theo status/date/provider/age, theo dõi lịch sử job/đơn và reconcile. Không hiển thị stack trace/API key/raw gateway payload cho Customer. |
| FR-22 | P1 | Dashboard định nghĩa funnel, p50/p95, job success/print pass, paid/delivered/cancel/refund, chi phí thực tế. Báo cáo không coi budget snapshot là actual expense, không tính paid=delivered. |
| FR-23 | P0 | Consent private ảnh/model; xóa hoặc anonymize theo retention policy có legal hold/đơn đang xử lý; audit không lưu ảnh/bí mật. Public share chỉ ở P2 sau consent riêng. |
| FR-24 | P1 | Text-to-3D qua cùng cơ chế quota/idempotency/provider capability, input prompt và policy; không bật nếu adapter chưa hỗ trợ. |
| FR-25 | P1 | Tạo version resize/repair từ model có sẵn; ghi parent version, chi phí operation, re-analysis và re-approval; không tự tạo lại ảnh có phí. |
| FR-26 | P1 | Nếu bán lượt: gói giá version hóa, payment xác nhận mới cấp Purchased ledger một lần; hoàn tiền thu hồi phần chưa dùng theo policy được nhóm duyệt. |
| FR-27 | P1 | Portal xưởng nếu triển khai có scoped membership/assignment, cập nhật tiến độ và báo chi phí; không có quyền khách khác/đổi giá/duyệt refund. |
| FR-28 | P2 | Multi-view/public gallery/đơn nhóm là change request riêng; mỗi tính năng cần consent, contract, DB và acceptance test trước bật. |

## 4. Quy tắc nghiệp vụ

| ID | Invariant |
|---|---|
| BR-01 | Balance Trial/Purchased ≥0; ledger là nguồn đối soát; tài khoản provider không phải tài khoản credit của Customer. |
| BR-02 | Mỗi operation key duy nhất theo owner + operation; cùng key khác fingerprint → conflict; không xoá key khi còn khả năng retry. |
| BR-03 | Job bind provider, model, pricing/policy version từ khi tạo; đổi default chỉ ảnh hưởng job mới. |
| BR-04 | Generate failed xác nhận → hoàn app credit một lần; kết quả mơ hồ giữ chờ đối soát; sự kiện provider hoàn tiền không tự đồng nghĩa hoàn lượt khách. |
| BR-05 | Model version/asset hash bất biến sau published; unit mm và up-axis metadata rõ ràng; volume mesh không phải resin sau slicing. |
| BR-06 | Review kỹ thuật và customer approval là hai quyết định riêng. Chúng phải tham chiếu cùng version/configuration của item. |
| BR-07 | Giá/thành phần quote do server tính; P0 package × quantity + shipping, không định giá bằng bounding-box volume. Giá seed 390k, 2 trial là cấu hình pilot. |
| BR-08 | Payment confirmed đủ tiền + review approved + approval đúng mẫu + assignment accepted mới cho in. Admin không bỏ qua. |
| BR-09 | PaymentState, FulfillmentState và ComplaintState độc lập; hoàn tiền không xóa lịch sử sản xuất/giao hàng. |
| BR-10 | Refund reservation + refund succeeded ≤ captured amount; transaction serializable/row lock khi reserve; provider unknown giữ reservation. |
| BR-11 | Chi phí theo đơn/sản phẩm khai báo rõ: partner unit × qty; AI per job/order thực tế; shipping/gateway per order; defect budget là dự phòng. |
| BR-12 | OrderStatusHistory và AuditLog chỉ append; snapshot trên đơn không thay đổi khi gói/địa chỉ cá nhân thay đổi. |
| BR-13 | Đơn đã sản xuất không được hủy trực tiếp; khiếu nại xét theo policy, tạo reprint/refund riêng. Không tự gán lỗi provider/xưởng cho khách. |
| BR-14 | Địa chỉ gồm address line, ward/commune, province/city và mã carrier tùy chọn; district legacy giữ cho tương thích, không dùng enum cố định địa giới. |
| BR-15 | MVP mỗi đơn một item, nhiều bản cùng mẫu; DDL cho phép nhiều item để mở rộng nhưng API P0 phải chặn thêm item khác. |

## 5. Yêu cầu phi chức năng và phép đo

Đây là **mục tiêu đề xuất**, chưa phải kết quả load test/SLA đã đạt. Nhóm phải chốt tải và ngân sách trước triển khai.

| ID | Mục tiêu kiểm chứng | Cách nghiệm thu |
|---|---|---|
| NFR-01 Security | 100% route private kiểm ownership/role; HTTPS; secret ngoài Git; proxy allowlist; reset/refresh hash, payload size/decode limit | Ma trận negative tests theo role, spoof IP, IDOR, file hỏng, scan secrets ở CI |
| NFR-02 Integrity | Replay/concurrency không double debit/capture/refund/start run; mọi tài chính có audit | 20 request đồng thời cùng key và khác key trên credit cuối; duplicate/out-of-order webhook; DB constraints |
| NFR-03 Performance | 50 concurrent users, 10k jobs/10k orders fixture: GET p95 ≤500 ms; accept job p95 ≤2 s sau upload (không tính provider); error rate <1% | Load 10 phút, giữ thông số máy/SQL/provider mock; file lớn đo riêng |
| NFR-04 Availability | Pilot API mục tiêu 99.5%/tháng ngoài maintenance đã thông báo; provider outage không làm mất job | Readiness SQL/storage, retry bounded, alert job age >60 phút (không auto fail/refund) |
| NFR-05 Recovery | RPO ≤24 giờ, RTO ≤4 giờ đề xuất; backup cả SQL/file metadata và blob | Restore sang môi trường biệt lập; kiểm checksum file referenced và replay outbox |
| NFR-06 Observability | traceId xuyên API/job/payment; log không chứa token, raw ảnh, đầy đủ địa chỉ; metric queue age, retry, cost, unknown outcomes | Inject failure và tìm được một trace + alert trong 5 phút |
| NFR-07 Maintainability | Domain không tham chiếu EF/HTTP; adapter external có contract tests; migrations chạy từ mọi baseline hỗ trợ | Build/CI, review dependency graph, migration tests |
| NFR-08 Compatibility | JSON camelCase, enum string; UTC ISO8601; VND decimal; file format/version/units rõ; backward-compatible API hoặc `/api/v2` | OpenAPI diff và FE contract tests |
| NFR-09 Privacy | Private mặc định; retention configurable, deletion được log, order active/retention hold không bị phá | Test delete/hold, kiểm không lộ raw provider response và PII trong audit |
| NFR-10 Testability | Provider/gateway/clock/storage thay được bằng fake; SQL tests dùng DB riêng; không dùng key trả phí trong CI | Chạy test offline provider; rollback/migrate scratch DB; báo cáo số pass/fail |

## 6. Interface và dữ liệu

API hiện tại được giữ ở `/api`; hợp đồng mục tiêu khi thay semantics checkout dùng `/api/v2`. Multipart upload, JSON nghiệp vụ; GET phân trang có limit tối đa 100. Mutations nhạy cảm dùng Idempotency-Key và expected rowversion/ETag. Lỗi trả ProblemDetails có code/traceId; 400 sai input, 401 chưa auth, 403 sai role, 404 tài nguyên không được thấy, 409 stale/key conflict, 422 policy/domain invalid, 429 rate limit, 503 provider chưa cấu hình.

Gateway callback dùng raw-body signature và validation theo adapter gateway được chọn; không tự bịa thuật toán chữ ký. Chỉ publish event sau transaction commit qua outbox. AI adapter phải công bố capability, unit/axis và output format; URL tải phải HTTPS allowlist và giới hạn redirect/bytes. DB/file lưu private; signed URL nếu có TTL ngắn và scope đúng asset.

Schema, khóa, nullable, precision và ownership xem DB design/DDL. SRS không yêu cầu mọi bảng P1 phải được deploy vào MVP ngay; thứ tự migration đã tách giai đoạn.

## 7. Vòng đời, ngoại lệ và acceptance gate

Xem state diagrams và transition table. Các nhánh bắt buộc nghiệm thu: credit hết/đồng thời, key replay/mismatch, provider POST ambiguous/GET 429, file lỗi/expired URL, review reject/đổi version, quote hết hạn, thanh toán trùng/sai tiền/đến muộn, xưởng từ chối, QC fail, giao thất bại, hủy có tiền, refund unknown và restore DB/file.

P0 hoàn tất khi mọi FR P0 có test pass và minh chứng demo, không còn R01–R07/R09 chặn pilot, restore diễn tập thành công, FE consumer test đúng hợp đồng, một model thật đi qua review/slicer/in kiểm chứng, một giao dịch sandbox gateway đi qua capture/refund. 20 tests baseline chưa thỏa gate này.

## 8. Giả định và câu hỏi mở cần chốt

| ID | Mặc định thiết kế | Quyết định cần nhóm chốt |
|---|---|---|
| A01 | 2 trial; 100–120 mm; package 390k, qty 1–10 | Policy, vật liệu/finish và quyền export thương mại |
| A02 | Quote 30 phút; checkout 30 phút | TTL/payment late policy theo gateway |
| A03 | Một gateway, VND, trả đủ trước in | Chọn gateway, phí thực, quy trình manual reconciliation |
| A04 | Staff làm việc với xưởng bên ngoài | Năng lực xưởng, lead time, tiêu chí QC, ai chịu reprint |
| A05 | Khiếu nại trong 7 ngày sau giao là đề xuất | Nhóm phê duyệt policy thực tế; không tự diễn giải thành tư vấn pháp lý |
| A06 | Retention ảnh/model không gắn đơn 90 ngày đề xuất | Quyền sử dụng ảnh, yêu cầu lưu tài chính, thời hạn backup và legal hold |
| A07 | BE riêng, FE dùng OpenAPI | Repo FE, tech stack FE, rubric chấm cuối của giảng viên |
| A08 | Chưa chọn Tencent làm adapter chạy thật | API Global/khu vực, quyền tài khoản, license, chi phí và benchmark |

Các giả định chưa chốt không được mô tả là điều khoản đã được khách/đối tác chấp nhận. Thay đổi A01–A06 phải cập nhật SRS, policy version và dữ liệu snapshot liên quan.
