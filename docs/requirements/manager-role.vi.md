# Đặc tả Vai trò Quản lý (Manager Role) và Luồng Vận hành

Phiên bản: 1.1 (Cập nhật sau tích hợp origin/main — Task MGR-001)
Ngày: 26/10/2026
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
   - Manager là vai trò giám sát vận hành cấp cao: tham gia toàn bộ các luồng vận hành của Staff/Admin hiện có (duyệt hàng đợi tác vụ/đơn hàng toàn hệ thống, hàng đợi vận hành v2, đối soát/cứu hộ tác vụ AI, thẩm định kỹ thuật mô hình in 3D, cập nhật tiến trình sản xuất, giải quyết khiếu nại in lại và hủy đơn hàng ở trạng thái Draft).
   - Manager **không** có quyền quản trị hệ thống và tài chính:
     - Các thao tác quản trị hệ thống cốt lõi: quản trị tài khoản người dùng, phân bổ và thu hồi vai trò (`PATCH /api/admin/users/{id}/role`), điều chỉnh credits thủ công (`POST /api/admin/users/{id}/credits`), và cập nhật bảng giá gói in (`PATCH /api/admin/pricing-packages/{id}`) là thẩm quyền độc quyền của **Admin** (Staff và Manager đều nhận mã lỗi `403 Forbidden`).
     - Các nghiệp vụ tài chính hoàn tiền: khởi tạo giao dịch hoàn tiền (`POST /api/v2/orders/{id}/refunds`), phê duyệt bồi thường hoàn tiền khiếu nại (`DefectResolutionType.RefundApproved`), duyệt yêu cầu hủy đơn đã thanh toán (`POST /api/v2/orders/cancellation-requests/{id}/review` với `approved=true`), duyệt hủy trực tiếp đơn đã thanh toán của người khác (`POST /api/v2/orders/{id}/cancel`), và xác nhận hoàn tiền thủ công đối với cổng offline (`POST /api/v2/refunds/{id}/reconcile` khi `OFFLINE_QUERY_UNSUPPORTED`) hiện do **Staff / Admin** thực hiện theo cơ chế mã nguồn kế thừa từ nhánh baseline `origin/main` (lưu ý: việc Staff có quyền hoàn tiền là đặc thù kế thừa từ hiện trạng mã nguồn main, khác biệt so với giới hạn phân quyền chặt chẽ trong SRS gốc). Vai trò Manager bị loại trừ tuyệt đối khỏi các cổng tạo nghĩa vụ hoặc xác nhận tài chính hoàn tiền này (hệ thống trả về `403 Forbidden` trước khi sinh bất kỳ biến đổi trạng thái hay bản ghi lịch sử/outbox nào). Manager chỉ được thực hiện đối soát tự động khi cổng thanh toán trả về bằng chứng xác thực rõ ràng (gateway evidence `Succeeded` hoặc `Failed`). Quyết định về bảng giá và chính sách hệ thống là quyết định nội bộ của THE BOYS, không suy diễn từ phần mềm của bên thứ ba.
2. **Mô hình xưởng đối tác bên ngoài (PartnerLabs theo A04)**:
   - THE BOYS đóng vai trò là nền tảng điều phối xưởng gia công đối tác bên ngoài (PartnerLabs), hệ thống không mặc định sở hữu hoặc vận hành xưởng in cơ khí. Do đó, THE BOYS không sao chép các tính năng điều khiển máy in vật lý hay kết nối trực tiếp thiết bị như Formlabs/Prusa.
3. **Bảo toàn Invariant nghiệp vụ (Business Invariants)**:
   - Cấp quyền Manager không làm suy yếu các rào chắn nghiệp vụ:
     - Không cho phép bắt đầu in mô hình chưa đạt thẩm định kỹ thuật (`TaskNotApproved`).
     - Không cho phép nhảy cóc hoặc quay lui trạng thái đơn hàng (`InvalidTransition`).
     - Không cho phép hủy trực tiếp đơn hàng đã bước vào giai đoạn in (`CannotCancel`).
     - Tạo đơn hàng mới giữ nguyên quy tắc chỉ chủ sở hữu mô hình mới được tạo (`owner-only`).
     - Manager bị chặn tuyệt đối ở cổng tài chính hoàn tiền (trả `403 Forbidden`, không sinh side effect số dư hay giao dịch ngân hàng).

---

## 2. Ma trận Phân quyền (Permission Matrix)

Hệ thống quản lý 4 vai trò chính:
- `Customer` (Enum = 0, Claim = `cus`): Khách hàng cá nhân, chỉ truy cập và thao tác trên tài nguyên của chính mình.
- *(Giá trị 1 là PartnerLab cũ thời kỳ legacy, được bảo lưu và không tái sử dụng)*.
- `Staff` (Enum = 2, Claim = `staff`): Nhân viên vận hành hệ thống của THE BOYS (hỗ trợ điều phối đơn, đối soát kỹ thuật mô hình, cập nhật trạng thái đơn, khởi tạo hoàn tiền theo thẩm quyền).
- `Admin` (Enum = 3, Claim = `admin`): Quản trị viên hệ thống của THE BOYS.
- `Manager` (Enum = 4, Claim = `manager`): Người giám sát vận hành của THE BOYS (giám sát hàng đợi v1/v2 toàn hệ thống, cứu hộ tác vụ, thẩm định in, điều phối đơn, giải quyết khiếu nại in lại, đối soát vận hành; không có quyền tài chính hoàn tiền).

| Phân hệ / Endpoint | Phương thức | Customer | Staff | Manager | Admin | Dẫn chứng Code / Route |
|---|---|:---:|:---:|:---:|:---:|---|
| **Xác thực & Tài khoản** | | | | | | |
| `/api/auth/register` | POST | Public | Public | Public | Public | [AuthController.cs](../../src/TheBoys.WebApi/Controllers/AuthController.cs#L74); gán cố định Customer |
| `/api/auth/login` | POST | Có | Có | Có | Có | [AuthController.cs](../../src/TheBoys.WebApi/Controllers/AuthController.cs#L140) |
| `/api/auth/me` | GET | Own | Own | Own | Own | [AuthController.cs](../../src/TheBoys.WebApi/Controllers/AuthController.cs#L190) |
| **Tạo & Giám sát Tác vụ AI (Generations v1)** | | | | | | |
| `/api/generations/image` | POST | Own | Own | Own | Own | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L40) |
| `/api/generations` (Danh sách) | GET | Chỉ tác vụ mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L67), `IsManagement` |
| `/api/generations/{id}` (Chi tiết) | GET | Chỉ tác vụ mình | Toàn bộ (+ provider IDs) | Toàn bộ (+ provider IDs) | Toàn bộ (+ provider IDs) | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L74), [GenerationQueries.cs](../../src/TheBoys.Application/Generations/Queries/GenerationQueries.cs#L40) |
| `/api/generations/{id}/quote` | GET | Chỉ tác vụ mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L81) |
| `/api/generations/{id}/files/{kind}` | GET | Chỉ tác vụ mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L88) |
| `/api/generations/{id}/review` | POST | 403 Forbidden | Có | Có | Có | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L108), `Roles.Management` |
| `/api/generations/{id}/recover` | POST | 403 Forbidden | Có | Có | Có | [GenerationsController.cs](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs#L115), `Roles.Management` |
| **Hàng đợi Vận hành v2 (Staff / Management Queues)** | | | | | | |
| `/api/v2/staff/generation-jobs` | GET | 403 Forbidden | Có | Có | Có | [StaffController.cs](../../src/TheBoys.WebApi/Controllers/StaffController.cs#L23), `Roles.Management` |
| `/api/v2/staff/orders` | GET | 403 Forbidden | Có | Có | Có | [StaffController.cs](../../src/TheBoys.WebApi/Controllers/StaffController.cs#L143), `Roles.Management` |
| **Xử lý Đơn hàng & Luồng v2 (Orders & Fulfillment v2)** | | | | | | |
| `/api/orders` (Tạo đơn mới v1) | POST | Own task | Own task | Own task | Own task | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L40) |
| `/api/orders` (Danh sách đơn v1) | GET | Chỉ đơn mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L65) |
| `/api/orders/{id}` (Chi tiết đơn v1) | GET | Chỉ đơn mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L72) |
| `/api/orders/{id}/cancel` (Hủy đơn v1) | POST | Chỉ đơn Draft mình | Đơn Draft bất kỳ | Đơn Draft bất kỳ | Đơn Draft bất kỳ | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L79) |
| `/api/v2/orders/{id}/cancel` (Hủy đơn v2) | POST | Đơn mình (paid: gửi request; unpaid: hủy trực tiếp) | Đơn bất kỳ (paid: duyệt hủy sang `RefundPending`; unpaid: hủy) | Đơn mình (paid: gửi request; unpaid: hủy). Đơn người khác: chỉ đơn Unpaid (hủy trực tiếp); **403 Forbidden** nếu đơn đã thanh toán | Đơn bất kỳ (paid: duyệt hủy sang `RefundPending`; unpaid: hủy) | [OrdersV2Controller.cs](../../src/TheBoys.WebApi/Controllers/v2/OrdersV2Controller.cs#L158), chặn Manager hủy đơn paid người khác |
| `/api/v2/orders/cancellation-requests/{id}/review` | POST | 403 Forbidden | Có (Duyệt `Approved=true` -> `RefundPending`; hoặc Từ chối) | Từ chối (`Approved=false`) hoặc **403 Forbidden** nếu Duyệt (`Approved=true`) | Có (Duyệt `Approved=true` -> `RefundPending`; hoặc Từ chối) | [OrdersV2Controller.cs](../../src/TheBoys.WebApi/Controllers/v2/OrdersV2Controller.cs#L176), chặn Manager duyệt hoàn tiền |
| `/api/orders/{id}/status` (Chuyển trạng thái v1) | PATCH | 403 Forbidden | Có | Có | Có | [OrdersController.cs](../../src/TheBoys.WebApi/Controllers/OrdersController.cs#L85), `Roles.Management` |
| `/api/v2/orders/{id}` (Chi tiết đơn v2) | GET | Chỉ đơn mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [OrdersV2Controller.cs](../../src/TheBoys.WebApi/Controllers/v2/OrdersV2Controller.cs#L97), `CanAccess` |
| `/api/v2/orders/{id}/fulfillment` | GET | Chỉ đơn mình | Toàn bộ hệ thống | Toàn bộ hệ thống | Toàn bộ hệ thống | [FulfillmentV2Controller.cs](../../src/TheBoys.WebApi/Controllers/v2/FulfillmentV2Controller.cs#L256), `IsManagement` |
| `/api/v2/partner-labs` | GET | 403 Forbidden | Có | Có | Có | [FulfillmentV2Controller.cs](../../src/TheBoys.WebApi/Controllers/v2/FulfillmentV2Controller.cs#L67), `Roles.Management` |
| `/api/v2/payments/reconcile/{id}` | POST | 403 Forbidden | Có | Có | Có | [PaymentReconciliationController.cs](../../src/TheBoys.WebApi/Controllers/v2/PaymentReconciliationController.cs#L18), `Roles.Management` |
| `/api/v2/refunds/{id}/reconcile` | POST | 403 Forbidden | Có (Đối soát tự động hoặc xác nhận chuyển tiền thủ công) | Có (Chỉ đối soát tự động theo gateway evidence; **403 Forbidden** nếu cổng offline yêu cầu xác nhận thủ công) | Có (Đối soát tự động hoặc xác nhận chuyển tiền thủ công) | [RefundsController.cs](../../src/TheBoys.WebApi/Controllers/v2/RefundsController.cs#L24), `Roles.Management`, rào chắn handler chặn Manager xác nhận tài chính thủ công |
| `/api/v2/defect-reports/{id}/resolve` (`ReprintApproved` / `Rejected`) | POST | 403 Forbidden | Có | Có | Có | [DefectReportsV2Controller.cs](../../src/TheBoys.WebApi/Controllers/v2/DefectReportsV2Controller.cs#L81) |
| `/api/v2/defect-reports/{id}/resolve` (`RefundApproved`) | POST | 403 Forbidden | Có | **403 Forbidden** | Có | [DefectReportsV2Controller.cs](../../src/TheBoys.WebApi/Controllers/v2/DefectReportsV2Controller.cs#L93), chặn Manager |
| `/api/v2/orders/{id}/refunds` (Tạo giao dịch hoàn tiền) | POST | 403 Forbidden | Có | **403 Forbidden** | Có | [OrdersV2Controller.cs](../../src/TheBoys.WebApi/Controllers/v2/OrdersV2Controller.cs#L206), chặn Manager |
| **Quản trị Hệ thống (Admin-Only)** | | | | | | |
| `/api/admin/users` | GET | 403 Forbidden | 403 Forbidden | **403 Forbidden** | Có | [AdminController.cs](../../src/TheBoys.WebApi/Controllers/AdminController.cs#L22), `Authorize(Roles = Roles.Admin)` |
| `/api/admin/users/{id}/role` | PATCH | 403 Forbidden | 403 Forbidden | **403 Forbidden** | Có | [AdminController.cs](../../src/TheBoys.WebApi/Controllers/AdminController.cs#L29), [AdminCommands.cs](../../src/TheBoys.Application/Admin/Commands/AdminCommands.cs#L21) |
| `/api/admin/users/{id}/credits` | POST | 403 Forbidden | 403 Forbidden | **403 Forbidden** | Có | [AdminController.cs](../../src/TheBoys.WebApi/Controllers/AdminController.cs#L38), [AdminCommands.cs](../../src/TheBoys.Application/Admin/Commands/AdminCommands.cs#L63) |
| `/api/admin/pricing-packages/{id}` | PATCH | 403 Forbidden | 403 Forbidden | **403 Forbidden** | Có | [AdminController.cs](../../src/TheBoys.WebApi/Controllers/AdminController.cs#L47), [AdminCommands.cs](../../src/TheBoys.Application/Admin/Commands/AdminCommands.cs#L85) |

---

## 3. Các Luồng Hoạt động (Operation Flows)

### 3.1. Luồng chính (Main Flow: Giám sát & Vận hành Tiêu chuẩn)
```
[Admin] Cấp role Manager cho User qua PATCH /api/admin/users/{id}/role
         ↓ (Được lưu vào SQL DB; OnTokenValidated đọc trực tiếp DB mỗi request)
[Manager] Đăng nhập nhận JWT (claim role="manager") hoặc sử dụng token hiện có
         ↓
[Manager] Xem hàng đợi tác vụ AI: GET /api/v2/staff/generation-jobs hoặc GET /api/generations
         ↓
[Manager] Kiểm tra chi tiết kỹ thuật và tải mô hình: GET /api/generations/{id}/files/stl
         ↓
[Manager] Thẩm định chất lượng in: POST /api/generations/{id}/review (approved=true)
         ↓
[Customer] Tạo đơn hàng từ mẫu đã duyệt: POST /api/orders hoặc thanh toán qua v2
         ↓
[Manager] Giám sát đơn qua GET /api/v2/staff/orders và cập nhật tiến trình điều phối sản xuất:
          1. PATCH /api/orders/{id}/status (status="printing", ghi chú xưởng gia công)
          2. PATCH /api/orders/{id}/status (status="packaged")
          3. PATCH /api/orders/{id}/status (status="shipping", trackingNumber="...")
          4. PATCH /api/orders/{id}/status (status="delivered")
```

1. **Cấp quyền**: Admin sử dụng `PATCH /api/admin/users/{id}/role` gửi `{ "role": "manager" }`.
   - Hệ thống ghi nhận `UserRole.Manager = 4`, cập nhật trực tiếp vào cơ sở dữ liệu.
2. **Kích hoạt tức thì (No-relogin)**: Pipeline xác thực `OnTokenValidated` tra cứu trạng thái và role từ cơ sở dữ liệu trên mỗi request, do đó token JWT đã cấp trước đó của người dùng phản ánh quyền Manager ngay tại request kế tiếp trong cùng instance mà không bắt buộc người dùng phải đăng nhập lại.
3. **Giám sát diện rộng**: Manager gọi các hàng đợi `GET /api/v2/staff/generation-jobs`, `GET /api/v2/staff/orders`, `GET /api/generations` và `GET /api/orders` để theo dõi các tác vụ và đơn hàng trên toàn hệ thống.
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

#### D. Tiếp nhận & Giải quyết khiếu nại (Defect Resolution)
- Khi khách hàng gửi khiếu nại sản phẩm sau khi giao qua `POST /api/v2/orders/{id}/defect-reports`:
  - Manager có quyền thẩm định và phê duyệt in lại (`ReprintApproved`) hoặc từ chối (`Rejected`) qua `POST /api/v2/defect-reports/{id}/resolve`.
  - **Financial Gate**: Nếu hướng giải quyết là hoàn tiền (`RefundApproved`), Manager bị chặn với mã `403 Forbidden`. Quyết định hoàn tiền bắt buộc phải do Staff/Admin phê duyệt.

### 3.3. Luồng Chuyển tiếp / Báo cáo sự cố lên Admin (Escalation Flows)

Vì Manager không có quyền can thiệp vào tài khoản, tài chính hay cấu hình lõi, các trường hợp sau đây bắt buộc phải chuyển tiếp lên Quản trị viên (Admin):

1. **Khởi tạo & Phê duyệt hoàn tiền (Refund Escalation — FR-18)**:
   - Khi sản phẩm giao bị hỏng nặng hoặc khách hàng hủy đơn sau khi đã thanh toán mà cần hoàn tiền mặt/chuyển khoản, Manager **không** có quyền gọi `POST /api/v2/orders/{id}/refunds`, duyệt yêu cầu hủy đơn đã thanh toán (`POST /api/v2/orders/cancellation-requests/{id}/review` với `approved=true`), duyệt hủy trực tiếp đơn đã thanh toán của người khác (`POST /api/v2/orders/{id}/cancel`), duyệt khiếu nại bồi hoàn `RefundApproved`, hoặc tự xác nhận chuyển tiền hoàn thủ công cho cổng offline (`POST /api/v2/refunds/{id}/reconcile` khi `OFFLINE_QUERY_UNSUPPORTED`).
   - Manager lập hồ sơ sự vụ và chuyển tiếp cho Admin/Staff xử lý phê duyệt hoàn tiền theo chính sách. Manager chỉ được thực hiện đối soát tự động khi cổng thanh toán trả về dữ liệu đối soát xác thực (gateway evidence `Succeeded` hoặc `Failed`).
2. **Cập nhật biểu phí & bảng giá (Pricing Package Escalation — FR-20)**:
   - Khi xưởng đối tác điều chỉnh giá vật liệu nhựa hoặc chi phí gia công, Manager không thể tự sửa giá.
   - Manager báo cáo để Admin thực hiện `PATCH /api/admin/pricing-packages/{id}`.
3. **Cấp phát số dư / Xử lý tranh chấp tài khoản (Credit Adjustment — FR-05, FR-20)**:
   - Khi khách hàng gặp sự cố trừ quota nhưng không ra kết quả mà hệ thống không tự hoàn lượt, Manager đề xuất Admin thực hiện `POST /api/admin/users/{id}/credits`.
4. **Phân quyền và thu hồi vai trò (Role Governance — FR-20)**:
   - Manager không có quyền nâng cấp hoặc hạ cấp tài khoản của nhân sự khác. Mọi thay đổi quyền hạn đều phải do Admin phê duyệt.

---

## 4. Bảng Đối chiếu: Hiện trạng Triển khai vs Mục tiêu SRS P0

Bảng dưới đây cập nhật hiện trạng mã nguồn sau khi đồng bộ `origin/main` (tích hợp các module Identity, Credit Ledger, Payment v2, Fulfillment v2, QC, Notifications) và phân định phạm vi trách nhiệm của vai trò Manager:

| Phân hệ / Khả năng | Yêu cầu SRS | Hiện trạng triển khai trong Mã nguồn | Mục tiêu SRS v1.0 (P0) / Giới hạn vận hành | Trạng thái Nghiệp vụ |
|---|---|---|---|---|
| **Cơ chế Phân quyền** | FR-20 (nền tảng) | 4 roles: `Customer`, `Staff`, `Admin`, `Manager`. Nhóm `Management` bao gồm `staff,manager,admin`. Kiểm tra DB trực tiếp trên mỗi request qua `OnTokenValidated`. | Quản trị vai trò (Customer, Staff, Manager, Admin), bảo vệ self-change và last-admin. Phạm vi FR-20 còn lại (version gói/policy bất biến toàn diện, phân tầng audit log sâu) được quản lý theo kế hoạch dự án. | **Hoàn tất scope Manager của MGR-001** |
| **Thanh toán (Payment v2)** | FR-12, FR-13, A03 | Đã triển khai module Payment v2: PayOS / Sandbox gateway, tạo payment attempt, idempotency, webhook và manual reconciliation. Kiểm thử bằng fake gateway trong integration tests. | Thanh toán đủ VND trước khi in (FR-12/13). Môi trường production đang vận hành ở chế độ Sandbox gateway; việc ký kết và cấu hình credential live PayOS/SePay sẽ thực hiện tại môi trường triển khai thực tế. | **Đã triển khai mã nguồn & Sandbox tests** |
| **Phân công Xưởng (Assignment)** | FR-15, A04 | Đã triển khai module Fulfillment v2: Quản lý PartnerLab, tạo phân công (offer), chấp nhận/từ chối phân công, theo dõi hạn chót sản xuất. | Theo giả định **A04**, THE BOYS làm việc với xưởng bên ngoài (PartnerLabs); hệ thống điều phối điện tử. Chưa tích hợp tự động hóa vật lý tại xưởng đối tác. | **Đã triển khai mã nguồn & integration tests** |
| **Kiểm định Chất lượng (QC)** | FR-16, A04 | Đã triển khai ghi nhận kết quả QC sản xuất (`RecordProductionRunQc`), lưu bằng chứng storage key, kích hoạt in lại nếu QC thất bại. Thẩm định 3D sơ bộ trước khi in (`ReviewGenerationTask`). | `ProductionRun` qua các bước Printing → PostProcessing → QC khi đã paid + approved + assignment accepted. QC đạt mới Packaged; hỏng tạo reprint run hoặc ghi nhận actual cost. **SRS không bắt buộc quy trình "QC 2 lớp"**. | **Đã triển khai mã nguồn & integration tests** |
| **Vận chuyển & Giao hàng** | FR-17 | Đã triển khai tạo vận đơn (`CreateShipment`), ghi nhận sự kiện chuyển phát (`RecordShipmentEvent`), lưu bằng chứng giao hàng (`DeliveryEvidenceStorageKey`). | Staff/Manager **P0 nhập thủ công** thông tin carrier/tracking hoặc hand-delivery evidence; ghi nhận sự kiện giao thất bại / giao lại / đã giao. Tích hợp tự động API hãng vận chuyển là **P1**, không thuộc P0. | **Đã triển khai mã nguồn P0 thủ công** |
| **Khiếu nại & Hoàn tiền** | FR-18, A05 | Đã triển khai tiếp nhận khiếu nại (`CreateDefectReport`), giải quyết khiếu nại (`ResolveDefectReport`), tạo giao dịch hoàn tiền (`CreateRefund`), đối soát hoàn tiền (`ReconcileRefund`). | Khách gửi defect report kèm bằng chứng; Staff/Manager giải quyết in lại hoặc từ chối; chỉ Staff/Admin mới được duyệt hoàn tiền và tạo lệnh hoàn tiền. | **Đã triển khai mã nguồn & integration tests** |

> [!NOTE]
> MGR-001 thiết lập **nền tảng vai trò Manager (Manager Role Foundation)** trong kiến trúc tổng thể của BE-Print3D. Việc các module v2 (Payment, Fulfillment, QC, Defects) đã có mã nguồn và test suite hoàn chỉnh trong `origin/main` giúp Manager thực hiện đầy đủ chức năng điều phối vận hành thực tế, đồng thời tuân thủ nghiêm ngặt rào chắn an toàn tài chính (không cấp quyền hoàn tiền cho Manager).

---

## 5. Giới hạn Kỹ thuật & Ghi chú Kiến trúc Token Validation

- **Cơ chế Xác thực Trực tiếp từ Cơ sở Dữ liệu (`OnTokenValidated`)**:
  - Tại kiến trúc hiện tại của hệ thống ([Program.cs](../../src/TheBoys.WebApi/Program.cs#L100-L135)), sự kiện `JwtBearerEvents.OnTokenValidated` thực hiện truy vấn trực tiếp vào SQL Database (`db.Users.AsNoTracking()`) trên **mỗi request** để kiểm tra trạng thái hoạt động của tài khoản (`AccountStatus`, `LockoutEndUtc`) và trích xuất vai trò hiện thời (`user.Role`).
  - Sau khi lấy role thực tế từ cơ sở dữ liệu, claim `role` trên `ClaimsIdentity` được gán lại ngay lập tức theo giá trị DB (`identity.AddClaim(new Claim("role", cachedRole))`).
  - Nhờ cơ chế này, bất kỳ sự thay đổi vai trò nào do Admin thực hiện (như cấp hoặc thu hồi quyền Manager) đều **có hiệu lực ngay lập tức tại request kế tiếp** trên cùng instance mà không bị phụ thuộc vào thời gian tồn tại (TTL) của bộ nhớ đệm.
- **Vai trò của Bộ nhớ đệm Cục bộ (`IUserRoleCache`)**:
  - Bộ nhớ đệm in-memory `IUserRoleCache` đóng vai trò hỗ trợ đọc nhanh và được đồng bộ mỗi khi token được xác thực.
  - Do tính năng xác thực truy vấn DB trực tiếp, hệ thống đạt tính nhất quán cao về phân quyền mà không xảy ra tình trạng cấp lậu quyền cho token cũ quá thời hạn.
