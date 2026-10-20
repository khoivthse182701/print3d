# Backlog ưu tiên phát triển BE

Ưu tiên theo rủi ro và dependency; đây là kế hoạch công việc, không phải tính năng đã code trong đợt review. Không ước lượng ngày hoàn thành khi chưa biết số người/năng lực/lịch môn. “Owner” là vai trò được đề xuất để nhóm tự phân công.

| Epic | Ưu tiên / phụ thuộc | Nội dung cụ thể | Done / evidence | Owner |
|---|---|---|---|---|
| E00 Baseline & conventions | Đã chuẩn bị hồ sơ | Review/inventory/research/SRS/DB/diagrams/folder rules | Hồ sơ và validation report; nhóm duyệt giả định A01–A08 | Cả nhóm |
| E01 Integration safety | P0, E00 | R02 provider registry pinning; R03 trusted proxy; R07/R19 geometry coverage/up-axis | Switch-provider job test; ingress spoof test; asymmetric STL test | BE integration |
| E02 Identity/privacy | P0, E00 | verify/reset/session/revoke/lock, consent, decode/pixel limits, private retention | AT01/02/06/23; secrets/redaction audit | BE security |
| E03 Credit & audit | P0, E00 | M1 migration ledger/account/history/audit/outbox; admin reason/key | Concurrency/debit/refund tests + opening balance reconciliation | BE data |
| E04 Model lifecycle | P0, E01/E03 | project/version/assets, immutable file hashes, review checklist/history, customer approval | AT07/08/09; re-review replacement; actual slicer evidence | BE models + vận hành |
| E05 Quote/checkout | P0, E04 | versioned package, shipping rules, persistent quote, expiry/snapshots, v2 order | AT10/11; changed price/material/height 409, atomic checkout | BE orders |
| E06 Payment/refund | P0, E03/E05 | gateway choice + sandbox adapter, inbox/signature, duplicate/late/wrong amount, reserve/refund | AT12–14, sandbox capture/refund; no print without money | BE payments |
| E07 Fulfillment | P0, E04/E06 | lab onboarding/capability, assignment, run/QC, shipment/events | AT15–17; QC failure + reprint; failed delivery evidence | BE fulfillment |
| E08 Support & notify | P0, E03/E06/E07 | defect evidence, resolution, after-sales reprint/refund, in-app outbox | AT18/19; retry/crash tests, notification dedupe | BE support |
| E09 Pilot readiness | P0, tất cả P0 | queue/metrics/alerts/readiness, backup restore, FE contract, live AI + print pilot | NFR gates + one complete sandbox order and physical review | QA/Ops + nhóm |
| E10 Creator enhancements | P1, E04/E09 | text, resize/repair, richer search/reorder, provider benchmarking | AT24/25; benchmark dataset + cost/quality report | BE AI |
| E11 Growth | P1/P2, business approval | credit commerce, scoped partner portal, dashboards, multi-view/B2B/gallery | AT22/26/27/28 + schema extensions and consent | Product + BE |

## PR slices đủ nhỏ để review

1. Registry pin provider + tests, không đổi UI.
2. Proxy/header limiter + deployment config example + tests.
3. Geometry coverage/up-axis metadata + regression tests; không tự nâng verdict healthy.
4. Ledger/account + atomic writes + backfill migration trên scratch DB.
5. Project/version/assets + compatibility read DTO.
6. Review/customer approval + versioned quote.
7. Checkout v2 + immutable item/snapshot.
8. Gateway sandbox + event dedupe, rồi refund reservation ở PR riêng.
9. Assignment → run/QC → shipment → complaint mỗi vertical slice có tests.

## Release gates

- **G0 Demo hiện tại:** build + 20 baseline tests. Chỉ chứng minh luồng offline giả lập.
- **G1 Data safety:** migration from supported baselines, ledger reconciliation, immutable version/ownership, backup restore.
- **G2 Money safety:** signed event, same key/replay, wrong/late capture, concurrent refund/cancel/start; gateway sandbox receipts.
- **G3 Print operations:** Staff checklist + customer approval + lab acceptance + QC + shipment evidence + complaint resolution.
- **G4 Pilot:** FE consumer + load/observability + một mẫu thật qua slicing/in; nhóm chốt chính sách và năng lực xưởng.

G1–G4 là việc phải làm tiếp khi triển khai sản phẩm. Đợt review không tuyên bố đã vượt các gate chỉ nhờ có tài liệu.
