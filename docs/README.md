# Hồ sơ phát triển THE BOYS — Backend

Phiên bản 1.0 · 19/10/2026 · baseline code `04874ed` · trạng thái: đề xuất để nhóm duyệt và triển khai.

Đây là hồ sơ cho **backend riêng biệt với frontend**. Review xác nhận code và kiểm thử trong repository này; không suy đoán giao diện FE đã có. SRS/DB vNext mô tả sản phẩm mục tiêu, không phải tuyên bố mọi tính năng đã chạy.

## Đọc theo thứ tự

1. [Review và các thiếu sót](review/project-review.vi.md): kết luận, mức độ, bằng chứng, hướng sửa.
2. [Kiểm kê luồng và API](review/flow-inventory.vi.md): toàn bộ điểm vào hiện có và luồng còn thiếu.
3. [Nghiên cứu sản phẩm tham khảo](research/competitor-study.vi.md): Tripo, Meshy, Tencent; nguồn chính thức và quyết định phạm vi.
4. [SRS](requirements/SRS.vi.md): yêu cầu chức năng, phi chức năng, quy tắc, tiêu chí nghiệm thu.
5. [Đặc tả use case](requirements/use-cases.vi.md) và [truy vết](requirements/traceability.vi.md).
6. [Thiết kế SWD và Mermaid](architecture/README.md): 5 loại sơ đồ chính theo slide môn học; ERD và communication bổ sung.
7. [Thiết kế DB](architecture/database-design.vi.md), [DDL vNext](../database/design/vnext.sql), [kế hoạch migration](../database/migrations/README.md).
8. [Hợp đồng BE–FE](api/backend-contract.vi.md), [backlog triển khai](planning/implementation-backlog.vi.md), [kết quả kiểm chứng](review/validation.vi.md).
9. [Quy tắc repository](../CONTRIBUTING.md), [demo hiện tại](runbooks/demo-flow.vi.md).

## Nguồn sự thật

| Nội dung | Nơi quản lý |
|---|---|
| Hành vi đã triển khai | `src/`, `tests/`, EF migrations |
| Yêu cầu sản phẩm mục tiêu | `docs/requirements/` |
| Mô hình thiết kế mục tiêu | `docs/architecture/`, `database/design/` |
| Thay đổi DB thực tế | EF migrations trong Infrastructure |
| Tài liệu kinh doanh đầu vào | `docs/business/` |
| Bản nháp và ghi chú cũ | `docs/archive/`; không dùng làm đặc tả đang hiệu lực |

Không có thay đổi schema nào tự động áp dụng vào database ứng dụng trong đợt review. DDL vNext chạy trên schema riêng trong database kiểm thử; cần triển khai migration theo từng đợt trước khi ứng dụng sử dụng thiết kế mới.
