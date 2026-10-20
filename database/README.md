# Database artifacts

- **Runtime source of truth:** `src/TheBoys.Infrastructure/Persistence/Configurations/` và `Persistence/Migrations/`; áp dụng qua EF Core.
- **Thiết kế tương lai:** [design/vnext.sql](design/vnext.sql), chỉ compile trong DB scratch riêng; không chạy thay migration của app.
- **Lịch sử tham khảo:** [legacy/](legacy/README.md), không chạy đồng thời với EF migrations.
- **Chuyển đổi:** [migration plan](migrations/README.md); mapping dữ liệu, backfill, rollback và gates.

Không commit `.bak`, `.mdf`, `.ldf`, connection có password, ảnh/model khách hoặc production dumps. `scripts/Verify-Design.ps1` có guard tên DB và cleanup.
