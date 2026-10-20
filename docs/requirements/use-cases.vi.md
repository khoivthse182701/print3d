# Đặc tả use case

Áp dụng template Ch06 slide 17: name, summary, dependency, actors, preconditions, main sequence, alternative sequences, postcondition. Mọi UC private có tiền điều kiện chung: session hợp lệ, account active, role/owner kiểm tại BE. Thất bại phải không để balance/state dở dang; mọi mutation nhạy cảm có audit. ID bên dưới được dùng trong Mermaid và traceability.

## UC-01 — Quản lý truy cập tài khoản

- **Tóm tắt/actors:** Guest đăng ký/đăng nhập; Customer verify/reset/refresh/logout. Phụ thuộc email sender cho verify/reset.
- **Tiền điều kiện:** public auth endpoint hoạt động, rate limit theo nguồn tin cậy.
- **Luồng chính:** (1) nhập email/password/name; (2) validate và normalize email; (3) tạo user Customer/hash, trial ledger; (4) gửi verify token một lần; (5) xác thực; (6) login cấp access và refresh session hash; (7) logout revoke session.
- **Thay thế:** email trùng không tạo user thứ hai; login/reset trả thông báo không tiết lộ tài khoản; reset token expired/reused bị từ chối; refresh reuse thu hồi family; locked account không được generate; email gửi lỗi có retry, không tạo lại user.
- **Hậu điều kiện:** account/session đúng policy, trial không cấp lặp; mật khẩu/token plaintext không được lưu/log.

## UC-02 — Tạo model từ ảnh

- **Tóm tắt/actors:** Customer tạo mô hình; AI provider là actor hỗ trợ. Phụ thuộc UC-01, UC-04.
- **Tiền điều kiện:** ảnh có quyền sử dụng, account verified theo policy, đủ quota; provider capability và ngân sách có sẵn.
- **Luồng chính:** (1) gửi ảnh/height/texture/consent + UUID; (2) xác thực input và fingerprint; (3) kiểm replay; (4) transaction trừ đúng bucket và ghi ledger, tạo project/version placeholder/job/outbox; (5) trả 202 + job ID; (6) worker claim job, snapshot provider/model; (7) submit/poll từng stage; (8) lưu assets/checksum, dimensions/analysis; (9) đánh dấu succeeded và notify; (10) khách xem version.
- **Thay thế:** ảnh lỗi 400 không trừ lượt; hết credit từ chối; cùng key trả job cũ, khác payload 409; DB conflict retry thao tác với key cũ; generate failed xác nhận hoàn một lần; convert/analyze lỗi giữ file và chuyển hỗ trợ; timeout submit gọi UC-03, không POST lại.
- **Hậu điều kiện:** một job và tối đa một debit; thành công có model version private, chưa được phép in chỉ vì job succeeded.

## UC-03 — Đối soát và tiếp tục job

- **Tóm tắt/actors:** Staff xử lý kết quả provider không chắc chắn. Phụ thuộc UC-02.
- **Tiền điều kiện:** job NeedsAttention; có thông tin request/time/provider/input hash.
- **Luồng chính:** (1) xem stage và attempts; (2) tra provider theo quyền tài khoản; (3) xác nhận task ID khớp operation/input; (4) ghi evidence + actor; (5) bind ID đúng provider/stage; (6) worker GET/download tiếp; (7) báo kết quả cho khách.
- **Thay thế:** không tìm được ID giữ unresolved và escalate; ID khác provider/stage bị từ chối; chỉ provider xác nhận chưa tạo mới cho phép submit mới theo operation mới; không tự refund theo thời gian chờ.
- **Hậu điều kiện:** quyết định có history; không nhân đôi chi phí chỉ do restart/timeout.

## UC-04 — Quản lý lượt tạo

- **Actors/phụ thuộc:** Customer xem balance; System trừ/hoàn; Admin cấp có lý do. Phụ thuộc account.
- **Tiền điều kiện:** CreditAccount tồn tại, operation key chưa dùng hoặc replay.
- **Luồng chính:** (1) khóa/account concurrency; (2) chọn trial trước purchased; (3) insert ledger unique operation; (4) cập nhật balance cùng transaction; (5) trả số dư và receipt.
- **Thay thế:** balance không đủ không ghi debit; duplicate trả receipt; admin thiếu lý do bị từ chối; refund trỏ debit gốc và không vượt debit; unknown provider chưa hoàn.
- **Hậu điều kiện:** ledger tổng khớp balance, mọi điều chỉnh có nguồn.

## UC-05 — Quản lý thư viện và phiên bản

- **Actors/phụ thuộc:** Customer, Staff theo quyền; phụ thuộc UC-02/UC-20.
- **Tiền điều kiện:** có project/version hoặc upload replacement do Staff; asset có owner.
- **Luồng chính:** list/filter project → chọn version → đọc geometry/analysis → tải GLB/STL private → đổi name/archive project khi hợp lệ. Tạo version mới từ mẫu cũ giữ parent, hash, units và provenance.
- **Thay thế:** version đang gắn order không xóa file; user khác 404; URL provider hết hạn dùng file nội bộ hoặc yêu cầu hỗ trợ; version thay đổi không kế thừa approval.
- **Hậu điều kiện:** file published bất biến; mỗi đơn vẫn trỏ đúng mẫu đã đặt.

## UC-06 — Duyệt khả năng in

- **Actors/phụ thuộc:** Staff, lab qua Staff; UC-05.
- **Tiền điều kiện:** version ready, STL cùng kích thước/cấu hình cần in; worker đã lưu phạm vi kiểm tra.
- **Luồng chính:** (1) xem mesh report/units; (2) mở slicer kiểm wall/support/orientation/build volume/material; (3) ghi checklist/evidence; (4) approve với note; (5) lưu review append-only và notify.
- **Thay thế:** missing/unknown thin-wall coverage bắt buộc kiểm thủ công; không kín/không xử lý được thì reject, nêu cách sửa; replacement tạo version mới và chạy lại; thao tác đồng thời sai rowversion 409.
- **Hậu điều kiện:** approval kỹ thuật chỉ áp dụng version/config hash cụ thể; review cũ còn để truy vết.

## UC-07 — Chốt mẫu và nhận báo giá

- **Actors/phụ thuộc:** Customer; UC-05/06.
- **Tiền điều kiện:** own ready version; gói active; cấu hình nằm năng lực xưởng. Có thể preview giá trước kỹ thuật, nhưng checkout phải có approved review.
- **Luồng chính:** (1) chọn mẫu/height/material/finish/engraving/qty/address; (2) xem preview và giới hạn màu/thành phẩm; (3) xác nhận configuration hash; (4) BE lưu CustomerApproval; (5) tính và lưu quote snapshot/expiry/policy; (6) khách xem tổng tiền.
- **Thay thế:** đổi height/material/engraving tạo config/version phù hợp, approval cũ invalid cho quote mới; giá đổi trả quote mới cần xác nhận; không có shipping/service area thì không checkout.
- **Hậu điều kiện:** quote gắn approval, review và mẫu; chưa là giao dịch paid.

## UC-08 — Đặt đơn và thanh toán

- **Actors/phụ thuộc:** Customer, Gateway; UC-07/09.
- **Tiền điều kiện:** quote chưa hết hạn, approved version/config, địa chỉ/số lượng hợp lệ.
- **Luồng chính:** (1) checkout UUID; (2) transaction kiểm owner/quote, tạo order/item immutable snapshots PendingPayment; (3) tạo PaymentAttempt và gọi gateway với merchant reference duy nhất; (4) trả payment URL; (5) khách trả tiền; (6) UC-09 xác nhận; (7) notify và chuyển hàng đợi điều phối.
- **Thay thế:** duplicate checkout trả order; quote stale 409; tạo payment session timeout giữ attempt unknown, query gateway thay tạo charge thứ hai; khách đóng trang vẫn nhận callback; client báo success không đủ căn cứ paid.
- **Hậu điều kiện:** một order/key; Paid chỉ sau receipt được xác thực đúng số tiền.

## UC-09 — Tiếp nhận và đối soát thanh toán

- **Actors/phụ thuộc:** Gateway hoặc Scheduler; UC-08.
- **Tiền điều kiện:** có attempt/reference; adapter gateway xác định chữ ký và timestamp/replay window.
- **Luồng chính:** (1) nhận raw payload; (2) xác thực nguồn/chữ ký, merchant/reference; (3) kiểm amount/currency; (4) insert event key unique; (5) lock order/attempt, cập nhật captured/state/history/outbox trong transaction; (6) trả ack sau commit.
- **Thay thế:** signature sai từ chối; duplicate ack không side effect; event pending đến sau success không hạ state; amount sai giữ reconciliation; success đến sau hủy ghi nhận tiền vào attempt rồi đưa RefundPending/đối soát, không mở production; crash sau commit trước ack replay an toàn.
- **Hậu điều kiện:** tiền được ghi nhận một lần; history/audit đồng bộ.

## UC-10 — Hủy đơn và hoàn tiền

- **Actors/phụ thuộc:** Customer yêu cầu, Staff xét sản xuất, Admin duyệt, Gateway thực hiện; UC-08/09.
- **Tiền điều kiện:** owner hợp lệ; policy hủy/hoàn tại thời điểm mua được snapshot.
- **Luồng chính:** unpaid → hủy trực tiếp; paid/chưa sản xuất → tạo cancel request → khóa assignment → Admin reserve refund trong transaction → gọi gateway → confirm/refund event → lưu receipt/notify.
- **Thay thế:** bắt đầu sản xuất đồng thời phải khóa cùng order, chỉ một transition thắng; đã in thì chuyển UC-14; partial refund giữ phần còn lại; timeout refund giữ reservation, query lại cùng reference; refund fail cho retry đã kiểm chứng, không tạo reservation thứ hai.
- **Hậu điều kiện:** tổng refund không vượt captured; FulfillmentState lịch sử không bị mất; trạng thái tiền tách riêng.

## UC-11 — Phân công xưởng

- **Actors/phụ thuộc:** Staff, lab giao tiếp ngoài hệ thống; UC-08/09.
- **Tiền điều kiện:** đủ tiền, đúng review/customer approval, lab active và phù hợp material/size/capacity.
- **Luồng chính:** chọn lab → gửi package/spec/file version → ghi assignment Offered → nhận xác nhận → Accepted và due date/cost snapshot → ghi status/history.
- **Thay thế:** reject/expired → ghi lý do → chọn lab khác; không được hai assignment active; đổi version cần khách và kỹ thuật duyệt lại, không âm thầm gửi file mới.
- **Hậu điều kiện:** trách nhiệm sản xuất và file được chốt có truy vết; chưa tự bắt đầu in.

## UC-12 — Theo dõi sản xuất và QC

- **Actors/phụ thuộc:** Staff đại diện lab; UC-11.
- **Tiền điều kiện:** assignment accepted, mọi gate BR-08 hợp lệ.
- **Luồng chính:** tạo run → Printing → PostProcessing → QualityCheck → lưu QC evidence → pass → Packaged; lưu actual cost riêng với budget.
- **Thay thế:** fail QC giữ run failed/evidence, tạo run Reprint có số thứ tự mới hoặc escalation refund; lỗi file quay về version/review, không sửa asset đã published; không đánh dấu packaged bỏ QC.
- **Hậu điều kiện:** một run có outcome, chi phí và bằng chứng; reprint không ghi đè run đầu.

## UC-13 — Giao và xác nhận nhận hàng

- **Actors/phụ thuộc:** Staff; Carrier P1; UC-12.
- **Tiền điều kiện:** packaged QC passed, address snapshot đầy đủ.
- **Luồng chính:** tạo shipment có carrier/tracking → Shipped → append tracking events → Delivered với evidence/time → notify.
- **Thay thế:** thất bại → DeliveryFailed → giao lại hoặc ReturnToSender; quá SLA cảnh báo; không sửa địa chỉ sau pickup nếu chưa xác nhận carrier; retry event không nhân timeline.
- **Hậu điều kiện:** có trạng thái/nguồn evidence; không coi nhập tracking là khách đã nhận.

## UC-14 — Giải quyết khiếu nại

- **Actors/phụ thuộc:** Customer, Staff, Admin; UC-13 hoặc sản xuất có lỗi.
- **Tiền điều kiện:** own order, còn thời hạn policy hoặc có override kèm lý do.
- **Luồng chính:** (1) gửi issue/evidence UUID; (2) Staff triage; (3) xác định trách nhiệm/phương án; (4) reprint qua UC-12 hoặc refund qua UC-10; (5) ghi resolution và notify; (6) đóng sau xác nhận kết quả.
- **Thay thế:** thiếu evidence yêu cầu bổ sung; duplicate trả case cũ; tranh chấp escalate Admin; reprint thất bại mở run/case follow-up, không xóa lần trước.
- **Hậu điều kiện:** quyết định, bằng chứng và chi phí theo từng lần; Customer chỉ thấy case của mình.

## Các use case quản trị và mở rộng

| ID / name / summary | Actor; dependency; precondition | Main sequence | Alternatives | Postcondition |
|---|---|---|---|---|
| UC-15 Quản trị user/gói/policy | Admin; UC-01; authenticated Admin | Chọn entity → xem version → nhập lý do/thay đổi → validate → update + audit | Admin cuối/self-demotion bị chặn; stale 409; không sửa snapshot đơn | Policy mới chỉ áp dụng giao dịch mới |
| UC-16 Nhận thông báo | Customer/Staff, Scheduler; UC-02/09/12/13/14; outbox pending | Claim event → tạo notification duy nhất → delivered/read | Retry backoff; poison message dead-letter và cảnh báo | Không mất sự kiện, không gửi trùng do restart |
| UC-17 Theo dõi vận hành và báo cáo | Staff/Admin; các UC trước; dữ liệu theo quyền | Filter queue/age → xem trace → đối soát; Admin xem funnel/cost/export | Dữ liệu thiếu actual phải hiển thị unknown, không dùng budget thay | Báo cáo có định nghĩa, cửa sổ UTC và nguồn |
| UC-18 Quản lý dữ liệu cá nhân | Customer/Staff; UC-05; xác minh chủ thể | Gửi yêu cầu → kiểm active order/hold → purge/anonymize theo policy → audit | Đang xử lý đơn giữ cần thiết và giải thích; backup expiry ghi rõ | Private mặc định, không xóa FK/receipt đang cần |
| UC-19 Tạo model từ text (P1) | Customer/AI; UC-02/04; capability supported | Validate prompt/consent → cùng orchestration UC-02 | Không support/model invalid trả lỗi trước debit | Model/version private có provenance prompt |
| UC-20 Tạo phiên bản resize/repair (P1) | Customer/Staff; UC-05/06; source ready | Chọn operation → báo chi phí → job mới parent version → phân tích lại | Unknown submit UC-03; không dùng approval cũ | Version mới, giữ source và order cũ |
| UC-21 Mua lượt (P1) | Customer/Gateway; UC-09/04; gói active được duyệt kinh doanh | Chọn pack → thanh toán → confirmed → ledger grant một lần | Sai/trùng/late payment đối soát, không grant sớm | Purchased credit có receipt/source |
| UC-22 Cập nhật qua portal xưởng (P1) | PartnerMember; UC-11; membership scoped | Xem assignment của lab → accept/reject → cập nhật run/evidence | Truy cập assignment khác 404; không đủ quyền mutation 403 | Tiến độ/audit đúng lab, không quyền Staff toàn cục |

UC-21/22 cần mở rộng schema theo phase: credit purchase và partner membership chưa nằm trong DDL P0. FR-28 quản lý change request cho các ý tưởng P2; các use case cụ thể của P2 sẽ được bổ sung khi chốt scope.
