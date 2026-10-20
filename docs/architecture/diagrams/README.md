# Mermaid gallery — thiết kế mục tiêu

Nguồn chính là các file `.mmd`. File này được sinh bằng `node tools/diagrams/render.mjs`. Xem [giải thích và nguồn slide](../README.md).

## D01-use-cases

[Source](D01-use-cases.mmd)

```mermaid
flowchart LR
    Customer["Actor: Customer"]
    Staff["Actor: Staff"]
    Admin["Actor: Admin"]
    AI["External: AI Provider"]
    Gateway["External: Payment Gateway"]
    subgraph BE["THE BOYS Backend — target P0"]
        Auth(["UC-01 Quản lý tài khoản"])
        Gen(["UC-02 Tạo model từ ảnh"])
        Recover(["UC-03 Đối soát job"])
        Credits(["UC-04 Quản lý lượt"])
        Library(["UC-05 Quản lý mẫu và version"])
        Review(["UC-06 Duyệt kỹ thuật"])
        Approve(["UC-07 Chốt mẫu và báo giá"])
        Checkout(["UC-08 Đặt đơn và thanh toán"])
        Reconcile(["UC-09 Xác nhận giao dịch"])
        Cancel(["UC-10 Hủy và hoàn tiền"])
        Assign(["UC-11 Phân công xưởng"])
        QC(["UC-12 Sản xuất và QC"])
        Ship(["UC-13 Giao hàng"])
        Claim(["UC-14 Khiếu nại"])
        Manage(["UC-15 Quản trị"])
        Notify(["UC-16 Thông báo"])
        Ops(["UC-17 Theo dõi vận hành"])
        Privacy(["UC-18 Dữ liệu cá nhân"])
    end
    Customer --- Auth
    Customer --- Gen
    Customer --- Library
    Customer --- Approve
    Customer --- Checkout
    Customer --- Cancel
    Customer --- Claim
    Customer --- Notify
    Customer --- Privacy
    Staff --- Recover
    Staff --- Review
    Staff --- Assign
    Staff --- QC
    Staff --- Ship
    Staff --- Claim
    Staff --- Ops
    Admin --- Manage
    Admin --- Cancel
    Admin --- Credits
    AI --- Gen
    AI --- Recover
    Gateway --- Checkout
    Gateway --- Reconcile
    Gateway --- Cancel
    Gen -. "include: quota" .-> Credits
    Recover -. "extend: kết quả mơ hồ" .-> Gen
    Checkout -. "include: xác nhận tiền" .-> Reconcile
```

## D02-order-activity

[Source](D02-order-activity.mmd)

```mermaid
flowchart TD
    Start((Start)) --> Image["Customer: upload ảnh và chọn chiều cao"]
    Image --> Valid{"BE: input, consent, credit hợp lệ?"}
    Valid -->|Không| Error["Trả lỗi; không trừ lượt"]
    Error --> Stop((End))
    Valid -->|Có| Generate["BE: debit + job; worker gọi provider"]
    Generate --> Result{"Kết quả?"}
    Result -->|Mơ hồ| Reconcile["Staff: đối soát; không POST lại"]
    Reconcile --> Result
    Result -->|Generate thất bại xác nhận| Refund["BE: hoàn lượt một lần"]
    Refund --> Stop
    Result -->|Thành công| Version["BE: version + file + analysis coverage"]
    Version --> Review["Staff: kiểm slicer và cấu hình in"]
    Review --> Printable{"Duyệt kỹ thuật?"}
    Printable -->|Không| Revise["Customer/Staff: sửa hoặc chọn version mới"]
    Revise --> Version
    Printable -->|Có| Confirm["Customer: chốt version, vật liệu, engraving"]
    Confirm --> Quote["BE: quote tổng giá có expiry"]
    Quote --> Checkout["Customer: checkout + thanh toán"]
    Checkout --> Paid{"Gateway: tiền đã xác thực?"}
    Paid -->|Chờ| Wait["PendingPayment; poll/reconcile"]
    Wait --> Paid
    Paid -->|Hủy hoặc hết hạn| Cancel["Cancelled; tiền đến muộn vào đối soát"]
    Cancel --> Stop
    Paid -->|Có| Assign["Staff: giao việc cho xưởng"]
    Assign --> Accepted{"Xưởng nhận?"}
    Accepted -->|Không| Assign
    Accepted -->|Có| Print["Xưởng qua Staff: in và hậu kỳ"]
    Print --> QC{"Staff: QC pass?"}
    QC -->|Không| Resolution{"In lại hay hoàn tiền?"}
    Resolution -->|In lại| NewRun["Lưu run lỗi; tạo run mới"]
    NewRun --> Print
    Resolution -->|Hoàn tiền| MoneyBack["Admin: reserve refund; gateway xác nhận"]
    MoneyBack --> Stop
    QC -->|Có| Pack["Đóng gói và gửi hàng"]
    Pack --> Delivery{"Giao thành công?"}
    Delivery -->|Không| Retry["Giao lại hoặc hoàn hàng"]
    Retry --> Delivery
    Delivery -->|Có| Delivered["Delivered + bằng chứng"]
    Delivered --> Complaint{"Khách khiếu nại?"}
    Complaint -->|Không| Stop
    Complaint -->|Có| Support["Staff: case + evidence; reprint hoặc refund"]
    Support --> Stop
```

## D03-domain-class

[Source](D03-domain-class.mmd)

```mermaid
classDiagram
    class User {
        +Guid id
        +Role role
        +AccountStatus status
    }
    class CreditAccount {
        -int trialBalance
        -int purchasedBalance
        +Debit(operationKey)
        +Refund(debitId)
    }
    class CreditEntry {
        +Guid id
        +int delta
        +string bucket
        +string operationKey
    }
    class ModelProject {
        +Guid id
        +string name
    }
    class GenerationJob {
        +Guid id
        +string provider
        +string model
        +JobStatus status
        +Resume(evidence)
    }
    class ModelVersion {
        +Guid id
        +int versionNo
        +string geometryHash
        +decimal heightMm
    }
    class ModelAsset {
        +string storageKey
        +string sha256
        +string format
        +string unit
    }
    class PrintReview {
        +string configurationHash
        +ReviewDecision decision
    }
    class CustomerApproval {
        +string configurationHash
        +DateTime approvedAt
    }
    class Quote {
        +decimal totalVnd
        +DateTime expiresAt
        +ValidateForCheckout()
    }
    class Order {
        +Guid id
        +FulfillmentStatus fulfillmentStatus
        +PaymentStatus paymentStatus
        +StartProduction()
        +RequestCancellation()
    }
    class OrderItem {
        +int quantity
        +decimal unitPriceVnd
        +string assetHash
    }
    class PaymentAttempt {
        +string merchantReference
        +decimal amountVnd
        +Confirm(verifiedEvent)
    }
    class Refund {
        +decimal amountVnd
        +Reserve()
    }
    class PartnerAssignment {
        +Guid labId
        +Accept()
        +Reject(reason)
    }
    class ProductionRun {
        +int runNo
        +RecordQc(evidence)
    }
    User "1" -- "1" CreditAccount : owns
    CreditAccount "1" -- "0..*" CreditEntry : ledger
    User "1" -- "0..*" ModelProject : owns
    ModelProject "1" -- "0..*" GenerationJob : processes
    ModelProject "1" -- "0..*" ModelVersion : versions
    GenerationJob "0..1" -- "0..*" ModelVersion : produces
    ModelVersion "1" *-- "1..*" ModelAsset : published files
    ModelVersion "1" -- "0..*" PrintReview : technical review
    ModelVersion "1" -- "0..*" CustomerApproval : confirmation
    Quote "0..*" --> "1" CustomerApproval : binds
    Quote "0..*" --> "1" PrintReview : requires approved
    User "1" -- "0..*" Order : places
    Order "0..1" --> "1" Quote : consumes
    Order "1" *-- "1..*" OrderItem : contains
    OrderItem "0..*" --> "1" ModelVersion : immutable reference
    Order "1" -- "0..*" PaymentAttempt : attempts
    PaymentAttempt "1" -- "0..*" Refund : refunds
    Order "1" -- "0..*" PartnerAssignment : assignment history
    PartnerAssignment "1" -- "0..*" ProductionRun : print and reprint
```

## D04-generation-sequence

[Source](D04-generation-sequence.mmd)

```mermaid
sequenceDiagram
    actor C as Customer
    participant API as Generation API - boundary
    participant S as Generation Service - control
    participant DB as SQL - entities
    participant W as Worker
    participant P as Pinned AI Adapter
    participant F as Private Storage
    C->>API: image + configuration + Idempotency-Key
    API->>S: validated input and owner
    S->>DB: lookup owner/key
    alt replay same fingerprint
        DB-->>API: existing job
        API-->>C: 200 existing job
    else new request
        S->>F: save validated input
        S->>DB: BEGIN debit + ledger + job + outbox
        DB-->>S: COMMIT
        API-->>C: 202 jobId
        W->>DB: claim stage, mark Submitting, COMMIT
        W->>P: submit using job provider/model
        alt task ID received
            P-->>W: providerTaskId
            W->>DB: persist ID before polling
            loop same ID until terminal
                W->>P: GET status
                P-->>W: status or retryable GET error
                W->>DB: progress and next poll
            end
            alt success
                W->>F: download, validate, checksum immutable files
                W->>DB: stage result, advance or publish version + outbox
            else generate failed confirmed
                W->>DB: Failed + refund ledger once + outbox
            end
        else POST outcome unknown
            W->>DB: NeedsAttention + evidence
            Note over W,P: No automatic POST retry, UC-03 reconciles
        end
    end
    C->>API: GET job
    API->>DB: owner-filtered read
    API-->>C: status, stage, safe result URLs
```

## D05-payment-sequence

[Source](D05-payment-sequence.mmd)

```mermaid
sequenceDiagram
    actor C as Customer
    participant API as Checkout API - boundary
    participant O as Order Service - control
    participant DB as SQL - entities and inbox
    participant G as Payment Gateway
    participant N as Outbox Worker
    C->>API: quoteId + Idempotency-Key
    API->>O: authenticated checkout
    O->>DB: BEGIN check quote, owner, expiry and approvals
    alt stale quote or key mismatch
        DB-->>O: reject without new order
        API-->>C: 409 confirm new quote
    else valid or replay
        O->>DB: new order/items/history or existing order
        DB-->>O: COMMIT
        O->>DB: persist or reuse payment attempt by operation key
        O->>G: create checkout using merchant reference
        G-->>API: payment URL or uncertain response
        API-->>C: order and payment action
    end
    C->>G: pay
    G->>API: signed event
    API->>O: verified reference, amount, currency
    O->>DB: BEGIN inbox dedupe + lock order
    alt duplicate event
        DB-->>O: no further mutation
    else wrong amount or late success on cancelled order
        O->>DB: record receipt and NeedsAttention, no production
    else matching success
        O->>DB: capture + Paid + AwaitingAssignment + history + outbox
    end
    DB-->>O: COMMIT
    API-->>G: acknowledgement
    N->>DB: claim committed notification
    N-->>C: payment result
    Note over C,API: Browser redirect alone never marks Paid
```

## D06-order-state

[Source](D06-order-state.mmd)

```mermaid
stateDiagram-v2
    [*] --> PendingPayment: checkout(valid quote)
    PendingPayment --> AwaitingAssignment: paymentConfirmed [matching amount and approvals]
    PendingPayment --> Cancelled: cancel or expiry [no capture]
    AwaitingAssignment --> Assigned: labAccepted [one active assignment]
    Assigned --> AwaitingAssignment: assignmentWithdrawn [no run started]
    AwaitingAssignment --> Cancelled: cancelAccepted [refund reserved if paid]
    Assigned --> Cancelled: cancelAccepted [no run started]
    Assigned --> Printing: startRun [paid and approvals and accepted assignment]
    Printing --> PostProcessing: printComplete
    PostProcessing --> QualityCheck: finishingComplete
    QualityCheck --> Printing: qcFailed [new reprint run approved]
    QualityCheck --> Cancelled: terminateProduction [failure evidence and refund decision]
    QualityCheck --> Packaged: qcPassed [evidence recorded]
    Packaged --> Shipping: dispatched [tracking and carrier]
    Shipping --> Delivered: deliveryConfirmed [evidence]
    Shipping --> DeliveryFailed: deliveryAttemptFailed
    DeliveryFailed --> Shipping: retryDispatch
    DeliveryFailed --> Returned: returnedToSender
    Delivered --> [*]
    Returned --> [*]
    Cancelled --> [*]
    note right of Delivered
        Complaint, refund and after-sales reprint
        have separate records and lifecycle.
    end note
```

## D07-generation-state

[Source](D07-generation-state.mmd)

```mermaid
stateDiagram-v2
    [*] --> Queued: accept [credit debited once]
    Queued --> Processing: claim [provider pinned]
    Queued --> Cancelled: cancel [not submitted] / refundOnce
    Processing --> Succeeded: finalStageSucceeded [assets validated]
    Processing --> Failed: providerFailureConfirmed
    Processing --> NeedsAttention: ambiguousSubmit or invalidOutput
    NeedsAttention --> Processing: reconcile [matching task ID and evidence]
    NeedsAttention --> Failed: providerConfirmsFailure
    Succeeded --> [*]
    Failed --> [*]
    Cancelled --> [*]
    note right of Processing
        Completed stages advance the checkpoint.
        GET retries keep the same task ID.
        Both remain in Processing until terminal.
    end note
    note right of Failed
        Refund app credit only when generation failed
        before a model was produced. Use ledger once.
    end note
    note right of NeedsAttention
        Alert after prolonged wait.
        Elapsed time alone does not prove failure.
    end note
```

## D08-core-erd

[Source](D08-core-erd.mmd)

```mermaid
erDiagram
    Users ||--|| CreditAccounts : has
    Users ||--o{ RefreshSessions : authenticates
    Users ||--o{ IdentityTokens : verifies
    Users ||--o{ ConsentRecords : grants
    CreditAccounts ||--o{ CreditLedger : records
    Users ||--o{ ModelProjects : owns
    ModelProjects ||--o{ GenerationJobs : runs
    GenerationJobs ||--o{ GenerationStages : checkpoints
    GenerationJobs |o--o{ ModelVersions : produces
    ModelProjects ||--o{ ModelVersions : versions
    ModelVersions ||--o{ ModelAssets : files
    ModelVersions ||--o{ PrintReviews : reviewed
    ModelVersions ||--o{ CustomerApprovals : approved
    CustomerApprovals ||--o{ Quotes : confirms
    PrintReviews ||--o{ Quotes : qualifies
    PricingPackages ||--o{ Quotes : prices
    Quotes ||--o| Orders : consumed
    Users ||--o{ Orders : places
    Orders ||--|{ OrderItems : contains
    ModelVersions ||--o{ OrderItems : pins
    Orders ||--o{ OrderStatusHistory : records
    Orders ||--o{ PaymentAttempts : pays
    PaymentAttempts ||--o{ PaymentEvents : receives
    PaymentAttempts ||--o{ Refunds : returns
    Orders ||--o{ PartnerAssignments : assigns
    PartnerLabs ||--o{ PartnerAssignments : accepts
    PartnerAssignments ||--o{ ProductionRuns : produces
    Orders ||--o{ Shipments : ships
    Shipments ||--o{ ShipmentEvents : tracks
    Orders ||--o{ DefectReports : supports
    DefectReports ||--o{ DefectEvidence : proves
    Refunds |o--o{ DefectReports : resolves
    ProductionRuns |o--o{ DefectReports : reprints
    Users ||--o{ AuditLogs : performs
    Users ||--o{ Notifications : receives
    OutboxMessages ||--o{ Notifications : delivers
    Users ||--o{ IdempotencyRequests : deduplicates
    Orders ||--o{ OrderCancellationRequests : requests
    Users ||--o{ PrivacyRequests : controls
```

## D09-payment-state

[Source](D09-payment-state.mmd)

```mermaid
stateDiagram-v2
    [*] --> Unpaid
    Unpaid --> Paid: verifiedCapture [amount matches order]
    Unpaid --> NeedsAttention: mismatched or late capture
    Paid --> RefundPending: reserveRefund [sum within captured]
    PartiallyRefunded --> RefundPending: reserveAdditionalRefund [balance sufficient]
    RefundPending --> PartiallyRefunded: refundConfirmed [total less than captured]
    RefundPending --> Refunded: refundConfirmed [total equals captured]
    RefundPending --> NeedsAttention: providerOutcomeUnknown
    NeedsAttention --> Paid: reconciliation [valid capture and no refund]
    NeedsAttention --> RefundPending: reconciliation [refund still reserved]
    NeedsAttention --> PartiallyRefunded: reconciliation [partial refund confirmed]
    NeedsAttention --> Refunded: reconciliation [full refund confirmed]
    Refunded --> [*]
    note right of RefundPending
        Failed attempt does not release funds blindly.
        Query provider and reconcile reservation first.
    end note
```

## D10-communication

[Source](D10-communication.mmd)

```mermaid
flowchart LR
    FE["External FE / Customer"]
    Gateway["External Gateway"]
    Provider["External AI Provider"]
    subgraph Backend["Backend modules — integrated communication"]
        API["boundary: API / auth"]
        Gen["control: GenerationService"]
        Credits["entity service: Credits"]
        Models["entity service: Models"]
        Order["control: OrderService"]
        Pay["boundary + control: Payments"]
        Work["control: Fulfillment"]
        Outbox["infrastructure: Outbox / Notifications"]
        DB[(SQL Server)]
    end
    FE -->|"1: createJob"| API
    API -->|"1.1: validate and submit"| Gen
    Gen -->|"1.2: debitOnce"| Credits
    Gen -->|"1.3: submit and poll pinned adapter"| Provider
    Gen -->|"1.4: publishVersion"| Models
    FE -->|"2: approve and checkout"| API
    API -->|"2.1: consumeQuote"| Order
    Order -->|"2.2: createAttempt"| Pay
    Pay -->|"2.3: createPayment"| Gateway
    Gateway -->|"3: verifiedEvent"| Pay
    Pay -->|"3.1: markPaidOnce"| Order
    Order -->|"4: assign and start when guards pass"| Work
    Credits --> DB
    Models --> DB
    Order --> DB
    Pay --> DB
    Work --> DB
    DB -->|"5: committed events"| Outbox
    Outbox -->|"5.1: notify"| FE
```
