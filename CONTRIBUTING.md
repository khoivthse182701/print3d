# Quy tắc phát triển và tổ chức file GitHub

Áp dụng cho repository BE. FE giữ repo/deployment riêng; chỉ chia sẻ OpenAPI/contracts/version và tài liệu tích hợp. Không thêm React/Vue/npm ứng dụng vào `src/` BE. `tools/` được phép chứa dependency dev của công cụ tài liệu.

## 1. Quy tắc đặt file

| Loại thay đổi | Vị trí bắt buộc |
|---|---|
| Entity/value object/domain policy | `src/TheBoys.Domain/{Entities,ValueObjects,Policies}/` |
| Use case | `src/TheBoys.Application/<Module>/Commands/<Action>/` hoặc `Queries/<Action>/` |
| Interface cần bởi use case | Application module hoặc `Common/Interfaces/` nếu dùng chung thật sự |
| SQL mapping/migrations | `src/TheBoys.Infrastructure/Persistence/{Configurations,Migrations}/` |
| AI/gateway/storage adapters | Infrastructure theo capability; hiện `Printing/`, module mới `Payments/`, `Storage/` |
| HTTP route/auth/validation | WebApi; controller không chứa quy trình domain hoặc gọi provider trực tiếp |
| Request/response DTO mới | `WebApi/Contracts/<Module>/`; một public type/file khi thêm/sửa lớn |
| Tests | `tests/TheBoys.Tests/{Unit,Contracts,Integration,Fixtures}/` |
| Yêu cầu và UC | `docs/requirements/` |
| Quyết định kiến trúc | `docs/architecture/decisions/NNNN-short-name.md` khi phát sinh ADR |
| Diagram source | `docs/architecture/diagrams/Dxx-name.mmd`; cập nhật gallery bằng renderer |
| Nghiên cứu có nguồn/ngày | `docs/research/` |
| SQL thiết kế chưa triển khai | `database/design/`; ghi rõ không phải migration |
| Tài liệu cũ/AI drafts | `docs/archive/`; không để `gemini-code-*.md` tại root/src |
| Generated output/test reports | `artifacts/`, `TestResults/`, đều ignored |

Không tạo folder rỗng để giả có tính năng. Module mới chỉ tạo khi có code/đặc tả. Không di chuyển hàng loạt namespace chỉ để đổi thẩm mỹ; file hiện tại có nhiều type là nợ kỹ thuật đã ghi, tách trong PR tính năng liên quan. Không đổi migration ID hoặc sửa lịch sử migration đã phát hành.

## 2. Dependency và naming

Domain độc lập framework. Application phụ thuộc Domain. Infrastructure implement interfaces của Application. WebApi là composition root. Giao tiếp external qua adapter và typed contracts; không đưa secret/provider DTO vào entity public response.

PascalCase cho C# file/type, tên file trùng type chính; command thể hiện hành động nghiệp vụ (`ApprovePrintReviewCommand`), query chỉ đọc. Markdown mới dùng kebab-case và `.vi.md` cho tiếng Việt; giữ tên `SRS.vi.md`/README/CONTRIBUTING là ngoại lệ rõ ràng. Mermaid ID ổn định D01…; FR/UC/BR/AT không tái sử dụng cho ý nghĩa khác.

## 3. Branch và PR

- Branch mới: `codex/<short-task>` cho công việc do Codex tạo; nhóm có thể dùng `feature/<id>-<name>`, `fix/<id>-<name>`, `docs/<name>` theo quy ước đã thống nhất.
- PR mô tả vấn đề, hành vi sau thay đổi, FR/UC liên quan, DB/API compatibility, test đã chạy và giới hạn. Tách design/review khỏi việc tuyên bố tính năng đã triển khai.
- Không push trực tiếp vào main; ít nhất một thành viên khác review. Thiết lập branch protection và required CI trên GitHub bởi maintainer; tài liệu này không tự cấu hình remote.
- Commit gợi ý: `docs: define checkout requirements`, `feat(payments): reconcile verified events`, `fix(generation): resolve pinned provider`.
- Không commit password/API key/token/ảnh khách/bản dump DB. `.env.example` chỉ placeholder. Secret đã lộ phải rotate; xóa file hiện tại không xóa lịch sử Git.

## 4. Definition of Done

1. Mục tiêu/use case/acceptance rõ; không có policy mâu thuẫn SRS.
2. Source, API contract, DB migration và diagram thay đổi đồng bộ; breaking change có version/migration plan.
3. Tests phù hợp pass, SQL migration kiểm trên DB test riêng; lỗi/cạnh tranh/quyền có coverage cho luồng tiền/job.
4. Không ghi log PII/secrets; request/response có phân quyền; giá/snapshot do server quyết.
5. Reviewer xác nhận immutable model/version, idempotency, state guards và ledger khi liên quan.
6. README/runbook/traceability có liên kết đúng; ghi rõ mock/sandbox/live trong evidence.

## 5. Công cụ diagram

```powershell
# Lần đầu, cho Puppeteer tải Chromium tự quản lý.
npm.cmd ci --prefix tools/diagrams
node tools/diagrams/render.mjs
```

Nếu có Chromium sẵn, đặt `PUPPETEER_SKIP_DOWNLOAD=true` khi cài và `MERMAID_BROWSER_PATH` tới executable trước render. File SVG output local ignored, source `.mmd`, gallery và lockfile commit. Mỗi thay đổi source cần render để bắt lỗi cú pháp; trước nộp xem trực quan SVG để kiểm layout.
