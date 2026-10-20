# THE BOYS — 3D Generation & Print Ordering Backend

Backend .NET 10 / ASP.NET Core / EF Core SQL Server. Frontend được phát triển trong repository riêng và dùng API; thư mục `tools/diagrams` chỉ là công cụ tài liệu, không phải FE.

## Bắt đầu với hồ sơ dự án

- [Mục lục hồ sơ](docs/README.md)
- [Review và khoảng trống](docs/review/project-review.vi.md), [32 nhóm luồng](docs/review/flow-inventory.vi.md)
- [SRS](docs/requirements/SRS.vi.md), [use cases](docs/requirements/use-cases.vi.md), [truy vết](docs/requirements/traceability.vi.md)
- [Thiết kế SWD theo slide](docs/architecture/README.md), [Mermaid gallery](docs/architecture/diagrams/README.md)
- [Thiết kế DB](docs/architecture/database-design.vi.md), [migration plan](database/migrations/README.md)
- [Nghiên cứu Tripo / Meshy / Tencent](docs/research/competitor-study.vi.md)
- [Quy tắc GitHub và xếp file](CONTRIBUTING.md), [backlog](docs/planning/implementation-backlog.vi.md)

**Phân biệt hiện trạng với mục tiêu:** source hiện tại chạy demo ảnh → model → duyệt → đơn offline. SRS và DDL vNext là thiết kế phát triển, chưa phải các tính năng production đã triển khai. Database ứng dụng tiếp tục do EF migrations quản lý; không chạy DDL legacy hoặc vNext lên DB đang dùng.

## Chạy và kiểm thử

Cần .NET SDK 10 và SQL Server hoặc SQL LocalDB. Cấu hình secrets, initialize database, provider và API mẫu xem [runbook](docs/runbooks/demo-flow.vi.md).

```powershell
dotnet tool restore
dotnet restore TheBoys.slnx
dotnet build TheBoys.slnx --no-restore

# Chọn DB test local; tests luôn tự tạo DB TheBoys_Test_<UUID>.
$env:THEBOYS_TEST_SQL = 'Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True'
dotnet test TheBoys.slnx --no-build

# Compile và kiểm constraints DDL thiết kế trong DB scratch riêng.
powershell -File scripts/Verify-Design.ps1 -Server '(localdb)\MSSQLLocalDB'
```

API local theo launch profile: `http://localhost:5133`, Scalar `/scalar/v1`, OpenAPI `/openapi/v1.json` trong Development. `GET /health` hiện chỉ là liveness. Tests dùng provider giả lập, không gọi API AI có phí.

## Cấu trúc repository

```text
.config/                    # dotnet tool manifest
.github/                    # CI, PR template
src/
  TheBoys.Domain/            # entities, enums, business rules
  TheBoys.Application/       # use cases, commands/queries, interfaces
  TheBoys.Infrastructure/    # EF persistence/migrations, providers, storage, worker
  TheBoys.WebApi/            # HTTP, auth, DI composition root
tests/TheBoys.Tests/
  Unit/                     # domain/pricing/geometry
  Contracts/                # mocked external API contracts
  Integration/              # SQL + HTTP + migrations
  Fixtures/                 # isolated DB, fake providers/storage
docs/
  requirements/             # SRS, use cases, traceability
  architecture/diagrams/    # Mermaid sources and gallery
  api/                      # BE–FE contract
  review/                   # findings, inventory, validation
  research/                 # sources and comparison
  planning/                 # backlog and release gates
  runbooks/                 # setup/demo/operations
  business/                 # CP3 and approved business inputs
  archive/                  # historical drafts, not current specifications
database/
  design/                   # target DDL and constraint checks
  legacy/                   # old references, never runtime source of truth
  migrations/               # migration strategy, not executable EF classes
scripts/                    # repository verification scripts
tools/diagrams/             # documentation renderer and locked dependencies
artifacts/                  # generated local outputs, ignored
```

Ngày review bắt đầu 19/10/2026; kiểm chứng bàn giao 20/10/2026. Xem [bằng chứng và giới hạn](docs/review/validation.vi.md).
