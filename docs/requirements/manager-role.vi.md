# Đặc tả Vai trò Quản lý (Manager Role) và Luồng Vận hành

Phiên bản: 1.0
Ngày: 25/10/2026
Mã nhiệm vụ: `MGR-001`
Tài liệu liên quan: [SRS.vi.md](SRS.vi.md), [use-cases.vi.md](use-cases.vi.md), [backend-contract.vi.md](../api/backend-contract.vi.md), [demo-flow.vi.md](../runbooks/demo-flow.vi.md)

---

## 1. Bối cảnh và Quyết định Thiết kế

Người dùng đã xác nhận ngày 25/10/2026: **Manager là một vai trò (role) riêng biệt trong hệ thống BE-Print3D, tách rời hoàn toàn với Staff và Admin**.

### 1.1. Tham chiếu thực tế và Quyết định Thiết kế của THE BOYS

#### A. Dữ kiện từ các nguồn tham khảo công nghiệp
- **Formlabs — Sharing and accessing print data** ([Nguồn Formlabs](https://formlabs.com/support/Sharing-and-Accessing-Print-Data-with-Dashboard/), truy cập 25/10/2026):
  - Hệ thống Formlabs phân cấp thành viên thành hai cấp bậc cơ bản: *Member* và *Admin*.
  - Dữ kiện thực tế từ nguồn: Chỉ thành viên có quyền *Admin* mới được phép quản lý quyền truy cập và thay đổi cấp độ tài khoản của các thành viên khác (*change access level / membership*).
- **Formlabs Dashboard** ([Nguồn Formlabs Dashboard](https://formlabs.com/software/dashboard/), truy cập 25/10/2026):
  - Cung cấp giao diện quản lý tập trung hàng đợi in ấn (*centralized print queue*), giám sát tiến độ công việc và lịch sử in.
- **Prusa Connect overview** ([Nguồn Prusa Blog](https://blog.prusa3d.com/prusa-connect-a-network-solution-for-remote-control-of-3d-printers_90364/), truy cập 25/10/2026):
  - Cung cấp cơ chế phân quyền nhóm (*team roles*) và cho phép phân công người dùng theo dõi hàng đợi in (*print queue*).

#### B. Quyết định thiết kế riêng cho nền tảng THE BOYS
Dựa trên các tham khảo trên cùng với yêu cầu nghiệp vụ của dự án, THE BOYS đưa ra các quyết định thiết kế kiến trúc như sau:
1. **Tách biệt phân quyền vận hành khỏi quản trị hệ thống**:
   - Manager là vai trò giám sát vận hành cấp cao: tham gia toàn bộ các luồng vận hành của Staff/Admin hiện có (duyệt hàng đợi tác vụ/đơn hàng toàn hệ thống, đối soát/cứu hộ tác vụ AI, thẩm định kỹ thuật mô hình in 3D, cập nhật tiến trình sản xuất và hủy đơn hàng ở trạng thái Draft).
   - Manager **không** có quyền quản trị hệ thống. Mọi thao tác quản trị tài khoản, phân bổ role, điều chỉnh credits thủ công, cập nhật biểu phí gói in (`/api/admin/pricing-packages`) và phê duyệt hoàn tiền thuộc thẩm quyền độc quyền của **Admin**. Quyết định về bảng giá và chính sách hệ thống là quyết định nội bộ của THE BOYS, không suy diễn từ phần mềm của bên thứ ba.
2. **Mô hình xưởng đối tác bên ngoài (PartnerLabs theo A04)**:
   - THE BOYS đóng vai trò là nền tảng điều phối xưởng gia công đối tác bên ngoài (PartnerLabs), hệ thống không mặc định sở hữu hoặc vận hành xưởng in cơ khí. Do đó, THE BOYS không sao chép các tính năng điều khiển máy in vật lý hay kết nối trực tiếp thiết bị như Formlabs/Prusa.
3. **Bảo toàn Invariant nghiệp vụ (Business Invariants)**:
   - Cấp quyền Manager không làm suy yếu các rào chắn nghiệp vụ:
     - Không cho phép bắt đầu in mô hình chưa đạt thẩm định kỹ thuật (`TaskNotApproved`).
     - Không cho phép nhảy cóc hoặc quay lui trạng thái đơn hàng (`InvalidTransition`).
     - Không cho phép hủy trực tiếp đơn hàng đã bước vào giai đoạn in (`CannotCancel`).
     - Tạo đơn hàng mới giữ nguyên quy tắc chỉ chủ sở hữu mô hình mới được tạo (`owner-only`).

---

## 2. Ma trận Phân quyền (Permission Matrix)

Hệ thống quản lý 4 vai trò chính:
- `Customer` (Enum = 0, Claim = `cus`): Khách hàng cá nhân, chỉ truy cập và thao tác trên tài nguyên của chính mình.
- *(Giá trị 1 là PartnerLab cũ thời kỳ legacy, được bảo lưu và không tái sử dụng)*.
- `Staff` (Enum = 2, Claim = `staff`): Nhân viên vận hành hệ thống của THE BOYS (hỗ trợ điều phối đơn, đối soát kỹ thuật mô hình, cập nhật trạng thái đơn).
- `Admin` (Enum = 3, Claim = `admin`): Quản trị viên hệ thống của THE BOYS.
- `Manager` (Enum = 4, Claim = `manager`): Người giám sát vận hành của THE BOYS (giám sát hàng đợi toàn hệ thống, cứu hộ tác vụ, thẩm định in, điều phối đơn và hủy đơn Draft).

| Phân hệ / Endpoint | Phương thức | Customer | Staff | Manager | Admin | Dẫn chứng Code / Route |
|---|---|:---:|:---:|:---:|:---:|---|
| **Xác thực & Tài khoản** | | | | | | |
| `/api/auth/register` | POST | Public | Public | Public | Public | [AuthController.cs](../../src/TheBoys.WebApi/Controllers/AuthController.cs#L28); chặn unmapped `role` |
| `/api/auth/login` | POST | Có | Có | Có | Có | [AuthController.cs](../../src/TheBoys.WebApi/Controllers/AuthController.cs#L41) |
| `/api/auth/me` | GET | Own | Own | Own | Own | [AuthController.cs](../../src/TheBoys.WebApi/Controllers/AuthController.cs#L55) |
| **Tạo & Giám sát Tác vụ AI (Generations)** | | | | | | |
| `/api/generations/image` | POST | Own | Own | Own | Own | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L40) |
| `/api/generations` (Danh sách) | GET | Chỉ tác vụ mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L67), `IsManagement` |
| `/api/generations/{id}` (Chi tiết) | GET | Chỉ tác vụ mình | Toàn bộ (+ provider IDs) | Toàn bộ (+ provider IDs) | Toàn bộ (+ provider IDs) | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L74), [GenerationQueries.cs](../../src/TheBoys.Application/Generations/Queries/GenerationQueries.cs#L40) |
| `/api/generations/{id}/quote` | GET | Chỉ tác vụ mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L81) |
| `/api/generations/{id}/files/{kind}` | GET | Chỉ tác vụ mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L88) |
| `/api/generations/{id}/review` | POST | 403 Forbidden | Có | Có | Có | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L108), `Roles.Management` |
| `/api/generations/{id}/recover` | POST | 403 Forbidden | Có | Có | Có | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L115), `Roles.Management` |
| **Xử lý Đơn hàng (Orders)** | | | | | | |
| `/api/orders` (Tạo đơn mới) | POST | Own task | Own task | Own task | Own task | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L40), [CreateOrderCommand.cs](../../src/TheBoys.Application/Orders/Commands/CreateOrder/CreateOrderCommand.cs#L49) |
| `/api/orders` (Danh sách đơn) | GET | Chỉ đơn mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L65), [OrderQueries.cs](../../src/TheBoys.Application/Orders/Queries/OrderQueries.cs#L19) |
| `/api/orders/{id}` (Chi tiết đơn) | GET | Chỉ đơn mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L72), [OrderQueries.cs](../../src/TheBoys.Application/Orders/Queries/OrderQueries.cs#L38) |
| `/api/orders/{id}/cancel` | POST | Chỉ đơn Draft mình | Đơn Draft bất kỳ | Đơn Draft bất kỳ | Đơn Draft bất kỳ | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L79), [CancelOrderCommand.cs](../../src/TheBoys.Application/Orders/Commands/CancelOrder/CancelOrderCommand.cs#L21) |
| `/api/orders/{id}/status` | PATCH | 403 Forbidden | Có | Có | Có | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L85), `Roles.Management` |
| **Quản trị Hệ thống (Admin-Only)** | | | | | | |
| `/api/admin/users` | GET | 403 Forbidden | 403 Forbidden | **403 Forbidden** | Có | [AdminController.cs](../../src/TheBoys.WebApi/Controllers/AdminController.cs#L22), `Authorize(Roles = Roles.Admin)` |
| `/api/admin/users/{id}/role` | PATCH | 403 Forbidden | 403 Forbidden | **403 Forbidden** | Có | [AdminController.cs](../../src/TheBoys.WebApi/Controllers/AdminController.cs#L29), [AdminCommands.cs](../../src/TheBoys.Application/Admin/Commands/AdminCommands.cs#L21) |
| `/api/admin/users/{id}/credits` | POST | 403 Forbidden | 403 Forbidden | **403 Forbidden** | Có | [AdminController.cs](../../src/TheBoys.WebApi/Controllers/AdminController.cs#L38), [AdminCommands.cs](../../src/TheBoys.Application/Admin/Commands/AdminCommands.cs#L63) |
| `/api/admin/pricing-packages/{id}` | PATCH | 403 Forbidden | 403 Forbidden | **403 Forbidden** | Có | [AdminController.cs](../../src/TheBoys.WebApi/Controllers/AdminController.cs#L47), [AdminCommands.cs](../../src/TheBoys.Application/Admin/Commands/AdminCommands.cs#L85) |
| `/api/admin/refunds` *(Mục tiêu SRS FR-18)* | POST | N/A | N/A | N/A | Chưa có API | Thuộc mục tiêu SRS P0, chưa có endpoint trong source |

---

## 3. Các Luồng Hoạt động (Operation Flows)

### 3.1. Luồng chính (Main Flow: Giám sát & Vận hành Tiêu chuẩn)
```
[Admin] Cấp role Manager cho User qua PATCH /api/admin/users/{id}/role
         ↓ (Cache in-memory lập tức bị vô hiệu hóa)
[Manager] Đăng nhập nhận JWT (claim role="manager") hoặc sử dụng token hiện có
         ↓
[Manager] Xem hàng đợi tác vụ AI: GET /api/generations
         ↓
[Manager] Kiểm tra chi tiết kỹ thuật và tải mô hình: GET /api/generations/{id}/files/stl
         ↓
[Manager] Thẩm định chất lượng in: POST /api/generations/{id}/review (approved=true)
         ↓
[Customer] Tạo đơn hàng từ mẫu đã duyệt: POST /api/orders
         ↓
[Manager] Giám sát đơn và cập nhật tiến trình điều phối sản xuất:
          1. PATCH /api/orders/{id}/status (status="printing", ghi chú xưởng gia công)
          2. PATCH /api/orders/{id}/status (status="packaged")
          3. PATCH /api/orders/{id}/status (status="shipping", trackingNumber="...")
          4. PATCH /api/orders/{id}/status (status="delivered")
```

1. **Cấp quyền**: Admin sử dụng `PATCH /api/admin/users/{id}/role` gửi `{ "role": "manager" }`.
   - Hệ thống ghi nhận `UserRole.Manager = 4`, ghi nhận thời gian `UpdatedAtUtc`.
   - `userRoleCache.Remove(targetUserId)` được gọi ngay lập tức, xóa cache role in-memory.
2. **Kích hoạt tức thì (No-relogin)**: Token JWT đã cấp trước đó của người dùng sẽ phản ánh quyền Manager ở ngay request kế tiếp trong cùng instance mà không bắt buộc người dùng phải đăng nhập lại.
3. **Giám sát diện rộng**: Manager gọi `GET /api/generations` và `GET /api/orders` để theo dõi các tác vụ và đơn hàng trên toàn hệ thống.
4. **Thẩm định kỹ thuật**:
   - Manager tải file STL kiểm tra độ khép kín (watertight) và độ dày thành mô hình.
   - Manager gọi `POST /api/generations/{id}/review` với `{ "approved": true, "note": "Đã kiểm tra lát cắt và lưới tam giác" }`.
5. **Điều phối đơn hàng**:
   - Sau khi mô hình được duyệt, đơn hàng Draft có thể được chuyển sang `Printing`.
   - Manager cập nhật tuần tự: `Printing` -> `Packaged` -> `Shipping` (kèm mã vận đơn) -> `Delivered`.

### 3.2. Luồng rẽ nhánh / Ngoại lệ (Alternate Flows)

#### A. Cứu hộ tác vụ gặp sự cố (Task Recovery — Phân biệt GET Transient vs POST Ambiguous)
Theo cơ chế xử lý của hệ thống trong [`GenerationProcessor.cs`](../../src/TheBoys.Infrastructure/Printing/GenerationProcessor.cs#L66-L85):
- **Lỗi mạng tạm thời (GET Transient / Rate Limit 429)**: Khi worker thực hiện thao tác GET thăm dò trạng thái từ API bên ngoài (Meshy/Tripo) mà gặp lỗi kết nối chập chờn hoặc bị throttle (`ex.Transient == true`), worker sẽ **tự động retry / backoff** sau khoảng thời gian trễ (30 giây) mà **không** đưa tác vụ vào trạng thái `NeedsAttention`.
- **Lỗi không xác định khi nộp tác vụ (POST Ambiguous / Uncertain Submission)**: Khi worker gửi yêu cầu POST nộp bước tạo mô hình mà kết nối bị đứt đoạn hoặc phản hồi không rõ ràng (`ex.Uncertain == true`), worker **không tự động gửi lại (không resubmit)** nhằm tránh trừ tiền/quota tài khoản hai lần. Lúc này tác vụ được đưa vào trạng thái `GenerationStatus.NeedsAttention` (`ErrorMessage` ghi rõ cần đối soát).
- **Thao tác cứu hộ của Manager**:
  - Manager kiểm tra tác vụ qua `GET /api/generations/{id}` (nhìn thấy thông tin `providerTaskIds`).
  - Manager đối soát với trang quản trị của nhà cung cấp để lấy Task ID chính xác của bước hiện tại.
  - Manager gọi `POST /api/generations/{id}/recover` kèm `providerTaskId`.
  - **Guard**: Nếu tác vụ không ở trạng thái `NeedsAttention`, hệ thống trả về lỗi `409 Conflict` (`InvalidStatus`).

#### B. Từ chối duyệt mô hình (Model Rejection)
- Nếu mô hình bị lỗi lưới, thành quá mỏng hoặc không đủ điều kiện in, Manager gọi `POST /api/generations/{id}/review` với `{ "approved": false, "note": "Thành mô hình dưới 1.5mm, cần khách sửa ảnh" }`.
- Mô hình chuyển sang trạng thái `isApprovedForPrinting = false`, `reviewStatus = "rejected"`.
- **Guard**: Bất kỳ nỗ lực nào nhằm chuyển đơn hàng tương ứng sang `Printing` sẽ bị chặn với mã `409 Conflict` (`TaskNotApproved`).

#### C. Hủy đơn hàng ở bước sơ khởi (Draft Cancellation)
- Nếu khách hàng liên hệ yêu cầu hủy đơn hoặc phát hiện sai sót trước khi in, Manager thực hiện `POST /api/orders/{id}/cancel`.
- Đơn hàng chuyển sang trạng thái `Cancelled`, lưu lại thông tin người hủy trong `statusNote`.
- **Guard**: Nếu đơn hàng đã bắt đầu in (`Printing` trở đi), lệnh hủy trực tiếp sẽ bị từ chối với mã `409 Conflict` (`CannotCancel`).

### 3.3. Luồng Chuyển tiếp / Báo cáo sự cố lên Admin (Escalation Flows)

Vì Manager không có quyền can thiệp vào tài khoản, tài chính hay cấu hình lõi, các trường hợp sau đây bắt buộc phải chuyển tiếp lên Quản trị viên (Admin):

1. **Yêu cầu hoàn tiền (Refund Escalation — FR-18)**:
   - Khi sản phẩm giao bị hỏng hoặc khách hàng hủy đơn sau khi đã thanh toán, Manager **không** có quyền tự hoàn tiền.
   - Manager lập hồ sơ sự vụ và chuyển tiếp cho Admin xử lý phê duyệt hoàn tiền theo chính sách.
2. **Cập nhật biểu phí & bảng giá (Pricing Package Escalation — FR-20)**:
   - Khi xưởng đối tác điều chỉnh giá vật liệu nhựa hoặc chi phí gia công, Manager không thể tự sửa giá.
   - Manager báo cáo để Admin thực hiện `PATCH /api/admin/pricing-packages/{id}`.
3. **Cấp phát số dư / Xử lý tranh chấp tài khoản (Credit Adjustment — FR-05, FR-20)**:
   - Khi khách hàng gặp sự cố trừ quota nhưng không ra kết quả mà hệ thống không tự hoàn lượt, Manager đề xuất Admin thực hiện `POST /api/admin/users/{id}/credits`.
4. **Phân quyền và thu hồi vai trò (Role Governance — FR-20)**:
   - Manager không có quyền nâng cấp hoặc hạ cấp tài khoản của nhân sự khác. Mọi thay đổi quyền hạn đều phải do Admin phê duyệt.

---

## 4. Bảng Đối chiếu: Hiện trạng Demo vs Mục tiêu SRS P0

Tài liệu này xác định nền tảng vai trò Manager trong giai đoạn hiện tại. Bảng dưới đây phân biệt rõ các chức năng đã có trong mã nguồn với các mục tiêu P0 trong SRS đang thiếu phụ thuộc:

| Phân hệ / Khả năng | Yêu cầu SRS | Hiện trạng triển khai trong Mã nguồn | Mục tiêu SRS v1.0 (P0) / Giới hạn baseline | Trạng thái Nghiệp vụ |
|---|---|---|---|---|
| **Cơ chế Phân quyền** | FR-20 (một phần) | 4 roles: `Customer`, `Staff`, `Admin`, `Manager`. Nhóm `Management` bao gồm `staff,manager,admin`. | Quản trị vai trò (Customer, Staff, Manager, Admin), bảo vệ self-change và last-admin. Phạm vi FR-20 còn lại (khóa account, version gói/policy bất biến, audit before/after loại bỏ PII/secret) chưa thuộc task MGR-001. | **Hoàn tất scope Manager của MGR-001** (FR-20 mới đáp ứng một phần) |
| **Thanh toán (Payment)** | FR-12, FR-13, A03 | Phương thức giả lập ngoại tuyến (`paymentMode = "demo_offline_no_online_charge"`), không kết nối gateway thật. | Thanh toán đủ VND trước khi in (FR-12/13). Theo giả định **A03**, cổng thanh toán trực tuyến **chưa chốt nhà cung cấp cụ thể** (chưa chọn cổng thanh toán); cần xác định phí thực và quy trình manual reconciliation. | **Chưa triển khai** (Phụ thuộc module Payments) |
| **Phân công Xưởng (Assignment)** | FR-15, A04 | Trạng thái đơn được cập nhật trực tiếp qua API nội bộ (`OrdersController.UpdateStatus`) phục vụ demo luồng; không có danh bạ hay kết nối xưởng thật. | Theo giả định **A04**, THE BOYS làm việc với xưởng bên ngoài (PartnerLabs); Staff chọn xưởng đối tác active có năng lực, gửi offer, theo dõi accept/reject/expiry và SLA sản xuất. | **Chưa triển khai** (Phụ thuộc module PartnerLabs) |
| **Kiểm định Chất lượng (QC)** | FR-16, A04 | Thẩm định kỹ thuật sơ bộ trên mô hình 3D (watertight, slice check) trước khi duyệt in (`ReviewGenerationTaskCommand`). | `ProductionRun` qua các bước Printing → PostProcessing → QC khi đã paid + approved + assignment accepted. QC đạt mới Packaged; hỏng tạo reprint run hoặc hỗ trợ, lưu actual cost và evidence. **SRS không bắt buộc quy trình "QC 2 lớp"** (đây là đề xuất thảo luận ngoài baseline, chưa được duyệt). | **Chưa triển khai** (Phụ thuộc module ProductionRun/QC) |
| **Vận chuyển & Giao hàng** | FR-17 | Cập nhật thủ công trạng thái `Shipping` và chuỗi text `trackingNumber`. | Staff **P0 nhập thủ công** thông tin carrier/tracking hoặc hand-delivery evidence; ghi nhận sự kiện giao thất bại / giao lại / đã giao; trạng thái `Delivered` bắt buộc có bằng chứng giao hàng. Tích hợp tự động API hãng vận chuyển là **P1**, không thuộc P0. | **Chưa triển khai** (Phụ thuộc module Shipping) |
| **Khiếu nại & Hoàn tiền** | FR-18, A05 | Hủy đơn Draft tại chỗ (`CancelOrderCommand`), ghi chú chuỗi văn bản `statusNote`. | Khách gửi defect report kèm >= 1 bằng chứng; Staff đánh giá, Admin duyệt refund hoặc Staff chốt reprint run theo policy; hỗ trợ nhiều evidence/resolution, không ghi đè lịch sử. | **Chưa triển khai** (Phụ thuộc module Claims & Refunds) |

> [!NOTE]
> MGR-001 thiết lập **nền tảng vai trò Manager (Manager Role Foundation)** và các luồng vận hành hiện có của hệ thống. Task này **không tuyên bố hoàn tất toàn bộ yêu cầu P0** của SRS do các phân hệ thanh toán trực tuyến, phân công xưởng đối tác bên ngoài, quản lý sản xuất/QC và giải quyết khiếu nại chưa được xây dựng.

---

## 5. Giới hạn Kỹ thuật & Ghi chú Kiến trúc Cache

- **Phạm vi Invalidation của Cache**:
  - Hiện tại, `UserRoleCache` sử dụng `IMemoryCache` nội bộ của tiến trình Web API ([UserRoleCache.cs](../../src/TheBoys.Infrastructure/Services/UserRoleCache.cs)).
  - Khi Admin cập nhật role của người dùng qua `UpdateUserRoleCommandHandler`, cache cục bộ của instance tiếp nhận lệnh sẽ được xóa ngay lập tức (`userRoleCache.Remove`).
  - Request kế tiếp trong cùng instance API sẽ đọc trực tiếp từ SQL Database và cập nhật lại claim trên `ClaimsIdentity`.
- **Giới hạn trong Môi trường Đa Instance (Multi-instance Limitation)**:
  - Trong kịch bản triển khai cụm cân bằng tải (load balancer) với nhiều instance backend phân tán, việc xóa `IMemoryCache` ở instance A sẽ **không** tự động xóa bộ nhớ đệm ở instance B.
  - Khi mở rộng quy mô hệ thống, cần thay thế bộ nhớ đệm cục bộ bằng cơ chế phân tán (Distributed Cache như Redis Cache) hoặc bổ sung cơ chế truyền tin (Pub/Sub Event Bus) để thông báo xóa cache đồng bộ trên toàn bộ cụm máy chủ.
