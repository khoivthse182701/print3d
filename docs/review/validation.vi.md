# Bằng chứng kiểm chứng và giới hạn bàn giao

Review bắt đầu 19/10/2026, bàn giao kiểm chứng 20/10/2026 (Asia/Bangkok). Baseline source `04874ed`. Chỉ kiểm backend, không có repo FE. Đây là báo cáo review/thiết kế, không phải chứng nhận triển khai mọi FR mục tiêu.

## Kết quả thực thi

| Kiểm tra | Kết quả / phạm vi |
|---|---|
| Baseline `dotnet test TheBoys.slnx` | 20 passed, 0 failed, 0 skipped trước sắp xếp folder; SQL Server + provider fake |
| Sau tổ chức folder, build/test trên LocalDB | 20 passed, 0 failed, 0 skipped; TRX ở TestResults local, provider fake; không gọi AI có phí |
| `dotnet tool restore` | Thành công; manifest chuẩn `.config/dotnet-tools.json`, EF 10.0.13 |
| `dotnet ef migrations has-pending-model-changes --project src/TheBoys.Infrastructure --startup-project src/TheBoys.WebApi --no-build` | Không có model runtime drift so với migration snapshot |
| `Verify-Design.ps1 -Server '(localdb)\MSSQLLocalDB'` | DDL 34 bảng compile và fixture hợp lệ; negative tests chặn balance âm, job key trùng, hoàn credit hai lần, JSON hỏng, tổng quote sai và approval/config mismatch |
| Mermaid CLI 11.12.0 | 10 source diagrams render SVG thành công; gallery nhúng được sinh từ source |
| Probe R19 với STL bất đối xứng Z-up | Mẫu có tỷ lệ X:Y:Z = 200:50:100, yêu cầu cao 100 mm: report trả height=100 nhưng extent Z sau normalize chỉ 50 mm; tái hiện được bằng code runtime hiện tại |
| `Verify-Docs.ps1` | 19 tài liệu đang hiệu lực, 68 liên kết local hợp lệ, source/gallery Mermaid đồng bộ |
| Bảo toàn file di chuyển | 28 file đối chiếu Git blob hash giữ nguyên nội dung; runbook demo được sửa có chủ đích theo code hiện tại |
| Dọn DB test | Query sys.databases trên LocalDB trả 0 database còn lại thuộc prefix DesignCheck/Test sau kiểm chứng |
| `git diff --check` | Không có lỗi whitespace trong tracked diff |

Lần test mặc định `Server=localhost` ở phiên kiểm chứng cuối thất bại do MSSQLSERVER đang stopped; tài khoản hiện tại không start service được. Chuyển sang SQL LocalDB và chạy lại đầy đủ đã pass. Không dùng lỗi môi trường đó để bỏ qua integration tests.

SQL verifier tự tạo/xóa DB `TheBoys_DesignCheck_<UUID>`. Tests tạo/xóa `TheBoys_Test_<UUID>`. Không chạy DDL mới lên DB ứng dụng; không thay connection, dữ liệu, provider key hoặc schema runtime của ứng dụng.

Đã xem ảnh render của cả 10 diagram; phát hiện và sửa dấu `;` khiến state renderer tạo node thừa ở D07 dù render không báo lỗi. Sơ đồ tổng quát Activity/ERD dài, nên xem SVG ở độ phóng lớn hoặc dùng source Mermaid trong GitHub. PNG/SVG kiểm tra nằm ở `artifacts/diagrams/` và không commit.

## Audit theo yêu cầu người dùng

| Yêu cầu | Evidence bàn giao |
|---|---|
| Review project và phần còn thiếu | project-review: 19 findings, mức ưu tiên, file/trigger/impact/acceptance; flow inventory đối chiếu controllers/handlers/entities |
| Thống kê tất cả luồng BE hiện tại | 20 controller routes + 2 minimal endpoints; 32 nhóm flow, state reachable và enum-only tách rõ |
| Nghiên cứu Tripo/Meshy/Tencent | competitor-study: nguồn chính thức, ngày đọc, so sánh capability/limitation và phạm vi MVP; không bịa benchmark |
| Tinh chỉnh DB để phát triển | DDL target, 34 bảng/data dictionary/constraints, ERD, transaction invariants, migration mapping/rollback; compile trong DB riêng |
| Bộ SRS | 28 FR, 10 NFR, 15 BR, 22 UC, assumptions, interface, acceptance và traceability; bản draft cần nhóm chốt policy |
| Ít nhất 4–5 diagram theo slide | 5 loại chính Use Case/Activity/Class/Sequence/State, 10 diagram tổng gồm ERD/Communication; architecture index chỉ rõ chương/slide |
| Folder và quy tắc GitHub | README/CONTRIBUTING, tài liệu theo nhóm, tests theo loại, migrations tập trung, tool manifest, PR template/CI source |
| Tách FE/BE, ưu tiên BE | Repo giữ .NET BE; FE contract riêng và chưa tuyên bố kiểm UI; tools/diagrams chỉ dev tooling |

## Giới hạn của bằng chứng

- Contract tests HTTP dùng fake responses. Chưa xác nhận hợp đồng Tripo/Meshy thật, tài khoản API, model quality hoặc thời gian/chi phí thực tế.
- DDL constraints không thay validation/transaction/RBAC của application. Các invariant cross-row (refund sum, owner của approval, transition) được chỉ rõ bên application trong DB design; chưa được code vNext triển khai.
- CI workflow đã thêm vào repository nhưng chưa push/chạy GitHub Actions; branch protection chưa cấu hình remote. Các lệnh build/test/SQL/render tương ứng đã kiểm local như bảng trên; execution runner Ubuntu/Windows của GitHub vẫn cần first run.
- Mermaid dùng flowchart để biểu diễn use case/activity/communication tương thích; rubric yêu cầu UML ký pháp tuyệt đối cần chuyển sang công cụ UML theo giải thích trong architecture.
- Chưa thực hiện live gateway sandbox payment/refund, load test, backup restore sản phẩm, FE consumer tests hoặc in mẫu vật lý. Các việc đó nằm trong G1–G4 backlog, không thuộc tuyên bố hoàn thành sản phẩm.
- Không commit/push/PR; đây là thay đổi local để nhóm review. File cũ được di chuyển giữ nội dung, không xóa tài liệu kinh doanh/lịch sử.
