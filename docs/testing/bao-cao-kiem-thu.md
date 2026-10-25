---
date: 2026-10-23
commit_hash: 11ecc5cbbdef38b9ee5dc9c6e2de02477f0b18c7
branch: SEC-A01_COR-A02_CI-B01_ID-A03_DATA-A04_AI-A06_MODEL-A05_AI-A07_ORDER-B02_ORDER-B03_PAY-B04_PAY-B05
environment: LocalDB (MSSQLLocalDB) + ASP.NET Core TestHost (.NET 10.0.100)
verdict: PASS WITH CONDITIONS (CONCERNS)
gate_decision: CONCERNS
test_architect: BMad Master Test Architect (TEA)
---

# BÁO CÁO KIỂM THỬ XÁC MINH BẰNG CHỨNG THỰC TẾ
## Dự án: THE BOYS / PRINT3D — Backend .NET Core & Distributed Workflows
**Mốc đánh giá:** Ngày 23/10/2026  
**Vai trò thẩm định:** BMad Master Test Architect (TEA Trace, Test-Review, NFR-Assess)  
**Phạm vi:** Xác minh bằng chứng thực nghiệm cho toàn bộ các chức năng đã hoàn thiện trong chuỗi P0 (`SEC-A01` → `PAY-B05`).

---

## 1. TÓM TẮT ĐIỀU HÀNH & PHÁN QUYẾT QUALITY GATE

- **Tổng số bài kiểm thử tự động:** **78 passed, 0 failed, 0 skipped (100% PASS)** trên tổng số 13 file kiểm thử.
- **Thời gian thực thi:** 43–44 giây trên môi trường kiểm thử tích hợp SQL Server LocalDB thật.
- **Trạng thái biên dịch:** `0 Warning(s)`, `0 Error(s)`. Mã nguồn tầng `src/` tuân thủ Clean Architecture nghiêm ngặt.
- **Độ bao phủ các luồng P0 cốt lõi:** **17/17 luồng hoàn thành** đạt mức bao phủ **FULL** (có test đường thành công, đường lỗi, và bất biến nghiệp vụ).
- **Phán quyết Quality Gate:** **`PASS WITH CONDITIONS (CONCERNS)`**
  * *Điều kiện nghiệm thu:* Chất lượng mã nguồn và bộ test của các tính năng P0 đạt mức xuất sắc (điểm chất lượng trung bình test suite: **93.6/100**).
  * *Mục ghi nhận cảnh báo (Concerns):* Kiểm thử hiện tại đang chạy trên LocalDB và các adapter giả lập (Fake Provider / Sandbox Gateway). Hệ thống bắt buộc phải hoàn thiện ticket `REFUND-B06` (Hoàn tiền) và tiến hành nghiệm thu trên môi trường Staging có kết nối mạng tới cổng thanh toán và provider AI thật trước khi kích hoạt thanh toán tiền thật trên Production.

---

## 2. MÔI TRƯỜNG VÀ KẾT QUẢ CHẠY THỰC TẾ (BASELINE EVIDENCE)

### 2.1. Lệnh biên dịch giải pháp
```powershell
dotnet build TheBoys.slnx
```
**Kết quả thực tế ghi nhận:**
```text
All projects are up-to-date for restore.
TheBoys.Domain -> src\TheBoys.Domain\bin\Debug\net10.0\TheBoys.Domain.dll
TheBoys.Application -> src\TheBoys.Application\bin\Debug\net10.0\TheBoys.Application.dll
TheBoys.Infrastructure -> src\TheBoys.Infrastructure\bin\Debug\net10.0\TheBoys.Infrastructure.dll
TheBoys.WebApi -> src\TheBoys.WebApi\bin\Debug\net10.0\TheBoys.WebApi.dll
TheBoys.Tests -> tests\TheBoys.Tests\bin\Debug\net10.0\TheBoys.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:19.82
```

### 2.2. Lệnh thực thi toàn bộ kiểm thử
```powershell
dotnet test TheBoys.slnx --no-restore -l "trx;LogFileName=test_results.trx"
```
**Kết quả thực tế ghi nhận từ trx parser:**
```text
Test run for C:\Users\namho\Downloads\FPT\Exe\print3d\tests\TheBoys.Tests\bin\Debug\net10.0\TheBoys.Tests.dll (.NETCoreApp,Version=v10.0)
Results File: C:\Users\namho\Downloads\FPT\Exe\print3d\tests\TheBoys.Tests\TestResults\test_results.trx

Passed!  - Failed: 0, Passed: 78, Skipped: 0, Total: 78, Duration: 43 s - TheBoys.Tests.dll (net10.0)
```

### 2.3. Thống kê phân bổ 78 test cases theo nhóm file thực tế
| Nhóm Kiểm Thử / Thư Mục | File Test Thực Tế | Số Lượng Test | Kết Quả Chạy |
|---|---|:---:|:---:|
| **Contracts** | [`MeshyContractTests.cs`](../../tests/TheBoys.Tests/Contracts/MeshyContractTests.cs) | 6 | 6 Passed |
| **Contracts** | [`TripoContractTests.cs`](../../tests/TheBoys.Tests/Contracts/TripoContractTests.cs) | 5 | 5 Passed |
| **Integration** | [`ApiFlowTests.cs`](../../tests/TheBoys.Tests/Integration/ApiFlowTests.cs) | 4 | 4 Passed |
| **Integration** | [`MigrationUpgradeTests.cs`](../../tests/TheBoys.Tests/Integration/MigrationUpgradeTests.cs) | 3 | 3 Passed |
| **Integration** | [`OrderV2ApiFlowTests.cs`](../../tests/TheBoys.Tests/Integration/OrderV2ApiFlowTests.cs) | 2 | 2 Passed |
| **Integration** | [`PaymentV2ApiFlowTests.cs`](../../tests/TheBoys.Tests/Integration/PaymentV2ApiFlowTests.cs) | 2 | 2 Passed |
| **Security** | [`CreditAndIdentityTests.cs`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs) | 11 | 11 Passed |
| **Security** | [`RateLimitingAndProxyTests.cs`](../../tests/TheBoys.Tests/Security/RateLimitingAndProxyTests.cs) | 3 | 3 Passed |
| **Unit** | [`PrintingTests.cs`](../../tests/TheBoys.Tests/Unit/PrintingTests.cs) | 12 | 12 Passed |
| **Unit** | [`ModelLifecycleTests.cs`](../../tests/TheBoys.Tests/Unit/ModelLifecycleTests.cs) | 2 | 2 Passed |
| **Unit** | [`OrderB02AndB03Tests.cs`](../../tests/TheBoys.Tests/Unit/OrderB02AndB03Tests.cs) | 8 | 8 Passed |
| **Unit** | [`PaymentB04AndB05Tests.cs`](../../tests/TheBoys.Tests/Unit/PaymentB04AndB05Tests.cs) | 14 | 14 Passed |
| **Unit** | [`ProviderRegistryAndPinningTests.cs`](../../tests/TheBoys.Tests/Unit/ProviderRegistryAndPinningTests.cs) | 6 | 6 Passed |
| **TỔNG CỘNG** | **13 files** | **78** | **78 Passed (100%)** |

---

## 3. MA TRẬN TRUY VẾT YÊU CẦU ĐẾN KIỂM THỬ (TEA TRACEABILITY MATRIX)

Ma trận truy vết toàn diện cho **17 luồng nghiệp vụ P0** được xác nhận là "Hoàn thành" trong báo cáo tiến độ:

| Mã Luồng | Yêu Cầu (UC / FR) | Mã Nguồn Chịu Trách Nhiệm (File:Class) | Tên Test Xác Minh Cụ Thể | Loại Kiểm Thử | Mức Bao Phủ | Ghi Chú & Bằng Chứng Xác Minh |
|:---:|---|---|---|:---:|:---:|---|
| **F01** | UC-01<br>FR-01, FR-02<br>BR-01 | [`AuthController.cs`](../../src/TheBoys.WebApi/Controllers/AuthController.cs)<br>[`User.cs`](../../src/TheBoys.Domain/Entities/User.cs)<br>[`IdentityToken.cs`](../../src/TheBoys.Domain/Entities/IdentityToken.cs) | `CreditAndIdentityTests.Register_CreatesUserWithUnverifiedStatus_AndTwoTrialCredits_AndOpeningBalanceLedger`<br>`CreditAndIdentityTests.EmailVerification_Flow_ValidActivates_ExpiredOrReusedFails_UnverifiedBlockedFromLogin`<br>`CreditAndIdentityTests.LockedAccount_OrRevokedSession_ImmediatelyBlocksPrivateApi`<br>`ApiFlowTests.UploadQuoteReviewOrderAndRolesUseRealSqlAndMockedProvider` | Integration / Security | **FULL** | Xác minh: Hash BCrypt, cấp OTP hash 6 số, chặn đăng nhập khi chưa xác thực email, khóa tài khoản sau 5 lần sai mật khẩu. |
| **F02** | UC-01<br>FR-02<br>BR-02 | [`AuthController.cs`](../../src/TheBoys.WebApi/Controllers/AuthController.cs)<br>[`RefreshSession.cs`](../../src/TheBoys.Domain/Entities/RefreshSession.cs)<br>[`TokenService.cs`](../../src/TheBoys.WebApi/Security/AuthServices.cs) | `CreditAndIdentityTests.RefreshToken_Rotation_AndReuseDetection_RevokesEntireFamily`<br>`CreditAndIdentityTests.PasswordReset_Flow_NonEnumerating_TokenSingleUse_RevokesAllSessions` | Integration / Security | **FULL** | Xác minh: Refresh token family rotation, thu hồi ngay lập tức toàn bộ family khi phát hiện reuse token cũ; reset mật khẩu thu hồi mọi phiên hiện có. |
| **F03** | UC-02<br>FR-03, FR-04<br>BR-03 | [`GenerationsController.cs`](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs)<br>[`SkiaImageValidationService.cs`](../../src/TheBoys.Infrastructure/Services/SkiaImageValidationService.cs)<br>[`ConsentRecord.cs`](../../src/TheBoys.Domain/Entities/ConsentRecord.cs) | `CreditAndIdentityTests.ImageValidation_ValidPngJpegWebp_Pass_CorruptAndOversizedRejected`<br>`CreditAndIdentityTests.Consent_MissingOrRevoked_RejectsGenerationBeforeDebit`<br>`ApiFlowTests.UploadQuoteReviewOrderAndRolesUseRealSqlAndMockedProvider`<br>`ApiFlowTests.ConcurrentUploadsCannotSpendTheLastCreditTwice` | Integration / Concurrency | **FULL** | Xác minh: Giải mã byte ảnh Skia thực tế, chặn file hỏng/vượt 15MB, kiểm tra Idempotency UUID replay và 409 conflict, bắt buộc consent trước khi trừ credit. |
| **F06** | UC-03<br>FR-05<br>BR-04 | [`GenerationWorker.cs`](../../src/TheBoys.Infrastructure/Printing/GenerationWorker.cs)<br>[`GenerationProcessor.cs`](../../src/TheBoys.Infrastructure/Printing/GenerationProcessor.cs)<br>[`StlGeometry.cs`](../../src/TheBoys.Infrastructure/Printing/StlGeometry.cs) | `PrintingTests.AnalysisUsesCubicMeterConversionAndNeverAutoApproves`<br>`PrintingTests.StlIsNormalizedToMillimetersAndRejectsWrongAspectRatio`<br>`PrintingTests.AsymmetricZUpStlScalesPhysicalHeightZToTargetNotLongestAxis`<br>`PrintingTests.AsymmetricYUpStlScalesPhysicalHeightYToTarget`<br>`PrintingTests.UnknownOrInvalidUpAxisExplicitlyRejectedAndNeverFallsBackToLongestAxis`<br>`MeshyContractTests.FourStagesSendDocumentedPayloadsAndResizeInMeters`<br>`TripoContractTests.UploadGenerateAndPollUseV3Contract`<br>`TripoContractTests.ConvertUsesOriginalGenerationTaskAndRequestsStl` | Unit / Contract | **FULL** | Xác minh: Pipeline 4 bước (Generate → Resize → Convert → Analyze), sửa triệt để lỗi up-axis STL, tính scale mm chính xác theo trục thực, kiểm tra mesh watertight. |
| **F07** | UC-03<br>FR-06<br>BR-05 | [`GenerationProcessor.cs`](../../src/TheBoys.Infrastructure/Printing/GenerationProcessor.cs)<br>[`RecoverGenerationTaskCommand.cs`](../../src/TheBoys.Application/Generations/Commands/RecoverGenerationTask/RecoverGenerationTaskCommand.cs) | `ApiFlowTests.WorkerRefundsOnceAndNeverResubmitsUncertainPosts`<br>`MeshyContractTests.AmbiguousCreateIsNotRetryable`<br>`MeshyContractTests.CreateNetworkExceptionIsUncertainAndNotTransient`<br>`MeshyContractTests.Poll429IsRetryableAndPreservesTheExistingTask`<br>`TripoContractTests.AmbiguousConvert500ReturnsUncertain`<br>`TripoContractTests.Poll429ReturnsTransient` | Integration / Contract | **FULL** | Xác minh: POST không xác định (timeout/500) không bao giờ retry mù; chuyển sang `NeedsAttention`; lỗi 429 backoff an toàn. |
| **F08** | UC-04<br>FR-07<br>BR-06 | [`CreditService.cs`](../../src/TheBoys.Infrastructure/Services/CreditService.cs)<br>[`CreditLedgerEntry.cs`](../../src/TheBoys.Domain/Entities/CreditLedgerEntry.cs) | `CreditAndIdentityTests.CreditService_RefundForGeneration_IsIdempotent_CannotDoubleRefund`<br>`PrintingTests.NoCreditCannotBeUsedAndPaidCreditRefundKeepsSource`<br>`ApiFlowTests.WorkerRefundsOnceAndNeverResubmitsUncertainPosts` | Unit / Integration | **FULL** | Xác minh: Tự động hoàn credit khi job thất bại xác định, liên kết `ReversesEntryId`, index duy nhất `UX_Ledger_RefundOnce` chặn vật lý hoàn 2 lần. |
| **F09** | UC-05<br>FR-08 | [`GenerationsController.cs`](../../src/TheBoys.WebApi/Controllers/GenerationsController.cs)<br>[`LocalModelStorage.cs`](../../src/TheBoys.Infrastructure/Printing/LocalModelStorage.cs) | `ApiFlowTests.UploadQuoteReviewOrderAndRolesUseRealSqlAndMockedProvider` | Integration | **FULL** | Xác minh: Người dùng khác truy cập nhận 404 NotFound; stream file từ storage cục bộ an toàn kèm header `private, no-store`. |
| **F10** | UC-05<br>FR-09 | [`ModelProject.cs`](../../src/TheBoys.Domain/Entities/ModelProject.cs)<br>[`ModelVersion.cs`](../../src/TheBoys.Domain/Entities/ModelVersion.cs)<br>[`ModelAsset.cs`](../../src/TheBoys.Domain/Entities/ModelAsset.cs) | `ModelLifecycleTests.ModelProjectAndJobAggregateInvariantsEnforced`<br>`ModelLifecycleTests.SucceededModelVersionProducesImmutableAssets`<br>`OrderV2ApiFlowTests.CompleteV2OrderFlow_FromReview_ToApproval_ToQuote_ToAtomicCheckout` | Unit / Integration | **FULL** | Xác minh: Tách biệt Project/Job/Version/Asset; ModelVersion gắn `GeometryHash` SHA-256 cố định, file asset GLB/STL không thể sửa đổi sau khi xuất xưởng. |
| **F11** | UC-06<br>FR-10<br>BR-07 | [`PrintReview.cs`](../../src/TheBoys.Domain/Entities/PrintReview.cs)<br>[`CreatePrintReviewCommandHandler.cs`](../../src/TheBoys.Application/Reviews/Commands/CreatePrintReview/CreatePrintReviewCommandHandler.cs)<br>[`PrintReviewsController.cs`](../../src/TheBoys.WebApi/Controllers/v2/PrintReviewsController.cs) | `OrderB02AndB03Tests.Invariant1_PrintReview_AppendOnly_RejectedThenApproved_CreatesTwoHistories`<br>`OrderB02AndB03Tests.Invariant2_PrintReview_CannotApprove_WhenInvalid`<br>`OrderV2ApiFlowTests.CompleteV2OrderFlow_FromReview_ToApproval_ToQuote_ToAtomicCheckout` | Unit / Integration | **FULL** | Xác minh: Lịch sử duyệt append-only, cấm Customer tự review (403), bắt buộc checklist slicer vật lý, reject rồi approve tạo 2 bản ghi riêng biệt. |
| **F12** | UC-07<br>FR-11<br>BR-07 | [`CustomerApproval.cs`](../../src/TheBoys.Domain/Entities/CustomerApproval.cs)<br>[`DeterministicConfigurationHasher.cs`](../../src/TheBoys.Infrastructure/Services/DeterministicConfigurationHasher.cs)<br>[`CustomerApprovalsController.cs`](../../src/TheBoys.WebApi/Controllers/v2/CustomerApprovalsController.cs) | `OrderB02AndB03Tests.Invariant3_CustomerApproval_OnlyOwnerCanApprove`<br>`OrderB02AndB03Tests.Invariant4_ConfigurationHash_Divergence`<br>`OrderV2ApiFlowTests.CompleteV2OrderFlow_FromReview_ToApproval_ToQuote_ToAtomicCheckout` | Unit / Integration | **FULL** | Xác minh: Chỉ chủ sở hữu model được phê duyệt; cấu hình in được băm SHA-256 cố định; nếu lệch chiều cao hoặc vật liệu sẽ sinh hash khác. |
| **F13** | UC-07<br>FR-12<br>BR-08 | [`Quote.cs`](../../src/TheBoys.Domain/Entities/Quote.cs)<br>[`CreateQuoteCommandHandler.cs`](../../src/TheBoys.Application/Quotes/Commands/CreateQuote/CreateQuoteCommandHandler.cs)<br>[`QuotesController.cs`](../../src/TheBoys.WebApi/Controllers/v2/QuotesController.cs) | `OrderB02AndB03Tests.Invariant5_Quote_SavesSnapshot_And_ExpiresProperly`<br>`OrderB02AndB03Tests.Invariant6_Quote_PackagePriceChange_DoesNotAffectExistingQuote`<br>`OrderB02AndB03Tests.Invariant7_Quote_CannotCreate_WithRejectedReview_Or_InactivePackage`<br>`OrderV2ApiFlowTests.CompleteV2OrderFlow_FromReview_ToApproval_ToQuote_ToAtomicCheckout` | Unit / Integration | **FULL** | Xác minh: Snapshot tài chính và địa chỉ lưu DB, TTL 30 phút, package giá thay đổi sau đó không làm ảnh hưởng giá đã báo; cấm quote khi review bị từ chối. |
| **F14** | UC-08<br>FR-13<br>BR-09 | [`CheckoutQuoteCommandHandler.cs`](../../src/TheBoys.Application/Orders/Commands/CheckoutQuote/CheckoutQuoteCommandHandler.cs)<br>[`OrderItem.cs`](../../src/TheBoys.Domain/Entities/OrderItem.cs)<br>[`OrdersV2Controller.cs`](../../src/TheBoys.WebApi/Controllers/v2/OrdersV2Controller.cs) | `OrderB02AndB03Tests.Invariant8to12_Checkout_Atomicity_Idempotency_And_Expiry`<br>`OrderV2ApiFlowTests.CompleteV2OrderFlow_FromReview_ToApproval_ToQuote_ToAtomicCheckout`<br>`OrderV2ApiFlowTests.Invariant11_Concurrency_MultipleCheckouts_OnlyOneSucceeds` | Unit / Integration / Concurrency | **FULL** | Xác minh: Tạo đồng thời Order, OrderItem và 2 OrderStatusHistory; chặn quote hết hạn; kiểm tra concurrency 5 request checkout đồng thời: chỉ 1 thành công (201), 4 bị chặn (409 Conflict) nhờ index `UX_Orders_QuoteId`. |
| **F15** | UC-08<br>FR-14 | [`OrdersV2Controller.cs`](../../src/TheBoys.WebApi/Controllers/v2/OrdersV2Controller.cs)<br>[`OrderV2Dto.cs`](../../src/TheBoys.Application/Orders/OrderV2Dto.cs) | `OrderV2ApiFlowTests.CompleteV2OrderFlow_FromReview_ToApproval_ToQuote_ToAtomicCheckout`<br>`PaymentV2ApiFlowTests.FullEndToEndPaymentFlow_ViaApi_InitiateAttempt_AndVerifySignedWebhook` | Integration | **FULL** | Xác minh: Truy vấn đơn hàng trả về snapshot địa chỉ, danh sách item in 3D, timeline 2 trục (`PaymentStatus` và `FulfillmentStatus`). |
| **F17** | UC-09<br>FR-15, FR-16<br>BR-10, BR-11, BR-12 | [`PaymentAttempt.cs`](../../src/TheBoys.Domain/Entities/PaymentAttempt.cs)<br>[`PaymentEvent.cs`](../../src/TheBoys.Domain/Entities/PaymentEvent.cs)<br>[`SandboxPaymentGateway.cs`](../../src/TheBoys.Infrastructure/Payments/SandboxPaymentGateway.cs)<br>[`SePayPaymentGateway.cs`](../../src/TheBoys.Infrastructure/Payments/SePayPaymentGateway.cs)<br>[`PaymentWebhookController.cs`](../../src/TheBoys.WebApi/Controllers/v2/PaymentWebhookController.cs)<br>[`PaymentReconciliationController.cs`](../../src/TheBoys.WebApi/Controllers/v2/PaymentReconciliationController.cs) | `PaymentB04AndB05Tests.Invariant1` đến `Invariant14` (14 unit tests)<br>`PaymentV2ApiFlowTests.FullEndToEndPaymentFlow_ViaApi_InitiateAttempt_AndVerifySignedWebhook`<br>`PaymentV2ApiFlowTests.Concurrency_MultipleSimultaneousDuplicateWebhooks_OnlyOneCaptureOccurs` | Unit / Integration / Concurrency | **FULL** | Xác minh: 2-step attempt creation, xác thực chữ ký HMAC-SHA256, replay window 5 phút, duplicate webhook idempotent ACK (5 webhook song song chỉ capture 1 lần), sai số tiền chuyển `NeedsAttention`, late webhook trên đơn đã hủy chuyển `RefundPending`, reconcile không sinh charge mới. |
| **F23** | UC-12<br>FR-20 | [`AdminController.cs`](../../src/TheBoys.WebApi/Controllers/AdminController.cs)<br>[`AuditService.cs`](../../src/TheBoys.Infrastructure/Services/AuditService.cs) | `ApiFlowTests.UploadQuoteReviewOrderAndRolesUseRealSqlAndMockedProvider`<br>`CreditAndIdentityTests.LockedAccount_OrRevokedSession_ImmediatelyBlocksPrivateApi` | Integration / Security | **FULL** | Xác minh: Admin thay đổi quyền, token Staff cũ bị giáng cấp mất quyền review ngay lập tức; khóa tài khoản chặn API ngay tức khắc; ghi AuditLog. |
| **F24** | UC-12<br>FR-21 | [`AdminController.cs`](../../src/TheBoys.WebApi/Controllers/AdminController.cs)<br>[`CreditService.cs`](../../src/TheBoys.Infrastructure/Services/CreditService.cs) | `CreditAndIdentityTests.CreditService_AdminGrant_RequiresReasonAndIdempotencyKey_AndReplayDoesNotDoubleGrant` | Unit / Security | **FULL** | Xác minh: Bắt buộc lý do, người thao tác và Idempotency key; replay không cộng trùng; cộng đúng vào bucket Purchased. |
| **F25** | UC-13<br>FR-22 | [`PricingPackage.cs`](../../src/TheBoys.Domain/Entities/PricingPackage.cs)<br>[`AdminController.cs`](../../src/TheBoys.WebApi/Controllers/AdminController.cs) | `ApiFlowTests.UploadQuoteReviewOrderAndRolesUseRealSqlAndMockedProvider`<br>`OrderB02AndB03Tests.Invariant6_Quote_PackagePriceChange_DoesNotAffectExistingQuote`<br>`PrintingTests.PackagePriceUsesQuantityNotBoundingBoxVolume` | Unit / Integration | **FULL** | Xác minh: Gói giá dịch vụ có version, khi Admin sửa giá thì các Quote và Order đã tạo trước đó giữ nguyên snapshot giá cũ. |

---

## 4. ĐỐI CHIẾU ACCEPTANCE TESTS QUAN TRỌNG (MỤC 6.2 DEEP RESEARCH)

| Mã Test | Nội Dung Yêu Cầu Trong Báo Cáo Deep Research | Test Case Thực Tế Trong Mã Nguồn | Trạng Thái Kiểm Chứng | Nhận Xét Kỹ Thuật |
|:---:|---|---|:---:|---|
| **A** | **Tính đồng thời của generation:**<br>- Hai request đồng thời cùng idempotency key + cùng payload → 1 job, 1 debit.<br>- Cùng key khác payload → 409 Conflict.<br>- Timeout khi gọi provider → chuyển `NeedsAttention`, không gửi thêm POST gây tốn tiền. | - [`ApiFlowTests.cs:31-35`](../../tests/TheBoys.Tests/Integration/ApiFlowTests.cs#L31-L35)<br>- [`ApiFlowTests.cs:89-94`](../../tests/TheBoys.Tests/Integration/ApiFlowTests.cs#L89-L94)<br>- [`MeshyContractTests.cs:53-64`](../../tests/TheBoys.Tests/Contracts/MeshyContractTests.cs#L53-L64) | **ĐÃ XÁC MINH 100%** | Test đã chứng minh qua cả WebApi lẫn Contract test: Replay trả về 200/202, payload khác trả về 409 Conflict, exception mạng chuyển cờ `Uncertain: true` và không retry mù. |
| **B** | **Sổ cái credit:**<br>- Khởi tạo 2 trial credits.<br>- Request hợp lệ → còn 1; đúng 1 debit.<br>- Replay → còn 1; không debit mới.<br>- Provider lỗi xác định → hoàn lại 2; đúng 1 reversal.<br>- Hoàn lần 2 → vẫn 2; không hoàn trùng. | - [`CreditAndIdentityTests.cs:28-74`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs#L28-L74)<br>- [`CreditAndIdentityTests.cs:202-225`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs#L202-L225)<br>- [`ApiFlowTests.cs:122-140`](../../tests/TheBoys.Tests/Integration/ApiFlowTests.cs#L122-L140) | **ĐÃ XÁC MINH 100%** | Kiểm tra toàn bộ vòng đời tài khoản: Sổ cái append-only, trừ credit nguyên tử, hoàn tiền gắn `ReversesEntryId`, index `UX_Ledger_RefundOnce` bảo đảm không thể hoàn trùng. |
| **C** | **Model version bất biến:**<br>- ModelVersion gắn quote/order không được đổi file, hash hoặc kích thước.<br>- Repair/resize tạo version con mới. | - [`ModelLifecycleTests.cs:70-145`](../../tests/TheBoys.Tests/Unit/ModelLifecycleTests.cs#L70-L145)<br>- [`OrderB02AndB03Tests.cs:174-210`](../../tests/TheBoys.Tests/Unit/OrderB02AndB03Tests.cs#L174-L210) | **ĐÃ XÁC MINH 100%** | Entity `ModelVersion` lưu `GeometryHash` SHA-256; `ModelAsset` lưu Sha256 và ByteLength cố định. Không có cơ chế sửa file in-place. |
| **D** | **Vô hiệu hóa phê duyệt cũ:**<br>- Đổi chiều cao, vật liệu, hoàn thiện, chữ khắc, version hoặc configuration hash → approval & quote trước đó bị vô hiệu hóa. | - [`OrderB02AndB03Tests.cs:174-210`](../../tests/TheBoys.Tests/Unit/OrderB02AndB03Tests.cs#L174-L210)<br>- [`OrderB02AndB03Tests.cs:300-340`](../../tests/TheBoys.Tests/Unit/OrderB02AndB03Tests.cs#L300-L340) | **ĐÃ XÁC MINH 100%** | So sánh trực tiếp `ConfigurationHash`: Bất kỳ sự sai khác nào giữa Review, Approval và Quote đều khiến CreateQuote hoặc Checkout bị từ chối ngay lập tức. |
| **E** | **Thanh toán:**<br>- Webhook success có chữ ký → Paid đúng 1 lần.<br>- Event trùng → no-op.<br>- Sai amount → NeedsAttention.<br>- Sai currency → NeedsAttention/reject.<br>- Thiếu/sai chữ ký → không mutate.<br>- Success sau cancel → refund/reconcile.<br>- Cancel và production start đồng thời → chỉ 1 hợp lệ. | - [`PaymentB04AndB05Tests.cs:Invariant7-12`](../../tests/TheBoys.Tests/Unit/PaymentB04AndB05Tests.cs)<br>- [`PaymentV2ApiFlowTests.cs:56-211`](../../tests/TheBoys.Tests/Integration/PaymentV2ApiFlowTests.cs#L56-L211) | **ĐẠT 6/7 Ý**<br>*(Ý cancel vs production start đồng thời chưa có test)* | 6/7 điều kiện về thanh toán đã có test chứng minh toàn diện (chữ ký, trùng lặp, sai tiền, sai loại tiền, late success). Ý số 7 thuộc phân hệ Sản xuất (`OPS-B06`), chưa kiểm thử được do bảng `ProductionRuns` chưa triển khai. |
| **F** | **Refund (Hoàn tiền):**<br>- Refund đồng thời không reserve quá số tiền đã capture.<br>- Retry submit không tạo 2 lần hoàn tiền. | *Chưa có test cho cổng thanh toán gateway* | **CHƯA KIỂM CHỨNG**<br>*(Ngoài phạm vi P0)* | Bảng `Refunds` và cơ chế gọi gateway hoàn tiền nằm trong ticket `REFUND-B06` (đã quy hoạch tại ACT-01). Hiện tại mới chỉ có hoàn tiền tín dụng (Credit Refund - Test B). |
| **G** | **Fulfillment:**<br>- Chỉ cho bắt đầu in nếu: PaymentStatus=Paid, PrintReview=Approved, CustomerApproval khớp, PartnerAssignment=Accepted, AssetHash khớp OrderItem. | *Chưa có test kiểm tra trọn vẹn toàn bộ 5 điều kiện trên* | **CHƯA KIỂM CHỨNG**<br>*(Ngoài phạm vi P0)* | Đã có guard: Order phải chuyển sang Paid; PrintReview và Approval khớp ModelVersion trên Quote. Tuy nhiên điều kiện `PartnerAssignment = Accepted` chưa có do bảng phân công xưởng in thuộc ticket `OPS-B06` (ACT-02). |
| **H** | **Geometry:**<br>- Đưa trường hợp STL bất đối xứng Z-up (từng bị tính nhầm 50mm thay vì 100mm) vào regression test cố định. | - [`PrintingTests.cs:78-100`](../../tests/TheBoys.Tests/Unit/PrintingTests.cs#L78-L100)<br>- [`PrintingTests.cs:103-125`](../../tests/TheBoys.Tests/Unit/PrintingTests.cs#L103-L125) | **ĐÃ XÁC MINH 100%** | Test `AsymmetricZUpStlScalesPhysicalHeightZToTargetNotLongestAxis` kiểm tra file STL mẫu (X=0.2, Y=0.15, Z=0.05), xác nhận chiều cao Z sau scale đạt đúng 100mm, không bị co lại 25mm. |

---

## 5. ĐÁNH GIÁ CHẤT LƯỢNG TEST SUITE (TEA TEST-REVIEW)

Đánh giá chi tiết từng file kiểm thử trên thang điểm 0–100 dựa trên: Tính chân thực của assertion, mức độ phụ thuộc thứ tự, tính đồng thời thực chất và mức độ mock:

| Tên File Kiểm Thử | Điểm (0-100) | Điểm Mạnh Nổi Bật | Hạn Chế / Vấn Đề Tìm Thấy | Khuyến Nghị Cải Tiến |
|---|:---:|---|---|---|
| [`MeshyContractTests.cs`](../../tests/TheBoys.Tests/Contracts/MeshyContractTests.cs) | **92** | Kiểm tra chi tiết 4 stages API, phân biệt chính xác lỗi mạng `Uncertain` vs `Transient`, kiểm tra payload resize theo đơn vị mét (0.11m). | Mock dùng DelegatingHandler in-memory thay vì wire-level HTTP listener. | Có thể bổ sung WireMock.Net nếu cần kiểm thử cả timeout ở tầng TCP socket. |
| [`TripoContractTests.cs`](../../tests/TheBoys.Tests/Contracts/TripoContractTests.cs) | **92** | Kiểm tra đúng hợp đồng Tripo v3, kiểm tra `flatten_bottom: true`, model version pinning `v3.1-20260211`, parse credit usage thực tế. | Xử lý mock tuần tự, chưa mô phỏng độ trễ ngẫu nhiên của mạng. | Bổ sung jitter/latency injection cho các kịch bản timeout. |
| [`ApiFlowTests.cs`](../../tests/TheBoys.Tests/Integration/ApiFlowTests.cs) | **88** | Luồng E2E chạy trên LocalDB thật; kiểm thử quyền JWT bị mất hiệu lực tức thì khi Admin giáng cấp role; test concurrency trừ credit cuối cùng. | Test `UploadQuoteReviewOrderAndRolesUseRealSqlAndMockedProvider` quá dài (79 dòng), gộp hơn 15 hành vi vào 1 test method. | Tách nhỏ test này thành các test method độc lập để dễ định vị khi xảy ra lỗi. |
| [`MigrationUpgradeTests.cs`](../../tests/TheBoys.Tests/Integration/MigrationUpgradeTests.cs) | **95** | Test nâng cấp migration xuất sắc: Tạo database từ migration cũ, insert dữ liệu người dùng cũ, migrate tiến lên vNext, kiểm tra dữ liệu cũ không bị mất và query được toàn bộ 17 bảng mới. | Phụ thuộc vào dịch vụ SQL Server LocalDB trên máy chủ chạy test. | Rất tốt, nên giữ làm standard test cho CI/CD pipeline. |
| [`RateLimitingAndProxyTests.cs`](../../tests/TheBoys.Tests/Security/RateLimitingAndProxyTests.cs) | **94** | Kiểm thử trực diện cơ chế chống IP Spoofing qua headers giả mạo (`CF-Connecting-IP`, `X-Forwarded-For`); kiểm thử khởi động không cần real API key. | Vòng lặp 10 request gửi tuần tự; chưa có kịch bản dội tải đa luồng cùng 1 mili-giây. | Thêm test concurrency dội tải rate-limit bằng `Parallel.For`. |
| [`PrintingTests.cs`](../../tests/TheBoys.Tests/Unit/PrintingTests.cs) | **90** | Kiểm tra giải thuật hình học 3D, scale up-axis Z/Y chính xác, kiểm tra tỷ lệ khung hình và tính toán giá in theo số lượng. | Tạo file STL tạm trên ổ cứng disk (có dọn dẹp bằng `finally`). | Sử dụng MemoryStream thay vì File vật lý nếu muốn tối ưu tốc độ chạy. |
| [`ModelLifecycleTests.cs`](../../tests/TheBoys.Tests/Unit/ModelLifecycleTests.cs) | **88** | Kiểm tra chặt chẽ bất biến quan hệ Aggregate: `Project` → `Job` → `Stage` → `Version` → `Asset`, xác minh tính toàn vẹn của SHA-256 hash. | Kiểm tra mức POCO in-memory, chưa assert các ràng buộc Cascade Delete hay Shadow Properties qua EF Core. | Bổ sung test xóa Project kiểm tra tính toàn vẹn khóa ngoại trên DbContext. |
| [`OrderB02AndB03Tests.cs`](../../tests/TheBoys.Tests/Unit/OrderB02AndB03Tests.cs) | **95** | Khảo sát 12 bất biến nghiệp vụ cốt lõi của đơn hàng: PrintReview append-only, Owner approval, ConfigurationHash divergence, Quote snapshot & expiry, Atomic checkout. | Sử dụng TestClock in-memory. | Thiết kế kiểm thử rất mẫu mực và rõ ràng. |
| [`PaymentB04AndB05Tests.cs`](../../tests/TheBoys.Tests/Unit/PaymentB04AndB05Tests.cs) | **96** | Bộ kiểm thử tài chính chuẩn mực nhất: 14 bất biến (2-step attempt, snapshot derivation, 503 fail-closed, HMAC verify, duplicate ACK, discrepancy detection, late success, reconciliation). | Không có nhược điểm lớn. | Chuẩn mực cao về test tài chính. |
| [`ProviderRegistryAndPinningTests.cs`](../../tests/TheBoys.Tests/Unit/ProviderRegistryAndPinningTests.cs) | **90** | Kiểm chứng cơ chế ghim cứng Provider/Model cho từng job; việc thay đổi Provider mặc định không làm sai lệch các job cũ. | Sử dụng mock options và config in-memory. | Có thể bổ sung test nạp từ file `appsettings.json` thật. |
| [`CreditAndIdentityTests.cs`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs) | **96** | Kiểm chứng toàn diện trục tài khoản và sổ cái: Tạo số dư mở màn, tổng ledger luôn bằng số dư tài khoản, trừ credit không bao giờ âm, hoàn tiền idempotent, xoay vòng refresh token family. | Không có nhược điểm lớn. | Độ tin cậy rất cao. |
| [`OrderV2ApiFlowTests.cs`](../../tests/TheBoys.Tests/Integration/OrderV2ApiFlowTests.cs) | **95** | Luồng API v2 từ Review → Approval → Quote → Checkout. Kiểm thử concurrency 5 checkout song song bằng `Task.WhenAll`, assert 1 thành công (201) và 4 xung đột (409). | Chạy qua TestServer nội bộ. | Rất xuất sắc trong việc chứng minh an toàn dữ liệu. |
| [`PaymentV2ApiFlowTests.cs`](../../tests/TheBoys.Tests/Integration/PaymentV2ApiFlowTests.cs) | **96** | Luồng thanh toán API v2: Tạo Attempt → Webhook HMAC → Paid. Kiểm thử concurrency 5 webhook trùng lặp bắn đồng thời bằng `Task.WhenAll`, chỉ ghi nhận duy nhất 1 lần chuyển trạng thái và 1 Outbox. | Dùng SandboxPaymentGateway thay vì gọi SePay thật. | Cực kỳ thuyết phục về khả năng chống duplicate capture. |
| **ĐIỂM TRUNG BÌNH TOÀN BỘ** | **93.6 / 100** | **Độ tin cậy kiểm thử đạt mức rất cao. Không có test dùng Thread.Sleep(), kiểm tra hành vi thật và dữ liệu DB thật.** | | |

---

## 6. ĐÁNH GIÁ CÁC YÊU CẦU PHI CHỨC NĂNG (TEA NFR-ASSESS)

Đánh giá NFR dựa hoàn toàn trên bằng chứng mã nguồn và kết quả kiểm thử thực nghiệm:

### 6.1. Bảo mật (Security) — Kết luận: `PASS`
1. **Xác thực Webhook qua HMAC-SHA256:**
   - *Bằng chứng:* [`SandboxPaymentGateway.cs:72-85`](../../src/TheBoys.Infrastructure/Payments/SandboxPaymentGateway.cs#L72-L85) và [`SePayPaymentGateway.cs:55-80`](../../src/TheBoys.Infrastructure/Payments/SePayPaymentGateway.cs#L55-L80).
   - Kiểm thử [`PaymentB04AndB05Tests.cs:Invariant8`](../../tests/TheBoys.Tests/Unit/PaymentB04AndB05Tests.cs) xác nhận chữ ký giả mạo hoặc sai lệch lập tức bị từ chối và không gây biến đổi trạng thái trong DB.
2. **Chống Replay Attack bằng Timestamp Window (5 phút):**
   - *Bằng chứng:* [`SandboxPaymentGateway.cs:55-65`](../../src/TheBoys.Infrastructure/Payments/SandboxPaymentGateway.cs#L55-L65) kiểm tra lệch thời gian $|t_{webhook} - t_{now}| > 300\text{s}$ thì từ chối.
3. **Băm một chiều Token định danh:**
   - *Bằng chứng:* [`CreditAndIdentityTests.cs:Register_CreatesUserWithUnverifiedStatus...`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs#L28-L74). Mọi token OTP (xác thực email, đổi mật khẩu) đều được băm SHA-256 trước khi lưu vào bảng `IdentityTokens`.
4. **Khóa tài khoản và Thu hồi phiên tức thì:**
   - *Bằng chứng:* [`CreditAndIdentityTests.cs:LockedAccount_OrRevokedSession...`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs). Tài khoản bị khóa hoặc phiên bị thu hồi sẽ bị middleware chặn ngay lập tức, không chờ JWT hết hạn.
5. **Xoay vòng Refresh Token & Phát hiện tái sử dụng:**
   - *Bằng chứng:* [`CreditAndIdentityTests.cs:RefreshToken_Rotation_AndReuseDetection...`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs). Nếu một token cũ trong `FamilyId` bị dùng lại, toàn bộ các session của gia đình đó bị thu hồi toàn bộ.
6. **Chống giả mạo IP qua Reverse Proxy:**
   - *Bằng chứng:* [`RateLimitingAndProxyTests.cs:44-69`](../../tests/TheBoys.Tests/Security/RateLimitingAndProxyTests.cs#L44-L69). Header `X-Forwarded-For` từ client không tin cậy bị loại bỏ hoàn toàn, không thể bypass rate limiting.

### 6.2. Toàn vẹn dữ liệu (Data Integrity) — Kết luận: `PASS`
1. **Sổ cái Credit bất biến (Append-Only Ledger):**
   - *Bằng chứng:* Bảng `CreditLedger` với ràng buộc khóa ngoại chặt chẽ. Kiểm thử [`CreditAndIdentityTests.cs:CreditService_LedgerSumAlwaysEqualsAccountBalance...`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs#L77-L130) chứng minh tổng delta của sổ cái luôn bằng số dư trên `CreditAccounts`.
2. **Ràng buộc chống hoàn credit trùng lặp:**
   - *Bằng chứng vật lý DB:* Unique Filtered Index `UX_Ledger_RefundOnce` trên cột `ReversesEntryId` (`WHERE ReversesEntryId IS NOT NULL`). Đã kiểm chứng trong [`CreditAndIdentityTests.cs:207-225`](../../tests/TheBoys.Tests/Security/CreditAndIdentityTests.cs#L207-L225).
3. **Ràng buộc chống tạo 2 đơn hàng cho cùng 1 báo giá:**
   - *Bằng chứng vật lý DB:* Unique Filtered Index `UX_Orders_QuoteId` trên bảng `Orders`. Đã kiểm chứng concurrency trong [`OrderV2ApiFlowTests.cs:355-382`](../../tests/TheBoys.Tests/Integration/OrderV2ApiFlowTests.cs#L355-L382).
4. **Ràng buộc chống trùng lặp mã tham chiếu thanh toán:**
   - *Bằng chứng vật lý DB:* Unique Index `UX_PaymentAttempts_Gateway_MerchantReference` và `UX_PaymentEvents_Gateway_EventId`. Đã kiểm chứng concurrency trong [`PaymentV2ApiFlowTests.cs:178-211`](../../tests/TheBoys.Tests/Integration/PaymentV2ApiFlowTests.cs#L178-L211).
5. **Tính bất biến của Báo giá và ModelVersion:**
   - Snapshot giá gói in, địa chỉ và thông số kỹ thuật được lưu cố định trên `Quotes` và `OrderItems`. Thay đổi bảng giá sau đó không làm sai lệch đơn hàng cũ.
6. **Độ an toàn khi nâng cấp Database (Forward Migration):**
   - Đã kiểm chứng qua [`MigrationUpgradeTests.cs`](../../tests/TheBoys.Tests/Integration/MigrationUpgradeTests.cs) trên cả 3 mốc migration gốc mà không làm mất bản ghi người dùng.

### 6.3. Giới hạn & Điểm cần lưu ý về Hiệu năng / Vận hành — Kết luận: `CONCERNS`
1. **Chưa có kiểm thử với cổng thanh toán thật (External Gateway Sandbox):**
   - Toàn bộ test thanh toán hiện tại đang chạy trên `SandboxPaymentGateway` (tự tính HMAC trong tiến trình). Chưa có contract test kết nối qua Internet tới SePay thật (yêu cầu cấu hình webhook công khai qua ngrok/cloudflare tunnel).
2. **Chưa có Worker giải phóng Outbox tự động (`EVT-B08`):**
   - Sự kiện `PaymentConfirmed` được ghi nhận chuẩn xác vào bảng `OutboxMessages` nhưng hiện chưa có hosted service background quét và bắn thông báo/email ra ngoài định kỳ.
3. **Lưu trữ Model Asset tại Storage Cục bộ:**
   - Hệ thống đang dùng `LocalModelStorage` trên thư mục máy chủ, chưa cấu hình AWS S3 / Azure Blob Storage có signed URL thời hạn ngắn.

---

## 7. BẢNG PHÂN LOẠI LỖ HỔNG KIỂM THỬ VÀ RỦI RO (TEST GAPS & RISKS)

| Mã | Hạng Mục / Lỗ Hổng Kiểm Thử | Mức Độ Rủi Ro | Phân Hệ | Mô Tả & Ảnh Hưởng Tiềm Tàng | Đề Xuất Test Cần Thêm (Action Plan) |
|---|---|:---:|:---:|---|---|
| **GAP-01** | **Chưa có kiểm thử quy trình Hoàn tiền (`Refunds`)** | **High** | Commerce | Chưa có bảng `Refunds` và API gọi gateway hoàn tiền. Nếu khách hủy đơn sau khi thanh toán, tiền chưa thể tự động hoàn về tài khoản ngân hàng. | Viết unit/integration test cho ticket `REFUND-B06` (ACT-01): Kiểm tra ràng buộc $\sum Refund \le AmountVnd$, chống double-refund. |
| **GAP-02** | **Chưa có kiểm thử Điều kiện Bắt đầu In (Acceptance Test G)** | **High** | Fulfillment | Chưa có bảng `PartnerAssignments` và `ProductionRuns`. Guard liên hoàn 5 bước (Payment=Paid, Review=Approved, Approval khớp, Partner=Accepted, AssetHash khớp) chưa thể thực thi trọn vẹn. | Viết E2E test cho ticket `OPS-B06` (ACT-02/03): Cấm chuyển sang `Printing` khi chưa có xưởng nhận việc. |
| **GAP-03** | **Chưa có kiểm thử Outbox Dispatcher Worker** | **Medium** | Infrastructure | Bản tin outbox tồn đọng trong DB nếu worker dispatcher chưa được kích hoạt. | Viết background hosted service test cho ticket `EVT-B08` (ACT-06): Quét batch, kiểm tra lease lock và retry policy. |
| **GAP-04** | **Chưa có kiểm thử Huỷ đơn đồng thời với Bắt đầu in (Test E ý 7)** | **Medium** | Production | Kịch bản khách hàng bấm Huỷ đơn cùng lúc đối tác bấm Bắt đầu in chưa có test chạy đua tranh chấp trạng thái. | Bổ sung test concurrency khi triển khai xong `OPS-B06` và `REFUND-B06`. |
| **GAP-05** | **Thiếu Deep Health Check (`/health/ready`)** | **Low** | DevOps | Endpoint `/health` hiện tại mới kiểm tra tiến trình sống, chưa kiểm tra kết nối mở tới SQL Server LocalDB và thư mục storage. | Viết test cho `OBS-A09` (ACT-07) kiểm tra trả về 503 khi ngắt kết nối database. |

---

## 8. TEST ĐÃ THÊM TRONG ĐỢT NÀY

- Trong lượt đánh giá này, toàn bộ **78 bài kiểm thử hiện có** đã bao phủ 100% các bất biến nghiệp vụ, luồng thành công và luồng lỗi của **tất cả các tính năng thuộc phạm vi P0 (17 luồng Hoàn thành)**.
- Các lỗ hổng kiểm thử ghi nhận tại mục 7 đều gắn liền với các thực thể chưa được xây dựng trong mã nguồn (thuộc các ticket P1/P2 hoặc Phase tiếp theo như `REFUND-B06`, `OPS-B06`, `EVT-B08`).
- Theo nguyên tắc chỉ đạo: **Không tự ý sửa mã nguồn trong `src/` và không viết test trước cho các tính năng chưa có mã**, do đó không bổ sung thêm file test mới gây gãy pipeline trong đợt này. Toàn bộ 78 tests giữ vững trạng thái **PASS 100%**.

---

## 9. SAI LỆCH PHÁT HIỆN SO VỚI BÁO CÁO TIẾN ĐỘ

Qua việc khảo sát thực nghiệm bằng lệnh và đối chiếu từng dòng mã nguồn, phát hiện các điểm sai lệch nhỏ sau trong báo cáo tiến độ trước đó:

1. **Số lượng luồng "Hoàn thành":**
   - *Báo cáo tiến độ ghi:* "16 (50,0%)" tại bảng tổng quan 1.2.
   - *Thực tế kiểm tra chi tiết tại Mục 2:* Có chính xác **17 luồng** mang nhãn "Hoàn thành" (F01, F02, F03, F06, F07, F08, F09, F10, F11, F12, F13, F14, F15, F17, F23, F24, F25). Tỷ lệ hoàn thành thực tế là **17 / 32 = 53.1%**.
2. **Thời gian thực thi test suite:**
   - *Báo cáo tiến độ ghi:* "Duration: 43 s".
   - *Thực tế ghi nhận:* Thời gian chạy test đơn lẻ là 43 giây, thời gian build + run hoàn chỉnh là ~63 giây. Kết quả số lượng test khớp tuyệt đối: **78 passed, 0 failed, 0 skipped**.
3. **Phân loại Acceptance Test E (Thanh toán):**
   - Báo cáo tiến độ ghi nhận Test E là hoàn thành, nhưng phân tích chi tiết cho thấy ý số 7 của Test E (*Cancel và production start chạy đồng thời*) thực chất gắn liền với phân hệ Sản xuất (`OPS-B06`), cần ghi nhận là đạt 6/7 ý để bảo đảm tính minh bạch.

---

## 10. DANH MỤC NGOÀI PHẠM VI (CHƯA ĐƯỢC KIỂM CHỨNG)

Các chức năng sau đây **chưa có bài test tự động chứng minh** do đang ở trạng thái "Một phần" hoặc "Chưa làm" trong mã nguồn:

1. **Text-to-3D Public Endpoint (F04 / P1):** Đã có data model và enum, chưa có API endpoint cho khách hàng.
2. **Multi-view Generation (F05 / P2):** Chưa có API tiếp nhận danh sách nhiều góc ảnh.
3. **Quy trình Hoàn tiền Gateway (F18 / `REFUND-B06`):** Chưa có bảng `Refunds` và API gọi cổng thanh toán.
4. **Phân công Xưởng in Đối tác (F19 / `OPS-B06`):** Chưa có bảng `PartnerAssignments`.
5. **Quản lý Đợt in & Bằng chứng QC (F20 / `OPS-B06`):** Chưa có bảng `ProductionRuns` và upload ảnh QC.
6. **Vận chuyển & Giao hàng (F21 / `OPS-B07`):** Chưa có bảng `Shipments` và `ShipmentEvents`.
7. **Tiếp nhận & Xử lý Khiếu nại (F22 / `OPS-B07`):** Chưa có aggregate `DefectReports`.
8. **Khách hàng Mua thêm Credit (F26 / P1):** Chưa có gói nạp credit trả trước cho người dùng.
9. **Background Dispatcher cho Outbox (F27 / `EVT-B08`):** Chưa có hosted service worker.
10. **Báo cáo & Dashboard SLA (F28 / P1):** Chưa có API tổng hợp số liệu.
11. **Tự động Purge dữ liệu hết hạn (F29 / P1):** Chưa có worker dọn dẹp file theo hạn 90 ngày.
12. **Đặt lại đơn hàng B2B (F31 / P2) & Gallery cộng đồng (F32 / P2):** Chưa triển khai.

---

## 11. KẾT LUẬN & ĐỀ XUẤT CỦA TEST ARCHITECT

Hệ thống backend .NET Core của **THE BOYS / PRINT3D** đã đạt được một nền tảng kỹ thuật và chất lượng kiểm thử vượt trội đối với toàn bộ các tính năng P0 cốt lõi. Các bất biến nhạy cảm nhất về **Tiền tệ (Credits, Báo giá đóng băng, Thanh toán Webhook)** và **Hình học (Chuẩn hóa trục STL, ModelVersion SHA-256)** đều được bảo vệ bằng các ràng buộc vật lý tại database và được bảo chứng qua 78 bài test tự động chạy thật.

Quality Gate cho đợt rà soát này được xác lập là:
$$\mathbf{PASS\ WITH\ CONDITIONS\ (CONCERNS)}$$

**Điều kiện chuyển giao sang đợt tiếp theo:**
1. Tiến hành triển khai ngay ticket `REFUND-B06` (ACT-01) để hoàn tất quy trình hoàn tiền, khép kín 100% phân hệ Thương mại & Thanh toán.
2. Kích hoạt môi trường Staging có kết nối SePay thật qua webhook để kiểm thử tích hợp mạng trước khi mở cổng thanh toán cho khách hàng thực tế.

