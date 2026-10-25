# Đặc tả Vai trò Quản lý (Manager Role) và Luồng Vận hành

Phiên bản: 1.0  
Ngày: 25/10/2026  
Mã nhiệm vụ: `MGR-001`  
Tài liệu liên quan: [SRS.vi.md](SRS.vi.md), [use-cases.vi.md](use-cases.vi.md), [backend-contract.vi.md](../api/backend-contract.vi.md), [demo-flow.vi.md](../runbooks/demo-flow.vi.md)

---

## 1. Bối cảnh và Quyết định Thiết kế

Người dùng đã xác nhận ngày 25/10/2026: **Manager là một vai trò (role) riêng biệt trong hệ thống BE-Print3D, tách rời hoàn toàn với Staff và Admin**.

### 1.1. Nguyên tắc phân quyền và tham chiếu thực tế
- **Tham khảo mô hình phân cấp công nghiệp (Formlabs Dashboard, Prusa Connect)**:
  - Formlabs phân tách quyền giữa thành viên quản trị (*Admin*) và thành viên vận hành (*Member*). Chỉ Admin mới có quyền thay đổi chính sách, giá gói và cấp phát quyền tài khoản.
  - Prusa Connect phân chia quyền quản lý hàng đợi (*Queue Management*) độc lập với quản trị hệ thống.
- **Quyết định thiết kế cho THE BOYS**:
  - **Manager** là vai trò giám sát vận hành cấp cao: tham gia toàn bộ các luồng vận hành của Staff/Admin hiện có (duyệt hàng đợi, đọc dữ liệu toàn hệ thống xuyên người dùng, đối soát/cứu hộ tác vụ AI, thẩm định kỹ thuật mô hình in 3D, cập nhật tiến trình sản xuất và hủy đơn hàng ở trạng thái Draft).
  - **Giới hạn an toàn (Least Privilege)**: Manager **không** có quyền quản trị hệ thống. Mọi route `/api/admin/*` (danh sách người dùng, thay đổi role, cộng credit thủ công, cập nhật bảng giá) vẫn duy trì là **Admin-only**.
  - **Bảo toàn Invariant nghiệp vụ**: Việc cấp quyền Manager không cho phép bỏ qua các quy tắc nghiệp vụ (business invariants) như: không được in mô hình chưa duyệt (`TaskNotApproved`), không thể quay ngược trạng thái đơn hàng (`InvalidTransition`), không thể hủy đơn khi đã bắt đầu in (`CannotCancel`), và không được tự tạo đơn hàng thay cho khách khác (tạo đơn giữ nguyên quy tắc owner-only).

---

## 2. Ma trận Phân quyền (Permission Matrix)

Hệ thống quản lý 4 vai trò chính:
- `Customer` (Enum = 0, Claim = `cus`): Khách hàng cá nhân, chỉ truy cập tài nguyên của chính mình.
- *(Giá trị 1 là PartnerLab cũ thời kỳ legacy, được bảo lưu và không tái sử dụng)*.
- `Staff` (Enum = 2, Claim = `staff`): Nhân viên xưởng in, vận hành sản xuất.
- `Admin` (Enum = 3, Claim = `admin`): Quản trị viên tối cao của hệ thống.
- `Manager` (Enum = 4, Claim = `manager`): Quản lý giám sát vận hành xưởng in.

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
| `/api/admin/refunds` *(Mục tiêu SRS)* | POST | N/A | N/A | N/A | Chưa có API | Thuộc mục tiêu SRS P0, chưa triển khai endpoint |

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
[Manager] Giám sát đơn và cập nhật tiến trình sản xuất:
          1. PATCH /api/orders/{id}/status (status="printing", ghi chú xưởng)
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

#### A. Cứu hộ tác vụ gặp sự cố (Task Recovery)
- Khi kết nối mạng hoặc nhà cung cấp AI gặp lỗi chập chờn, tác vụ chuyển sang trạng thái `NeedsAttention` (`GenerationStatus.NeedsAttention = 6`).
- Manager kiểm tra tác vụ qua `GET /api/generations/{id}` (nhìn thấy thông tin `providerTaskIds`).
- Manager gọi `POST /api/generations/{id}/recover` với `providerTaskId` tương ứng.
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

1. **Yêu cầu hoàn tiền (Refund Escalation)**:
   - Khi sản phẩm giao bị hỏng hoặc khách hàng hủy đơn sau khi đã thanh toán, Manager **không** có quyền tự hoàn tiền.
   - Manager lập biên bản sự việc và chuyển tiếp cho Admin xử lý qua quy trình đối soát tài chính.
2. **Cập nhật biểu phí & bảng giá (Pricing Package Escalation)**:
   - Khi xưởng in thay đổi giá vật liệu nhựa hoặc chi phí gia công, Manager không thể tự sửa giá.
   - Manager báo cáo để Admin thực hiện `PATCH /api/admin/pricing-packages/{id}`.
3. **Cấp phát số dư / Xử lý tranh chấp tài khoản (Credit Adjustment)**:
   - Khi khách hàng gặp lỗi AI làm mất lượt nhưng không thể tự động bù, Manager đề xuất Admin thực hiện `POST /api/admin/users/{id}/credits`.
4. **Phân quyền và thu hồi vai trò (Role Governance)**:
   - Manager không có quyền nâng cấp hoặc hạ cấp tài khoản của nhân viên khác. Mọi thay đổi quyền hạn đều phải do Admin phê duyệt.

---

## 4. Bảng Đối chiếu: Hiện trạng Demo vs Mục tiêu SRS P0

Tài liệu này xác định nền tảng vai trò Manager trong giai đoạn hiện tại. Bảng dưới đây phân biệt rõ các chức năng đã có trong mã nguồn với các mục tiêu P0 trong SRS đang thiếu phụ thuộc:

| Phân hệ / Khả năng | Hiện trạng triển khai trong Mã nguồn | Mục tiêu SRS v1.0 (P0) / Yêu cầu tương lai | Trạng thái Nghiệp vụ |
|---|---|---|---|
| **Cơ chế Phân quyền** | 4 roles: `Customer`, `Staff`, `Admin`, `Manager`. Nhóm `Management` bao gồm `staff,manager,admin`. | Đầy đủ vai trò vận hành, giám sát và quản trị hệ sinh thái. | **Đã hoàn thành** (MGR-001) |
| **Thanh toán (Payment)** | Phương thức giả lập ngoại tuyến (`paymentMode = "demo_offline_no_online_charge"`), không trừ tiền thật. | Tích hợp cổng thanh toán trực tuyến (VNPay/MoMo/SePay), Webhook IPN, lưu vết giao dịch Ledger bất biến. | **Chưa triển khai** (Phụ thuộc module Payments) |
| **Phân công Xưởng (Assignment)** | Xưởng THE BOYS tự xử lý demo, cập nhật trực tiếp trạng thái đơn hàng. | Quản lý danh bạ PartnerLab, chào việc (offer), tiếp nhận (accept), theo dõi SLA sản xuất theo hợp đồng. | **Chưa triển khai** (Phụ thuộc module PartnerLabs) |
| **Kiểm định Chất lượng (QC)** | Review kỹ thuật sơ bộ trên mô hình 3D (watertight, slice check) trước khi in. | Biên bản QC 2 lớp (xưởng đối tác + nội bộ), checklist tiêu chuẩn kích thước/bề mặt, bằng chứng ảnh chụp. | **Chưa triển khai** (Phụ thuộc module QualityAssurance) |
| **Vận chuyển & Giao hàng** | Cập nhật thủ công trạng thái `Shipping` và chuỗi văn bản `trackingNumber`. | Tích hợp API đối tác vận chuyển (GHN/GHTK), tạo vận đơn tự động, Webhook trạng thái hành trình thực. | **Chưa triển khai** (Phụ thuộc module Shipping) |
| **Khiếu nại & Hoàn tiền** | Hủy đơn Draft tại chỗ, ghi chú chuỗi văn bản `statusNote`. | Bảng `DefectReports`, quy trình phân xử tranh chấp, xét duyệt hoàn tiền tự động qua cổng thanh toán. | **Chưa triển khai** (Phụ thuộc module Claims & Refunds) |

> [!NOTE]
> MGR-001 thiết lập **nền tảng vai trò Manager (Manager Role Foundation)** và các luồng vận hành hiện có của hệ thống. Task này **không tuyên bố hoàn tất toàn bộ yêu cầu P0** của SRS do các phân hệ thanh toán, phân công xưởng và khiếu nại chưa được xây dựng.

---

## 5. Giới hạn Kỹ thuật & Ghi chú Kiến trúc Cache

- **Phạm vi Invalidation của Cache**:
  - Hiện tại, `UserRoleCache` sử dụng `IMemoryCache` nội bộ của tiến trình Web API ([UserRoleCache.cs](../../src/TheBoys.Infrastructure/Services/UserRoleCache.cs)).
  - Khi Admin cập nhật role của người dùng qua `UpdateUserRoleCommandHandler`, cache cục bộ của instance tiếp nhận lệnh sẽ được xóa ngay lập tức (`userRoleCache.Remove`).
  - Request kế tiếp trong cùng instance API sẽ đọc trực tiếp từ SQL Database và cập nhật lại claim trên `ClaimsIdentity`.
- **Giới hạn trong Môi trường Đa Instance (Multi-instance Limitation)**:
  - Trong kịch bản triển khai cụm cân bằng tải (load balancer) với nhiều instance backend phân tán, việc xóa `IMemoryCache` ở instance A sẽ **không** tự động xóa bộ nhớ đệm ở instance B.
  - Khi mở rộng quy mô hệ thống, cần thay thế bộ nhớ đệm cục bộ bằng cơ chế phân tán (Distributed Cache như Redis Cache) hoặc bổ sung cơ chế truyền tin (Pub/Sub Event Bus) để thông báo xóa cache đồng bộ trên toàn bộ cụm máy chủ.
