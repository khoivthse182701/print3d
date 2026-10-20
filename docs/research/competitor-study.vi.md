# Nghiên cứu Tripo, Meshy và Tencent Hunyuan 3D

Đối chiếu tài liệu công khai chính thức ngày **19/10/2026**. “meishy” được hiểu là **Meshy**. Đây là desk research; chưa đăng nhập gói trả phí, chưa benchmark trên cùng bộ ảnh, không kết luận hãng nào tạo mẫu đẹp/nhanh/rẻ nhất. Năng lực web, API và mã nguồn tự host được phân biệt.

## Quan sát có nguồn

| Nguồn | Điều xác nhận được | Bài học cho THE BOYS |
|---|---|---|
| [Tripo OpenAPI](https://docs.tripo3d.ai/) | Có nhóm tạo ảnh tham chiếu, tạo model từ text/ảnh/nhiều góc, hậu xử lý texture/mesh/rigging | Quản lý job theo capability; không gộp mọi hậu xử lý vào một lần generate |
| [Tripo text-to-model](https://docs.tripo3d.ai/model-generation/text-to-model-v3-0-v3-1.html) | Có API riêng cho tạo model từ text | Prompt/ảnh là input của một job, asset đầu ra có thể dùng lại |
| [Tripo texture](https://docs.tripo3d.ai/texture/texture-model-v3-0-20250812.html) | Texturing là một tác vụ riêng | Cho phép sửa version và tách chi phí mỗi bước |
| [Meshy tổng quan](https://docs.meshy.ai/en) | Tạo từ text/ảnh, workflow hậu xử lý, các định dạng GLB/FBX/OBJ/STL/USDZ/3MF | Chọn GLB để xem, STL để in đơn sắc; màu trên GLB không tự chuyển thành màu vật phẩm |
| [Meshy phân tích in](https://docs.meshy.ai/en/api/analyze-printability) | Có metrics hình học và kiểm tra thin walls; wall_thickness_mm là ngưỡng, không phải số đo nhỏ nhất | Lưu cả verdict, phạm vi kiểm tra, raw report; cần review kỹ thuật tại kích thước cuối |
| [Meshy task lifecycle](https://docs.meshy.ai/en/api/task-lifecycle) | Job bất đồng bộ, tiếp tục GET cùng ID; timeout/429/5xx khi poll không đồng nghĩa thất bại; gợi ý webhook khi nhiều task | Không nhân đôi POST mất phản hồi; checkpoint, đối soát và quan sát hàng đợi |
| [Meshy API pricing](https://docs.meshy.ai/en/api/pricing) | Tính credit theo operation; resize/convert/repair có cách tính riêng, analyze được ghi miễn phí tại ngày kiểm tra | Ledger lượt của khách tách khỏi chi phí/credit provider; không hard-code giá website vào giá bán |
| [Tencent Cloud API overview](https://cloud.tencent.com/document/product/1804/120838) | Có job Pro/Rapid, texture, topology, UV, rigging, conversion; thuộc sản phẩm/cloud khu vực được tài liệu đó mô tả | Adapter capability và region/config riêng; không giả định toàn bộ capability có ở tài khoản Global |
| [Tencent Global quick start](https://intl.cloud.tencent.com/zh/document/product/1281/74125) | Submit trả JobId, Query trả WAIT/RUN/FAIL/DONE | Chuẩn hóa trạng thái bên trong; lưu provider request/job ID |
| [Hunyuan3D-2 repository](https://github.com/Tencent-Hunyuan/Hunyuan3D-2) | Có pipeline sinh shape và texture, code/model tự triển khai, liên kết Studio | Self-host là phương án hạ tầng khác API thương mại; cần GPU, vận hành và rà license theo phiên bản |

Các kết luận thiết kế ở cột cuối là **suy luận của nhóm review**, không phải tính năng đã được các hãng cam kết cho THE BOYS. Tài liệu hiện hành Tripo public có đường API/version khác implementation `/v3` trong repo; tests hiện chỉ mock hợp đồng này. Cần kiểm chứng bằng tài liệu tài khoản và sandbox trước khi đổi adapter; không tự sửa sang một version khác dựa trên trang tổng quan.

## So sánh năng lực và khoảng trống

| Nhóm năng lực | Tripo | Meshy | Tencent | THE BOYS hiện tại | Quyết định |
|---|---|---|---|---|---|
| Image-to-3D | Có [OpenAPI] | Có [Overview] | Có [Global] | Có API + 2 adapters | P0 củng cố độ tin cậy |
| Text-to-3D | Có [text-to-model] | Có [Overview] | Có [Global] | Chỉ enum/field | P1 |
| Multi-view | Có [OpenAPI] | Chưa xác minh trong phạm vi nguồn đã đọc | Có hướng mở rộng ở repository, không suy ra mọi gói API | Chưa có | P2 |
| Texture/hậu xử lý | Có [texture/OpenAPI] | Có [Overview/Pricing] | Có [Cloud overview] | Flag texture, pipeline cố định | P1 version hóa; không thêm rigging cho MVP in |
| Kiểm tra khả năng in | Không xác minh dịch vụ phân tích tương đương | Có [Analyze] | Không xác minh dịch vụ phân tích tương đương | Meshy analyze / Tripo kiểm tra local hạn chế | P0 review slicer |
| Xuất file | Cần chốt format theo adapter/account | Nhiều format [Overview] | Conversion [Cloud overview] | GLB + STL | Giữ MVP, P2 3MF |
| Job asynchronous | Cần đối chiếu contract account | Có [Lifecycle] | Có [Global] | Worker + recover | P0 pin provider, P1 deadline/metrics |
| Đặt sản xuất tại xưởng VN, giao hàng, khiếu nại | Không xác minh từ nguồn trên | Không xác minh từ nguồn trên | Không xác minh từ nguồn trên | Demo đơn offline | Đây là phần THE BOYS phải thiết kế và vận hành |

Không đánh dấu “không có” khi nguồn chưa đủ. Các hãng trên là tham chiếu năng lực sáng tạo; CP3 định vị THE BOYS là bên cung cấp trải nghiệm từ tạo mẫu đến thành phẩm qua đối tác. Vì vậy không nên sao chép toàn bộ studio, animation, video, scene trước khi giải quyết đơn hàng và chất lượng in.

## Phạm vi đề xuất

**P0 trước pilot thu tiền:** ảnh → job đáng tin cậy → phiên bản model → kiểm tra in → khách xác nhận mẫu → báo giá có hiệu lực → thanh toán → giao xưởng → QC → giao hàng → hỗ trợ/hoàn tiền. Kèm ledger, audit, quyền truy cập và khôi phục sự cố.

**P1:** text input, sửa/resize phiên bản không regenerate ảnh, thư viện tìm kiếm/đặt lại, quản lý xưởng và dashboard, mua lượt nếu mô hình kinh doanh được duyệt.

**P2:** nhiều góc nhìn, public gallery có consent, nhóm/B2B, 3MF/màu, portal xưởng riêng. Rigging/animation/video không nằm trong MVP thành phẩm.

## Kế hoạch benchmark trước chọn provider mặc định

Dùng cùng 20 ảnh có quyền sử dụng: người toàn thân, tượng bust, vật thể mảnh, thú cưng, logo/mascot; mỗi loại 4 ảnh. Cùng chiều cao 100 mm, texture off/on tách nhóm. Ghi model/version/date/input hash, tổng credit từng stage, thời gian p50/p95, lỗi, độ khớp mẫu do người chấm, độ kín/độ dày, thời gian sửa/slice, kết quả in thử. Ngân sách và mẫu vật lý phải được nhóm chốt trước khi chạy có phí.

Scorecard đề xuất: tỷ lệ vượt review in 35%, độ giống ảnh 25%, tổng chi phí đến file in 20%, latency 10%, ổn định API/khả năng đối soát 10%. Trọng số là giả định thử nghiệm. Chưa có điểm thực nghiệm; không chọn nhà cung cấp chỉ bằng demo marketing.
