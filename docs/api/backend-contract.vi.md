# Hợp đồng BE–FE

FE và BE riêng repo. Bảng API hiện tại ở [inventory](../review/flow-inventory.vi.md); phân quyền 4 vai trò (Customer, Staff, Manager, Admin) chi tiết tại [đặc tả role Manager](../requirements/manager-role.vi.md); route v2 dưới đây là **đề xuất chưa triển khai**. OpenAPI runtime hiện tại là nguồn contract cho FE đang chạy, không dùng endpoint v2 trước khi PR tương ứng được merge/deploy.

## Quy tắc chung v2

- Auth bearer access token; refresh qua cookie HttpOnly/Secure hoặc cơ chế được threat model duyệt. Không đặt JWT vào URL/file query. Cookie cross-origin cần CORS origins cụ thể, SameSite phù hợp và CSRF cho state-changing refresh/logout.
- Mỗi tạo job/checkout/payment/refund/claim/credit grant gửi `Idempotency-Key: UUID`. Scope user+operation. Fingerprint canonical request; replay trả tài nguyên cũ, mismatch 409. Mutation state gửi `If-Match` rowversion base64, stale 409/412 theo hợp đồng chọn thống nhất (đề xuất 409 cho v2 domain conflicts).
- JSON camelCase, enums string, dates UTC ISO8601. Money decimal VND (không IEEE float để tính ở FE), dimensions mm, volumeCm3 chỉ tham khảo. Chỉ BE tính tổng giá.
- List: `{ items, page, pageSize, totalCount }`, pageSize 1–100; sort allowlist, filter status/provider/date. DTO không trả entity EF, provider IDs/raw payload/PII không cần thiết.
- Error ProblemDetails: `{ type, title, status, code, detail, traceId }`; 400 input, 401 session, 403 role, 404 resource inaccessible, 409 conflict/stale/expired quote, 422 domain policy, 429 throttled, 503 integration unavailable. Không nhét stack trace vào detail.

## Route v2 theo module

| UC | Method / route | Input chính | Success / ngoại lệ |
|---|---|---|---|
| 01 | POST `/api/v2/auth/register`, `/login`, `/verify-email` | email/password/name hoặc one-use token | account/session; invalid/trùng 400/409 |
| 01 | POST `/auth/forgot-password`, `/reset-password`, `/refresh`, `/logout` dưới v2 | email hoặc token; session cookie | 202 generic reset, 200 rotate, 204 logout; reuse 401 |
| 02 | POST `/api/v2/generation-jobs/image` | multipart image,heightMm,texture,consentVersion,key | 202 `{id,status,stage,progress,links}`; 409 key mismatch, 422 quota |
| 02,03 | GET `/api/v2/generation-jobs/{id}` | owner/Staff | status, safe errorCode, resultVersionId; 404 |
| 03 | POST `/api/v2/staff/generation-jobs/{id}/reconcile` | taskId,stage,evidence,note,expectedVersion | 200 checkpoint; 409 mismatched provider/state |
| 04 | GET `/api/v2/credits`, `/api/v2/credits/ledger` | current user + page | balances và ledger redacted |
| 04,15 | POST `/api/v2/admin/users/{id}/credit-adjustments` | delta,bucket,reason,key | 201 receipt; 422 insufficient balance |
| 05 | GET `/api/v2/model-projects`, `/{id}/versions` | filters/page | own projects/versions |
| 05 | GET `/api/v2/model-versions/{id}`, `/assets/{assetId}` | auth owner | metadata hoặc private streamed file |
| 06 | POST `/api/v2/staff/model-versions/{id}/reviews` | decision,configHash,checklist,evidence,note | 201 review; 422 technical gate |
| 07 | POST `/api/v2/model-versions/{id}/approvals` | configuration,policyVersion,key | 201 approval/configHash |
| 07 | POST `/api/v2/quotes` | approvalId,reviewId,packageId,qty,address | 201 quote/expiry/totals; 422 size/coverage |
| 08 | POST `/api/v2/orders` | quoteId,key | 201 order or replay 200; 409 expired/stale |
| 08 | POST `/api/v2/orders/{id}/payment-attempts` | gateway,key | 201 attempt/actionUrl; unknown returns pending reconciliation |
| 09 | POST `/api/v2/webhooks/payments/{gateway}` | signed raw body and provider headers | 2xx only after inbox commit; invalid signature reject per gateway adapter |
| 10 | POST `/api/v2/orders/{id}/cancellation-requests` | reason,key | 201 request/cancel decision; 409 production started |
| 10 | POST `/api/v2/admin/payment-attempts/{id}/refunds` | amount,reason,key | 202 reserved/submitted; 422 amount exceeds available |
| 11 | GET/POST `/api/v2/staff/partner-labs` | paging or capability/contact | 200/201 lab; Admin policy for onboarding |
| 11 | POST `/api/v2/staff/orders/{id}/assignments` | labId,dueAt,cost,key | 201 offered; 409 active assignment |
| 11 | POST `/api/v2/staff/assignments/{id}/decisions` | accepted/rejected,note,evidence | 200; 409 stale |
| 12 | POST `/api/v2/staff/assignments/{id}/production-runs` | quantity,key | 201 run; 422 payment/review/approval gate |
| 12 | POST `/api/v2/staff/production-runs/{id}/events` | eventType,note,evidence,actualCost | 201 history; 409 invalid transition |
| 13 | POST `/api/v2/staff/orders/{id}/shipments` | carrier,tracking,key | 201 shipment |
| 13 | POST `/api/v2/staff/shipments/{id}/events` | status,evidence,occurredAt,eventKey | 201/replay; 422 no proof of delivery |
| 14 | POST `/api/v2/orders/{id}/defect-reports` | reason,evidenceIds,key | 201 case |
| 14 | POST `/api/v2/staff/defect-reports/{id}/decisions` | resolution,note,linked refund/run | 200; 403 unauthorized refund approval |
| 15 | PATCH `/api/v2/admin/users/{id}/role`, `/status` | target role/status,reason,version | 200; 409 last admin/stale |
| 15 | POST `/api/v2/admin/pricing-packages/{code}/versions` | material/finish/price/range,reason | 201 immutable version |
| 16 | GET `/api/v2/notifications`; POST `/{id}/read` | paging / owned notification | 200 / 204 |
| 17 | GET `/api/v2/staff/queues`, `/api/v2/admin/reports` | allowlisted filters/date range | 200 redacted queue/report |
| 18 | POST `/api/v2/privacy-requests` | type,reason,key | 202 tracked request; hold decision explained |

Routes rút gọn `/auth/...` trong bảng đều có prefix `/api/v2`. Một endpoint nhận upload evidence phải kiểm file bytes/size/owner trước cấp evidenceId; lưu private và chỉ liên kết vào case/review hợp lệ. Đây là hạng mục Support/Review, chưa tồn tại trong source.

## Payload checkout đề xuất

```json
{ "quoteId": "5b68b568-8cef-4ff7-a751-122f70f290f4" }
```

Request không nhận totalAmount/trạng thái paid từ client. Response gồm orderId/orderCode, fulfillmentStatus, paymentStatus, immutable items/totals, paymentActions và rowVersion. Quote response gồm material/finish/height/width/depth/engraving/quantity/shipping/total/expiry cùng approvalId và reviewId để FE hiển thị rõ mẫu sẽ in.

## FE integration acceptance

FE upload một lần, poll theo Retry-After hoặc 5 giây mặc định, hủy poll khi rời màn hình nhưng không coi job bị hủy. Render progress như tiến độ tương đối; không hứa ETA. GET file bằng auth fetch → Blob URL cho viewer; revoke Blob URL khi unmount. STL không chứa texture nên preview màu phải có chú thích phạm vi thành phẩm.

FE tách trạng thái tạo AI, duyệt kỹ thuật, khách chốt mẫu, thanh toán, sản xuất, giao hàng. Khi 409 quote/version đổi phải hiển thị thay đổi và xin khách xác nhận lại; không retry ngầm với giá mới. Sau redirect thanh toán, FE đọc order từ BE và hiển thị “đang xác nhận” cho đến event verified.

Chưa có repo FE được cung cấp để chạy consumer tests. Không đánh dấu viewer/checkout UI hoàn thành chỉ vì BE có endpoint. P1 text/repair/credit purchase/partner portal cần OpenAPI riêng theo FR-24–27 khi triển khai.
