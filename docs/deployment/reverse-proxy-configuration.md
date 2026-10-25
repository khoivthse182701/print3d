# Reverse Proxy & Trusted Networks Deployment Guide

Tài liệu này hướng dẫn thiết lập Reverse Proxy, CDN (Cloudflare, Nginx, AWS ALB, Azure App Gateway) an toàn cho `TheBoys.WebApi` nhằm ngăn ngừa tấn công giả mạo địa chỉ IP (IP Spoofing) và bảo vệ Rate Limiter.

---

## 1. Nguyên tắc Bảo mật Reverse Proxy (Secure-by-Default)

1. **Không tin cậy trực tiếp các header IP do client tự gửi:**
   - Các header như `CF-Connecting-IP`, `X-Real-IP`, `X-Forwarded-For` có thể bị kẻ tấn công trực tiếp chèn vào HTTP request để phân tán IP giả mạo nhằm lách rate limit.
   - Ứng dụng **chỉ đọc địa chỉ IP từ kết nối mạng thực tế (`RemoteIpAddress`)** thông qua middleware chuẩn `ForwardedHeadersMiddleware`.

2. **Chỉ xử lý Forwarded Headers từ Proxy được tin cậy:**
   - Mặc định khi chưa khai báo danh sách proxy tin cậy, ứng dụng sẽ **không tin bất kỳ proxy nào**. Mọi header forwarded từ kết nối trực tiếp đều bị bỏ qua.
   - Chỉ khi kết nối socket đến từ địa chỉ IP thuộc danh sách `KnownProxies` hoặc `KnownNetworks`, middleware mới cập nhật `RemoteIpAddress` thành IP nguyên bản của client.

---

## 2. Cấu trúc Cấu hình `ReverseProxySettings`

Cấu hình trong `appsettings.json` hoặc biến môi trường:

```json
{
  "ReverseProxySettings": {
    "ForwardLimit": 1,
    "KnownProxies": [],
    "KnownNetworks": []
  }
}
```

- **`ForwardLimit`**: Số lượng proxy hops được tin cậy (mặc định là `1` nếu chỉ đứng sau 1 proxy hoặc CDN). Đặt `null` nếu chuỗi proxy dài hơn và đã được kiểm soát.
- **`KnownProxies`**: Mảng các địa chỉ IP cụ thể của reverse proxy (ví dụ: `["127.0.0.1", "10.0.0.10"]`).
- **`KnownNetworks`**: Mảng các dải mạng theo định dạng CIDR (ví dụ: `["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"]`).

---

## 3. Hướng dẫn Triển khai theo Từng Môi trường Thực tế

### 3.1. Triển khai sau Cloudflare CDN

Khi ứng dụng chạy sau Cloudflare, chỉ tin cậy các dải IP công khai chính thức của Cloudflare:

```json
{
  "ReverseProxySettings": {
    "ForwardLimit": 1,
    "KnownNetworks": [
      "173.245.48.0/20",
      "103.21.244.0/22",
      "103.22.200.0/22",
      "103.31.4.0/22",
      "141.101.64.0/18",
      "108.162.192.0/18",
      "190.93.240.0/20",
      "188.114.96.0/20",
      "197.234.240.0/22",
      "198.41.128.0/17",
      "162.158.0.0/15",
      "104.16.0.0/13",
      "104.24.0.0/14",
      "172.64.0.0/13",
      "131.0.72.0/22"
    ]
  }
}
```

*Lưu ý:* Luôn cấu hình tường lửa (WAF/Security Group) ở tầng hạ tầng để chặn kết nối trực tiếp đến backend mà không thông qua Cloudflare.

### 3.2. Triển khai sau Nginx / Docker Private Network

Khi backend chạy trong mạng riêng Docker hoặc Kubernetes đứng sau Nginx Ingress:

```json
{
  "ReverseProxySettings": {
    "ForwardLimit": 1,
    "KnownNetworks": [
      "10.0.0.0/8",
      "172.16.0.0/12",
      "192.168.0.0/16"
    ]
  }
}
```

Cấu hình Nginx tương ứng (`nginx.conf`):

```nginx
location / {
    proxy_pass http://backend_upstream;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

### 3.3. Môi trường Local Development & Testing

Trong môi trường cục bộ và unit/integration testing:
- Để trống `KnownProxies: []` và `KnownNetworks: []`.
- Ứng dụng chạy trực tiếp với client socket IP (loopback), không phụ thuộc vào reverse proxy.
- Các bài test kiểm tra giả mạo IP sẽ xác nhận rằng client không thể tự đưa header giả để đổi bucket rate limiter.

