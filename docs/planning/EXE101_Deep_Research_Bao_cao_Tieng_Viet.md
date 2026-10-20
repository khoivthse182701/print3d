# EXE101 – THE BOYS / PRINT3D
## BÁO CÁO NGHIÊN CỨU CHUYÊN SÂU VÀ KẾ HOẠCH HOÀN THIỆN BACKEND

**BẢN DỊCH TIẾNG VIỆT TỪ BÁO CÁO DEEP RESEARCH**  
**Mốc đánh giá:** ngày 20/10/2026 (múi giờ Asia/Ho_Chi_Minh)  
**Repository:** https://github.com/khoivthse182701/print3d  
**Branch:** `main`  
**Commit đối chiếu:** `c4580975456325e14f61f3f951ae094cd6097f6b`  
**Thời điểm commit:** 00:51:20 UTC, tương đương 07:51:20 ngày 20/10/2026 giờ Việt Nam.  
**Mục đích:** Dịch và trình bày đầy đủ kết luận, bảng đối chiếu, kế hoạch triển khai và bằng chứng của báo cáo nghiên cứu đã hoàn tất. Đây là bản dịch của kết quả nghiên cứu, **không phải một lần rà soát commit mới hơn**.

---

# 1. GIẢ ĐỊNH VÀ TÓM TẮT ĐIỀU HÀNH

## 1.1. Các giả định và giới hạn

Báo cáo đánh giá repository `khoivthse182701/print3d`, nhánh `main`, tại ngày 20/10/2026. Repository phân biệt rõ phần backend đang thực sự chạy được và thiết kế SRS/database cho phiên bản tiếp theo. Trong bản dịch này, sự phân biệt đó được giữ nguyên: **có trong tài liệu không đồng nghĩa đã code xong**.

Commit mới nhất tại mốc nghiên cứu có thông điệp `docs: add backend review, SRS, database design and repository structure`. Commit này chủ yếu bổ sung nền tảng tài liệu, review, thiết kế và CI; không có nghĩa toàn bộ SRS mục tiêu đã được triển khai. Baseline dùng để kiểm kê luồng thực thi trong tài liệu là commit `04874ed`.

| Hạng mục | Cách xử lý trong báo cáo |
|---|---|
| Hạ tầng chạy production/cloud | Chưa xác định. Khuyến nghị trung lập với nền tảng, phù hợp ASP.NET Core và SQL Server. |
| Docker/Kubernetes/App Service/VM | Chưa xác định. Chỉ đưa ra yêu cầu cho artifact, cấu hình môi trường và quy trình phát hành. |
| SLA | Chưa xác định. Các ngưỡng giám sát chỉ là đề xuất kỹ thuật. |
| RTO/RPO | Chưa xác định. Cần kiểm thử backup/restore trước pilot. |
| Cổng thanh toán cuối cùng | Chưa chốt. Hệ thống có thiết lập liên quan thanh toán nhưng đơn hàng hiện vẫn ở chế độ demo/offline, chưa có payment handler thực. |
| Frontend | Không thuộc phạm vi repository được đánh giá; FE được quản lý riêng. |
| Gói thương mại Meshy/Tripo | Chưa xác định. Không được hard-code hạn mức của một gói bất kỳ; hạn mức Meshy thay đổi theo plan. |
| Tài khoản, khu vực, quy trình mở dịch vụ Tencent | Chưa xác định. Tencent chỉ là nhà cung cấp thứ ba tiềm năng. |
| Khả năng làm việc của 2 BE | Effort là **giờ kỹ thuật ước lượng**, không phải cam kết lịch. Không tính thời gian xin phê duyệt, kích hoạt tài khoản AI, công việc FE hoặc thời gian in mẫu thật. |

## 1.2. Đánh giá tổng thể

Repository đã có nền tảng **tài liệu kỹ thuật tốt hơn đáng kể so với mức hoàn thiện sản phẩm thực tế**. Nhóm hiện có SRS nhất quán, 22 use case, thiết kế database mục tiêu gồm 34 bảng, các diagram, backlog ưu tiên và GitHub Actions CI chạy được. Tại commit trên, hai job CI (`test` trên Windows và `diagrams` trên Ubuntu) đã chạy thành công. Pipeline gồm build, test, kiểm tra thiết kế schema mục tiêu và kiểm tra tài liệu.

Tuy nhiên, phần ứng dụng thực thi hiện vẫn chủ yếu là **pipeline phục vụ demo**:

`Tải ảnh lên → AI tạo model → phân tích STL → Staff duyệt kỹ thuật → tạo đơn offline → Staff cập nhật trạng thái đơn thủ công.`

Theo kiểm kê chính thức trong repository, có **32 nhóm business flow**, gồm **13 đã triển khai ở mức có code chạy**, **9 triển khai một phần**, **10 chưa triển khai**. Nhãn “đã triển khai” ở đây không đồng nghĩa đủ điều kiện vận hành thương mại.

| Phân hệ | Tình trạng thực tế | Yêu cầu đích | Mức độ |
|---|---|---|---|
| Vòng đời tài khoản | Chỉ đăng ký, đăng nhập, lấy thông tin bản thân | Xác minh email, reset mật khẩu, refresh token, thu hồi phiên/logout, khóa tài khoản | P0 – chặn phát hành |
| Credits | Số dư và cờ trừ/hoàn trên job | Tài khoản credit và sổ cái append-only | P0 |
| Vòng đời model | `GenerationTask` chứa model, file và review | Project → Job → Version → Asset | P0 |
| Khách hàng duyệt mẫu | Chưa có | Phê duyệt bất biến, gắn với version/cấu hình | P0 |
| Báo giá | Tính tức thời trong response | Báo giá lưu DB, có version và hạn dùng | P0 |
| Thanh toán | Entity `Payment` tối giản, chưa có API/handler | Payment attempt + event inbox xác thực + đối soát | P0 |
| Hoàn tiền | Chưa có | Quy trình reserve/submit/reconcile chống hoàn hai lần | P0 |
| Đối tác in | `PartnerLab` và khóa ngoại | Giao việc/nhận/từ chối/lịch sử | P0 |
| Sản xuất/QC | Thay đổi status ở mức demo | Production run, bằng chứng QC, tái in | P0 |
| Thông báo | Chưa có | Transactional outbox + notification | P0 |
| Chọn AI provider | Một provider toàn cục lúc khởi động | Ghim provider và model theo từng job | P0 – độ tin cậy |
| Secrets | Từng commit khóa Meshy vào Git history | Đưa secrets ra ngoài repo và quét lịch sử | Critical |
| Hình học 3D | Lỗi chiều cao/up-axis đã được tái hiện | Kích thước vật lý tin cậy trước khi in | P0 |

**Phát hiện bảo mật cần xử lý ngay:** commit `a7a5006c636fafc1d1f4667fe50e91b32582e183` từng chứa một credential Meshy trong các file cấu hình được Git theo dõi. Commit sau đã xóa giá trị khỏi HEAD, nhưng không xóa nó khỏi lịch sử Git. Phải giả định khóa đã lộ, thực hiện thu hồi/thay khóa, kiểm tra usage/billing và quét lịch sử. Không hiển thị lại khóa trong tài liệu này.

**Định hướng triển khai:** trước hết vá bảo mật và lỗi đúng/sai dữ liệu; sau đó chuyển database đích từng phần; hoàn thiện model/ledger/quote/payment; bổ sung phối hợp nhà in, QC, hỗ trợ; cuối cùng mới qua các cổng phát hành. Không nên ưu tiên thêm nhiều loại AI generation khi các bất biến tiền và dữ liệu chưa được xây dựng.

Báo cáo đưa ra ngân sách P0 xấp xỉ **215–235 giờ kỹ thuật** cho 2 BE ở phần tóm tắt; khi cộng các khoảng công việc theo milestone, khoảng đánh giá chi tiết là **212–252 giờ**, và nên **lập kế hoạch khoảng 225–230 giờ** làm mốc trung tâm. Bản thử nghiệm Tencent là phần tùy chọn cộng thêm **10–14 giờ**. Đây là ước lượng lập kế hoạch, không phải số giờ thực đo.

---

# 2. BASELINE REPOSITORY VÀ ĐỐI CHIẾU YÊU CẦU

## 2.1. Cấu trúc repository hiện tại

| Layer/module | Trách nhiệm hiện tại | Đường dẫn tiêu biểu |
|---|---|---|
| Domain | Entity và enum nghiệp vụ | `src/TheBoys.Domain/Entities/`, `Enums/` |
| Application | Commands, queries, MediatR, use case | `src/TheBoys.Application/Admin`, `Generations`, `Orders`, `Printing` |
| Infrastructure | EF Core, AI providers, lưu file, worker | `src/TheBoys.Infrastructure/Persistence`, `Printing` |
| Web API | Controller, JWT/Auth, rate limit, DI/composition root | `src/TheBoys.WebApi/` |
| Tests | Unit, contract, integration, fixture | `tests/TheBoys.Tests/` |
| Requirements | SRS, use case, traceability | `docs/requirements/` |
| Architecture | Database design và Mermaid diagram | `docs/architecture/` |
| Target database | File thiết kế `vnext.sql`, chỉ dùng để review | `database/design/` |
| Runtime migration | EF Core migrations thực thi | `src/TheBoys.Infrastructure/Persistence/Migrations/` |
| Planning | Backlog backend, release gates | `docs/planning/` |

Stack: .NET 10, ASP.NET Core, EF Core và SQL Server. Frontend không nằm trong phạm vi repository này. HTTP surface có **20 controller routes + 2 minimal endpoints**, tổng cộng **22 tổ hợp method/route**. Controller chính: `AuthController`, `GenerationsController`, `OrdersController`, `AdminController`; `ApiControllerBase` xử lý hành vi chung.

Subsystem tạo model tập trung tại:

```text
src/TheBoys.Infrastructure/Printing/
  GenerationWorker.cs
  GenerationProcessor.cs
  MeshyProvider.cs
  TripoProvider.cs
  StlGeometry.cs
  LocalModelStorage.cs
```

`GenerationWorker` được đăng ký dạng hosted service. `GenerationProcessor` xử lý các giai đoạn của công việc. Dependency Injection hiện chọn **một** implementation `IModelGenerationProvider` dựa trên `AiProviders:Active` khi ứng dụng khởi động.

Báo cáo validation ghi nhận **20 bài test PASS, 0 FAIL, 0 SKIP**, chạy SQL LocalDB và mock/fake phản hồi của AI providers. Chưa có lời gọi AI trả phí thực trong lượt test này. GitHub Actions tại commit được kiểm tra cũng thành công ở job build/test và job diagram.

## 2.2. Tài liệu DOCX và nguồn yêu cầu

Lượt quét cây repository đệ quy trên branch `main` **không tìm thấy file `.docx`**. Vì vậy báo cáo Deep Research không thực sự đọc được một SRS Word trong repo. Nguồn yêu cầu được dùng là Markdown/text, đặc biệt:

```text
docs/requirements/SRS.vi.md
docs/requirements/use-cases.vi.md
docs/requirements/traceability.vi.md
docs/review/flow-inventory.vi.md
docs/architecture/database-design.vi.md
docs/business/...
```

SRS xác định **28 functional requirements**, phân P0/P1/P2:

- **P0:** Image-to-3D; quản lý model/version và file riêng tư; review khả năng in; phê duyệt của khách; quote/checkout/payment; phối hợp nhà in; QC/giao hàng; khiếu nại/hoàn tiền; ledger/audit; bảo mật/vận hành.
- **P1:** Text-to-3D; resize/repair; mua thêm credits; reporting; cổng thông tin đối tác.
- **P2:** Nhiều góc ảnh/multi-view; thư viện model chia sẻ công khai; đơn nhóm; tiện ích B2B.

Tài liệu use case quy định **UC-01 đến UC-22**. Các UC-08 đến UC-14 yêu cầu checkout thật, xử lý thanh toán đã xác minh, refund, giao việc đối tác, QC, bằng chứng giao hàng, xử lý khiếu nại. UC-15 đến UC-22 bao gồm chính sách quản trị, thông báo, vận hành, quyền riêng tư, text generation, sửa model, mua credit và partner portal giới hạn.

## 2.3. Kiểm kê 32 business flow

**Quy ước:**

- **Đã triển khai:** Có code thực thi ở một mức độ; không hàm ý production-ready.
- **Một phần:** Có entity, field, state, route hoặc thao tác demo nhưng thiếu trọn luồng nghiệp vụ.
- **Chưa triển khai:** Thiếu handler/endpoint/state machine dùng được.

| Mã | Business flow | UC / FR | Trạng thái | Bằng chứng hiện tại / phần thiếu | Việc cần hoàn thiện |
|---|---|---|---|---|---|
| F01 | Đăng ký, đăng nhập, `me` | UC-01, FR-01 | Đã triển khai | `AuthController.cs` | Chưa xác minh email |
| F02 | Reset mật khẩu, session, logout | UC-01, FR-02 | Chưa triển khai | Không có các handler/route tương ứng | Refresh, token reset, revoke, lockout |
| F03 | Tải ảnh và tạo job | UC-02, FR-03 | Đã triển khai | `GenerationsController.Create`, `CreateGenerationTaskCommand` | Decode ảnh thật, giới hạn pixel, consent |
| F04 | Text-to-3D | UC-19, FR-24 | Một phần | Có `GenerationTask.Prompt` và cấu trúc task type | Chưa có public route/command/orchestrator |
| F05 | Multi-view | FR-28 / P2 | Chưa triển khai | Không có API nhận nhiều ảnh | Phải thiết kế mới |
| F06 | Generate → resize → convert → analyze | UC-02, FR-04 | Đã triển khai | Worker, Processor, adapters | Provider pinning, checkpoint |
| F07 | Timeout mơ hồ, recover | UC-03 | Đã triển khai | `RecoverGenerationTaskCommand`, `/recover` | Xác thực provider task thực sự thuộc đầu vào gốc |
| F08 | Hoàn credit khi generation lỗi | UC-04, FR-05 | Đã triển khai | Job/account flags và xử lý trong processor | Chưa có credit ledger |
| F09 | Danh sách/xem/tải model | UC-05, FR-06 | Đã triển khai | Generation queries, `/files/{kind}` | Chưa quản lý project/version |
| F10 | Edit, resize, version, chọn model | UC-05/20, FR-07/25 | Chưa triển khai | Chưa có aggregate `ModelVersion` | Resize hiện không phải lifecycle version hoàn chỉnh |
| F11 | Staff review khả năng in | UC-06, FR-08 | Đã triển khai | `ReviewGenerationTaskCommand`, `/review` | Review bị ghi đè; thiếu lịch sử/bằng chứng |
| F12 | Khách hàng duyệt model | UC-07, FR-09 | Chưa triển khai | Không có approval entity/handler | Gắn duyệt vào model version/config hash |
| F13 | Package và báo giá | UC-07, FR-10 | Đã triển khai | `GetPrintQuoteQuery`, `/quote` | Chưa lưu báo giá có version/expiry |
| F14 | Tạo đơn hàng | UC-08, FR-11 | Đã triển khai | `OrdersController.Create`, `CreateOrderCommand` | Chỉ là đơn demo, không checkout dựa trên quote |
| F15 | Danh sách/chi tiết đơn | UC-08+ | Đã triển khai | OrderQueries và Orders controller | Chưa có timeline event hoàn chỉnh |
| F16 | Hủy đơn | UC-10, FR-14 | Một phần | `CancelOrderCommand` | Chỉ hủy Draft; chưa hủy đơn đã thanh toán/refund |
| F17 | Thanh toán/đối soát | UC-08/09, FR-12/13 | Một phần | Entity/table `Payment` tối giản | Chưa API/adapter/signature/event inbox |
| F18 | Hoàn tiền | UC-10, FR-14 | Chưa triển khai | Không có runtime refund workflow | Reservation, idempotency, reconcile |
| F19 | Nhà in và giao việc | UC-11, FR-15 | Một phần | `PartnerLab`, `Order.PartnerLabId` | Chưa offer/accept/reject/history |
| F20 | Sản xuất và QC | UC-12, FR-16 | Một phần | Chỉ có demo status | Chưa ProductionRun, bằng chứng QC, lịch sử in lại |
| F21 | Giao hàng | UC-13, FR-17 | Một phần | Tracking field + Shipping/Delivered | Chưa shipment aggregate, event và bằng chứng thất bại |
| F22 | Khiếu nại/in lại | UC-14, FR-18 | Một phần | Defect fields gộp trong Order | Chưa case/evidence/resolution lifecycle |
| F23 | Quản lý role người dùng | UC-15, FR-20 | Đã triển khai | AdminController và AdminCommands | Audit chưa đủ |
| F24 | Admin cộng credits | UC-04/15 | Đã triển khai | Admin credit command | Chưa reason + ledger + operation idempotency |
| F25 | Thay đổi/điều chỉnh giá package | UC-15 | Đã triển khai | Admin pricing command | Chưa price version bất biến và CRUD vật liệu |
| F26 | Mua thêm credits | UC-21, FR-26 | Chưa triển khai | Không có customer purchase flow | Chỉ cộng khi payment verified |
| F27 | Notifications | UC-16, FR-19 | Chưa triển khai | Chưa outbox/notification module | Transactional outbox và chống gửi trùng |
| F28 | Reporting/SLA/cost | UC-17, FR-22 | Chưa triển khai | Chưa reporting endpoint | Theo dõi chỉ số, chi phí thật, conversion funnel |
| F29 | Quyền riêng tư/lưu trữ/xóa | UC-18, FR-23 | Một phần | Có vài soft-delete/filter | Chưa purge/anonymize/legal hold workflow |
| F30 | Health/recovery | NFR/Ops | Một phần | `/health` luôn trả OK | Readiness kiểm tra dependency, thử restore |
| F31 | Reorder/group/B2B | P2 | Chưa triển khai | Quantity chỉ hỗ trợ cùng một mặt hàng | Kiến trúc multi-item/schedule |
| F32 | Gallery/share | P2 | Chưa triển khai | Không có entry point | Quyền đồng ý chia sẻ/moderation |

| Trạng thái | Số flow | Tỷ lệ trong 32 nhóm |
|---|---:|---:|
| Đã có chức năng thực thi | 13 | 40,6% |
| Một phần | 9 | 28,1% |
| Chưa triển khai | 10 | 31,3% |

**Không thể kết luận dự án đã hoàn thiện 69%.** Phần khó nhất của P0 — thanh toán, refund, model version bất biến, khách duyệt, ledger, QC và outbox — lại nằm trong nhóm partial/missing.

## 2.4. Những quan sát trực tiếp về code

`GenerationsController.Create` đã kiểm tra idempotency key dạng UUID, chiều cao mục tiêu 100–120 mm, dung lượng ảnh tối đa 15 MiB, nhận dạng chữ ký PNG/JPEG/WEBP, hash nội dung/cấu hình để phát hiện yêu cầu lặp và trả `202 Accepted` cho tác vụ mới. Download đã kiểm tra quyền sở hữu, gửi header `private, no-store`.

**Thiếu sót:** Nhận diện chữ ký file không tương đương giải mã ảnh hoàn chỉnh. Cần thực sự decode để xác minh cấu trúc, width/height và giới hạn megapixel, cũng như chính sách đồng ý xử lý ảnh.

`OrdersController.Create` trả trường:

```text
paymentMode = "demo_offline_no_online_charge"
```

Vì vậy luồng hiện hành chắc chắn là **đặt đơn offline/demo**, chưa phải thanh toán online thực. Lệnh tạo đơn dùng generation task thay vì `QuoteId` đã lưu và customer approval.

**Lỗi STL quan trọng:** Với file STL không đối xứng, hệ thống có thể nhận chiều cao người dùng yêu cầu là 100 mm nhưng sau normalize trục Z thực tế chỉ cao 50 mm. Lỗi bắt nguồn từ việc đồng nhất “cạnh dài nhất” với chiều cao, bỏ qua `up-axis`. Cần fix và bổ sung unit/up-axis metadata trước khi mở rộng providers.

---

# 3. PHÂN TÍCH DATA MODEL, DATABASE VÀ KIẾN TRÚC PROVIDER

## 3.1. Database hiện chạy khác với thiết kế mục tiêu

`ApplicationDbContext` runtime hiện chỉ có **6 DbSet nghiệp vụ**:

```text
Users
PricingPackages
PartnerLabs
GenerationTasks
Orders
Payments
```

Tài liệu thiết kế đã đề xuất **database vNext 34 bảng** trong `database/design/vnext.sql`. Tệp này có kiểm soát để dùng làm **bản nháp thiết kế/review**; nó **không phải một EF Core migration và không đồng nghĩa đã chạy trên SQL Server hiện tại**.

Thiết kế đích đã phân tách các phần P0 quan trọng:

- `CreditAccounts`, `CreditLedger`: số dư và sổ cái cộng/trừ/hoàn credit.
- `ModelProjects`, `GenerationJobs`, `GenerationStages`, `ModelVersions`, `ModelAssets`: quản lý dự án tạo 3D, từng lần gọi AI, giai đoạn, phiên bản model và file.
- `PrintReviews`, `CustomerApprovals`: lịch sử duyệt kỹ thuật và khách hàng phê duyệt bất biến.
- `Quotes`, `Orders`, `OrderItems`, `OrderStatusHistory`: báo giá và luồng bán hàng minh bạch.
- `PaymentAttempts`, `PaymentEvents`, `Refunds`: giao dịch, sự kiện từ gateway và hoàn tiền.
- `PartnerAssignments`, `ProductionRuns`: phân công nhà in, các lần sản xuất và QC.
- `Shipments`, `ShipmentEvents`: vận chuyển và lịch sử giao hàng.
- `DefectReports`, `DefectEvidence`: khiếu nại và chứng cứ lỗi.
- `AuditLogs`, `OutboxMessages`, `Notifications`: truy vết tác động, phát sự kiện đáng tin cậy và thông báo.
- `IdempotencyRequests`, chính sách hủy và quyền riêng tư.

### Quan hệ nghiệp vụ nên triển khai

```text
User ──1:1── CreditAccount ──1:N── CreditLedger
  │
  └──1:N── ModelProject
              ├──1:N── GenerationJob ──1:N── GenerationStage
              │               └──0:1── ModelVersion
              └──1:N── ModelVersion ──1:N── ModelAsset
                                      ├──1:N── PrintReview
                                      └──1:N── CustomerApproval
                                                     │
PricingPackage ───────────────────────────┐           │
                                          ▼           ▼
                                      Quote ──1:1── Order
                                                  ├──1:N OrderItem
                                                  ├──1:N PaymentAttempt ──1:N PaymentEvent
                                                  │          └──1:N Refund
                                                  ├──1:N PartnerAssignment
                                                  │          └──1:N ProductionRun
                                                  ├──1:N Shipment ──1:N ShipmentEvent
                                                  └──1:N DefectReport ──1:N DefectEvidence
```

`AiProviders` và `AiProviderModels` là **đề xuất bổ sung của báo cáo**, chưa tồn tại trong bản DDL vNext. Không được ghi nhận như một bảng đã có.

Thiết kế vNext đã có nhiều bất biến hữu ích: chỉ một debit credit cho mỗi job, một reversal cho debit gốc, unique gateway event ID, giới hạn tổng tiền trong quote, chỉ một partner assignment đang active cho mỗi order và yêu cầu bằng chứng QC đối với production run được đánh dấu pass.

## 3.2. Gap analysis của database

| Tính năng | Runtime hiện tại | Trong vNext | Đề xuất triển khai |
|---|---|---|---|
| Credit ledger | Chưa có; đang dựa balance/flag | Có thiết kế | Entity + migration + backfill |
| Model versions | Chưa có; file/review gộp vào GenerationTask | Có thiết kế | Split project/job/version/asset trước checkout thật |
| Provider registry | Global config | Vẫn chưa có entity riêng | Thêm registry logic và metadata nếu cần |
| Provider secrets | Config | Không được đưa khóa thật vào DB | Secret Manager/env, DB chỉ lưu tên tham chiếu |
| Chi phí AI | Chưa ghi actual cost bền vững | Có ProviderCredits/ActualCostVnd | Gắn giá thực theo model và phiên bản giá |
| Payment attempt | Chỉ có Payment tối giản | Có thiết kế | Chuyển thành payment attempts |
| Webhook inbox | Chưa có | Có thiết kế | Xác thực trước khi cập nhật trạng thái Paid |
| Refund | Chưa có | Có thiết kế | Reserve → submit → reconcile |
| Quote | Chỉ tính trong request | Có thiết kế | Persist + version + expiry |
| Customer approval | Chưa có | Có thiết kế | Gắn version và canonical config hash |
| Review history | Bị ghi đè trên job | Append-only | Chuyển sang bảng lịch sử |
| ProductionRun | Chưa có | Có thiết kế | Phục vụ reprint và actual cost |
| ShipmentEvent | Chưa có | Có thiết kế | Phục vụ bằng chứng giao thành công/thất bại |
| Audit | Chưa đủ | Có thiết kế | Bắt buộc cho admin/payment |
| Outbox | Chưa có | Có thiết kế | Cần trước khi phát thông báo quan trọng |

Lý do phải tách aggregate: `GenerationTask` hiện chứa quá nhiều thứ trong cùng một record — provider task ID, cờ trừ/hoàn credit, trạng thái review, đường dẫn GLB/STL, kích thước hình học và trạng thái xử lý. `Order` cũng gộp snapshot tài chính, địa chỉ giao hàng, nhà in, khiếu nại, tracking và nhiều mốc thời gian. Entity `Payment` mới chỉ có method/reference/amount/status/raw response/timestamps, không đủ để chứa nhiều lần thanh toán, hoàn một phần hoặc lịch sử reconciliation.

## 3.3. Thiết kế Provider Registry

Cơ chế hiện tại:

```text
AiProviders:Active
    ↓
Chọn Meshy HOẶC Tripo
    ↓
DI đăng ký một IModelGenerationProvider duy nhất
```

Vấn đề: nếu job A đã gửi sang Meshy nhưng sau đó admin đổi provider mặc định thành Tripo rồi restart service, phần poll tiếp theo có thể được điều hướng qua sai adapter. Trong khi đó SRS yêu cầu job phải được ghim nhà cung cấp, model và version ngay khi tạo.

Đề xuất abstraction:

```csharp
public interface IModelProviderRegistry
{
    IModelGenerationProvider Resolve(string providerKey);
    ProviderCapabilities GetCapabilities(string providerKey, string modelKey);
}
```

Đăng ký đồng thời các adapter được bật:

```text
MeshyProvider
TripoProvider
TencentHunyuanProvider    // tùy chọn
```

Mỗi generation job cần ghi tối thiểu:

```text
ProviderKey
ProviderModelKey
ProviderApiVersion
ProviderPricingVersion
ProviderAccountScope
CapabilitySnapshot
```

Metadata provider/model có thể quản lý bằng cấu hình versioned hoặc bổ sung bảng `AiProviders`/`AiProviderModels` với các trường `ProviderKey`, `DisplayName`, `IsEnabled`, `AccountScope`, `SecretReference`, `BaseUrl`, `RateLimitStrategy`, `ModelKey`, `SupportsImage`, `SupportsText`, `SupportsMultiView`, `SupportsTexture`, `SupportsDirectStl`, `EffectiveFromUtc`, `RetiredAtUtc`, `CostModelJson`. **Không lưu giá trị API key trong SQL.**

---

# 4. KIỂM TOÁN BẢO MẬT VÀ ĐỘ TIN CẬY

## 4.1. Sự cố lộ Meshy API key – hành động Critical

Commit `a7a5006c636fafc1d1f4667fe50e91b32582e183` đưa credential Meshy vào file cấu hình được commit. Commit `4a1e6157c14c1600cdc474b1564b682874e6baac` sau đó thay giá trị bằng placeholder/trống. Việc xóa secret trong commit mới **không xóa secret trong Git history**.

Các bước cần làm:

1. Thu hồi hoặc rotate credential Meshy ngay lập tức.
2. Kiểm tra usage/billing logs kể từ trước thời điểm bị lộ.
3. Quét **toàn bộ lịch sử Git**, không chỉ source hiện tại.
4. Rotate thêm mọi credential khác nếu scanner phát hiện.
5. Có thể cân nhắc rewrite lịch sử Git sau khi đã rotate, nhưng rewrite history **không thay thế** việc vô hiệu hóa key cũ.
6. Bật secret scanning và push protection ở repository/CI để phòng tái diễn.

`.gitignore` hiện đã ignore `.env`, `.env.*`, biến thể appsettings dùng local, runtime storage, test output và generated artifacts. Tuy nhiên `.gitignore` không giúp bảo vệ một khóa mới được chèn vào `appsettings.json` đang được Git theo dõi.

Hợp đồng cấu hình an toàn nên là:

```text
appsettings.json (theo dõi trên Git)
  → tên cấu hình, giá trị mặc định không bí mật, placeholder rỗng

Local developer
  → dotnet user-secrets hoặc local config bị ignore

CI
  → GitHub Actions encrypted secrets / environment secrets

Production
  → environment variables hoặc secret manager của môi trường triển khai
```

App hiện dùng `WebApplication.CreateBuilder(args)` và đọc JWT/provider settings qua `IConfiguration`, nên về cấu trúc có thể chuyển sang cấu hình bảo mật bên ngoài source.

## 4.2. Rate limiting và proxy headers

Code rate limiter đang đọc trực tiếp một số header như `CF-Connecting-IP` và `X-Real-IP`, trong khi forwarded-header configuration làm trống danh sách proxy/network được tin cậy. Khi production đứng sau reverse proxy, chỉ được tin cậy IP do proxy **được cấu hình hợp lệ** xác thực; không được coi client-supplied headers là chứng cứ IP thật. Cần thêm các bài test cố tình giả mạo forwarded headers.

## 4.3. Những biện pháp tốt đã có

- JWT có kiểm tra issuer, audience, lifetime, signature; clock skew ngắn.
- Role được đọc/kiểm tra lại sau khi token hợp lệ.
- Có danh sách CORS origin cấu hình được.
- Có rate limit theo người dùng và tác vụ generation.
- JSON deserialization hạn chế trường không khai báo (`JsonUnmappedMemberHandling.Disallow`).
- Provider HTTP clients tắt redirect tự động; kiểm tra HTTPS và hostname cấu hình.
- File tải xuống đã kiểm tra ownership và không trả link provider tạm cho khách trực tiếp.
- Generation có cơ chế lưu trạng thái trước POST nhằm tránh retry mù trong tình huống request tạo model đã có thể được tính phí.

## 4.4. Rủi ro triển khai phải có test

| Rủi ro | Phản ứng cần thiết | Mức độ ưu tiên đề xuất |
|---|---|---|
| Credential từng lộ trên Git | Rotate, scan history, bật push protection | Critical |
| Kích thước STL sai trục Z | Sửa thuật toán; lưu unit/up-axis; regression test file bất đối xứng | P0 |
| Chuyển default AI provider làm poll sai job cũ | Provider registry, pin per-job, test đổi provider khi job pending | P0 |
| Client giả mạo IP từ forwarded headers | Trusted proxies + spoofing tests | P0 |
| Dùng credit bằng balance + flag | Atomic ledger, unique debit/reversal, concurrency tests | P0 |
| Review/model bị thay đổi sau khi đặt hàng | ModelVersion và hash bất biến, invalidation rule | P0 |
| Client tự nhận thanh toán thành công | Webhook chữ ký, event inbox, đối soát gateway | P0 |
| Refund trùng | Reservation/state machine/idempotency | P0 |
| Provider trả temporary link hoặc sai host | Tải về private storage, enforce allowlist/HTTPS, checksum | P0 |
| AI POST timeout mơ hồ | `NeedsAttention` + manual reconciliation, không submit lại mù | P0 |
| Chỉ có watertight check | QC thực tế, slicer, kiểm tra thành mỏng/overhang/tự cắt | P0 trước pilot |
| Worker quét tuần tự và file lưu local | Metric/queue, bounded concurrency, object storage trước khi scale nhiều instance | P1 vận hành |

Không nên gọi STL “printable” chỉ vì mesh `watertight`: độ dày thành, self-intersection, overhang, support, độ ổn định đế và khả năng tương thích máy in vẫn cần kiểm tra chuyên dụng hoặc con người xác nhận.

---

# 5. NGHIÊN CỨU CÁC NỀN TẢNG AI 3D BÊN NGOÀI

**Lưu ý về độ tin cậy:** Các đặc tính API và đơn vị giá dưới đây được nghiên cứu từ tài liệu chính thức tại mốc 20/10/2026. Chúng **không chứng minh** tài khoản thực của THE BOYS đang có quyền dùng các endpoint/model đó. Những nội dung liên quan version, hạn mức, billing hoặc commercial terms phải kiểm chứng bằng account và sandbox trước khi chốt hợp đồng tích hợp.

## 5.1. So sánh tổng quan Meshy – Tripo – Tencent Hunyuan 3D

| Nền tảng | API/khả năng được mô tả trong tài liệu chính thức | Mô hình chi phí | Rate/concurrency | Ý nghĩa với EXE101 |
|---|---|---|---|---|
| **Meshy** | REST + Bearer auth; Image-to-3D `/openapi/v1/image-to-3d`; các model `meshy-6-lite`, `meshy-6`, `meshy-7.1`, `latest`; task bất đồng bộ | Trả trước bằng API credits; Meshy 6/7 image-to-3D: 20 credits không texture, 30 có texture, 35 nếu texture 8K; Ultra cộng 5 | Các gói Pro/Premium/Ultra/Studio được báo cáo có 20 requests/s, hàng đợi tùy gói 10/30/100/20; Enterprise 100 requests/s và queue mặc định 100 | Đường dẫn endpoint hiện khá khớp; cần pin model, điều tiết 429, ghi actual cost |
| **Tripo** | Tài liệu chính thức mới dùng Bearer auth và `POST https://api.tripo3d.ai/v2/openapi/task`; upload `/v2/openapi/upload/sts` | Credits/PAYG; Turbo image-to-3D 20/30 (không/có texture), P1 là 40/50; option khác cộng phí | Generation-limit báo HTTP 429/code 2000; giới hạn concurrency riêng theo tác vụ | Adapter v3-style trong repo khác với public docs hiện tại; phải chạy sandbox/contract test |
| **Tencent Hunyuan 3D** | Job async submit/query; bản Professional có text/image/multiview; có giao diện tương thích OpenAI | Prepaid khoảng ¥0,09–0,10/credit; postpaid ¥0,12/credit; Pro normal 20 credits, Geometry 15, Rapid text/image 15 | Pro mặc định 3 job đồng thời; Rapid và một số tác vụ hậu xử lý mặc định 1; rate API thường tách biệt | Provider thứ ba có tiềm năng; cần xác minh onboarding qua TokenHub và khu vực/account |

Nguồn tham chiếu chính thức:

- Meshy Image-to-3D: https://docs.meshy.ai/en/api/image-to-3d
- Meshy pricing: https://docs.meshy.ai/en/api/pricing
- Meshy rate limits: https://docs.meshy.ai/en/api/rate-limits
- Tripo quick start: https://platform.tripo3d.ai/docs/quick-start
- Tripo billing: https://platform.tripo3d.ai/docs/billing
- Tripo upload: https://platform.tripo3d.ai/docs/upload
- Tencent overview: https://cloud.tencent.com/document/product/1804/120696
- Tencent API overview: https://cloud.tencent.com/document/product/1804/120838
- Tencent pricing: https://cloud.tencent.com/document/product/1804/123461

## 5.2. Meshy – đánh giá chi tiết

`MeshyProvider.cs` đang gọi `openapi/v1/image-to-3d` kèm Bearer authentication; cách này phù hợp với endpoint mà tài liệu Meshy chính thức đang mô tả tại thời điểm nghiên cứu. Meshy chấp nhận JPEG/PNG/WEBP, kể cả data URI, và tài liệu liệt kê GLB, STL, 3MF trong các định dạng đầu ra được hỗ trợ.

Các model chuẩn hiện có `meshy-6-lite`, `meshy-6`, `meshy-7.1`, `latest`. Vì vậy giá trị mặc định `meshy-6-lite` trong repo không tự động bị xem là sai; nhưng không nên coi nó là một hằng số không thay đổi trong code. `meshy-7` cũ đã bị thay thế bằng `meshy-7.1`, và loại `lowpoly` dự kiến ngừng hỗ trợ **30/10/2026** theo thông báo chính thức ở mốc nghiên cứu.

Hạng mục cần bổ sung:

1. Ghi provider và model được chọn vào từng job.
2. Ghi lượng API credits tiêu thụ và chi phí ước tính/chi phí thực bằng VNĐ.
3. Đọc thông tin rate-limit và tuân thủ `Retry-After`.
4. Phân biệt hết tốc độ request với hết sức chứa hàng đợi long-running task.
5. Không retry POST tạo model trong tình huống kết quả không rõ ràng; chuyển `NeedsAttention` và đối soát.
6. Benchmark model `meshy-6-lite` so với `meshy-7.1/latest` trên tập ảnh thật dành cho quà lưu niệm trước khi chuyển mặc định.
7. Bổ sung contract test cho **mọi endpoint thực sự được dùng**, không chỉ bước submit.

Chi phí công bố cho Image-to-3D Meshy 6/7: 20 credits khi không texture; 30 credits khi có texture; 35 credits với 8K; Meshy 7 Ultra cộng 5 credits. Printability analysis được liệt kê miễn phí, còn printability repair là 10 credits. Đây là **đơn vị credit của Meshy**, không phải giá VNĐ. Phải dùng giá credits theo gói THE BOYS mua thực tế để ghi vào hệ thống kế toán.

Giới hạn theo gói thay đổi mạnh, nhất là số lượng job được xếp hàng, do đó một vòng poll cố định và dày không phải chiến lược tối ưu. Nên áp dụng backoff/pacing và quan sát tín hiệu từ provider.

## 5.3. Tripo – đánh giá chi tiết

`TripoProvider.cs` hiện giả định giao diện:

```text
Base URL: https://openapi.tripo3d.ai/v3
POST files
POST generation/image-to-model
POST models/convert
GET tasks/{id}
```

Trong khi tài liệu công khai mới mà báo cáo tra cứu minh họa:

```text
POST https://api.tripo3d.ai/v2/openapi/task
POST https://api.tripo3d.ai/v2/openapi/upload/sts
```

**Điều này chưa đủ để kết luận v3 đã ngừng hoạt động.** v3 có thể vẫn được duy trì cho khách hàng cũ. Nhưng sự khác biệt đủ lớn để xếp thành rủi ro tương thích API đáng chú ý. Không đổi mã v3 sang v2 một cách mù quáng; tạo adapter/contract riêng, kiểm thử với sandbox hoặc tài khoản thật, rồi mới quyết định chuyển.

Theo tài liệu pricing ở mốc nghiên cứu, nhóm Turbo/V3.1/V3.0/V2.5/V2.0 tiêu thụ khoảng 20 credits không texture và 30 credits có texture; P1 có mức khoảng 40/50 credits. Các lựa chọn low-poly, parts, quad, style hoặc chất lượng texture có thể làm tăng credits.

Tripo cho biết một số URL tải output chỉ có hiệu lực **60 giây**. Điều này củng cố quyết định đúng của backend: tải file ngay về **private storage do THE BOYS kiểm soát**, chứ không trả link provider tạm thời cho customer.

Đề xuất kiến trúc adapter:

```text
TripoProviderV2
  → submit task
  → upload theo API chính thức
  → poll task
  → download output ngay khi sẵn sàng
  → copy vào IModelStorage
  → hash file
  → persist provider/model/pricing version
```

Cần phân loại lỗi: `429`/code generation-limit là áp lực tải/queue tạm thời; `403` hoặc code thiếu provider credits là vấn đề ngân sách tài khoản; content-policy rejection là lỗi riêng; không gộp tất cả thành cùng một “AI generation failed”.

## 5.4. Tencent Hunyuan 3D – đánh giá chi tiết

Tencent có thể là adapter thứ ba nhưng phải đi qua `IModelProviderRegistry` thay vì viết riêng nhánh Tencent trực tiếp vào `GenerationProcessor`.

Tại mốc báo cáo, tài liệu Tencent mô tả khả năng text-to-3D, image-to-3D, Professional multi-view; bản model 3.1 có độ chi tiết hình học và texture cao hơn, cho phép tối đa 8 góc đầu vào theo product overview.

Luồng API 3.0 truyền thống là bất đồng bộ:

```text
SubmitHunyuanTo3DProJob
    → nhận JobId

QueryHunyuanTo3DProJob
    → WAIT / RUN / FAIL / DONE
```

Tencent cũng có API tương thích OpenAI với hình thức key-based authentication cho một số dòng Professional. Với backend .NET hiện có worker async, adapter có thể được xây như sau:

```text
TencentHunyuanProvider.CreateAsync()
  → submit
  → lưu ngay JobId của Tencent

TencentHunyuanProvider.GetAsync()
  WAIT → Pending
  RUN  → Processing
  FAIL → ProviderFailure
  DONE → download về private storage
```

Không đánh đồng **số job AI đang xử lý đồng thời** với **HTTP requests/second**. Tài liệu được nghiên cứu nêu Pro mặc định 3 job đồng thời; Rapid và nhiều công cụ hậu xử lý mặc định 1; các endpoint truy vấn/hậu xử lý có thể có giới hạn HTTP riêng.

Theo biểu giá Tencent tháng 7/2026 trong báo cáo: prepaid khoảng ¥0,10/credit ở gói 1.000 credits, giảm đến ¥0,09/credit ở gói lớn 100.000 credits; postpaid ¥0,12/credit. Normal Professional 20 credits, Geometry 15, một số LowPoly/Sketch 25, Rapid image/text 15; multi-view/PBR/định dạng/số mặt có thể tính thêm. Thí dụ Professional Normal 20 credits tương đương **¥2,00 nếu prepaid nhỏ nhất hoặc ¥2,40 nếu postpaid**, chưa tính tùy chọn. Đây chỉ là phép nhân biểu giá công bố; **không phải quotation theo VNĐ và không phải hợp đồng dành cho THE BOYS**.

Tencent cũng đã thông báo dịch chuyển năng lực các model về **TokenHub**; nền tảng cũ có thể dừng mua mới/thêm model, trong khi các gói đã mua tiếp tục dùng theo điều kiện hiện hành. Vì vậy cần xác định onboarding path chính xác tại thời điểm tích hợp, vùng hỗ trợ và giấy phép khai thác thương mại trước khi phụ thuộc vào provider này.

Tài liệu: https://cloud.tencent.com/document/product/1804/120706 và https://cloud.tencent.com/document/product/1804/123461

## 5.5. Hợp đồng kỹ thuật chung cho mọi provider

Mọi adapter (Meshy, Tripo, Tencent) phải vượt qua cùng bộ tiêu chí:

```text
Create không tự retry mù sau timeout không rõ kết quả.
Provider/model/API version bất biến từ lúc tạo job.
Poll an toàn khi retry.
HTTP 429 tuân thủ pacing/backoff theo provider.
Thiếu tiền ở tài khoản AI phân biệt với thiếu credits của user.
Download output ngay về storage của THE BOYS.
Lưu checksum, byte length, units và up-axis.
Không lộ API key và provider temporary URLs cho khách.
Có thể đối soát credit/actual monetary cost của provider.
Có thể tắt provider cho job mới nhưng job cũ vẫn tiếp tục được xử lý.
```

Đây là abstraction quan trọng hơn việc ép mọi nền tảng AI sử dụng cùng một HTTP contract.

## 5.6. Chiến lược benchmark thực tế

Báo cáo đề xuất tổ chức benchmark bằng khoảng **20 ảnh đại diện cho các món quà lưu niệm**. Nên đánh giá theo cùng một rubric kỹ thuật và kinh doanh:

| Trục đánh giá | Dữ liệu cần ghi |
|---|---|
| Fidelity | Mức độ giống ảnh nguồn; nhận diện đặc điểm chủ thể; chi tiết nhỏ |
| Geometry | Watertight; lỗi normal; self-intersection; cạnh/đỉnh bất thường |
| Physical dimensions | Unit/up-axis; kích thước XYZ sau normalize; tỷ lệ mẫu |
| Printability | Thành mỏng; overhang; support; khả năng slicer; tỷ lệ in thành công |
| Performance | Thời gian submit → model; tỷ lệ thất bại; số poll |
| Cost | Credits tiêu thụ; chi phí VNĐ thực; chi phí sửa/convert |
| Customer experience | Model có đủ tốt để khách duyệt và trả tiền không? |

Không tự nhận provider nào tốt nhất cho EXE101 trước khi có dữ liệu in mẫu vật lý và chi phí thực.

---

# 6. ROADMAP TRIỂN KHAI VÀ CÁC CỔNG NGHIỆM THU

Backlog trong repository đã có thứ tự hợp lý:

`An toàn tích hợp → Identity/Privacy → Ledger/Audit → Model lifecycle → Quote/Checkout → Payments/Refund → Fulfillment → Support/Notifications → Sẵn sàng pilot.`

Báo cáo đề xuất chuyển thứ tự đó thành các milestone có tiêu chí rời cổng (exit criteria) rõ ràng.

## 6.1. Milestone và effort

| Milestone | Nội dung | Effort ước tính | Exit criteria |
|---|---|---:|---|
| Security + correctness | Rotate secrets; scan Git history; sửa trusted proxy; sửa STL up-axis | 14–18 giờ | Không còn credential còn hiệu lực đã biết; regression STL pass; không spoof được source IP tùy ý |
| Identity + nền tảng tài chính | Verify/reset/refresh/revoke/lock; consent; CreditAccount/ledger; audit/idempotency/outbox core | 32–38 giờ | UC-01/04, concurrency/token reuse pass; balance đối soát ledger đúng |
| Model lifecycle | Project/job/stage/version/asset; immutable hash; review history; customer approval | 32–36 giờ | Không ghi đè model version; thay đổi cấu hình làm invalid review/approval cũ |
| Provider platform | Registry/factory; job pinning; harden Meshy; validate/rework Tripo; cost telemetry | 28–34 giờ | Job tồn tại qua việc đổi default provider; contract sandbox pass; không retry paid POST mù |
| Quote + checkout | Package version; quote lưu DB và hết hạn; atomic checkout/order items | 20–24 giờ | Quote cũ hoặc model khác trả conflict; checkout lặp chỉ có một order |
| Payment + refund | Gateway abstraction; payment attempts; verified event inbox; reconcile; cancel; refund reserve | 32–38 giờ | Chống event trùng, đến muộn, sai tiền; redirect phía client không tự đặt Paid; không refund đôi |
| Fulfillment + support | Giao nhà in; sản xuất/QC; shipment events; defect/reprint; notification | 34–40 giờ | Không in trước khi Paid+Approved+Accepted; có QC evidence; giao thất bại/khiếu nại/in lại có lịch sử |
| Operational pilot gate | Readiness, telemetry, scans, restore test, FE contract validation, thử provider thật có giới hạn | 20–24 giờ | Đủ bằng chứng G1–G4; chạy trọn sandbox order + một mẫu in thật được kiểm soát |

**Tổng các khoảng P0 chi tiết:** khoảng 212–252 giờ; báo cáo đề nghị budget trung tâm khoảng **225 giờ**. **Tencent proof-of-concept:** cộng thêm khoảng 10–14 giờ nếu chọn thực hiện.

## 6.2. Các acceptance test đặc biệt quan trọng

### A. Tính đồng thời của generation

Hai request đồng thời dùng cùng idempotency key và cùng payload phải tạo **một job và một debit**. Cùng key nhưng payload khác phải trả conflict. Khi POST lên AI provider timeout mà không xác định đã thành công hay chưa, phải chuyển trạng thái reconciliation/NeedsAttention, **không gửi thêm một POST có khả năng bị tính tiền**.

### B. Sổ cái credit

```text
Tài khoản khởi tạo: 2 free trial credits.
Request A hợp lệ → còn 1 credit; có đúng 1 debit.
Replay A → còn 1 credit; không phát sinh debit mới.
Provider thất bại xác định → trở lại 2 credits; có 1 reversal.
Gọi refund credit lần thứ hai → vẫn là 2 credits; không hoàn trùng.
```

### C. Model version bất biến

Khi một `ModelVersion` đã được gắn vào quote/order, không lệnh nào được phép thay thế file, hash hoặc kích thước của chính version đó. Repair/resize tạo **version con mới**.

### D. Vô hiệu hóa phê duyệt cũ

Nếu user hoặc staff thay đổi chiều cao, vật liệu, lớp hoàn thiện, nội dung khắc chữ, model version hoặc `canonicalConfigurationHash`, approval và quote trước đó phải bị coi là không còn hợp lệ.

### E. Thanh toán

```text
Webhook success có chữ ký hợp lệ → Paid đúng 1 lần.
Event trùng → no-op; không ghi thêm tiền.
Sai amount → NeedsAttention.
Sai currency → NeedsAttention/reject.
Event thiếu/không hợp lệ chữ ký → không được coi là thanh toán đáng tin.
Success đến muộn sau cancel → reconciliation/refund; không tự chạy in.
Cancel và production start chạy đồng thời → chỉ một transition hợp lệ thành công.
```

### F. Refund

Các request refund đồng thời không được reserve số tiền lớn hơn tổng capture còn khả dụng. Retry submit không được tạo hai lần hoàn tiền.

### G. Fulfillment

Chỉ cho bắt đầu in nếu thỏa **tất cả**:

```text
PaymentStatus = Paid
PrintReview = Approved
CustomerApproval khớp ModelVersion + Configuration
PartnerAssignment = Accepted
AssetHash khớp OrderItem
```

### H. Geometry

Đưa trường hợp STL bất đối xứng Z-up trước đây bị tính thành 50 mm thay vì 100 mm vào regression test cố định cho unit/integration testing.

## 6.3. Đường găng về phụ thuộc nghiệp vụ

```text
ModelVersion bất biến
        ↓
CustomerApproval
        ↓
Persistent Quote
        ↓
Checkout
        ↓
Verified Payment
        ↓
Partner Assignment
        ↓
Production/QC
        ↓
Shipping/Support
```

**Không nên xây payment trước khi làm đúng immutable model version và persistent quote**. Nếu thanh toán xảy ra cho một model/config có thể bị sửa sau đó, việc đối soát và hoàn tiền sẽ trở nên rất khó.

## 6.4. Minh họa lịch làm việc cho 2 BE

Lịch dưới đây được dịch từ phần minh họa của báo cáo, **không phải deadline cam kết**. Ngày chỉ cho thấy mối quan hệ chồng lấn giữa các luồng công việc.

| Giai đoạn | BE-A | BE-B | Điều kiện đi tiếp |
|---|---|---|---|
| Khởi động | Cùng xử lý rotate key, STL, trusted proxy | Cùng xử lý CI security/scan | Không còn lỗi Critical chặn demo/pilot |
| Nền tảng | Identity sessions, credit ledger, migration model | PR CI, API contracts, thiết kế quote/payment | Quy ước migration và idempotency thống nhất |
| Model/provid​er | Project/job/version/assets, registry | Print review/customer approval, quote | Có ModelVersion bất biến |
| Commerce | AI contract tests, cost telemetry | Checkout, PaymentAttempt/Event | Có persisted quote và webhook trust |
| Production | Theo dõi generation, readiness | Refund/cancel, assignment, production/QC | Không in nếu chưa Paid/Approved |
| Pilot | Live AI validation, monitoring | Shipment, support, outbox, release tests | Pass G1–G4, test với FE và nhà in |

---

# 7. KẾ HOẠCH GIAO TASK CHI TIẾT CHO 2 BACKEND DEVELOPERS

Báo cáo phân **BE-A** phụ trách những bất biến của Identity/Data/Generation và **BE-B** phụ trách Commerce/Fulfillment. Việc chia này không có nghĩa mỗi người chỉ được động tới một layer: cả hai đều có thể phải chỉnh Domain/Application/Infrastructure/WebApi/Tests nhưng cần tránh cùng sửa migration/aggregate trong một PR chưa thống nhất.

## 7.1. Bảng ticket và phân công gốc từ Deep Research

| Ticket | Owner | Giờ | Phụ thuộc | Module/file chính | Tiêu chí nghiệm thu |
|---|---|---:|---|---|---|
| SEC-A01 – Dọn sự cố credential | BE-A | 4 | Không | Git history, config, runbook, CI | Meshy key đã rotate; báo cáo history scan sạch hoặc ngoại lệ rõ ràng |
| COR-A02 – Sửa STL/up-axis | BE-A | 6 | Không | `StlGeometry.cs`, `Unit/PrintingTests.cs`, model metadata | Regression file bất đối xứng trả đúng chiều cao trục Z |
| ID-A03 – Vòng đời phiên đăng nhập | BE-A | 16 | Nền tảng DB | `Application/Auth`, Domain identity, Auth controller, EF migration | verify/reset/refresh/logout/revoke/lock tests pass |
| DATA-A04 – Credit ledger | BE-A | 16 | Quy chuẩn migration | Domain/Application credits/Persistence | Debit/replay/refund/grant đồng thời vẫn atomic |
| MODEL-A05 – Project/version/assets | BE-A | 22 | A04 | Domain model lifecycle, migrations, generation handlers | Version/assets bất biến; đơn hàng cũ không đổi |
| AI-A06 – Provider registry | BE-A | 12 | Provider metadata | `Application/Printing`, `Infrastructure/DependencyInjection.cs`, adapters | Job cũ vẫn poll đúng provider sau khi đổi mặc định |
| AI-A07 – Cập nhật hợp đồng AI | BE-A | 18 | A06 | `MeshyProvider.cs`, Tripo adapter mới/sửa, contract tests | Chứng minh contract thực/sandbox; xử lý rate limit |
| AI-A08 – Tencent POC tùy chọn | BE-A | 12 | A06 | `TencentHunyuanProvider.cs`, tests/config | Submit/poll/download qua registry; default disabled |
| CI-B01 – Pipeline bảo mật | BE-B | 8 | Không | `.github/workflows`, security/dependency config | Required PR checks có scan secret/SAST/dependency |
| ORDER-B02 – Review/approval/quote | BE-B | 16 | A05 | Orders/Pricing, migrations, API | Review+approval gắn cùng version/config, quote lưu DB và hết hạn |
| ORDER-B03 – Checkout v2 | BE-B | 10 | B02 | CreateOrder/Orders controller/contracts | Tạo Order/OrderItem atomic từ quote; chống replay |
| PAY-B04 – Payment attempts/events | BE-B | 22 | B03, A04 | Payments module mới ở Application/Infrastructure/WebApi | Chỉ gateway event đã verify mới tạo Paid |
| PAY-B05 – Refund/cancel | BE-B | 14 | B04 | Refund/cancellation entities/handlers | Không refund vượt capture; retry không hoàn đôi |
| OPS-B06 – Nhà in/sản xuất/QC | BE-B | 20 | B04, A05 | Fulfillment module mới | Partner phải accept; production runs/QC append-only |
| OPS-B07 – Shipping/support | BE-B | 14 | B06 | Shipment/Support modules | Có bằng chứng giao hàng, luồng thất bại/khiếu nại/in lại |
| EVT-B08 – Outbox/notification | BE-B | 10 | A04 foundation | Outbox worker, Notifications module | Crash/retry vẫn chỉ tạo một thông báo logic |
| OBS-A09 – Metrics/cost/readiness | BE-A | 10 | A06/A07 | Worker/provider instrumentation, health readiness | Có metrics provider/job/payment, readiness thực |
| REL-B09 – Release tests | BE-B | 10 | Toàn bộ P0 | Integration tests, migration/restore scripts | Có bằng chứng tự động đạt G1–G4 |

**Phân bổ workload theo báo cáo, không tính Tencent:**

- BE-A: khoảng **104 giờ**.
- BE-B: khoảng **124 giờ**.
- Tổng: khoảng **228 giờ kỹ thuật**.

**Nếu thêm Tencent POC:** BE-A khoảng 116 giờ; BE-B khoảng 124 giờ. BE-B có effort tổng cao hơn nhưng phần lớn phụ thuộc model/version, trong khi giai đoạn đầu có thể làm CI, hợp đồng API và fake provider/payment test để giảm thời gian chờ.

## 7.2. Chi tiết yêu cầu theo luồng BE-A

### BE-A / SEC-A01 – Kiểm soát credential

**Đầu vào:** Git history chứa credential Meshy, cấu hình appsettings, quyền truy cập dashboard Meshy. **Đầu ra:** key cũ revoked, key mới chỉ ở secret store, incident note, CI scan.

**Acceptance criteria:** repo HEAD và history scan có kết quả ghi nhận; không đưa key mới vào PR; có bằng chứng account usage được đối chiếu; app chạy được local/CI với biến môi trường hoặc user-secrets.

### BE-A / COR-A02 – Geometry correction

**Đầu vào:** STL Z-up bất đối xứng; hàm trong `StlGeometry.cs`. **Đầu ra:** thuật toán scale theo trục chiều cao được xác định; metadata đơn vị/up-axis; test regression.

**Acceptance criteria:** model X/Y lớn hơn Z vẫn ra Z height đúng với chiều cao người dùng chọn; không mặc định longest axis = height; có test case Z-up, Y-up hoặc trường hợp không rõ orientation phải được xử lý minh bạch.

### BE-A / ID-A03 – Identity/session lifecycle

**Đầu vào:** auth Register/Login/Me hiện hữu. **Đầu ra:** email verification, password reset, refresh-token rotation, revoke/logout, lockout, secure token storage/expiry.

**Acceptance criteria:** reset token single-use và timeout; logout thu hồi refresh session; refresh token reuse detection; không log token; audit sự kiện nhạy cảm; migration/backfill tương thích dữ liệu User cũ.

### BE-A / DATA-A04 – Credit account và ledger

**Đầu vào:** balance và refund flags hiện hữu. **Đầu ra:** CreditAccount, CreditLedger, uniqueness/indexes, transaction logic, backfill và reconciliation.

**Acceptance criteria:** không double debit; không double reversal; chuyển credits cho admin có reason/audit/idempotency; số dư truy xuất được đối chiếu từ sổ cái; có test đồng thời trên SQL Server.

### BE-A / MODEL-A05 – Model lifecycle

**Đầu vào:** `GenerationTask` đang gộp mọi field. **Đầu ra:** ModelProject, GenerationJob, GenerationStage, ModelVersion, ModelAsset; migration an toàn; mapping từ generation cũ.

**Acceptance criteria:** asset hash lưu và kiểm tra được; tạo version mới không ghi đè version cũ; quote/order tham chiếu version cụ thể; job cũ được đọc tiếp; không mất file/private ownership.

### BE-A / AI-A06 – Provider registry và pinning

**Đầu vào:** DI switch toàn cục. **Đầu ra:** registry/factory cho Meshy và Tripo; persisted provider/model/version trên mỗi job.

**Acceptance criteria:** job Meshy đang pending vẫn poll Meshy khi default đổi sang Tripo; provider bị disable cho job mới nhưng job cũ vẫn có adapter resolve; thiếu provider trả lỗi rõ ràng, không silent switch.

### BE-A / AI-A07 – Adapter/contract integration

**Đầu vào:** Meshy/Tripo adapter hiện tại và tài liệu mới. **Đầu ra:** contract tests đầy đủ; bảo vệ HTTP 429, Retry-After, credential isolation, chi phí thực; Tripo v2 adapter khi đã xác nhận account.

**Acceptance criteria:** `submit/poll/convert/download` pass theo tài khoản kiểm thử; timeout mơ hồ không tạo thêm paid POST; link tạm được tải ngay; code phân biệt errors và usage.

### BE-A / AI-A08 – Tencent proof-of-concept (P1/tùy chọn)

**Đầu vào:** provider registry đã hoàn thành, tài khoản/tencent key được cấp, điều khoản region/licensing xác nhận. **Đầu ra:** adapter submit/poll/download bị tắt mặc định và report latency/cost/quality.

**Acceptance criteria:** không làm ảnh hưởng Meshy/Tripo; có contract test; không bật production chỉ vì mock test pass.

### BE-A / OBS-A09 – Quan sát hệ thống

**Đầu ra:** telemetry theo `TraceId`, `JobId`, `StageId`, `ProviderKey`, `ModelVersionId`, metrics queue/rate/error/actual cost, readiness API. Không được log raw ảnh hoặc API key.

## 7.3. Chi tiết yêu cầu theo luồng BE-B

### BE-B / CI-B01 – Bảo mật CI/CD

**Đầu ra:** PR pipeline có secret scan, CodeQL/SAST, dependency review và checks bắt buộc. Phải giữ nguyên các job build/test/diagrams đã chạy được thay vì thay toàn bộ CI.

### BE-B / ORDER-B02 – Review, approval và quote

**Đầu vào:** ModelVersion của BE-A. **Đầu ra:** append-only PrintReview; CustomerApproval gắn hash; quote persist, versioned và expiring; snapshot giá và cấu hình.

**Acceptance criteria:** model chưa được staff approve hoặc khách chưa duyệt không được checkout; thay đổi model/config invalidate approval/quote; quote hết hạn không thể dùng.

### BE-B / ORDER-B03 – Checkout v2

**Đầu vào:** `QuoteId` hợp lệ và customer approval. **Đầu ra:** Order/OrderItem mới, snapshot tài chính, status history, idempotent checkout.

**Acceptance criteria:** order đúng customer/quote; một quote không checkout thành hai order do concurrent request; server tính lại và kiểm tra dữ liệu thương mại, không tin tổng tiền client gửi.

### BE-B / PAY-B04 – Gateway và payment events

**Đầu vào:** order hợp lệ, gateway được nhóm chọn. **Đầu ra:** PaymentAttempt, PaymentEvent inbox, chữ ký webhook, reconciliation service, status transitions.

**Acceptance criteria:** webhook không hợp lệ chữ ký không cập nhật Paid; wrong amount/currency bị giữ lại xử lý; event trùng no-op; browser redirect không phải nguồn sự thật thanh toán.

### BE-B / PAY-B05 – Cancellation/refunds

**Đầu vào:** payment capture và refund policy. **Đầu ra:** refund request/reserve/submit/reconcile, cancellation status machine, audit.

**Acceptance criteria:** không hoàn vượt tiền đã thu; retry không tạo refund trùng; cancel diễn ra đúng quy định của các giai đoạn in/giao; callback đến muộn không kích hoạt sản xuất.

### BE-B / OPS-B06 – Đối tác, sản xuất và QC

**Đầu vào:** paid order và approved model. **Đầu ra:** PartnerAssignment offer/accept/reject, ProductionRun, QC checklist/evidence, reprint history.

**Acceptance criteria:** chỉ giao nhà in khi đủ điều kiện; partner acceptance bắt buộc trước production; QC pass cần chứng cứ; lỗi QC có thể tạo run mới nhưng không ghi đè run cũ.

### BE-B / OPS-B07 – Shipping và hỗ trợ

**Đầu ra:** Shipment, ShipmentEvent, tracking, delivery evidence; DefectReport/DefectEvidence; complaint resolution/reprint/refund.

**Acceptance criteria:** giao hàng thành công/thất bại đều có lịch sử; khiếu nại liên kết được đến order/model/production run; không mất bằng chứng khi chuyển trạng thái.

### BE-B / EVT-B08 – Transactional outbox và notifications

**Đầu ra:** OutboxMessage/Notification, worker có retry/backoff, dedupe.

**Acceptance criteria:** update nghiệp vụ commit thành công và outbox event commit cùng transaction; crash/restart không làm mất event; phát trùng vật lý không làm user nhận nhiều notification logic.

### BE-B / REL-B09 – Integration/release testing

**Đầu ra:** API integration tests, migrate từ DB cũ, backup/restore runbook, payment sandbox test, test hỏng mạng/tác vụ; bằng chứng G1–G4.

**Acceptance criteria:** migration có backfill và rollback strategy/restore; end-to-end E2E qua staging; không phát hành pilot nếu các invariant money/print thất bại.

## 7.4. Thứ tự PR khuyến nghị

```text
PR A   Fix secret / proxy / STL correctness
PR B   Identity sessions và migration
PR C   CreditAccount + CreditLedger + backfill
PR D   Provider registry / pinning
PR E   ModelProject / GenerationJob / Stage / Version / Assets
PR F   PrintReview append-only + CustomerApproval
PR G   Persisted Quote
PR H   Checkout / OrderItem migration
PR I   PaymentAttempt + PaymentEvent inbox
PR J   Cancellation + Refund
PR K   PartnerAssignment + ProductionRun + QC
PR L   Shipment + Complaint
PR M   Outbox + Notifications
PR N   Observability + release gates
```

**Nguyên tắc migration:** không sửa file migration đã phát hành và từng chạy ở môi trường chung. Tạo migration mới theo hướng tiến (forward-only), có backfill, đối soát và xác minh upgrade từ baseline đang hỗ trợ. Không merge một migration “34 bảng thay toàn bộ” nếu chưa đảm bảo backward compatibility.

---

# 8. CI/CD, GIÁM SÁT, KIỂM SOÁT CHI PHÍ VÀ RELEASE GATES

## 8.1. CI/CD hiện có và đề xuất mở rộng

CI hiện tại là một nền tảng tốt, **không nên thay mới từ đầu**. Pipeline đang chạy build/tests, verify SQL design, kiểm tra docs và drift của Mermaid diagrams. Cần mở rộng PR checks theo chuỗi:

```text
Format / static checks
        ↓
Quét secrets ở diff và Git history
        ↓
Restore / Build
        ↓
Unit tests
        ↓
Contract tests với mocked AI provider
        ↓
SQL integration tests
        ↓
Kiểm tra EF migration từ baseline cũ
        ↓
CodeQL / SAST
        ↓
Dependency review
        ↓
Schema / Docs / Diagram verification
```

GitHub CodeQL hỗ trợ C# và có chế độ phân tích phù hợp dự án .NET. Nên kết hợp GitHub secret scanning/push protection (nếu khả dụng theo repo/account) và Gitleaks quét code/history. Vì repository đã từng chứa một credential, secret scanning phải là **release gate bắt buộc**.

Nguồn: https://docs.github.com/en/code-security/concepts/code-scanning/codeql/codeql-for-compiled-languages và https://github.com/gitleaks/gitleaks-action

## 8.2. Quy trình triển khai môi trường

Vì chưa biết nhóm triển khai trên Azure, AWS, VPS hay mô hình nào, báo cáo không gắn sẵn một nền tảng cụ thể. Có thể chuẩn hóa pipeline deployment theo hợp đồng:

```text
main pass toàn bộ required CI
        ↓
Build artifact/container bất biến; ghi commit SHA
        ↓
Tạo SBOM nếu có khả năng
        ↓
Deploy staging
        ↓
Chạy migration bằng tài khoản DB chuyên biệt
        ↓
Readiness smoke test
        ↓
Payment sandbox test
        ↓
Provider smoke test có giới hạn chi phí
        ↓
Phê duyệt pilot thủ công
        ↓
Deploy production
```

**Không cấp cho runtime application account toàn quyền chạy migration** trong môi trường production. Nên có database identity riêng cho migration và quy định bảo vệ dữ liệu append-only như ledger/history/audit.

## 8.3. Hệ thống chỉ số giám sát tối thiểu

| Phân hệ | Metrics cần thu thập | Lý do cảnh báo |
|---|---|---|
| API | Request volume, 4xx/5xx, latency p50/p95/p99 | Phát hiện API xuống cấp |
| Generation | Tuổi job queued/running, success/fail/NeedsAttention | Sức khỏe AI worker/provider |
| Provider | HTTP code, số 429, polling latency, active tasks | Hạn mức và capacity bên ngoài |
| Cost | Credits/job, cost/job thực, daily spend | Bất thường chi phí |
| Credit | Chênh lệch giữa ledger và balance | Tính đúng đắn tài khoản credit |
| Payment | Pending age, verified success, invalid signature, NeedsAttention | An toàn dòng tiền |
| Refund | Reserved/succeeded/failed amount và thời gian tồn | Chống kẹt/double refund |
| Fulfillment | Assignment age, QC fail, reprint rate | Hiệu quả nhà in |
| Outbox | Pending age, retry, dead letter | Tránh mất thông báo |
| Database | Kết nối, migration version, query latency | Persistence/readiness |
| Storage | Upload/download failure, checksum mismatch | Toàn vẹn assets |

Các trường trace nên có `TraceId`, `JobId`, `StageId`, `ProviderKey`, `ProviderTaskId` (chỉ trong management logs), `ModelVersionId`, `OrderId` khi có. Không đưa API keys, ảnh gốc, password/reset token, full card data hay PII khách hàng vào telemetry.

## 8.4. Chi phí thực và đối soát provider

Thiết kế `GenerationStages` vNext đã dự kiến `ProviderCredits` và `ActualCostVnd`. Nên bổ sung/chuẩn hóa bản ghi chi phí:

```text
ProviderKey
ModelKey
PricingVersion
Operation
ProviderUsageUnit
ProviderUsageQuantity
ProviderCurrency
ProviderActualCost
ConvertedCostVnd
FxRateReference
RecordedAtUtc
```

Với đồ án sinh viên, ban đầu có thể ghi provider credits thực và đơn giá của gói credit thực mua. Nguyên tắc quan trọng: **`AiCostBudgetSnapshot` chỉ là ngân sách dự kiến, không phải actual AI cost**. Tương tự, giá đối tác và giá in giả định không phải là cost thực nếu chưa nhận báo giá/hóa đơn/đối soát sản xuất.

Nên đặt cảnh báo:

```text
Một generation vượt số credits dự kiến.
Chi phí provider trong ngày vượt hạn mức ngân sách.
Một user tạo nhiều NeedsAttention job bất thường.
Job có provider task nhưng không có stage cost/usage tương ứng.
Usage trên dashboard provider khác với usage nội bộ.
```

## 8.5. Release gates G0 đến G4

| Gate | Bằng chứng bắt buộc để kết luận đạt |
|---|---|
| **G0 – Demo** | Build và tests chạy; demo image → model → review → offline order hoạt động |
| **G1 – Data safety** | Forward migration pass, ledger reconcile, model version bất biến, restore drill thành công |
| **G2 – Money safety** | Payment sandbox có chữ ký; test duplicate/late/wrong-amount; test đồng thời cancel/refund/start |
| **G3 – Print operations** | Checklist Staff review; customer approval; nhà in accept; chứng cứ QC; chứng cứ giao hàng; xử lý complaint |
| **G4 – Pilot** | FE consumer test, dashboards, gọi live AI có giới hạn, chạy slicer và kiểm nghiệm mẫu in vật lý |

**CI xanh không đủ chứng minh đã qua G2–G4.** Tại thời điểm review, validation có test giả lập/provider mock và LocalDB, nhưng chưa có xác minh live AI contract có tính phí, gateway sandbox/refund, restore sản phẩm, load test, FE consumer test hoặc xác minh mẫu in thật.

---

# 9. PHỤ LỤC BẰNG CHỨNG VÀ NGUỒN THAM CHIẾU

## 9.1. Commit và CI được đối chiếu

```text
Repository: khoivthse182701/print3d
Branch: main
Commit: c4580975456325e14f61f3f951ae094cd6097f6b
Message: docs: add backend review, SRS, database design and repository structure
UTC time: 2026-10-20T00:51:20Z
```

GitHub Actions:

```text
Workflow: Backend CI
Run ID: 39350114205
Head: c4580975456325e14f61f3f951ae094cd6097f6b
Status: completed
Conclusion: success
Jobs: test, diagrams – đều thành công
```

Xem trực tiếp: https://github.com/khoivthse182701/print3d/actions/runs/39350114205

Báo cáo validation ghi nhận:

```text
20 passed
0 failed
0 skipped
SQL LocalDB
Fake AI providers
```

## 9.2. Danh mục code cần đọc/đối chiếu khi giao việc

| Mục tiêu | File/đường dẫn |
|---|---|
| Image upload, download, review, recover | `src/TheBoys.WebApi/Controllers/GenerationsController.cs` |
| Demo offline order | `src/TheBoys.WebApi/Controllers/OrdersController.cs` |
| Runtime EF DbSets | `src/TheBoys.Infrastructure/Persistence/ApplicationDbContext.cs` |
| Aggregate generation hiện tại | `src/TheBoys.Domain/Entities/GenerationTask.cs` |
| Aggregate order/payment | `src/TheBoys.Domain/Entities/Order.cs` |
| DI chọn một provider | `src/TheBoys.Infrastructure/DependencyInjection.cs` |
| Meshy adapter | `src/TheBoys.Infrastructure/Printing/MeshyProvider.cs` |
| Tripo adapter | `src/TheBoys.Infrastructure/Printing/TripoProvider.cs` |
| STL normalization/printability | `src/TheBoys.Infrastructure/Printing/StlGeometry.cs` |
| Worker và processor | `src/TheBoys.Infrastructure/Printing/GenerationWorker.cs`, `GenerationProcessor.cs` |
| EF migrations | `src/TheBoys.Infrastructure/Persistence/Migrations/` |
| Contract/SQL/printing tests | `tests/TheBoys.Tests/` |

## 9.3. Tài liệu yêu cầu và database quan trọng

**Thứ tự ưu tiên tham khảo khi triển khai:**

```text
1. docs/requirements/SRS.vi.md
2. docs/requirements/use-cases.vi.md
3. docs/requirements/traceability.vi.md
4. docs/architecture/database-design.vi.md
5. docs/planning/implementation-backlog.vi.md
6. docs/review/flow-inventory.vi.md
7. docs/review/validation.vi.md
8. database/design/vnext.sql
```

Các đường dẫn GitHub tương ứng có thể mở theo mẫu: `https://github.com/khoivthse182701/print3d/blob/main/<đường-dẫn>`.

## 9.4. Sự cố credential trong lịch sử Git

```text
Commit lộ key:
a7a5006c636fafc1d1f4667fe50e91b32582e183
chore: configure Meshy API Key and align database connection

Commit vệ sinh cấu hình:
4a1e6157c14c1600cdc474b1564b682874e6baac
chore: restore appsettings.json and sanitize configuration for git
```

Không in literal key. Nhóm cần xác nhận việc rotate/revoke trên dashboard và quét toàn bộ lịch sử, không chỉ nhìn HEAD.

## 9.5. Nguồn chính thức bên ngoài

### Meshy

- Image-to-3D API: https://docs.meshy.ai/en/api/image-to-3d
- Pricing: https://docs.meshy.ai/en/api/pricing
- Rate limits: https://docs.meshy.ai/en/api/rate-limits
- Changelog: https://docs.meshy.ai/en/api/changelog

### Tripo

- Quick start: https://platform.tripo3d.ai/docs/quick-start
- Billing: https://platform.tripo3d.ai/docs/billing
- Upload: https://platform.tripo3d.ai/docs/upload
- FAQ: https://platform.tripo3d.ai/docs/faq
- API general: https://platform.tripo3d.ai/docs/general

### Tencent Hunyuan 3D

- Product overview: https://cloud.tencent.com/document/product/1804/120696
- API overview: https://cloud.tencent.com/document/product/1804/120838
- Pricing: https://cloud.tencent.com/document/product/1804/123461
- FAQ: https://cloud.tencent.com/document/product/1804/120706
- Quick start: https://cloud.tencent.com/document/product/1804/120757

### GitHub security

- CodeQL for compiled languages: https://docs.github.com/en/code-security/concepts/code-scanning/codeql/codeql-for-compiled-languages
- Secret scanning: https://docs.github.com/en/code-security/how-tos/secure-your-secrets/detect-secret-leaks/enable-secret-scanning
- Gitleaks: https://github.com/gitleaks/gitleaks-action

---

# 10. KẾT LUẬN VÀ HƯỚNG TRIỂN KHAI NGẮN GỌN

**EXE101 không thiếu số lượng controller; dự án thiếu các bất biến dữ liệu và quy trình có thể kiểm toán được.** Những phần cần đầu tư nhiều nhất hiện tại là:

1. **Bảo mật ngay:** vô hiệu hóa khóa Meshy bị lộ trong lịch sử, sửa trusted proxy và lỗi chiều cao STL.
2. **Nền dữ liệu:** sổ cái credit, model version bất biến, lưu approval/review dạng lịch sử.
3. **AI platform:** pin Meshy/Tripo theo job, xác minh contract thật, đo chi phí AI và chuẩn hóa assets.
4. **Commerce:** persistent quote, checkout an toàn, PaymentAttempt/Event, chữ ký webhook và refund.
5. **Physical fulfillment:** giao việc nhà in, sản xuất/QC, shipping, complaint/reprint và bằng chứng vận hành.
6. **Release:** kiểm nghiệm FE, payment sandbox, restore, benchmark provider và sản phẩm in thật.

Với 2 backend developers, bảng ticket gốc phân bổ khoảng **104 giờ cho BE-A** và **124 giờ cho BE-B** (không tính Tencent); mục tiêu hợp lý là làm xong trục nghiệp vụ **Image → ModelVersion → Approval → Quote → Verified Payment → Partner → Production/QC → Delivery**, không mở rộng rigging, animation, tự host GPU hoặc multi-view ở giai đoạn P0 nếu chưa đáp ứng cổng nghiệm thu.

**Ghi chú biên tập:** Đây là bản dịch có biên tập định dạng và diễn giải rõ một số acceptance criteria từ bảng ticket của báo cáo gốc (đặc biệt các tiểu mục 5.6 và 7.2–7.3). Các bảng kiểm kê, số liệu tiến độ, cấu trúc database, mức effort, thứ tự phụ thuộc và kết luận kỹ thuật được giữ theo kết quả nghiên cứu ngày 20/10/2026. Việc thay đổi phạm vi, hạn mức provider hoặc thực hiện thêm commit sau ngày này cần được kiểm tra lại trước khi giao việc thật.
