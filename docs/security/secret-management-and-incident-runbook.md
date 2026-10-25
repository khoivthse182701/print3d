# Secret Management & Credential Incident Runbook

Tài liệu này hướng dẫn quản lý bí mật (credentials, API keys, tokens) trong môi trường phát triển cục bộ, CI/CD và quy trình phản ứng khi xảy ra sự cố lộ khóa.

---

## 1. Nguyên tắc cốt lõi (Zero Plaintext Secrets)

1. **Tuyệt đối không lưu plaintext secrets vào Git:**
   - Các file `appsettings.json`, `appsettings.Development.json` và cấu hình mẫu trong mã nguồn chỉ chứa giá trị rỗng (`""`) hoặc placeholder.
   - Không commit các file chứa secret thực tế (`appsettings.Production.json`, `appsettings.*.local.json`, `.env`).

2. **Cách nạp secret theo từng môi trường:**
   - **Local Development:** Sử dụng .NET User Secrets hoặc biến môi trường cục bộ.
   - **CI / Testing:** Sử dụng mock providers hoặc biến môi trường giả lập (không yêu cầu API key thật để build/test).
   - **Staging / Production:** Nạp qua biến môi trường của container/server hoặc Secret Manager / Key Vault chuyên dụng.

---

## 2. Hướng dẫn cấu hình bí mật cục bộ (.NET User Secrets & Env Vars)

Dự án `TheBoys.WebApi` đã kích hoạt sẵn UserSecrets (`theboys-print3d-local`).

### 2.1. Cấu hình bằng .NET User Secrets (Khuyến nghị cho Local Dev)

Từ thư mục gốc của repository, thực hiện:

```bash
# Cấu hình Meshy API Key
dotnet user-secrets set "AiProviders:Meshy:ApiKey" "<YOUR_NEW_MESHY_API_KEY>" --project src/TheBoys.WebApi

# Cấu hình Tripo API Key (nếu sử dụng)
dotnet user-secrets set "AiProviders:Tripo:ApiKey" "<YOUR_NEW_TRIPO_API_KEY>" --project src/TheBoys.WebApi

# Cấu hình JWT Secret (tối thiểu 32 ký tự / 256 bits)
dotnet user-secrets set "JwtSettings:Secret" "<YOUR_STRONG_RANDOM_JWT_SECRET>" --project src/TheBoys.WebApi

# Cấu hình Bootstrap Admin Password
dotnet user-secrets set "BootstrapAdmin:Password" "<STRONG_BOOTSTRAP_PASSWORD>" --project src/TheBoys.WebApi
```

### 2.2. Cấu hình bằng Biến Môi Trường (Environment Variables)

Hệ thống .NET tự động ánh xạ cấu hình phân cấp từ biến môi trường bằng dấu phân cách hai gạch dưới (`__`):

| Cấu hình | Tên biến môi trường tương ứng | Mô tả |
| :--- | :--- | :--- |
| `AiProviders:Meshy:ApiKey` | `AiProviders__Meshy__ApiKey` | API Key Meshy AI |
| `AiProviders:Tripo:ApiKey` | `AiProviders__Tripo__ApiKey` | API Key Tripo AI |
| `JwtSettings:Secret` | `JwtSettings__Secret` | Khóa ký JWT HMAC-SHA256 |
| `PaymentSettings:SePay:ApiKey` | `PaymentSettings__SePay__ApiKey` | API Key cổng thanh toán SePay |
| `MailSettings:Password` | `MailSettings__Password` | Mật khẩu ứng dụng SMTP |
| `BootstrapAdmin:Password` | `BootstrapAdmin__Password` | Mật khẩu tài khoản admin khởi tạo |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Chuỗi kết nối cơ sở dữ liệu |

---

## 3. Cấu hình trong CI/CD (GitHub Actions)

Trong GitHub Actions:
- Các pipeline tự động (Unit test, Integration test với mock provider) không yêu cầu và không phụ thuộc vào secret thật của bên thứ ba.
- Khi cần tích hợp với môi trường live, các giá trị được khai báo trong **Repository Settings** $\rightarrow$ **Secrets and variables** $\rightarrow$ **Actions** (ví dụ `MESHY_API_KEY`, `TRIPO_API_KEY`).
- Tuyệt đối không echo hay in giá trị của biến bí mật ra console/log của GitHub Actions.

---

## 4. Incident Response Checklist: Xử lý sự cố lộ Credential

Khi phát hiện hoặc nghi ngờ API key/secret bị lộ:

- [ ] **Bước 1: Thu hồi (Revoke) khóa cũ ngay lập tức**
  - Đăng nhập vào dashboard của nhà cung cấp dịch vụ (ví dụ: [Meshy Dashboard](https://meshy.ai) hoặc cổng thanh toán tương ứng).
  - Vô hiệu hóa hoặc xóa bỏ hoàn toàn API key đã bị lộ.

- [ ] **Bước 2: Tạo khóa mới (Generate New Key)**
  - Tạo key mới với quyền hạn tối thiểu và đặt định mức quota/ngân sách phù hợp.

- [ ] **Bước 3: Kiểm tra và đối soát nhật ký sử dụng (Usage & Billing Audit)**
  - Kiểm tra lịch sử gọi API trên dashboard nhà cung cấp kể từ thời điểm nghi ngờ bị lộ.
  - Xác nhận xem có lưu lượng bất thường, yêu cầu trái phép hoặc phát sinh chi phí ngoài kế hoạch hay không.

- [ ] **Bước 4: Cập nhật kho lưu trữ bí mật an toàn (Update Secret Stores)**
  - Cập nhật key mới vào .NET User Secrets trên máy các nhà phát triển liên quan.
  - Cập nhật GitHub Secrets hoặc môi trường hosting sản xuất.
  - Không bao giờ dán key mới vào commit Git hoặc pull request.

- [ ] **Bước 5: Ghi nhận sự cố & Giám sát (Incident Post-Mortem)**
  - Lưu lại biên bản sự cố nội bộ (thời gian phát hiện, thời gian thu hồi, thiệt hại nếu có).
  - Kích hoạt cảnh báo số dư / rate-limit trên tài khoản nhà cung cấp.

---

## 5. Lưu ý về lịch sử Git (Historical Commit Reference)

Trong lịch sử commit của repository, commit `a7a5006` từng chứa khóa API thử nghiệm trước khi được loại bỏ ở commit tiếp theo. 

- **Hành động bắt buộc của Repository Owner:** 
  1. Đăng nhập dashboard dịch vụ bên ngoài và thực hiện thu hồi / hủy bỏ (revoke) vĩnh viễn khóa liên quan tới commit `a7a5006`.
  2. Không lưu hành hoặc sử dụng lại khóa cũ đó trên bất kỳ hệ thống nào.
- **Về lịch sử Git:** Nhằm bảo vệ tính toàn vẹn của nhánh và các branch đang phát triển, không tự ý chạy `git reset --hard` hay rewrite history trên repository chia sẻ khi chưa có sự thống nhất từ toàn bộ thành viên.

