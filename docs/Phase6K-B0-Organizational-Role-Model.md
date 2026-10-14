# Phase 6K-B0 — Organizational Role Model

Date: **2026-10-14 (Asia/Manila)**. Reference: [Phase 6K-A discovery](Phase6K-A-Legacy-Finance-Discovery.md). This is a source/authorization role update, not DCM/PCA implementation or a development/production deployment.

## 1. Canonical values and compatibility

Previous committed enum:

```csharp
public enum UserRole
{
    SuperAdmin = 1,
    CustomerRepresentative = 2,
    Engineer = 3,
    MeterReader = 4,
    Guest = 5,
    CustomerServiceOic = 6,
    Cashier = 7
}
```

New [canonical enum](../TMCWD/TMCWD.Model/Enumerations.cs):

```csharp
public enum UserRole
{
    SuperAdmin = 1,
    CustomerRepresentative = 2,
    Engineer = 3,
    MeterReader = 4,
    Guest = 5,
    CustomerServiceOic = 6,
    Billing = 7,
    BillingHead = 8,
    GeneralServices = 9,
    Accounting = 10
}
```

Values 1–7 are unchanged; 7 is renamed, not migrated. No alias named Cashier, BillingStaff, Collector or CashierHead was added. New values are explicit and unique. The starting working tree already contained an enum edit with the new numeric values plus names `CustomerService` and `GeneralService`; those names were aligned with the attached stakeholder contract (`CustomerRepresentative`, `GeneralServices`). Other starting changes, including the Phase 6K-A document, were preserved.

Before source changes, repository role declarations/conversions/seeds and development role distribution were inspected. No other canonical convention using 8–10 was found; no development user had those values. No conflicting number was silently reassigned.

## 2. Repository Cashier inventory

Every `UserRole.Cashier` source reference was inventoried before replacement. Source search included C#/Razor/JavaScript/Dart/SQL, tests, tools, claims/session handling, role comparisons/serialization, role-management callers and prior reports; generated/third-party assets were excluded from interpretation.

| Occurrence / location | Classification | Action |
|---|---|---|
| `Enumerations.cs`, Data authorization membership | ROLE NAME | Billing=7; explicit BillingHead membership below |
| MVC `HomeController` login switch | ROLE NAME | Billing destination preserved; BillingHead also enters Collections |
| MVC `CollectionsController.OnActionExecuting` | ROLE NAME | Explicit Billing/BillingHead/SuperAdmin check |
| MVC `BillingController.PostPenaltyCharges` role check | ROLE NAME | Existing allowed roles preserved; BillingHead added |
| `_InsideLayout.cshtml` Collections visibility | ROLE NAME | Billing/BillingHead/SuperAdmin; no visibility-based authorization |
| Administration `UserTransaction.GetRoles` | ROLE NAME | Ten canonical choices with readable labels and numeric values |
| ServiceInvoice API authorization comment | ROLE NAME | Documents Billing/BillingHead/SuperAdmin |
| BillingRegression and MeterReaderAssignedRegression enum references | ROLE NAME | Renamed and expected role sets updated |
| Numeric policy lists in regression hosts | ROLE NAME | Replaced duplicate definitions with the production configuration |
| Historic docs' Cashier role/policy descriptions | Historical ROLE NAME | Retained as dated evidence; this report supersedes current role names/memberships |
| `InvoiceRecord.CashierId/CashierName`, `GetByCashier` routes/services, receipt print label | BUSINESS TERMINOLOGY | Preserved; actor/collector metadata and existing external contracts |
| CurrentCollector, cash-handling/remittance, cashier session/error messages, cashier totals/test actors | BUSINESS TERMINOLOGY | Preserved cashiering activity and ownership terminology |
| Cashier labels in synthetic receipt fixtures | BUSINESS TERMINOLOGY | Preserved; not canonical enum aliases |

No browser/Dart/SQL canonical string Cashier claim or persisted role-name migration was required. Role assignments are integer values. Unrelated DM/CM CSS prefixes represent disconnection/collection monitoring. DCM module labels remain unchanged.

## 3. Organizational responsibilities

| Role | Current supported boundary | Confirmed future responsibility / limitation |
|---|---|---|
| SuperAdmin (1) | Existing explicit permissions preserved | May create and approve/post the same memo: sole confirmed self-approval exception |
| CustomerRepresentative (2) | Existing customer-facing and CustomerService permissions preserved | No additional financial supervisory authority |
| Engineer (3) | Existing engineering permissions preserved | Full service workflow finalized in Phase 6M; no invented authority |
| MeterReader (4) | Existing authenticated mobile/assignment boundary preserved | No web Collection authority |
| Guest (5) | Existing restricted boundary preserved | No operational financial authority |
| CustomerServiceOic (6) | Existing CustomerService and Phase 6J physical confirmation authority preserved | Separate from BillingHead; no role merger |
| Billing (7) | Former Cashier's Billing/Collection capabilities preserved | Billing and cashiering are one operational role; future memo create/submit, no approval/posting authority |
| BillingHead (8) | Explicit supervisory inheritance of Collection/BillingOperations; existing narrower checks still apply | Future memo review/approve/reject/post/reversal; cannot self-approve/post own memo |
| GeneralServices (9) | No financial or material workflow permission added | GSO approved-material issuance in Phase 6M; not a financial role |
| Accounting (10) | No financial mutation or operational collection permission added | Financial read/review/reporting access in Phase 6N, individually scoped |

Accounting and GeneralServices have canonical identities and authorized admin assignment choices, but no new portal destination or operational permissions. Existing Home login behavior clears sessions for roles without a configured destination and reports that limitation. This task does not create empty portals or send these roles into the mutable Admin dashboard. Their portal/access work is deferred to 6N/6M. No real employee was assigned a new role.

## 4. Authorization changes and unchanged policies

[AuthorizationPolicies.Configure](../TMCWD/TMCWD.Data/AuthorizationPolicies.cs) contains the existing Data policy registrations. [Data Program.cs](../TMCWD/TMCWD.Data/Program.cs) and three regression hosts call the same configuration, eliminating duplicated policy memberships that could drift. No hierarchy, numeric `role >= ...` rule, universal bypass or new permission framework was introduced.

| Policy | Before | After | Purpose / boundary |
|---|---|---|---|
| Collection | SuperAdmin(1), Cashier(7) | SuperAdmin(1), Billing(7), BillingHead(8) | Existing operational collection reads/writes, subject to unchanged action/ownership guards |
| BillingOperations | SuperAdmin(1), CustomerRepresentative(2), CustomerServiceOic(6), Cashier(7) | SuperAdmin(1), CustomerRepresentative(2), CustomerServiceOic(6), Billing(7), BillingHead(8) | Existing approved billing reads/commands; no quarantined workflow enablement |
| CustomerService | 1,2,6 | **Unchanged: 1,2,6** | Customer/account-service operations |
| ConfirmDisconnection | 1,6 | **Unchanged: 1,6** | Phase 6J physical confirmation; BillingHead denied |
| ConfirmReconnection | 1,6 | **Unchanged: 1,6** | Phase 6J physical confirmation; BillingHead denied |
| MeterReader | 4 | **Unchanged: 4** | Reader endpoints plus current-user/assignment/roster checks |
| SuperAdmin | 1 | **Unchanged: 1** | Administration/configuration boundary |
| PendingAuthorizationDecision | Authenticated and always false | **Unchanged: denies all roles** | Anonymous 401; all ten authenticated roles 403 |
| Fallback | Authenticated | **Unchanged** | Authentication alone grants no named operation policy |

There is no Accounting or GSO membership in Collection/BillingOperations, no new financial-write policy for them, and no DCM create/approval policy enabled. Appendix A lists every current API route carrying a changed policy, including HTTP methods. Existing secondary policies/service role checks still apply even where BillingOperations is the outer boundary.

### Route review and narrower security

| Route family | Read / mutation | Role update effect / preserved limitation |
|---|---|---|
| Account/Customer billing lookup, Billing reads/ledger/projection, Reading/ReadingSheet/roster/template/ZoneBook, rates/types/directory | Mostly GET; roster bulk read POST | Billing=7 preserved; BillingHead=8 approved supervisory billing access; customer mutations still require CustomerService |
| Reading save, sheet save/complete/post/zone progress, template save/deactivate | POST/PUT | BillingHead uses existing validated billing operations; mobile assignment is separate; SaveRange remains quarantined |
| Billing ChargeAnchor | POST | Existing zero-value account-linked anchor command; not generic bill mutation |
| BillingAdjustment SaveUpdate/SaveBatch | POST | Existing signed correction/idempotency/claim-actor flow; no new formula or DCM posting |
| OtherCharge save/deactivate/post-to-collection | POST/PUT | Existing contract/installment behavior only; no new charges or service fee changes |
| Payment ProcessCollection / OtherCharge ProcessPayment | POST | BillingHead inherits operation entry; amounts, claims, receipt allocation and transaction behavior unchanged |
| Payment CancelInvoice / GetByCashier / date-range reports | POST / GET | BillingHead does **not** inherit SuperAdmin-only cross-collector access; existing actor/ownership filters remain |
| Payment SaveUpdate | POST | Still rejects raw payment entry with conflict; role membership does not revive retired writes |
| AdvancePayment/PaymentCheck/BatchPaymentUpload/CollectionRemittance | POST and GET reads | Existing normal Collection operations; current ownership/validation rules retained |
| Penalty post/save/read | POST/GET | Existing water-only rules retained |
| Penalty SubmitWaiver/ReviewWaiver | POST | Outer BillingOperations now includes head; service still enforces CSR/SuperAdmin submit and OIC/SuperAdmin review; no BillingHead waiver authority |
| MeterMaintenance configuration reads | GET | Existing billing access only; maintenance configuration writes remain SuperAdmin |
| DCM, generic Billing SaveUpdate, Dashboard service reconciliation, material/inventory/service mutations | Mixed | PendingAuthorizationDecision untouched; all new roles denied |

The only production changes to existing operation checks are canonical name replacement and explicit role 8 addition at the two policies and their matching MVC entry/penalty/navigation/login consumers. No financial/service persistence implementation was changed.

## 5. Future DCM contract — approved, not enabled

| Operation | Future roles | Identity / maker-checker requirement |
|---|---|---|
| Create Debit/Credit Memo, submit | Billing, BillingHead, SuperAdmin | CreatedBy from validated claims |
| Review/approve/reject/post approved memo | BillingHead, SuperAdmin | ApprovedBy/PostedBy from claims; non-SuperAdmin creator cannot approve/post own memo |
| Future approved reversal | BillingHead, SuperAdmin | Detailed reversal policy/integrity still requires implementation |
| Posted history/report | Accounting only when implemented and scoped, plus approved operational reads | Read/report contract; no write membership implied |
| GSO memo access | None | No DCM authority |

**SuperAdmin exception:** SuperAdmin may create AND approve/post the same memo. BillingHead identity must differ from the creator even when another BillingHead creates the memo. Billing cannot approve/post. This is the new stakeholder contract; the Phase 6K-A report's previously unresolved role/maker-checker ownership is resolved to this extent, while finance formulas/reference/reversal/integrity work remains outstanding.

DCM API still has PendingAuthorizationDecision on Get, GetAll and Save; its route-controlled legacy actor remains unsafe and disabled. No memo table/script deployment, posting, amount formula, approval endpoint, historical snapshot mutation or PCA implementation was introduced. Future identity must never be authorized by `/Save/{userId}`, body CreatedBy/ApprovedBy/PostedBy or a client-supplied role.

## 6. Accounting report inventory and deferral

| Existing source / terminology | Current implementation | Accounting decision |
|---|---|---|
| BAM Date / BamDate in `Views/Billing/BillAdjustment.cshtml` and its view model | Field in existing Bill Adjustment page; no dedicated BAM report or authoritative acronym expansion found | **ACCOUNTING ACCESS DEFERRED TO PHASE 6N**; do not invent an expansion |
| DCM labels and Debit/Credit Memo module | Pending entry/history shell, denied API, absent deployed memo table as discovered in 6K-A; no DCM report | **ACCOUNTING ACCESS DEFERRED TO PHASE 6N** |
| Bill Adjustment history / account Ledger | Implemented financial reads under BillingOperations with current operational data scope | Potential future Accounting read-only policy; no shared write-policy membership |
| CollectionReportPdf/CollectionReportExcel, payment/receipt reports/reprints | Implemented Collection MVC/controller routes, POST print/export or GET reads, with existing collector scope and operational role gate | Future route-specific read/report permission; not granting Accounting the entire Collection controller |
| Dashboard service summaries/workflow/reconciliation | Several Data reads/writes are PendingAuthorizationDecision; MVC Admin is not an Accounting reporting portal | Keep quarantined; financial reporting access in 6N |
| Accounts Receivable/Billing reports | No dedicated implemented report route found by requested source inventory | Implement only in 6N; no placeholder enablement |

Stakeholder policy supports eventual Accounting read/review/printing/export of the listed financial reports. No Accounting authorization change was needed here: the available operational policies also allow mutations, report ownership/access needs a narrower boundary, and missing/quarantined reports cannot be enabled by an enum update. No new report implementation or BAM expansion was invented.

## 7. GSO material contract and deferral

`MaterialController` Get/GetAll/GetByJobOrderId/SaveUpdate/UpdateQuantityOrNewUnitCost/Delete and `InventoryController` reads/writes remain quarantined by PendingAuthorizationDecision. No stable GSO issuance permission or approved request/Job Order ownership lifecycle was found. **GENERAL SERVICES POLICY DEFERRED TO PHASE 6M.**

Future GSO scope: approved material requests, relevant inventory/Job Order reads, quantity issued, recipient, release timestamp/history. Actor identity must be claim-derived. Approval, request/issuance/receipt ownership, Job Order relationships and inventory integrity must be defined before enabling. No material issuance or inventory mutation was added, and GSO receives no customer balance, collections, financial approval, report or disconnection authority.

## 8. JWT, session, parsing and freshness

- `AuthController` and `ApiClientRegistration` issue numeric role claims, such as `"7"`, rather than `"Cashier"`/`"Billing"`.
- `PersistedTokenValidation` requires exactly one identity/role/stamp, parses numeric role and validates enum membership, committed persisted role, active/verified status, signing rules and current security stamp.
- `UserSecurityStamp` hashes the **numeric** role alongside other security fields. Renaming enum member 7 changes neither the stored fingerprint nor valid numeric tokens/sessions. Assigning an actual different role still changes the stamp and invalidates stale identity.
- MVC serialized User.Role is nullable int. Role dropdown values and Data UserProfile responses remain numeric. Enum values/names and numeric JSON/session serialization round-trip for all ten roles.
- No JsonStringEnumConverter, Cashier string-role compatibility alias or stale-session bypass was added. String-role claims fail the existing numeric boundary; reauthentication remains the response to stale/invalid security state.
- Existing role-4 secure/mobile session semantics, reader assignment and authorization are unchanged. No mobile source/database change is required.

No blanket token logout is required for unchanged persisted Role=7, and no role-update migration is needed. The running development hosts were not restarted/deployed; source changes take effect only when the updated build is deployed.

## 9. Development users — read-only evidence

Before edits, metadata/role queries against **127.0.0.1:3307/tmcwd** ran in a read-only transaction. Only aggregate Role/count/active/verified counts were returned; no credentials, names, email or employee assignment data was recorded.

| Persisted Role | Canonical interpretation after change | Users | Active | Verified |
|---|---|---:|---:|---:|
| 1 | SuperAdmin | 1 | 1 | 1 |
| 2 | CustomerRepresentative | 3 | 3 | 0 |
| 3 | Engineer | 1 | 1 | 0 |
| 4 | MeterReader | 5 | 5 | 5 |
| 5 | Guest | 0 | 0 | 0 |
| 6 | CustomerServiceOic | 1 | 1 | 1 |
| 7 | Billing, formerly canonical Cashier | 2 | 2 | 2 |
| 8 | BillingHead | 0 | 0 | 0 |
| 9 | GeneralServices | 0 | 0 | 0 |
| 10 | Accounting | 0 | 0 | 0 |

The existing canonical mapping plus explicit stakeholder organizational confirmation establishes the intended interpretation of Role=7. Aggregate database records do not independently certify employees' job duties. No user role/verification/activation/password was changed in development, no grants changed, and no new office assignment was guessed.

## 10. User management and security

[UserTransaction.GetRoles](../TMCWD/TMCWD.Administration/UserTransaction.cs) supplies the existing authorized `AddEditUser` Razor select with all ten requested labels: Super Admin, Customer Representative, Engineer, Meter Reader, Guest, Customer Service OIC, Billing, Billing Head, General Services, Accounting. Each value is its unchanged/explicit numeric role. No canonical Cashier option remains.

[UserWriteRequest](../TMCWD/TMCWD.Model/Administrator/UserCommands.cs) had a hardcoded 1–7 validation limit; it now accepts the confirmed 1–10 range using enum constants. Identity/name/email/role bounds and unmapped-field protections remain. [UsersController](../TMCWD/TMCWD.Data/Controllers/UsersController.cs) still requires SuperAdmin policy plus a persisted active verified SuperAdmin actor; it derives actor from claims and preserves peer-admin/self-demotion/deactivation protections. Ordinary users cannot assign themselves the new roles or SuperAdmin. No existing role-management authority was broadened.

Synthetic test role assignments are confined to the existing connection-local temporary `users` fixture in `tmcwd_test`; they do not provision any development user.

## 11. Verification

Build/test artifacts and synthetic browser screenshots/logs are isolated under ignored `.tmp`; running development services were not stopped. Initial sandbox-denied generated-file writes were retried with approved access. Existing solution warnings were not suppressed.

Focused added/updated coverage:

- Exact enum values 1–10, no aliases/duplicates, persisted-seven mapping, enum/JSON/session round trips and admin labels/values.
- Every production policy for every role plus anonymous denial; explicit string-role alias denial; numeric stamp compatibility and role-change freshness.
- Real HTTP numeric JWT/persisted-user boundaries for Billing/BillingHead/GSO/Accounting; stale role change rejection; DCM read quarantine and self-promotion denial.
- SuperAdmin-only new role assignment and all ordinary-role promotion rejection.
- MVC Collections entry gate for all ten roles; actual MVC signed API client scenarios for head/Billing/SuperAdmin acceptance and GSO/Accounting denial.
- Existing all-role quarantined mutation tests now include all new enums automatically; physical disconnection/reconnection, CustomerService, meter-reader, financial data/invoice protections remain covered.
- Existing MeterReaderAssignedRegression fixtures were stale relative to current installment/session contracts: `OtherChargeIds` became `InstallmentIds`, synthetic MVC session now includes its required stamp, and the expected authorized-call count increases from six to eight for the newly approved head. These are test-only alignments; payment/mobile production behavior is unchanged.

Executed verification results:

| Command | Result |
|---|---|
| `dotnet .tmp/phase6kb0-final/BillingRegression/BillingRegression.dll .` | PASS — 644 temporary-table database checks, plus four migration/concurrency checks |
| `dotnet .tmp/phase6kb0-final/MeterReaderAssignedRegression/MeterReaderAssignedRegression.dll` | PASS — MVC/API session, Collection ownership, reader, SOA and login boundaries |
| `node tests/BillingRegression/collections-browser-regression.cjs` | PASS |
| `dotnet build TMCWD/TMCWD.slnx --no-restore -m:1 -p:BaseOutputPath=...` | PASS — isolated output required because active local Application/Data processes held normal debug DLLs |

No verification suite is still running. No test was skipped. No speculative DCM/PCA tests or unrelated workflows were implemented.

## 12. Final verdicts

| Verdict | Result |
|---|---|
| ROLE INVENTORY | COMPLETE |
| CASHIER -> BILLING | PASS |
| BILLING NUMERIC VALUE PRESERVED AS 7 | PASS |
| BILLINGHEAD = 8 | PASS |
| GENERALSERVICES = 9 | PASS |
| ACCOUNTING = 10 | PASS |
| EXISTING ROLE VALUES 1–6 UNCHANGED | PASS |
| COLLECTION ACCESS | PASS — Billing, BillingHead, SuperAdmin only |
| BILLINGHEAD COLLECTION ACCESS | PASS |
| ACCOUNTING FINANCIAL WRITE ACCESS | NONE |
| GENERALSERVICES FINANCIAL ACCESS | NONE |
| PENDING ROUTES STILL QUARANTINED | PASS |
| JWT / SESSION COMPATIBILITY | PASS |
| USER MANAGEMENT | PASS |
| SOLUTION BUILD | PASS — 187 existing warnings, 0 errors |
| REGRESSION | PASS |
| AUTHORIZATION BROADENING OUTSIDE APPROVED SCOPE | NONE |
| PHASE 6K-B0 | PASS |
| PHASE 6K DEVELOPMENT INTEGRATION | NOT YET EVALUATED |
| PRODUCTION | DEFERRED / NOT EVALUATED |

## Appendix A. Changed-policy API route inventory

Generated from the actual Data controller endpoint metadata in the regression host using the production policy registration. Every row inherits the before/after role set in section 4; request validation, persisted identity, secondary policies and service ownership checks are unchanged. POST is not automatically a financial mutation: bulk roster lookup is a read. Raw Payment SaveUpdate remains rejected; waiver service checks remain narrower. No PendingAuthorizationDecision route was removed or enabled.

| Route | Method | Policy |
|---|---|---|
| `/api/Account/Get/{id}` | GET | BillingOperations |
| `/api/Account/GetAll` | GET | BillingOperations |
| `/api/Account/GetByAccountNumber/{accountNumber}` | GET | BillingOperations |
| `/api/Account/GetByCustomerId/{customerId}` | GET | BillingOperations |
| `/api/Account/GetByMeterNumber/{meterNumber}` | GET | BillingOperations |
| `/api/Account/GetByZoneAndBook/{zone}/{book}` | GET | BillingOperations |
| `/api/Account/GetByZoneBookAndSequence/{zone}/{book}/{seqFrom}/{seqTo}` | GET | BillingOperations |
| `/api/Account/Search` | GET | BillingOperations |
| `/api/AdvancePayment/Get/{id}` | GET | Collection |
| `/api/AdvancePayment/GetActiveByAccount/{accountId}` | GET | Collection |
| `/api/AdvancePayment/GetAll` | GET | Collection |
| `/api/AdvancePayment/GetByAccount/{accountId}` | GET | Collection |
| `/api/AdvancePayment/SaveUpdate/{userId}` | POST | Collection |
| `/api/BatchPaymentUpload/Get/{id}` | GET | Collection |
| `/api/BatchPaymentUpload/GetByDateRange` | GET | Collection |
| `/api/BatchPaymentUpload/Save/{userId}` | POST | Collection |
| `/api/Billing/AccountLedger/{accountNumber}` | GET | BillingOperations |
| `/api/Billing/ChargeAnchor` | POST | BillingOperations |
| `/api/Billing/Collection/{billingReferenceId}` | GET | Collection |
| `/api/Billing/Get/{id}` | GET | BillingOperations |
| `/api/Billing/GetAll` | GET | BillingOperations |
| `/api/Billing/GetByAccountId/{accountId}` | GET | BillingOperations |
| `/api/Billing/GetByBillingReference/{billingReferenceId}` | GET | BillingOperations |
| `/api/Billing/GetByJobOrderId/{jobOrderId}` | GET | BillingOperations |
| `/api/Billing/GetPaymentTransactionId/{paymentTransactionId}` | GET | BillingOperations |
| `/api/Billing/History/{accountNumber}` | GET | BillingOperations |
| `/api/BillingAdjustment/Get/{id}` | GET | BillingOperations |
| `/api/BillingAdjustment/GetAll` | GET | BillingOperations |
| `/api/BillingAdjustment/GetByReference/{reference}` | GET | BillingOperations |
| `/api/BillingAdjustment/History/{billingReferenceId}` | GET | BillingOperations |
| `/api/BillingAdjustment/SaveBatch/{userId}` | POST | BillingOperations |
| `/api/BillingAdjustment/SaveUpdate/{userId}` | POST | BillingOperations |
| `/api/ChargeType/Get/{id}` | GET | BillingOperations |
| `/api/ChargeType/GetAll` | GET | BillingOperations |
| `/api/ChargeType/GetByClassification/{classificationId}` | GET | BillingOperations |
| `/api/collection/service-invoice/{serviceInvoiceNumber}` | GET | Collection |
| `/api/CollectionRemittance/Save` | POST | Collection |
| `/api/Customer/Get/{id}` | GET | BillingOperations |
| `/api/Customer/GetByName/{firstname}/{lastname}` | GET | BillingOperations |
| `/api/Customer/GetCustomers` | GET | BillingOperations |
| `/api/Customer/GetCustomersFromIds` | GET | BillingOperations |
| `/api/Customer/Search/{searchString}` | GET | BillingOperations |
| `/api/DisconnectionMonitoring` | GET | BillingOperations |
| `/api/DisconnectionMonitoring/{accountNumber}` | GET | BillingOperations |
| `/api/MeterMaintenanceRate/by-billing-date/{billingDate}` | GET | BillingOperations |
| `/api/OtherCharge/Deactivate/{id}/{userId}` | PUT | BillingOperations |
| `/api/OtherCharge/Get/{id}` | GET | BillingOperations |
| `/api/OtherCharge/GetAll` | GET | BillingOperations |
| `/api/OtherCharge/GetByReference/{reference}` | GET | BillingOperations |
| `/api/OtherCharge/PostToCollection/{userId}` | POST | BillingOperations |
| `/api/OtherCharge/ProcessPayment/{userId}` | POST | Collection |
| `/api/OtherCharge/SaveUpdate/{userId}` | POST | BillingOperations |
| `/api/OtherFeeType/Get/{id}` | GET | BillingOperations |
| `/api/OtherFeeType/GetAll` | GET | BillingOperations |
| `/api/OtherFeeType/GetByName/{name}` | GET | BillingOperations |
| `/api/Payment/CancelInvoice/{userId}` | POST | Collection |
| `/api/Payment/Get/{id}` | GET | Collection |
| `/api/Payment/GetByBillingReference/{referenceId}` | GET | Collection |
| `/api/Payment/GetByCashier/{userId}` | GET | Collection |
| `/api/Payment/GetByDateRange` | GET | Collection |
| `/api/Payment/GetByMacAddress/{macAddress}` | GET | Collection |
| `/api/Payment/GetByMethod/{methodId}` | GET | Collection |
| `/api/Payment/GetByPaymentReference/{referenceId}` | GET | Collection |
| `/api/Payment/Invoice/{invoiceNumber}` | GET | Collection |
| `/api/Payment/ProcessCollection/{userId}` | POST | Collection |
| `/api/Payment/SaveUpdate/{userId}` | POST | Collection |
| `/api/Payment/SearchInvoices` | GET | Collection |
| `/api/PaymentCheck/Get/{id}` | GET | Collection |
| `/api/PaymentCheck/GetAll` | GET | Collection |
| `/api/PaymentCheck/GetByReference/{reference}` | GET | Collection |
| `/api/PaymentCheck/SaveUpdate/{userId}` | POST | Collection |
| `/api/Penalty/ActiveEntries` | GET | BillingOperations |
| `/api/Penalty/ChargeEntries` | GET | BillingOperations |
| `/api/Penalty/Get/{id}` | GET | BillingOperations |
| `/api/Penalty/GetAll` | GET | BillingOperations |
| `/api/Penalty/GetByReference/{reference}` | GET | BillingOperations |
| `/api/Penalty/PendingWaivers/{userId}` | GET | BillingOperations |
| `/api/Penalty/PostCharges/{userId}` | POST | BillingOperations |
| `/api/Penalty/ReviewWaiver/{userId}` | POST | BillingOperations |
| `/api/Penalty/SaveUpdate/{userId}` | POST | BillingOperations |
| `/api/Penalty/SubmitWaiver/{userId}` | POST | BillingOperations |
| `/api/Reading/Get/{id}` | GET | BillingOperations |
| `/api/Reading/GetAccountCurrentReading/{accountId}` | GET | BillingOperations |
| `/api/Reading/GetAccountPreviousReading/{accountId}` | GET | BillingOperations |
| `/api/Reading/GetByAccount/{accountId}` | GET | BillingOperations |
| `/api/Reading/GetByAccountAndBillingPeriod/{accountId}/{billingPeriod}` | GET | BillingOperations |
| `/api/Reading/GetByReader/{readerId}` | GET | BillingOperations |
| `/api/Reading/GetByZoneAndBook/{zone}/{book}` | GET | BillingOperations |
| `/api/Reading/GetRangeByReadingSheetIds` | POST | BillingOperations |
| `/api/Reading/SaveUpdate/{userId}` | POST | BillingOperations |
| `/api/ReadingSheet/CompleteReadingSheet` | POST | BillingOperations |
| `/api/ReadingSheet/Get/{id}` | GET | BillingOperations |
| `/api/ReadingSheet/GetAll` | GET | BillingOperations |
| `/api/ReadingSheet/GetByAssignedTo/{assignedTo}` | GET | BillingOperations |
| `/api/ReadingSheet/GetByBillingDate/{zone}/{book}/{billingDate}` | GET | BillingOperations |
| `/api/ReadingSheet/GetByZoneAndBook/{zone}/{book}` | GET | BillingOperations |
| `/api/ReadingSheet/GetByZoneBookAndAssignedTo/{zone}/{book}/{assignedTo}` | GET | BillingOperations |
| `/api/ReadingSheet/GetCurrentByAssignedTo/{zone}/{book}/{assignedTo}` | GET | BillingOperations |
| `/api/ReadingSheet/GetReadingSheetAccounts` | GET | BillingOperations |
| `/api/ReadingSheet/PartialPostReadingSheet` | POST | BillingOperations |
| `/api/ReadingSheet/SaveUpdate/{userId}` | POST | BillingOperations |
| `/api/ReadingSheet/UpdateReadingSheetsStatus/{userId}/{status}` | PATCH | BillingOperations |
| `/api/ReadingSheet/UpdateZoneProgress` | POST | BillingOperations |
| `/api/ReadingSheetTemplate/Deactivate/{id}/{userId}` | PUT | BillingOperations |
| `/api/ReadingSheetTemplate/Get/{id}` | GET | BillingOperations |
| `/api/ReadingSheetTemplate/GetAll` | GET | BillingOperations |
| `/api/ReadingSheetTemplate/GetByUser/{userId}` | GET | BillingOperations |
| `/api/ReadingSheetTemplate/SaveUpdate/{userId}` | POST | BillingOperations |
| `/api/Tariff/Get/{id}` | GET | BillingOperations |
| `/api/Tariff/GetAll` | GET | BillingOperations |
| `/api/Tariff/GetByClassification/{classification}` | GET | BillingOperations |
| `/api/Users/Get/{id}` | GET | BillingOperations |
| `/api/Users/GetUsers` | GET | BillingOperations |
| `/api/Users/GetUsersById` | GET | BillingOperations |
| `/api/Users/GetUsersByRole/{role}` | GET | BillingOperations |
| `/api/WaterRate/Get/{id}` | GET | BillingOperations |
| `/api/WaterRate/GetAll` | GET | BillingOperations |
| `/api/WaterRate/GetByClassification/{classification}` | GET | BillingOperations |
| `/api/WaterRate/GetByClassificationAndMeterSize/{classification}/{meterSize}` | GET | BillingOperations |
| `/api/ZoneBook/Get/{id}` | GET | BillingOperations |
| `/api/ZoneBook/GetAll` | GET | BillingOperations |
| `/api/ZoneBook/GetBooksByZone/{zone}` | GET | BillingOperations |
| `/api/ZoneBook/GetByIds` | GET | BillingOperations |
| `/api/ZoneBook/GetByWeek/{week}` | GET | BillingOperations |
| `/api/ZoneBook/GetByZone/{zone}` | GET | BillingOperations |
| `/api/ZoneBook/GetByZoneAndBook/{zone}/{book}` | GET | BillingOperations |
| `/api/ZoneBook/GetZones` | GET | BillingOperations |
