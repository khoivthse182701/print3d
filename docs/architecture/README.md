# Thiết kế SWD392 và bộ sơ đồ Mermaid

**Target design v1.0**, không mô tả toàn bộ code hiện tại. FE là hệ thống client riêng; module dưới đây thuộc BE. Đối chiếu slide tại `D:/FPT/SWD392/1_SWD392/Slide`; số slide là thứ tự 1-based trong PPTX, không phải số trang PDF.

## 5 loại sơ đồ chính được chọn

| Loại | Căn cứ slide đã đọc | Mục đích trong dự án | File |
|---|---|---|---|
| Use Case | Ch06 slide 7, 10, 13–18; Ch02 slide 3 | Chốt actor, ranh giới, mục tiêu, bắt buộc/tùy chọn | D01 |
| Activity | Ch06 slide 19–20 | Hành trình xuyên vai trò, nhánh từ chối/lỗi/QC | D02 |
| Class | Ch07 slide 3, 6, 9; Ch02 slide 5–6 | Entity, trách nhiệm, quan hệ và multiplicity | D03 |
| Sequence | Ch09-11 slide 6–8, 24–26 | Giao tiếp boundary/control/entity và transaction với AI/payment | D04, D05 |
| State Machine | Ch09-11 slide 17 và 22–26; Ch02 slide 9 | Lifecycle với event/guard, chống chuyển bước sai | D06, D07, D09 |

**Bổ sung:** D08 ERD cho schema DB; D10 Communication cho hướng COMET. Ch09-11 slide 8 nêu COMET ưu tiên communication để tích hợp kiến trúc, nhưng sequence hữu ích cho tương tác phức tạp. Vì job AI và payment có retry/transaction, giữ sequence chi tiết, thêm communication tổng hợp theo Ch13 slide 4 và 7–9. Nếu rubric yêu cầu communication cho từng UC, nhóm mở rộng từ D10 và đặc tả UC; chưa coi một D10 thay thế tất cả chúng.

D01 và D02 dùng Mermaid flowchart để biểu diễn ngữ nghĩa use case/activity tương thích renderer GitHub; actor ngoài boundary, oval use case, include/extend ghi nhãn. Đây là biểu diễn gần UML, không khẳng định mọi ký hiệu UML chuẩn được renderer hỗ trợ. D03/D04/D05/D06/D07/D09 dùng classDiagram/sequenceDiagram/stateDiagram-v2. Có thể chuyển ký hiệu sang công cụ UML nếu rubric đòi hình người/swimlane chuẩn.

## Các nguồn diagram

| ID | Source | Nội dung |
|---|---|---|
| D01 | [use cases](diagrams/D01-use-cases.mmd) | Customer, Staff, Admin, AI và Gateway; xưởng qua Staff |
| D02 | [activity](diagrams/D02-order-activity.mmd) | Từ tạo/chốt mẫu tới giao/claim |
| D03 | [class](diagrams/D03-domain-class.mmd) | Domain cốt lõi, không thay ERD toàn DB |
| D04 | [generation sequence](diagrams/D04-generation-sequence.mmd) | Debit/checkpoint/submit/poll/recover |
| D05 | [checkout sequence](diagrams/D05-payment-sequence.mmd) | Quote, order, payment event, duplicate/late |
| D06 | [order state](diagrams/D06-order-state.mmd) | Fulfillment, QC và giao thất bại |
| D07 | [generation state](diagrams/D07-generation-state.mmd) | Job và nhánh unknown/failed |
| D08 | [ERD](diagrams/D08-core-erd.mmd) | Quan hệ cốt lõi; toàn bộ FK ở DDL |
| D09 | [payment state](diagrams/D09-payment-state.mmd) | Payment/refund độc lập fulfillment |
| D10 | [communication](diagrams/D10-communication.mmd) | Message đánh số, module/backend boundary |

Xem bản nhúng GitHub tại [diagram gallery](diagrams/README.md). `.mmd` là nguồn chính; chạy `node tools/diagrams/render.mjs` để kiểm cú pháp, render SVG và cập nhật gallery. SVG chỉ tạo ở `artifacts/diagrams/` (ignored), source Mermaid được review trên GitHub.

## Quyết định kiến trúc

1. **Modular monolith** trong 4 project hiện có, chia application theo Identity, Generations, Models, Credits, Orders, Payments, Fulfillment, Support, Notifications, Admin/Reporting. Chưa cần microservices cho nhóm môn học.
2. **Dependency direction:** WebApi → Application; Infrastructure → Application + Domain; Application → Domain; Domain không phụ thuộc EF/HTTP. Composition root WebApi tham chiếu Infrastructure để DI.
3. **External adapters:** AI registry resolve theo job.Provider + model version, payment và shipping có interface riêng. Lỗi provider được map thành domain errors; không đưa DTO provider xuyên vào Domain.
4. **Transaction boundary:** job+debit+ledger+outbox; checkout+snapshot+history; payment inbox+state+outbox; refund reserve+audit. HTTP external luôn ngoài SQL transaction dài. Worker có lease/checkpoint và dedupe.
5. **State guards:** đặt transition trong domain policy/service, không cho client PATCH state tùy ý. HTTP action là approve/start/ship/cancel; server suy ra state mới.
6. **Storage:** DB giữ metadata/hash, file private ngoài DB. Version published không overwrite. Worker nhiều instance cần storage chung.

## Bảng chuyển trạng thái đích

| Object / from → to | Event | Guard | Side effect nguyên tử |
|---|---|---|---|
| Job Queued → Processing | worker claim | provider pinned, debit recorded | stage/checkpoint |
| Job Processing → NeedsAttention | ambiguous submit / invalid output | không rõ kết quả | audit, alert; không resubmit |
| Job Processing → Failed | confirmed provider failure | kết quả xác định | refund ledger nếu generate chưa tạo model |
| Job NeedsAttention → Processing | reconcile | evidence/provider ID đúng | resume stage cũ |
| Order PendingPayment → AwaitingAssignment | payment confirmed | amount/currency/reference đúng; approvals valid | receipt/history/outbox |
| Order PendingPayment → Cancelled | cancel/expiry | chưa capture hoặc khóa race | close checkout, late payment xử lý riêng |
| Order AwaitingAssignment → Assigned | lab accepted | active lab, một assignment active | assignment/history |
| Order Assigned → Printing | start run | paid, reviewed, customer approved, accepted | run/history |
| Printing → PostProcessing → QualityCheck | tiến độ thực tế | valid run + actor | run/history |
| QualityCheck → Packaged | QC pass | evidence/note | completed run/history |
| QualityCheck → Printing | reprint approved | failed run đã lưu, tạo run mới | run number tăng, budget/actual riêng |
| Packaged → Shipping | dispatch | tracking hoặc hand-delivery evidence | shipment/event/history |
| Shipping → Delivered | delivery confirmed | receipt/evidence | timestamp/event/notify |
| Shipping → DeliveryFailed → Shipping/Returned | carrier event | source hợp lệ, dedupe | history, không giả Delivered |
| AwaitingAssignment/Assigned → Cancelled | cancel accepted | chưa start run, lock order | refund reservation nếu đã paid |
| Payment Paid → RefundPending → Refunded/PartiallyRefunded | refund approved/confirmed | reserved+succeeded ≤ captured | receipt/history/outbox |

Cancelled/Delivered/Returned kết thúc một fulfillment cycle. Reprint sau giao là ProductionRun + Shipment mới liên kết complaint, không quay lùi trạng thái lịch sử đơn. Payment và complaint tiếp tục độc lập sau Delivered.

Tài liệu ký pháp tham khảo: [Mermaid class](https://mermaid.js.org/syntax/classDiagram.html), [state](https://mermaid.js.org/syntax/stateDiagram.html), [usage](https://mermaid.js.org/config/usage.html). Nguồn môn học được tóm lược ở bảng trên; không đưa các PPTX của giảng viên vào GitHub dự án.
