# Phase 6I-C — End-to-end workflow verification

Continuation completed **2026-10-12 (Asia/Manila)**. DEVELOPMENT / SYNTHETIC DATA ONLY.

**PHASE 6I-C DEVELOPMENT INTEGRATION: FAIL.** Mandatory Bill Adjustment verification found three reproducible integrity defects. **FULL APPLICATION SERVICE-WORKFLOW READINESS: BLOCKED. PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.** Phase 6I-B's earlier development PASS and historical permission failure remain valid for their recorded scope; this report does not erase them.

Production business logic, financial rules, authorization and dependencies were not changed. Changes in this phase are confined to test runners/fixtures/assertions and this report. No development financial/customer/configuration/user record, migration or seed was written. Destructive fixtures use local **tmcwd_test** only; development **127.0.0.1:3307/tmcwd** is read-only.

## 1. Actual workflow inventory and boundaries

The normal enabled path remains Razor/JavaScript → MVC → domain/shared HTTP transaction → Data controller → persistence service → MySQL. Flutter uses authenticated HTTP services and partitioned local SQLite; no direct database access or local maintenance calculator was introduced.

| Workflow | Actual connected implementation / storage | Classification and verification |
|---|---|---|
| Customer lookup/create/contact | CustomerSupport/Customer MVC, CustomerTransaction, CustomerController/CustomerService, strict customer commands, CustomerInformation/AddEditCustomer views; customers | IMPLEMENTED + ENABLED; existing HTTP/MVC security regressions PASS |
| Account creation/address/pending metadata | Account/CustomerSupport MVC, AccountTransaction, AccountController/AccountService, strict account commands; accounts | IMPLEMENTED + ENABLED; protected fields/overposting regressions PASS |
| Account activation/installation/transfer/disconnection | Dashboard and related Request/JobOrder operations, DashboardTransaction and customer-support transactions; accounts/requests/job_orders/application tables | IMPLEMENTED BUT QUARANTINED; BLOCKED BY POLICY |
| Reading Sheet template | ReadingSheet MVC, ReadingSheetTemplateTransaction, Data ReadingSheetTemplateController/Service, template DTO/entity; reading_sheet_templates | IMPLEMENTED + ENABLED; BillingOperations and validation regressions PASS |
| Sheet creation/assignment/progress/status | ReadingSheet MVC/views/reading-sheet scripts, ReadingSheetTransaction, ReadingSheetController/Service; reading_sheets/zone_books/accounts | IMPLEMENTED + ENABLED; service/API regression coverage, live full page sequence NOT VERIFIED |
| Reading entry/upload | Billing/ReadingSheet MVC, ReadingTransaction; ReadingController/ReadingService and mobile upload controller; readings | IMPLEMENTED + ENABLED; validation/assignment/retry regressions PASS |
| Raw Reading SaveRange | ReadingController.SaveRange | IMPLEMENTED BUT QUARANTINED; BLOCKED BY POLICY |
| PartialPost/completion | ReadingSheet MVC POSTs, ReadingSheetTransaction, Data ReadingSheetController and ReadingSheetService; billings/readings/invoice sequence | IMPLEMENTED + ENABLED; posting, retry, rollback and invoice allocation checks PASS |
| Mobile login/download/sync | auth_service.dart, reading_sheet_service.dart, local_database.dart; AuthController/MobileReadingSheetController → existing services | IMPLEMENTED + ENABLED; backend plus Flutter tests PASS; device NOT VERIFIED |
| Official mobile SOA/preview/cache | soa_screen.dart, OfficialSoa, official_soa_cache; mobile SOA endpoint and frozen invoice contract | IMPLEMENTED + ENABLED; server/Flutter assertions PASS |
| Bluetooth / provisional offline invoicing | Existing future roadmap; not implemented or exercised by this task | PARTIAL / NOT VERIFIED; no device or printer PASS claimed |
| Billing and Collections lookup | Billing/Collections MVC, BillingTransaction, Data BillingController/BillingService; CollectionBill and immutable snapshot DTOs; billings | IMPLEMENTED + ENABLED; obligation regressions PASS |
| Maintenance configuration/lookup | MeterMaintenanceRateTransaction, Data MeterMaintenanceRateController/Service and DTOs; meter_maintenance_rates | IMPLEMENTED + ENABLED API; deployed lookup PASS; dedicated MVC administration page NOT IMPLEMENTED in inspected flow |
| Bill Adjustment page/batch lines | BillingController.BillAdjustment/SubmitAdjustment, Views/Billing/BillAdjustment.cshtml, bill_adjustment.js, BillingAdjustmentTransaction, Data SaveBatch and BillingAdjustmentService; billing_adjustments/billings | IMPLEMENTED + ENABLED; normal flow PASS, mandatory retry gate FAIL |
| Single adjustment API | BillingAdjustmentController.SaveUpdate → BillingAdjustmentService.SaveUpdate | IMPLEMENTED + ENABLED API; missing-bill and atomicity gates FAIL; not the MVC page's batch submission path |
| Adjustment approval stage | No separate billing-adjustment approval/status transition in inspected controller/service/DTO | NOT IMPLEMENTED; no approval rule invented |
| Multi-bill adjustment batch | SaveBatch explicitly requires all lines to reference one bill | NOT IMPLEMENTED as a multi-bill contract; same-bill multi-line transaction tested |
| Penalty charging | Billing/Penalty views/scripts, PenaltyTransaction, PenaltyController/Service and LatePaymentPenalty | IMPLEMENTED + ENABLED; water-only/due-date/status/link tests PASS |
| Other Charge configuration/activation | Billing OtherCharges view/scripts, OtherChargeTransaction, OtherChargeController/Service; other_charges/other_charge_installments | IMPLEMENTED + ENABLED for supported routes; Phase 6G tests PASS |
| Service-origin charge creation | Dashboard/customer-support source workflows feeding charges | IMPLEMENTED BUT QUARANTINED at HTTP boundary; isolated source fixtures only |
| Collection payment/receipt/report | CollectionsController/views/scripts, PaymentTransaction, PaymentController/Service; payments/payment_receipts/remittances | IMPLEMENTED + ENABLED; Cash/Check/manual enabled online method, ownership and races PASS |
| Provider WebHook/EWalletTransaction mutation | Provider-specific Data controllers | IMPLEMENTED BUT QUARANTINED; no provider-trust bypass |
| Legacy Payment SaveUpdate / generic Billing SaveUpdate | Retired/denied generic mutation paths | LEGACY / BLOCKED BY POLICY; not used to settle bills |
| Cancellation/reprint | Collections MVC and PaymentController.CancelInvoice/Invoice, PaymentService; payments/invoice_cancellations/linked balances | IMPLEMENTED + ENABLED; adjusted and installment reversal PASS |
| Account Ledger | BillingController.AccountLedgerData, shared _AccountLedger partial/account-ledger.js, AccountLedgerService, AccountLedgerDto | IMPLEMENTED + ENABLED; cross-service reconciliation and browser tests PASS |
| Dashboard service summary/Charging/work orders | Admin/Dashboard UI, DashboardTransaction, ServiceDashboard/ServiceApplicationRecord, DashboardController service routes | IMPLEMENTED BUT QUARANTINED; live HTTP workflow BLOCKED BY POLICY; synthetic presentation tests PASS |
| Request / RequestDetail / RequestFile | CustomerSupport views, corresponding transactions, Data controllers/services; requests/request_details/request_files | IMPLEMENTED BUT QUARANTINED |
| JobOrder / inspection / recommendation / approval | Customer-support transactions and Data controllers/services; job_orders/inspection_reports/recommendations/approval_histories | IMPLEMENTED BUT QUARANTINED; isolated service harness PASS does not enable routes |
| Material / Inventory / Workflow | Customer-support/Engineering/Admin transactions, corresponding controllers/services; materials/inventories/workflows | IMPLEMENTED BUT QUARANTINED |
| Legacy service archive / sample reading UI | service_request_orders archive; reading-sheet-sample.js | LEGACY / UNUSED by authoritative posting; archive used only for synthetic migration fixtures |

Policies remain BillingOperations (roles 1/2/6/7), CustomerService (1/2/6), Collection (1/7), SuperAdmin (1), MeterReader (4 plus ownership), and PendingAuthorizationDecision (deny every role). The prior runtime inventory's **162 query routes**, **52 blocked query classifications** and **40 quarantined mutation routes** remain protected by the existing regression suite. Four blocked queries overlap previously quarantined mutating Dashboard GETs; these counts must not be summed as distinct routes. No SuperAdmin exception was added.

## 2. ServiceRequestRegression guard and fixture safety

`tests/ServiceRequestRegression/Program.cs` now requires **explicit SERVICE_REQUEST_CONNECTION**, local host and exact **tmcwd_test** before opening a connection. Application DefaultConnection/user-secrets fallback is removed. Missing, tmcwd, unknown database and remote-host configurations each return **exit 2 before database open**; all four rejection tests PASS.

The formerly schema-dependent harness now builds connection-local temporary tables from the existing EF model and the repository's legacy service-request CREATE TABLE artifact. It executes only extracted DDL, never recovered row data. Primary keys are retained; fixture collation is consistent with the development-compatible migration. Synthetic seed entities use EF rather than depending on legacy defaults.

Dashboard's repeated user aliases cannot reopen one MySQL temporary table. The harness owns a uniquely named **tm_srq_<GUID>_users** persistent fixture in tmcwd_test, seeds one synthetic operator, redirects EF user reads to it and drops only that owned table in finally. It does not read or change operational users. The rest of its migration/backfill/controller writes target temporary shadows. No startup migration or production service change was made.

**TEST HARNESS PASS: 44 assertions**, covering migration/rerun preservation, create/edit/completion, grouping/history guards, authoritative account links, installation duplicate prevention/snapshots, metadata preservation and three-table rollback. The harness directly invokes controller/service methods; **HTTP service workflow remains BLOCKED BY POLICY**. Direct calls are not proof that a denied route is usable.

## 3. Customer and Account

Existing 618-group regression coverage exercises customer/account HTTP and real MVC synthetic sessions: allowed contact/address/creation changes, strict overposting rejection, server-derived actor identity and anti-forgery, protected account number/customer ownership/zone-book-sequence/status/history fields. Issued billing and snapshot protections remain in the boundary fixtures. No live customer/account edit occurred in tmcwd.

Activation/installation and related service changes remain **BLOCKED BY POLICY**. A full authenticated Customer Information UI journey at all six viewport/theme combinations is **NOT VERIFIED** in this phase; source presence and synthetic command success are not a live page PASS.

## 4. Reading Sheet

Implemented template → sheet/assignment → reading → progress → post → completion source paths are traced through the existing MVC transactions and Data services. Existing tests protect valid reader assignment, range/roster restrictions, movement checks, duplicate/retry reading behavior, sheet locks/status guards, posting rollback and immutable invoice allocation. Status meanings remain 1 Created, 2 InProgress, 3 Completed, 4 Deleted, using shared enums.

Raw SaveRange stays denied. The full browser sequence creating a template through completion is **NOT VERIFIED**; synthetic API/service checks and enabled web source are recorded separately. Authoritative reading dates and issued consumption periods remain protected; no date/status rule was redesigned.

## 5. Flutter / Meter Reader

Actual Flutter routes are credential POST `api/auth/meter-reader/login`, assigned/download GETs, sheet reading-upload POST and official account SOA GET under `api/reading-sheet/mobile`. The client uses server-issued authorization and partitioned SQLite/cache repositories, preserving offline/pending work.

**Flutter PASS: 73 tests** across auth, download/persistence/local database, entry/sync/retry/status, account sync UI, SOA/cache and invoice preview. **Backend reader PASS: 43 assertions**, plus existing HTTP assigned-reader/download/SOA and persisted fingerprint checks in BillingRegression. No new local maintenance calculation or invoice reconstruction was added.

Three old preview assertions expected a visible QR payload string although production renders a QrImageView. Tests now require the existing QR image and exact server payload in the preview's SoaResult rather than adding plaintext to production rendering. Initial failures and an intermediate invalid test getter were corrected; final full suite passed. Production Flutter code is unchanged.

**DEVICE CHECK = NOT VERIFIED**, including real Android/network/session transitions and Bluetooth. A native-assets path-with-spaces failure was worked around with a temporary workspace drive alias; the alias was removed afterward. No SDK/package upgrade or cached reader data deletion occurred.

## 6. Billing / PartialPost

`--verify-development-deployment` reads the actual development configuration, verifies identity, then copies its rate into the existing tmcwd_test temporary fixture. It uses real PartialPost to assert water **100**, maintenance **20**, bill/remaining **120**, frozen snapshot components, unchanged Service Invoice identity on retry and one bill/fee. Existing rollback/invoice concurrency tests protect failed allocation and reading completion. Actual rate boundaries include **2025-01-01** and July 2025; **2024-12-31** fails closed.

No manually prepared development account/sheet was posted. This is deployed-value integration with isolated financial writes, not a live posting operation on tmcwd. Old invoice GETs keep issued snapshots and do not recalculate from current rates.

## 7. Meter Maintenance and read-only baseline

Final SQL confirms the reviewed UUID, port 3307, schema tmcwd and MySQL 8.0.46. Exactly one row remains **20.00 / 2025-01-01 / NULL**, audit IDs **5/5**; previous operator-confirmed ALREADY_PRESENT evidence remains attributed to the operator. No seed was executed here.

The previously recorded baseline and every raw issued snapshot digest remain identical: **30 billings, 230 readings, 16 sheets, 8 payments, 5 penalties, 0 Other Charges, 0 installments, 13 non-null snapshots**. Billing sums remain **6766.30 / 5027.30 / 717.00 / 34.00** for TotalBillAmount / RemainingAmount / BillArrearsAmount / BillingAdjustment. Development rate/configuration/financial records were read only.

## 8. Bill Adjustment — mandatory gate FAIL

The actual MVC page is `BillingController.BillAdjustment`, `Views/Billing/BillAdjustment.cshtml`, `bill_adjustment.js`; CSRF-protected SubmitAdjustment computes selected AsBilled − ShouldBe lines and calls BillingAdjustmentTransaction.SaveBatch. Data SaveUpdate/SaveBatch are BillingOperations-protected and derive the acting ID from validated claims. DTO is currently the shared/entity adjustment shape; no approval stage or multi-bill batch contract exists. SaveBatch means multiple lines for **one bill**.

Targeted `AdjustmentWorkflowVerification.cs` is opt-in with `--verify-adjustment-workflow`, uses only tmcwd_test and returns nonzero on any failed scenario. **Final targeted result: 4/7 PASS, 3 FAIL**:

| Scenario | Actual result |
|---|---|
| 120 bill → adjustment 30 → persisted obligation 90; Collections/Ledger; payment/cancellation | PASS; original identity/snapshot retained, BalanceBefore/restoration 90 |
| Real adjustment HTTP auth/validation/audit | PASS; anonymous 401, reader 403, malformed money 400 with no write; forged route/body creator 999 replaced by claim actor 1 |
| Single prior adjusted balance 90 + historical active penalty 17 → next arrears 107 | PASS; old snapshot unchanged |
| SaveUpdate nonexistent bill | **FAIL**; accepts MISSING reference and persists an orphan adjustment |
| Repeated identical SaveBatch | **FAIL**; two amount-30 rows, adjustment 60 and RemainingAmount 60 rather than stable 90 |
| SaveUpdate failure after its first SQL save | **FAIL**; adjustment row commits while bill remains 120 |
| SaveBatch injected partial failure | PASS; transaction restores all lines and bill |

Confirmed root causes are in **BillingAdjustmentService**: single save persists before checking billing and uses two saves without a encompassing transaction; batch serializes a bill with FOR UPDATE but has no request identity/duplicate protection. Row locking prevents interleaving/lost updates, not repeated-request duplication. These defects affect **enabled** APIs and the batch page flow, so they fail the financial gate rather than being labeled policy quarantines.

The preserved floor remains **max(20, TotalBillAmount − BillingAdjustment)**. Valid adjustments do not rewrite snapshot water/maintenance/invoice identity. Future non-20 maintenance values require separate Finance policy. No refund/negative-amount approval policy, approval stage, revised formula or fix was invented to make tests pass. Single-update reference/audit overposting and numeric range/scale limits warrant a separate remediation review; comprehensive adversarial coverage of those variants is **NOT VERIFIED** here. Multi-bill batching is unsupported, not silently tested as an approved feature.

Required follow-up: separately authorize a small integrity fix for missing-bill validation and atomic single save, and an approved retry/idempotency contract for adjustment submissions. Preserve signed financial rules and existing adjustment floor. The new tests intentionally expose failures until that work is completed.

## 9. Penalty

Existing calculation/API/service/browser checks PASS: **10% of water-only 100 = 10**, not 12; strict after-due rule; snapshot-based basis; paid/waived exclusion; active persisted penalties counted once; informational after-due reads create no rows; exact settlement/cancellation links. Maintenance, arrears, PCA, adjustment and Other Charges remain outside the penalty basis.

The new 107 arrears case uses a **historical persisted penalty fixture of 17** to test arrears addition; it does not calculate a new 17 penalty on a water-100 bill. New penalty calculation remains 10.

## 10. Other Charges

Phase 6G regressions PASS for enabled charge configuration, correct parent/child installment construction, first collectible child activation during posting, future child exclusion, no contract-principal collection, exact installment IDs and cancellation restoration. Other Charges/maintenance stay separate; no maintenance contract/installment row is inserted. Service-origin charging remains policy-blocked; isolated service fixtures do not expose those routes.

## 11. Collections

PASS within existing service/API/concurrency/browser fixtures: bill/account/invoice lookup, persisted adjusted balance, maintenance counted once, separate active penalty and collectible children, full-water-payment requirement, reference/receipt identity, Cash/Check/enabled manually recorded online method, duplicates/races, cashier ownership and immutable invoice reprint.

External EWallet provider callbacks/WebHook workflows remain **BLOCKED BY POLICY**; an enabled collection method does not certify provider trust. Retired generic Payment SaveUpdate is not used.

## 12. Cancellation

Existing tests plus the new adjusted-bill fixture PASS: restoration uses recorded **BalanceBefore 90**, not original 120, and preserves invoice number/snapshot. Existing penalty/installment reversal/race tests retain exact payment links and prevent duplicate collectible children/penalties. No development payment was canceled; all mutations were owned synthetic fixtures.

## 13. Account Ledger

PASS for Bills/Other Charges/History model and shared UI: original invoice identity/water/maintenance from snapshots, current persisted obligation, separate penalties, collectible children without contract/future overstatement, payment/cancellation history and available adjustment events. Actual BillingService.GetCollectionBill and AccountLedgerService.Get agree at **90** after adjustment. Ledger does not create a parallel current-rate financial calculation. The browser suite retains **31 ledger checks** across Collections and synthetic Dashboard entry points.

## 14. Dashboard / reports

Inspected ServiceDashboard's implemented counts are OnlineApplications, NewRequests, Inspection, Charging, Payment, Installation, Connect, Tapping, Tickets and RequestSummary; they are **service category/application counts**, not generic billing monetary totals or reading progress. ServiceApplicationRecord carries workflow/charge details. Do not reinterpret Charging COUNT as installment count.

Actual ServiceSummary/ServiceApplications and mutating Dashboard service GETs remain quarantined. **Live Dashboard service metrics/workflow: BLOCKED BY POLICY.** Pure browser/source charge presentation reconciles collectible child **250** without collecting parent contract **1000**. This proves presentation against synthetic responses, not access to quarantined Data routes. Dedicated total-billing/collection/paid-unpaid/overdue/account/reading-progress metrics are not established by this dashboard contract and are **NOT VERIFIED** as separate live reports. Enabled Collections reporting is covered by its service/concurrency ownership checks, not a full interactive export audit.

## 15. Service workflow

Request, detail, file, job order, inspection, recommendation, approval, materials/inventory and account activation/registration/installation flows remain **BLOCKED BY POLICY** at their relevant Data boundary. No arbitrary role assignment or SuperAdmin bypass was added. Legacy archive compatibility is service-harness-tested only. Existing installation/account linkage and direct service charge construction are tested in isolated fixtures; not every service request creates a financial charge and no universal charge rule is assumed.

**FULL APPLICATION SERVICE-WORKFLOW READINESS: BLOCKED**, despite direct service harness PASS. Missing organizational role/ownership/approval/provider policy must be resolved before these workflows can be considered enabled.

## 16. Cross-module reconciliation

For the new single-account adjustment fixture, original principal is 100 water + 20 maintenance = 120; adjustment 30 makes authoritative current principal **90**. Billing RemainingAmount, Collections AmountDue and Ledger TotalBalance each equal 90: **difference 0**. Payment then cancellation restores exactly 90, with frozen issued components intact. The next-period case freezes **90 + 17 = 107** arrears without recursively adding snapshot TotalDue or old arrears.

Existing Phase 6G scenarios reconcile separate child collectibility, e.g. prior principal/penalty obligation **397** plus collectible child **250 = 647**, excluding future contract balances. Maintenance is already included in persisted principal and is never added again. Dashboard synthetic presentation matches child collectibility; live service metrics cannot be used as a reconciliation source while quarantined.

The **duplicate adjustment retry** intentionally demonstrates a nonzero unexplained business difference (90 expected, 60 actual). The single-save orphan/partial-commit cases demonstrate corruption risks even without a balance mismatch in a happy path. Consequently cross-module happy-path agreement cannot yield overall PASS.

## 17. Browser / UI verification

Actual Collections Razor body/scripts and shared Ledger fixture PASS at **1440×900, 768×1024 and 390×844**, **light and dark**: six cases, dialog and all tab controls remain inside the viewport and usable. Tests set the application's data-theme attribute as well as emulated color preference. They also retain loading/error/empty state, safe text, validation, charge collection/cancellation and Escape/modal checks. A harness navigation race was fixed by waiting for the Dashboard document/scripts to finish loading before clicking.

These are **synthetic browser fixtures**, not a live authenticated full-site session. Existing real MVC synthetic-session boundary tests exercise authorization/CSRF and customer/account bridges, but do not prove every page's layout. Full authenticated Customer Information, Reading Sheet, Billing, Adjustment, Penalty, Other Charges, Dashboard and maintenance-administration journeys at all requested viewport/themes remain **NOT VERIFIED**. Meter Maintenance has a protected API but no dedicated MVC admin page in the inspected flow. No claim that 401/403 presentation is polished or that every table/modal is accessible is made without that evidence.

## 18. Concurrency

Existing invoice/posting allocation, payment/receipt/cashier conflicts and installment locking/reversal checks PASS using uniquely prefixed disposable tables in tmcwd_test, with owned cleanup. They protect rollback and duplicate identities. Adjustment SaveBatch source uses a billing-row lock; explicit simultaneous adjustment requests/payment-versus-adjustment races were **NOT VERIFIED**. Repeated identical requests are verified and **FAIL**. Single-save transactional failure is verified and **FAIL**. Do not equate receipt/posting concurrency PASS with adjustment integrity.

## 19. Commands and exact results

The running Data API locked ordinary project outputs. Builds therefore use **--artifacts-path .tmp/phase6ic-artifacts**, preserving the running Application/Data processes. This changes output location, not production code or test database scope.

| Executed check | Actual final result |
|---|---|
| Solution build | PASS; 0 warnings/errors in isolated incremental run |
| BillingRegression build | PASS; 0 errors, 1 existing synthetic PaymentConcurrency EF1002 warning |
| BillingRegression run with --verify-development-deployment --verify-adjustment-workflow | **Exit 1 overall**; existing **618** temporary-table groups PASS, four migration/concurrency groups PASS; mandatory targeted **4 PASS / 3 FAIL** |
| Browser regression | PASS **25**, 0 skipped |
| Penalty browser | PASS **5**, 0 skipped |
| Collections / Ledger browser | PASS **8 scenarios / 31 checks**, related checks and **6 viewport/theme cases** |
| MeterReaderAuthRegression build/run | PASS; 0 warnings/errors; **43 assertions** |
| ServiceRequestRegression build/run | PASS; 0 warnings/errors; **44 assertions**, plus **4 guard rejections** |
| Flutter test --no-pub | PASS **73** after test-only QR assertion correction |

The 618 summary prints before optional adjustment probes; **it does not mean the combined run passed**. New targeted successful groups are separately reported, not silently folded into that historical count. The four invoice/payment migration groups also remain separate counts. No skipped check is counted as PASS.

Initial failed builds/fixture setup/native path/QR assertions/browser readiness attempts are not final PASS evidence. Fixtures were corrected without changing production services. Intermediate new-test compile ambiguities were fixed. Earlier nullable/EF warnings remain historical technical debt; warnings were not suppressed. Exact local logs use `.tmp/phase6ic-*` (solution, billing-build/billing, reader-build/reader, service-build/service, browser, penalty, collections, flutter). These local artifacts are not production recovery evidence.

## 20. Modified files and remaining gaps

Phase changes: `tests/ServiceRequestRegression/Program.cs` (explicit guard, synthetic setup/cleanup/count); `tests/BillingRegression/AdjustmentWorkflowVerification.cs` (new opt-in integrity scenarios); `tests/BillingRegression/Program.cs` (opt-in invocation); `tests/BillingRegression/collections-browser-regression.cjs` (viewport/theme checks and readiness wait); `TMCWD.MeterReader.Mobile/test/service_invoice_preview_test.dart` (QR assertions); this report. Existing unrelated dirty work remains intact.

Remaining: three adjustment defects, numeric/audit/reference adversarial variants, simultaneous adjustment races, complete authenticated UI matrix, full web sheet lifecycle, live credential/Application bridge, actual Android/Bluetooth and enabled service policy/workflows. No financial formula/security rule was weakened. No new business approval or production-style administrator bootstrap was created.

## 21. Development verdict

**PHASE 6I-C DEVELOPMENT INTEGRATION: FAIL.** Billing/maintenance/penalty/Phase 6G/reader boundaries and valid adjusted financial reconciliation pass, but enabled adjustment requests can create orphan data, duplicate liability and partial commits. These are mandatory integrity failures, not optional UI gaps or policy quarantine failures. Preserve the failing tests and authorize focused remediation before reevaluating this gate.

**FULL APPLICATION SERVICE-WORKFLOW READINESS: BLOCKED.** Quarantined implementations cannot be counted as enabled functional success; their denial remains protected.

## 22. Production qualification and stop

**PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.** Development mock administrator/rate/fixtures provide no organizational production authorization. Production still requires independently restored/reconciled recovery, legitimate administrator, target/runtime/workflow verification, resolved integrity gates and explicit deployment authorization. No production deployment, retired seed, Git reset/clean or development financial write occurred. Work stops after verification and this report; Phase 6I-C is not declared complete/PASS.

## 2026-10-12 Phase 6I-C1 remediation continuation

This supplements the original three FAIL findings; it does not erase their historical results. See [Phase6I-C1-Bill-Adjustment-Integrity-Remediation.md](./Phase6I-C1-Bill-Adjustment-Integrity-Remediation.md).

**PHASE 6I-C1 BILL ADJUSTMENT REMEDIATION: PASS for source/tests.** Single and batch now share one explicit transaction and parameterized billing-row lock before adjustment writes. Missing bills return 404 with no orphan; single and batch failures roll back lines, balance and operation receipt together. Explicit Idempotency-Key GUIDs and a uniquely keyed receipt store make identical retries return their original result; different intentional keys can apply equal amounts. Creation audit/reference cannot be overwritten or moved; actor/updater remain claim-derived. MVC, shared HTTP service and JavaScript preserve the key, disable in-flight submits and retain uncertain retries, with server acknowledgement permitting a fresh intentional submission.

The additive 20261012_add_billing_adjustment_operations.sql was applied/rerun only through owned synthetic table mappings in tmcwd_test. It was NOT applied to development tmcwd; operation table count there is 0. Coordinated development schema/API/MVC rollout requires separate authorization. No startup migration, historical adjustment backfill or amount-based deduplication was introduced.

Final verification: 618 baseline database groups and the original four migration/concurrency groups PASS; original targeted 7/7 PASS, plus 3 added groups (10/10 total); four new adjustment/payment race cases and migration rerun PASS. Browser 25, penalty 5, adjustment UI retry 4, Collections 8 scenarios, Ledger 31 plus related checks and six viewport/theme cases PASS; reader 43 and guarded service 44 assertions PASS. Builds PASS; final solution retains 44 existing nullable warnings and BillingRegression retains one EF1002 warning. Flutter was NOT rerun because its production source/contracts were unaffected; prior 73-test evidence is retained, not represented as a new result.

Valid adjustment remains 120 - 30 = 90 in Billing/Collections/Ledger; payment cancellation restores adjusted BalanceBefore 90. Next-period single-prior fixture remains 90 + 17 persisted penalty = 107; snapshots/invoice identities remain immutable. Water-only penalties, PHP 20 floor, maintenance and Phase 6G separation remain intact. Development baseline counts/sums/raw snapshot hashes are unchanged. Signed negative corrections preserve existing behavior; additional debit/approval/priority/future-rate policy requires Finance decisions rather than guessed rules.

The overall financial regression gate was rerun and passes in isolated fixtures, but **overall Phase 6I-C development rollout after C1 is NOT VERIFIED** pending the separately authorized table/coordinated live rollout. This does not declare overall Phase 6I-C PASS solely from remediation. Full application service-workflow readiness remains BLOCKED BY POLICY; production remains DEFERRED / NOT EVALUATED. No development financial/customer/user/configuration write, production deployment or Git reset/clean occurred.

## 2026-10-12 Phase 6I-C1 Audit History continuation

This supplements the Phase 6I-C1 remediation entry above and the original three FAIL findings. It does not erase any historical result. See [Phase6I-C1-Bill-Adjustment-Integrity-Remediation.md §16](./Phase6I-C1-Bill-Adjustment-Integrity-Remediation.md).

**BILL ADJUSTMENT AUDIT PERSISTENCE: PASS.** Every successful adjustment now produces a permanent, immutable `billing_adjustments` row with `CreatedBy`, `DateCreated`, `UpdatedBy`, `DateUpdated`, and the new nullable `Reason` field. Reason is combined from the existing MVC Remarks dropdown and Explanation textarea, capped at 255 chars, excluded from the idempotency hash. Historical rows with NULL Reason remain valid.

**BILL ADJUSTMENT USER-FACING HISTORY: PASS (synthetic-fixture scope; live authenticated page NOT VERIFIED).** Staff on the Bill Adjustment page now see an Adjustment History section that appears after billing selection and displays Date, Service Invoice/Reference, Type, Amount, Reason, Made By, and Last Updated. The section handles loading, error, empty, and populated states with safe text rendering. The history fetch is wired to billing selection and does not mutate financial state.

**PHASE 6I-C1 SOURCE/TEST REMEDIATION: PASS.** 21/21 adjustment workflow scenarios (including new A–K audit history cases) PASS. 12/12 adjustment browser tests PASS. All other regression baselines unchanged and passing.

**DEVELOPMENT SCHEMA ROLLOUT: NOT DEPLOYED / NOT AUTHORIZED.** `20261012_add_billing_adjustment_reason.sql` and `20261012_add_billing_adjustment_operations.sql` have been applied only in tmcwd_test synthetic fixtures. Neither migration has been applied to development tmcwd. Coordinated deployment of both migrations together with the current source code requires separate authorization before adjustment writes succeed against the deployed application.

**OVERALL PHASE 6I-C DEVELOPMENT INTEGRATION: NOT YET VERIFIED.** The development operation table and reason column remain absent; a coordinated live rollout/check is pending. Phase 6I-C is not declared PASS from source remediation and isolated tests alone.

**FULL APPLICATION SERVICE-WORKFLOW READINESS: BLOCKED BY POLICY** (unchanged).

**PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED** (unchanged).

Final counts for this continuation: solution build PASS 0 errors; 619 DB checks PASS; 21/21 adjustment workflow PASS; 4 concurrency PASS; 12/12 adjustment browser PASS; 25 main browser PASS; 5 penalty browser PASS; 31 ledger checks + 6 viewport PASS; 43 reader assertions PASS; 44 service assertions PASS. Development baseline sums/counts/snapshot hashes remain unchanged. No development financial write, production deployment, Git reset, or schema application to tmcwd occurred.

## 2026-10-12 Phase 6I-C2 — Post-migration development verification

This supplements all prior entries. The stakeholder manually applied both reviewed C1 migrations to development `tmcwd` on 2026-10-12. This task verified the result. No source changes were made. See [Phase6I-C1-Bill-Adjustment-Integrity-Remediation.md §17](./Phase6I-C1-Bill-Adjustment-Integrity-Remediation.md) for full detail.

**Schema deployment confirmed:** `billing_adjustments.Reason VARCHAR(255) DEFAULT NULL` present; `billing_adjustment_operations` table present; both match reviewed migration artifacts exactly. Operation receipt count = 0 (no live adjustment submissions since deployment). One pre-existing historical adjustment row has `Reason = NULL` — legitimate.

**Financial baseline: UNCHANGED.** All counts, sums (TotalBillAmount 6766.30 / RemainingAmount 5027.30 / BillArrearsAmount 717.00 / BillingAdjustment 34.00), and all 13 `InvoiceSnapshotJson` SHA-256 hashes are identical before and after all regression runs.

**Solution build: PASS, 0 errors.** No entity-mapping or schema-mismatch errors introduced by Reason column or GetHistory implementation.

**Application compatibility: PASS (isolated fixture scope).** In-process HTTP host backed by tmcwd_test EF model (including new schema elements) returned correct 401/403/200 for History endpoint — no missing-table or unknown-column errors.

**Auth boundaries verified:** anonymous → 401, MeterReader → 403, BillingOperations → 200 for `GET api/BillingAdjustment/History/{ref}`.

**Live authenticated UI: NOT VERIFIED.** No safe known development credential available.

**Regression counts:**

| Suite | Result |
|---|---|
| BillingRegression DB | **619 PASS** |
| Adjustment workflow | **21/21 PASS** |
| Adjustment concurrency | **4/4 PASS** |
| Adjustment browser | **12/12 PASS** |
| Main browser | **25 PASS** |
| Penalty browser | **5 PASS** |
| Collections/Ledger browser | **31 checks + 6 viewport PASS** |
| MeterReaderAuth | **43 PASS** |
| ServiceRequest | **44 PASS** |
| Flutter | **NOT RERUN** (no contract change; prior 73-test evidence retained) |

**BILL ADJUSTMENT AUDIT PERSISTENCE: PASS**

**BILL ADJUSTMENT USER-FACING HISTORY: PASS (synthetic/API scope)**

**PHASE 6I-C1 SOURCE/TEST REMEDIATION: PASS**

**DEVELOPMENT SCHEMA ROLLOUT: PASS**

**LIVE AUTHENTICATED ADJUSTMENT UI: NOT VERIFIED**

**OVERALL PHASE 6I-C DEVELOPMENT INTEGRATION: PASS.** All mandatory gates from the Phase 6I-C2 specification are satisfied. This is the first time Phase 6I-C can be recorded as PASS at development integration level.

**FULL APPLICATION SERVICE-WORKFLOW READINESS: BLOCKED BY POLICY** (unchanged — quarantined service workflows remain denied).

**PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.**
