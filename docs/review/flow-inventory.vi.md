# Kiểm kê luồng và API hiện tại

Baseline `04874ed`, 19/10/2026. **Có** = có code thực thi; **Một phần** = có code/schema nhưng thiếu nhánh nghiệp vụ; **Thiếu** = chưa thấy entry point/handler. Thống kê theo bảng này, không phải phần trăm hoàn thành sản phẩm.

## 32 nhóm luồng nghiệp vụ

| ID | Nhóm luồng | Actor | Hiện trạng | Điểm thiếu / nhánh lỗi quan trọng |
|---|---|---|---|---|
| F01 | Đăng ký/đăng nhập/xem tài khoản | Guest, Customer | Có | Trùng email, sai mật khẩu; chưa verify |
| F02 | Reset mật khẩu, phiên đăng nhập, logout | Customer | Thiếu | Token hết hạn/reuse/revoke |
| F03 | Upload ảnh và tạo job | Customer | Có | Kiểm magic bytes, size, quota, idempotency; cần decode/consent |
| F04 | Text-to-3D | Customer | Một phần | Enum/Prompt chưa có API |
| F05 | Multi-view-to-3D | Customer | Thiếu | Chưa input nhiều ảnh |
| F06 | Worker generate/resize/convert/analyze | System | Có | Checkpoint và applock; cần provider pinning |
| F07 | Timeout mơ hồ/đối soát/recover | Staff | Có | Không tự resubmit; chưa chứng minh ID recover thuộc đúng input |
| F08 | Hoàn lượt khi generation thất bại | System | Có | Một lần, đúng bucket; chưa ledger |
| F09 | Danh sách/xem/tải model | Customer, Staff | Có | Ownership; chưa tổ chức project/version/tìm kiếm |
| F10 | Sửa model, resize, version và chọn mẫu | Customer, Staff | Thiếu | Đổi height hiện regenerate, không tái dùng model |
| F11 | Duyệt kỹ thuật khả năng in | Staff | Có | Ghi đè review, thiếu checklist và bằng chứng |
| F12 | Khách chốt mẫu và cấu hình cuối | Customer | Thiếu | Chưa approval/version hash |
| F13 | Xem gói và báo giá | Guest, Customer | Có | Báo giá trực tiếp, chưa expiry/snapshot quote |
| F14 | Tạo đơn chống trùng | Customer | Có | Một model/đơn, total server tính; chưa checkout thật |
| F15 | Xem danh sách/chi tiết đơn | Customer, Staff | Có | Customer chỉ xem của mình; chưa timeline đầy đủ |
| F16 | Hủy đơn | Customer, Staff | Một phần | Chỉ Draft; chưa cancel request/refund sau trả tiền |
| F17 | Thanh toán và đối soát | Customer, Gateway | Một phần | Có bảng Payment, không có API/handler |
| F18 | Hoàn tiền từng phần/toàn bộ | Admin, Gateway | Thiếu | Enum Refunded chưa có luồng |
| F19 | CRUD xưởng và phân công/nhận/từ chối | Staff | Một phần | Có PartnerLab và FK, thiếu API |
| F20 | Sản xuất/hậu kỳ/QC | Staff | Một phần | Chỉ Printing → Packaged; chưa production run/QC |
| F21 | Giao hàng/tracking | Staff | Một phần | Có Shipping/Delivered, tracking tùy chọn; chưa giao thất bại |
| F22 | Khiếu nại/bằng chứng/reprint | Customer, Staff | Một phần | Field defect, enum reprinting chưa có luồng |
| F23 | Đổi role và liệt kê users | Admin | Có | Không tự hạ mình, giữ admin; audit còn thiếu |
| F24 | Cấp lượt tạo | Admin | Có | Thiếu lý do/ledger/idempotency cho cấp |
| F25 | Cập nhật giá/bật tắt gói | Admin | Có | Chưa version giá/material CRUD |
| F26 | Mua lượt/subscription | Customer | Thiếu | Không được coi cấp lượt admin là mua lượt |
| F27 | Thông báo và retry | System | Thiếu | Email/in-app chưa có outbox |
| F28 | Thống kê conversion/chi phí/SLA | Admin | Thiếu | Chưa API báo cáo; snapshot không là actual cost |
| F29 | Quyền riêng tư/retention/xóa dữ liệu | Customer, Staff | Một phần | Có soft-delete field/filter, không có workflow |
| F30 | Health/backup/khôi phục | Ops | Một phần | Liveness có; readiness/restore chưa kiểm chứng |
| F31 | Đặt lại/đơn nhóm/B2B | Customer | Thiếu | Quantity cùng mẫu có, nhiều mẫu/lịch thanh toán chưa có |
| F32 | Gallery/chia sẻ/moderation | Customer, Staff | Thiếu | Chưa public share/consent/moderation |

**Tổng: 13 Có, 9 Một phần, 10 Thiếu.** Các nhóm “Có” vẫn có nợ kỹ thuật như mô tả; số lượng này không hàm ý đủ điều kiện production.

## 20 controller endpoints và 2 minimal endpoints

| Method | Route | Quyền | Handler / side effect |
|---|---|---|---|
| POST | `/api/auth/register` | Public | User + hash + 2 trial; JWT |
| POST | `/api/auth/login` | Public | Verify password; JWT |
| GET | `/api/auth/me` | Auth | User hiện tại |
| GET | `/api/pricing-packages` | Public | Active packages |
| POST | `/api/generations/image` | Auth | Ảnh + quota + queued job; 202/replay 200 |
| GET | `/api/generations` | Auth | Customer own; management all; 20/page |
| GET | `/api/generations/{id}` | Owner/management | Job/progress/result |
| GET | `/api/generations/{id}/quote` | Owner/management | Giá package × quantity + shipping config |
| GET | `/api/generations/{id}/files/{kind}` | Owner/management | image/glb/stl; file private |
| POST | `/api/generations/{id}/review` | Staff/Admin | Ghi quyết định review hiện tại |
| POST | `/api/generations/{id}/recover` | Staff/Admin | Gắn provider ID, tiếp tục stage |
| POST | `/api/orders` | Auth | Model phải thuộc caller; snapshot giá; 201/replay 200 |
| GET | `/api/orders` | Auth | Own/all theo role; 20/page |
| GET | `/api/orders/{id}` | Owner/management | Chi tiết đơn |
| POST | `/api/orders/{id}/cancel` | Owner/management | Draft → Cancelled |
| PATCH | `/api/orders/{id}/status` | Staff/Admin | Transition demo |
| GET | `/api/admin/users` | Admin | 50 users/page |
| PATCH | `/api/admin/users/{id}/role` | Admin | Đổi role + evict local cache |
| POST | `/api/admin/users/{id}/credits` | Admin | Tăng PurchasedRemaining |
| PATCH | `/api/admin/pricing-packages/{id}` | Admin | Giá/IsActive |
| GET | `/api/v2/staff/generation-jobs` | Staff/Admin | Hàng đợi tác vụ tạo 3D; filter status/review/provider/date/age, paging, sort oldest/newest |
| GET | `/api/v2/staff/orders` | Staff/Admin | Hàng đợi đơn hàng; filter status/orderCode/date/age, paging, sort oldest/newest |
| GET | `/` | Public | Redirect Scalar; UI docs chỉ mapped Development |
| GET | `/health` | Public | Liveness luôn ok |

Tổng **22 controller endpoints + 2 minimal endpoints = 24** (đếm mỗi HTTP method + route template một lần; route file `{kind}` tính một). OpenAPI/Scalar Development là công cụ tài liệu, không tính luồng nghiệp vụ.

## Đồ thị trạng thái thực sự reachable

- Generation: Queued → Processing → MeshOptimizing → Succeeded. Nhánh Failed khi provider xác nhận lỗi, NeedsAttention khi submit mơ hồ/tải file/format lỗi. Recover quay về Processing hoặc MeshOptimizing theo stage. Expired có enum nhưng chưa thấy logic chuyển tới.
- Stage Meshy: Generate → Resize → Convert → Analyze → Complete. Tripo: Generate → Convert → Analyze → Complete; analyze local.
- Review: Pending → Approved/Rejected; cho duyệt lại trên job Succeeded, ghi đè review cũ.
- Order: Draft → Printing → Packaged → Shipping → Delivered; Draft → Cancelled. Bắt đầu Printing đòi model Succeeded và Approved.
- PendingPayment/Paid/SentToPartner/Reprinting/PostProcessing/Refunded chưa reachable trong handler status hiện tại; PaymentStatus chưa có luồng thao tác.

## Liên kết kỹ thuật

Controller ở `src/TheBoys.WebApi/Controllers/`; ứng dụng ở `src/TheBoys.Application/{Generations,Orders,Admin}/`; worker ở `src/TheBoys.Infrastructure/Printing/`. SRS mục tiêu dùng UC-xx/FR-xx riêng, không lấy số route thay cho số use case.
