# Ma trận truy vết yêu cầu → thiết kế → kiểm thử

Trạng thái: **E** existing một phần/đầy đủ có code, **N** new target. Tests có tên là tests thực tế; mã AT là **kịch bản nghiệm thu cần viết**, không phải tests đã pass. Diagram D01–D10 xem architecture index. Mỗi PR triển khai ghi FR, UC, BR và AT bị ảnh hưởng.

| FR | UC | Hiện trạng / module đích | DB đích | Diagram | Nghiệm thu |
|---|---|---|---|---|---|
| 01 | 01 | E AuthController | Users | D01 | AT01 đăng ký trùng, role injection, login invalid |
| 02 | 01 | N Identity | RefreshSessions | D01 | AT02 expiry/reset one-use/reuse revoke |
| 03 | 02 | E Generations | GenerationJobs, ModelProjects | D02,D04 | AT03 invalid image/idempotency; `ApiFlowTests` |
| 04 | 02,03 | E Worker, cần registry | GenerationStages | D04,D07 | AT04 switch provider/resume/unknown; `ApiFlowTests` |
| 05 | 04 | E quota, N ledger | CreditAccounts,CreditLedger | D03,D04 | AT05 concurrent last credit/refund once; `ApiFlowTests` |
| 06 | 05 | E Generations/download | ModelAssets | D01,D03 | AT06 IDOR/private file; `ApiFlowTests` |
| 07 | 05 | N Models | ModelVersions,ModelAssets | D03,D08 | AT07 immutable version/order pin |
| 08 | 06 | E review, N history | PrintReviews | D02,D03 | AT08 reject/coverage/checklist/append-only |
| 09 | 07 | N Approvals | CustomerApprovals | D02,D05 | AT09 changed config invalidates approval |
| 10 | 07 | E quote, N persistent quote | Quotes, PricingPackages | D03,D05 | AT10 stale/expired/material change; `PrintingTests` |
| 11 | 08 | E CreateOrder, N checkout | Orders,OrderItems | D05,D06 | AT11 replay/stale quote/atomic snapshots |
| 12 | 08,09 | N Payments | PaymentAttempts | D05,D06 | AT12 confirmed payment gate/redirect untrusted |
| 13 | 09 | N PaymentEvents | PaymentEvents | D05 | AT13 duplicate/wrong/late/out-of-order |
| 14 | 10 | E cancel draft, N refund | Refunds,OrderCancellationRequests,OrderStatusHistory | D06,D09 | AT14 concurrent cancel/start/refund unknown |
| 15 | 11 | N Fulfillment | PartnerLabs,PartnerAssignments | D02,D03 | AT15 reject/reassign/single active |
| 16 | 12 | E Printing, N runs/QC | ProductionRuns | D02,D06 | AT16 gate/QC fail/reprint history |
| 17 | 13 | E basic shipping, N events | Shipments,ShipmentEvents | D02,D06 | AT17 no tracking/failed delivery/retry |
| 18 | 14 | N Support | DefectReports,DefectEvidence | D02,D09 | AT18 ownership/evidence/refund vs reprint |
| 19 | 16 | N Notifications | OutboxMessages,Notifications | D04,D05 | AT19 crash-after-commit/dedupe/dead-letter |
| 20 | 15 | E Admin, N audit/policy | Users,PricingPackages,AuditLogs | D01,D03 | AT20 last admin/role cache/price snapshots |
| 21 | 03,17 | E list/recover, N queue | GenerationJobs,AuditLogs | D07,D10 | AT21 filters/paging/error redaction |
| 22 | 17 | N Reporting P1 | Orders,PaymentAttempts,ProductionRuns | D10 | AT22 paid vs delivered/budget vs actual |
| 23 | 18 | N Privacy | ConsentRecords,PrivacyRequests,ModelAssets | D01,D08 | AT23 delete/hold/private by default |
| 24 | 19 | N text P1 | GenerationJobs | D04 | AT24 unsupported capability/no debit |
| 25 | 20 | N repair/resize P1 | ModelVersions,GenerationJobs | D03,D07 | AT25 parent linkage/re-analysis/no old approval |
| 26 | 21 | N credit commerce P1 | Purchase extension + ledger | D05 | AT26 paid grants once/chargeback policy |
| 27 | 22 | N partner portal P1 | Membership extension + assignments | D01 | AT27 cross-lab denied |
| 28 | Change request | N P2 | Chưa chốt | — | AT28 scope/design approved before development |

## Các bộ kiểm thử hiện tại

- `tests/TheBoys.Tests/Unit/PrintingTests.cs`: giá, credit, units, phân tích/chuẩn hóa STL.
- `tests/TheBoys.Tests/Contracts/MeshyContractTests.cs` và `TripoContractTests.cs`: HTTP payload và response giả lập; không chứng minh server thật chấp nhận.
- `tests/TheBoys.Tests/Integration/ApiFlowTests.cs`: SQL thật + fake provider, ownership/roles, quota concurrent, recover.
- `tests/TheBoys.Tests/Integration/MigrationUpgradeTests.cs`: các baseline lịch sử nâng cấp và rollback trên DB test.

## Ma trận NFR và gate

NFR-01 → AT01/02/06/20/23/27 + ingress test; NFR-02 → AT05/11/13/14/19; NFR-03 → load test; NFR-04/06 → chaos provider và queue alert; NFR-05 → restore drill; NFR-07/10 → CI + migration scratch; NFR-08 → OpenAPI diff/FE consumer; NFR-09 → retention/PII test.

Gate pilot cần toàn bộ FR P0/BR pass, không chỉ unit tests. Trạng thái thực thi các gate chưa đạt được ghi trong [validation](../review/validation.vi.md).
