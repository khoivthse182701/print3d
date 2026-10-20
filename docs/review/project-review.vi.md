# Review toàn bộ backend THE BOYS

Ngày 19/10/2026. Baseline `04874ed`. Phạm vi: 4 project .NET 10, controller/handler, worker AI, storage, EF Core SQL Server, migrations và tests. Đã đọc CP3 và slide SWD392 do nhóm cung cấp. Không có frontend trong phạm vi kiểm chứng.

## Kết luận

Dự án đã có một lát cắt chạy xuyên suốt từ ảnh đến đơn demo, cùng các luồng hỗ trợ; không chỉ có một endpoint generate. Tuy nhiên chưa đủ vòng đời bán thành phẩm theo CP3: thiếu chốt phiên bản mẫu với khách, thanh toán và đối soát, điều phối xưởng, QC, khiếu nại và hoàn tiền. Backend hiện là **demo đặt in offline**, không nên diễn giải enum/bảng chưa sử dụng thành chức năng đã hoàn thành.

Điểm nên giữ: kiến trúc Domain/Application/Infrastructure/WebApi; kiểm tra sở hữu file; snapshot giá; idempotency cho tạo task/đơn; SQL rowversion cho user/task/order; worker checkpoint trước gọi bên ngoài; không gửi lại POST có kết quả mơ hồ; kiểm tra kích thước STL. 20 test hiện có đã pass trên SQL Server với provider giả lập; đó không phải chứng nhận API thật hay chất lượng bản in.

## Các phát hiện cần xử lý

P0 = phải xử lý trước pilot có tiền/dữ liệu khách; P1 = hoàn thiện vận hành; P2 = mở rộng. Mức ưu tiên xét sản phẩm mục tiêu, không coi phạm vi demo đã công bố là lỗi lập trình.

| ID | Mức | Bằng chứng trong repo | Tình huống và tác động | Đề xuất / tiêu chí sửa |
|---|---|---|---|---|
| R01 | P0 | `UpdateOrderStatusCommand.cs`, nhánh `(Draft, Printing)`; `OrdersController.View` trả `demo_offline_no_online_charge` | Staff bắt đầu in khi chưa có giao dịch; `Payment` tồn tại nhưng không có API/handler thanh toán | Thêm checkout, payment event inbox, đối soát; chỉ cho sản xuất khi tiền đã xác nhận + khách chốt mẫu + staff duyệt |
| R02 | P0 | `DependencyInjection.cs` chỉ đăng ký một `IModelGenerationProvider`; `GenerationProcessor` và recover dùng provider hiện tại | Đổi cấu hình Meshy sang Tripo khi còn job Meshy: worker gửi task ID cũ sang Tripo; `task.Provider` đang lưu nhưng không dùng để resolve | Registry chọn adapter theo provider và model đã snapshot của job; test chuyển cấu hình giữa hai lần poll |
| R03 | P0 | `Program.cs`: xóa KnownIPNetworks/KnownProxies và limiter đọc trực tiếp CF-Connecting-IP/X-Real-IP | Request có thể thay header IP để chia bucket auth nếu ingress không lọc; trust proxy không giới hạn | Chỉ tin proxy cấu hình; limiter dùng RemoteIpAddress sau middleware tin cậy; kiểm thử spoof header qua ingress |
| R04 | P0 | `GenerationTask` chứa cả job/file/geometry/review; `ReviewGenerationTaskCommand` ghi đè lần duyệt trước | Đổi mẫu hoặc duyệt lại không có lịch sử, không chứng minh file nào đã được khách/xưởng chốt | Tách ModelVersion, ModelAsset, PrintReview, CustomerApproval; pin order item vào version/hash bất biến |
| R05 | P0 | `User.DeductCredit/RefundCredit`, `AddUserCreditsCommand` | Có chống âm và race bằng rowversion nhưng không có sổ cái, actor/lý do cấp lượt; không đối soát được balance | Ledger append-only, source bucket, operation key duy nhất; balance và ledger commit cùng transaction |
| R06 | P1 | `Order.StatusNote`, timestamps; không có history entity | Cập nhật sau ghi đè người/lý do trước; không biểu diễn nhiều lần reprint/giao lại | OrderStatusHistory, ProductionRun, ShipmentEvent; giữ lịch sử bất biến |
| R07 | P0 | `StlGeometry.AnalyzePrintability`: healthy suy ra từ cạnh manifold; `MinWallThicknessMm=null` | Tripo local analysis chưa kiểm tra độ dày/support/self-intersection đầy đủ; mô tả healthy dễ gây hiểu nhầm | Phân biệt analysis coverage; duyệt kỹ thuật cần checklist slicer và bằng chứng; watertight không đồng nghĩa in được |
| R08 | P1 | `PartnerLab` entity, `Order.PartnerLabId`; không có route quản lý/phân công | Không giao việc, chấp nhận/từ chối, theo dõi năng lực và tiền trả xưởng | MVP Staff ghi nhận thay xưởng, Assignment có lịch sử; portal đối tác P1 sau khi có scoped access |
| R09 | P0 | `Payments` chỉ có method/ref/amount/status; enum Order có Refunded nhưng handler không tới được | Không tiếp nhận callback có xác thực, chống duplicate, sai tiền, thanh toán đến muộn, refund | PaymentAttempts + PaymentEvents + Refunds; không tin redirect FE; chốt state trong transaction |
| R10 | P1 | `Order` có một DefectReason/EvidencePhotoUrl/ReprintApproved | Chưa có API khiếu nại và không biểu diễn nhiều bằng chứng/lần xử lý | DefectReport/DefectEvidence, quyết định refund hoặc production run mới |
| R11 | P1 | `AuthController`, `TokenService`, `UserRoleCache` | Chưa verify email, reset password, logout/revoke, session refresh; cache role local TTL 60s khiến instance khác còn quyền cũ tạm thời | Session/refresh token hash; revoke/lock account; invalidation liên instance hoặc truy vấn DB cho thao tác nhạy cảm |
| R12 | P1 | `GenerationWorker.Take(10)` chạy tuần tự; không deadline business, retry budget chưa dùng | Một download dài trì hoãn hàng đợi; job có thể chờ mãi, khó vận hành | Cảnh báo quá hạn, backoff/jitter, concurrency giới hạn theo provider; NeedsAttention thay vì tự kết luận mất tiền |
| R13 | P1 | `LocalModelStorage` tại App_Data; `/health` luôn ok | Nhiều instance không thấy cùng file; health không phản ánh SQL/worker/storage | Shared object storage, asset checksum/retention, readiness phụ thuộc; backup restore cả DB và file |
| R14 | P1 | `GenerateImageRequest` và `DetectImage` | Mới kiểm tra magic bytes/size, chưa decode ảnh/pixel limit; thiếu consent cho ảnh cá nhân | Decode trong giới hạn tài nguyên, loại file hỏng, policy consent/version, private mặc định |
| R15 | P1 | `Order` snapshots chi phí; Quantity 1–10; không có actual costs | Snapshot ngân sách chưa là chi phí thực trả, một số khoản theo đơn/sản phẩm không rõ; P&L dễ tính sai | Tách unit partner cost × qty và AI/shipping/payment phí theo đơn; lưu actual settlement, không gán ngân sách thành thực tế |
| R16 | P1 | `TaskType.TextTo3D`, `Prompt` có nhưng chỉ POST image | CP3 hứa Text-to-3D nhưng BE chưa cung cấp | Chốt image-first MVP; Text-to-3D P1 qua cùng orchestration, kiểm tra adapter capability |
| R17 | P1 | `PricingPackage`, API quote không lưu quote | Chưa có quote expiry/version, khách xác nhận chỉ expectedTotal; cùng giá nhưng đổi vật liệu có thể không được phát hiện | Persist quote gắn cấu hình/version, thời hạn, snapshot vật liệu/phí; 409 khi thay đổi |
| R18 | P2 | Không có README, workflow CI; DDL và config C# rời có thể bị nhầm với EF đang chạy | Người mới không biết dùng schema nào, ghi chú AI lẫn tài liệu chuẩn; hai nơi migration gây khó tìm | Đã tổ chức hồ sơ và tests; EF là nguồn schema runtime; quy tắc PR/CI và DDL đề xuất tách rõ |
| R19 | P0 | `StlGeometry.AnalyzePrintability`, `largestAxis = Array.IndexOf(extents, extents.Max())`; normalize sort cả ba cạnh | Model rộng hơn cao bị coi cạnh dài nhất là height; số hiển thị có thể đúng 100 mm nhưng trục đứng của vật thể không cao 100 mm | Lưu/chọn up-axis, scale theo chiều cao thực tế; test vật thể bất đối xứng rộng hơn cao; không chỉ assert report height |

Đường dẫn đầy đủ bằng chứng: các handler nằm trong `src/TheBoys.Application/`, controllers/Program trong `src/TheBoys.WebApi/`, worker/provider/storage trong `src/TheBoys.Infrastructure/Printing/`, entities trong `src/TheBoys.Domain/Entities/`. Tra cứu vị trí cụ thể bằng tên file hoặc bảng API ở hồ sơ kiểm kê.

## Độ lệch tài liệu cũ

- Demo ghi role đọc từ DB mỗi request, trong code đã có cache 60 giây. Cần mô tả đúng phạm vi invalidation.
- Demo mô tả GLB resize: đúng nhánh Meshy; Tripo giữ GLB preview và chuẩn hóa STL local. Không dùng preview GLB làm bằng chứng kích thước in.
- CP3 coi 2 lượt miễn phí và giá 300k/390k là giả định thử nghiệm; bản demo đã seed 390k và 2 lượt. SRS giữ chúng thành cấu hình/chính sách thử nghiệm, chưa coi là quyết định kinh doanh vĩnh viễn.
- Một enum không tương đương một luồng: PendingPayment/Paid/SentToPartner/Reprinting/PostProcessing/Refunded chưa reachable bằng API hiện hành.

## Phạm vi thực hiện của đợt này

Hoàn thành review, nghiên cứu, SRS, use cases, mô hình SWD, thiết kế DB có DDL kiểm tra được, kế hoạch migration và chuẩn repository. Không tự biến toàn bộ backlog thành code sản phẩm trong cùng đợt thiết kế. Những tính năng chưa triển khai được đánh dấu rõ trong inventory, traceability và backlog.
