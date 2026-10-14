# Phase 6K-A — Legacy Finance Discovery

Inspection date: **2026-10-14 (Asia/Manila)**. Scope: current working tree and read-only local development `127.0.0.1:3307/tmcwd`. Test persistence is restricted to connection-local synthetic tables in `tmcwd_test`. This report changes no financial behavior, authorization, schema, invoice, or operational data.

## 1. Executive summary

**Debit/Credit Memo: IMPLEMENTED BUT QUARANTINED, with PARTIAL financial implementation and undeployed persistence.** The navigation, form, MVC bridge, HTTP transaction, API, interface, EF entity/DbSet and standalone DDL exist. All three API actions require `PendingAuthorizationDecision`, which denies every authenticated role, including SuperAdmin. The development table `debit_credit_memos` does not exist. The service only inserts a positive-amount `Pending` record; it does not post debit/credit effects.

**PCA: NOT IMPLEMENTED as a financial mechanism.** No development PCA table/column, rate configuration, calculation, entry command or authorization policy was found. A nullable `OfficialInvoiceSnapshot.PowerCostAdjustment` contract exists, but posting always writes null. Of 32 development bills, 15 have snapshots; all 15 contain the PascalCase PCA property and none contains a non-null PCA amount. Mobile can display a server-issued value; Collections instead supplies a zero placeholder internally and hides PCA summary lines. These presentation/contract provisions are not a PCA engine.

**Routes safe to enable now: 0.** Creation needs approved financial semantics, claim-derived actors, deployed persistence, validation, idempotency, and posting/reversal boundaries. Reads also need approved access and deployed persistence. Preserve quarantine until those dependencies are satisfied.

The operational Bill Adjustment, current balance, invoice, penalty, arrears, installment, payment and cancellation flows already have common integration points. Reuse them after policy decisions; do not introduce a second balance engine or use memo category names as approved accounting rules.

## 2. Existing Debit/Credit Memo implementation — Matrix A

Paths below are relative to the repository root. Source links identify the inspected current implementation, not older report claims.

| Component | File/Table | Current State | Purpose | Problems | Recommendation |
|---|---|---|---|---|---|
| Navigation | `TMCWD/TMCWD.Application/Views/Shared/_InsideLayout.cshtml` | Visible Billing link | Opens memo page | UI visibility is not authority; no quarantine explanation | MODERNIZE after access policy |
| Page | [DebitCreditMemo.cshtml](../TMCWD/TMCWD.Application/Views/Billing/DebitCreditMemo.cshtml) | Implemented form; unusable end-to-end | Account and memo entry | No bill selector, posting/history/approval/reversal controls | MODERNIZE; POLICY REQUIRED |
| Browser | [debit-credit-memo.js](../TMCWD/TMCWD.Application/wwwroot/js/debit-credit-memo.js) | Real lookup/save calls | Validates basic inputs, sends antiforgery header | Stale selected account dataset; no retry key; UTC default date can differ from Manila | MODERNIZE; IDEMPOTENCY REMEDIATION REQUIRED |
| CSS | `TMCWD/TMCWD.Application/wwwroot/css/debit-credit-memo.css` | Two-column cards, responsive rules | Form appearance | Legacy selectors do not prove controls exist | KEEP visual system; review accessibility when enabled |
| MVC | [BillingController.cs](../TMCWD/TMCWD.Application/Controllers/BillingController.cs) | Session check; save and account-history bridge | Validate account, normalize text/dates, forward current session actor | API denial becomes 502 on save; denied read becomes empty history | MODERNIZE errors and eventual operation policy |
| HTTP transaction | [DebitCreditMemoTransaction.cs](../TMCWD/TMCWD.Billing/DebitCreditMemoTransaction.cs) | Registered and called | Save, GetAll, client-side account filter | GetAll failure returns empty list; fetches all accounts' memos before filtering | MODERNIZE; scoped server query after policy |
| Shared contract | [Model/Billing/DebitCreditMemo.cs](../TMCWD/TMCWD.Model/Billing/DebitCreditMemo.cs) | Present | MVC/API serialization | `int` ID/actors vs entity `long`; accepts persistence/audit fields | MODERNIZE external request boundary |
| API | [DebitCreditMemoController.cs](../TMCWD/TMCWD.Data/Controllers/DebitCreditMemoController.cs) | All 3 routes QUARANTINED | ID read, all read, creation | Save actor is route input; entity-bound command; incomplete validation | KEEP quarantine; SECURITY REMEDIATION REQUIRED |
| Interface/service | [IDebitCreditMemoService.cs](../TMCWD/TMCWD.Data/Services/IDebitCreditMemoService.cs), [DebitCreditMemoService.cs](../TMCWD/TMCWD.Data/Services/DebitCreditMemoService.cs) | Get/GetAll/insert only | Record pending request | No financial effect, bill linkage, approval, retry or reversal | MODERNIZE; POLICY REQUIRED |
| Entity/DbSet | [Entities/DebitCreditMemo.cs](../TMCWD/TMCWD.Data/Entities/DebitCreditMemo.cs), `Context/UserDbContext.cs` | Mapped but undeployed | Pending memo storage | AccountNumber only; no constrained billing/operation reference | MODERNIZE approved domain/schema |
| SQL | [debit_credit_memos.sql](../TMCWD/database/debit_credit_memos.sql) | Standalone legacy DDL, not applied | Defines proposed persistence | Outside current Data/Database approach; no FKs/business uniqueness | REPLACE with reviewed dated script when implementation is approved |
| Development table | `debit_credit_memos` | Absent | None deployed | No live memo records can exist in this table here | Do not provision during discovery |
| Customer Information | [CustomerInformation.cshtml](../TMCWD/TMCWD.Application/Views/CustomerSupport/CustomerInformation.cshtml) | Debit/Credit adjustments tab | Displays account-filtered memo history | Empty result hides API quarantine; creation history is not posting evidence | MODERNIZE unavailable state; KEEP escaped rendering |
| Demo data | [dcm-sample-data.js](../TMCWD/TMCWD.Application/wwwroot/js/dcm-sample-data.js) | LEGACY / UNUSED; no view includes it | In-memory samples/helpers | Fake `DCM-2024-...` IDs and approved samples are not accounting evidence | REMOVE / LEGACY in later cleanup; unchanged here |
| Memo tests | `PostingSecurity.cs`, `ReadSessionSecurity.cs`, `AccountLedgerRegression.cs`, customer-information browser harness | Existing safety coverage | Denials, no unsupported ledger queries, escaped fake history | No operational memo posting test because posting does not exist | KEEP; no speculative tests added |

Search covered repository source, SQL, scripts, tests, mobile and prior phase documentation for the requested memo/PCA variants, generic memo, and whole-word DM/CM. Generated outputs and third-party assets were excluded from source interpretation. DM/CM also identify disconnection-monitoring and collection-monitoring CSS classes, not separate memo implementations. No additional adjustment-memo engine or memo enum was found.

### UI detail

The page has an outer card and paired field rows: account lookup/read-only name; DCM category and debit/credit radio buttons (debit default); DCM date and optional bill-month-basis date; amount; explanation/remarks; Reset and Submit. Amount input is positive with 0.01 step; both kinds use a positive magnitude. The 20 textual categories include bounced check, cancelled receipt, defective connection/meter, deposit application, discount, duplicate account, erroneous reading, previous balance error, invalid payment/charges, late penalty, other, refund, reclassification, privilege, payment transfer, penalty waiver and withholding tax. They are labels, not implemented actions; the API accepts any nonblank category.

Account lookup uses `/Billing/SearchAccounts`, 300 ms debounce, keyboard arrows/Enter/Escape, escaped result HTML and click selection. The MVC search filters Active accounts and also loads reading data. Selection stores `dataset.fullAccountNumber`; typing a new nonempty value does not invalidate the prior selected account/name, so an old account can be submitted. Initial date uses UTC `toISOString()`, which can show the previous local day in Manila before 08:00. Labels mostly lack `for` bindings and the custom dropdown lacks a complete combobox/listbox contract. These are future enablement issues, not changes made here.

Explanation automatically fills editable remarks. Lookup failure has a visible message; save disables the button and reports errors via toast, resets only on success, and reenables in `finally`. No approval/status/history panel, billing ID/reference selector, numbering, edit/delete or posting/reversal control exists on this page. Customer Information separately displays category, kind, amount, explanation, date/status history. This is a real entry shell, not a usable accounting workflow.

## 3. Existing PCA implementation — Matrix B

| Location | Stored/Calculated/UI | Authoritative? | Snapshot? | Collections? | Ledger? | Recommendation |
|---|---|---|---|---|---|---|
| Billing entity/shared bill/deployed `billings` | No PCA column | No | JSON has optional field only | No component | No component | NOT IMPLEMENTED; POLICY REQUIRED |
| [OfficialInvoiceSnapshot.cs](../TMCWD/TMCWD.Model/Billing/OfficialInvoiceSnapshot.cs) | Nullable decimal contract | Only an issued value could be authoritative | Field exists, not a populated PCA mechanism | Not projected to CollectionBill | Not projected | KEEP null meaning; define future issuance policy |
| [ReadingSheetService.PartialPost](../TMCWD/TMCWD.Data/Services/ReadingSheetService.cs) | Explicit `PowerCostAdjustment: null` | No PCA source | Frozen null | No PCA added | No PCA added | KEEP current behavior; POLICY REQUIRED |
| Development snapshots | 15/15 property present, 0 non-null | No established PCA amount | Null values persisted | No effect | No effect | Do not backfill issued snapshots |
| [MobileReadingSheetController.Soa](../TMCWD/TMCWD.Data/Controllers/MobileReadingSheetController.cs), `MobileSoaDto` | Reads snapshot property | Pass-through only | Yes, if already present | N/A | N/A | KEEP; no local/server reconstruction |
| Mobile `official_soa.dart`, `soa_screen.dart`, cache/tests | Nullable parse/cache/conditional display | Server value only | Cached server response | N/A | N/A | KEEP; UI support is not financial implementation |
| Active `Views/Collections/Collections.cshtml`, `wwwroot/js/collections.js` | Hidden summary lines; JS `pca: 0` | No | No PCA projection | Placeholder internally | No | REMOVE / LEGACY placeholder when approved PCA integration replaces it |
| `Views/Billing/Collections.cshtml` | Old visible zero PCA and PCA/MMF heading | No | No | LEGACY view; no current Billing Collections action found | No | REMOVE / LEGACY after caller review |
| Calculator/config/entry/API | None found | No | None | None | None | POLICY REQUIRED before implementation |

Classification: **NOT IMPLEMENTED**, with **UI ONLY** placeholders and a nullable snapshot provision. This is neither ALREADY SNAPSHOTTED as a populated charge nor PERSISTED BUT NOT SNAPSHOTTED. Whether future PCA must freeze on issuance is **POLICY REQUIRED**; any chosen issued component must respect existing snapshot immutability.

## 4. Read-only database inventory — Matrix D

Metadata was inspected in a session configured `TRANSACTION READ ONLY`, inside a transaction rolled back afterward. Queries inspected information_schema tables, columns, keys, indexes, routines/triggers and one aggregate snapshot query. No customer rows, credentials or invoice bodies were exported. No DDL, data mutation, migration or memo request was executed against `tmcwd`.

Only `billing_adjustments` and `billing_adjustment_operations` match the requested finance table terms. No debit/credit/memo/PCA/power-cost column exists across visible development tables. No routines or triggers were returned under the configured account's metadata visibility; this is not a certification of objects hidden from that account. No matching stored-procedure implementation was found in repository SQL.

All inspected deployed tables use bigint auto-increment `Id` primary keys except `billing_adjustment_operations`, whose PK is `OperationId varchar(36)`. **No enforced foreign keys were returned for these tables.** The linkage column below is an application relationship, not an asserted FK. Appendix A records every inspected column/type/nullability.

| Table/Column | Purpose | Foreign Key / linkage | Audit | Current Usage | Recommendation |
|---|---|---|---|---|---|
| `debit_credit_memos` (absent) | Proposed pending memo | DDL AccountNumber only; no FK | Proposed Created/Updated actor/time | Undeployed; no financial use | MODERNIZE schema after policy; do not apply legacy script now |
| `billing_adjustments` | Signed bill correction lines; Amount decimal(6,2), Type int, Reason nullable varchar(255) | BillingReferenceId varchar(50), no FK | CreatedBy/DateCreated, UpdatedBy/DateUpdated | Aggregate drives BillingAdjustment and RemainingAmount | KEEP; clarify type semantics separately |
| `billing_adjustment_operations` | Durable retry receipt: PayloadHash char(64), ResultJson longtext | OperationId PK; BillingReferenceId logical bill link | ActorId, DateCreated datetime(6) | Current adjustment replay/conflict guard | KEEP; reuse established integrity pattern appropriately |
| `billings` | Current liability; Status; monetary MaterialsAmount/MMFAmount/Penalties/OtherCharges/BillingAdjustment/TotalBillAmount/RemainingAmount decimal(6,2); BillArrearsAmount nullable decimal(18,2) | AccountId, ReadingId, JobOrderId; BillingReferenceId nonunique index | Created/Updated actor/time | SnapshotJson longtext, ServiceInvoiceNumber nullable unique varchar(30), PaymentTransactionId logical receipt link | KEEP balance/snapshot boundaries; MMFAmount is legacy, not PCA |
| `payments` | Receipts: PaidAmount nullable decimal(6,2); BalanceBefore nullable decimal(65,30); Success, Method, ChargesOnly | BillingReference varchar(200); PaymentReference indexed, nonunique | Created/Updated actor/time | Exact settlement and cancellation restoration | KEEP; no memo/PCA link |
| `penalties` | Persisted penalty Amount decimal(6,2); PaymentStatus | BillingReferenceId indexed; PenaltyTypeId; SettledPaymentId | Created/Updated actor/time | Water-only posting, waiver, settlement | KEEP; no PCA/memo base |
| `other_charges` | Parent contract Amount decimal(6,2); Type, PayableIn, Particulars, IsActive, PaymentStatus, InstallmentCount | BillingReferenceId indexed; SettledPaymentId | Created/Updated actor/time | Existing service/charge contracts; no memo key | KEEP distinct from memos |
| `other_charge_installments` | Child Amount decimal(6,2); PaymentStatus; activation/due evidence | OtherChargeId; SettledPaymentId; ActivatedBillingReferenceId; unique (OtherChargeId,InstallmentNumber) | Created/Updated actor/time plus ActivatedAt | Due/current children drive collection | KEEP; do not create duplicate memo liabilities |
| `accounts` | Identity/classification/meter/zone/service status; no PCA/memo amount | CustomerId, ZoneBookId; AccountNumber varchar(50), no unique index returned | Created/Updated actor/time | Memo MVC account lookup; bills use AccountId | KEEP; stable linkage policy required for memos |
| `readings` | Current/previous reading decimal(6,2), Status, IsCompleted, Remarks | AccountId, ReadingSheetId | Created/Updated actor/time | Authoritative reading chronology | KEEP; no finance memo mechanism |
| `reading_sheets` | Assignment/status; billing/due/disconnection dates and sequence fields | AssignedTo, ZoneBookId | Created/Updated actor/time, DateUpload | Posting dates and workflow | KEEP; no PCA scheduling/configuration |

Relevant deployed uniqueness: unique `ServiceInvoiceNumber`; unique installment sequence; unique adjustment operation PK. `BillingReferenceId` is indexed, not unique in inspected `billings`. No memo numbering/index exists. Schema precision differences and missing enforced FKs require considered future migrations, not destructive repair during discovery.

Legacy proposed memo DDL has `Amount decimal(18,2)`, string category/kind/status, DcmDate DATE, nullable BillMonthBasis DATE, explanation/remarks varchar(1000), actor bigint and DATETIME audit values, indexes on AccountNumber and DcmDate. It supplies no approval/posting/reversal/operation columns, FK, check constraint or business reference uniqueness.

## 5. Routes — Matrix C

MVC conventional routes use `/Billing/{action}`. The page action has no explicit HTTP-method attribute; its intended browser use is GET, not an exclusive GET restriction. API routes have explicit methods. Policies below are current source behavior.

| Route | HTTP Method | Purpose | Current Policy | Financial Write? | Actor Source | Recommendation |
|---|---|---|---|---|---|---|
| `/Billing/DebitCreditMemo` | Conventional; intended GET, unconstrained action | Form | MVC authenticated session only | No | Session | ENABLED shell; POLICY REQUIRED for operational view |
| `/Billing/SearchAccounts?q=...` | GET | Shared Active account lookup | MVC session; downstream Account/Reading policies | No | Session/API bearer | KEEP shared behavior; does not confer memo permission |
| `/Billing/SaveDebitCreditMemo` | POST + antiforgery | Submit pending record | MVC session; downstream deny | Pending financial intent, no balance posting | MVC session ID passed in route | QUARANTINED downstream; MODERNIZE error/authorization |
| `/Billing/GetDebitCreditMemosByAccount?accountNumber=...` | GET | Customer Information history | MVC session; downstream deny | No | Session | ENABLED bridge, unusable read; false-empty result; MODERNIZE |
| `/api/DebitCreditMemo/Get/{id}` | GET | Record lookup | PendingAuthorizationDecision | No | Bearer validated but no audit mutation | QUARANTINED; POLICY REQUIRED |
| `/api/DebitCreditMemo/GetAll` | GET | All records | PendingAuthorizationDecision | No | Bearer | QUARANTINED; approved scope required |
| `/api/DebitCreditMemo/Save/{userId}` | POST | Insert Pending | PendingAuthorizationDecision | Writes memo row if ever enabled; currently denied | **Client-controlled route** | QUARANTINED; SECURITY + IDEMPOTENCY REMEDIATION REQUIRED |
| Memo approve/post/cancel/reverse/edit/delete | None | No implemented command | No policy | Not implemented | None | POLICY REQUIRED; do not invent routes |
| PCA view/config/change/post | None specific | No implemented mechanism | No policy | Not implemented | None | POLICY REQUIRED |
| `/api/BillingAdjustment/SaveUpdate/{userId}`, `/SaveBatch/{userId}` | POST; Idempotency-Key | Existing correction | BillingOperations | Yes | NameIdentifier claim overrides route ID | KEEP; no memo authorization inference |
| `/api/BillingAdjustment/History/{billingReferenceId}` | GET | Existing adjustment audit | BillingOperations | No | Bearer | KEEP separate history |
| `/api/Billing/Collection/{billingReferenceId}` | GET | Shared collectible projection | Collection | No | Bearer | KEEP future common projection integration point |
| `/api/reading-sheet/mobile/{id:int}/accounts/{accountId:int}/soa` | GET | Issued/cached invoice presentation | MeterReader and server assignment checks | No | Bearer reader identity | KEEP nullable issued PCA pass-through |

See [BillingAdjustmentController](../TMCWD/TMCWD.Data/Controllers/BillingAdjustmentController.cs), [BillingController (Data)](../TMCWD/TMCWD.Data/Controllers/BillingController.cs), [MobileReadingSheetController](../TMCWD/TMCWD.Data/Controllers/MobileReadingSheetController.cs). No new route is enabled by this report.

## 6. Authorization

[Data Program.cs](../TMCWD/TMCWD.Data/Program.cs) defines `PendingAuthorizationDecision` with authenticated-user requirement and an assertion that always returns false. Anonymous API requests receive 401; every valid authenticated canonical role receives 403 before service invocation. This includes SuperAdmin. View/create/approve/cancel/PCA authority remains **POLICY REQUIRED**. No implicit financial permission follows from the Billing menu.

Existing `BillingOperations` allows SuperAdmin, CustomerRepresentative, CustomerServiceOic and Cashier; `Collection` allows Cashier/SuperAdmin. These are policies for their existing operations, not approval of memo operations. SuperAdmin inheritance would be consistent with the established principle only after stakeholders explicitly approve an operational role/action and its ownership constraints. Keep all pending authorization policies.

## 7. Audit and trust boundaries

Memo service overwrites CreatedBy/UpdatedBy from its `userId` argument, timestamps both with UTC now and forces `Status = "Pending"`. MVC derives that argument from its authenticated session. **Data API accepts the actor from `{userId}` without overriding it from the authenticated claim: SECURITY REMEDIATION REQUIRED before enabling.** Entity binding also leaves client Id and business fields available. API basic validation checks account/category/explanation nonblank, exact lowercase debit/credit and Amount > 0. It does not validate account existence, field lengths against DDL, precision/range, bill ownership/linkage, category allowlist, default dates or permissible periods. MVC validates existence, normalizes strings/dates, but cannot substitute for the API boundary.

There is no ApprovedBy/ApprovedDate, posting actor/time, cancellation actor/reason, immutable mutation/event log or retained pre/post obligation. Explanation/Remarks are intent text, not an approved audit protocol. Updated fields merely mirror creation because no update method exists. Audit verdict: **REMEDIATION REQUIRED**.

## 8. Bill Adjustment boundary and comparison

[BillingAdjustmentService](../TMCWD/TMCWD.Data/Services/BillingAdjustmentService.cs) is a functioning signed correction mechanism. It validates one bill, known enum type, nonzero two-decimal amount bounded by decimal(6,2), actor/operation key; rejects settled bills; locks Billing with `FOR UPDATE`; writes lines, recomputes aggregate/current balance and saves the durable operation result atomically. Replays verify actor/bill/payload and return original result. Reason is deliberately excluded from replay hash, with first-write authority.

| Mechanism | Purpose | Increases balance | Decreases balance | Changes RemainingAmount | Separate audit | Separate reference | Approval |
|---|---|---|---|---|---|---|---|
| Bill Adjustment | Existing bill-linked signed corrections | Negative amount can increase it | Positive amount decreases it, subject to PHP 20 floor | Yes, exact formula below | billing_adjustments + operation receipt | BillingReferenceId, line Id, OperationId; no memo number | Authorized direct save; no memo-style approval |
| Debit Memo | Currently records Pending intent | **No implemented effect**; future POLICY REQUIRED | No implemented effect | No | Proposed creation row only; undeployed | Proposed numeric Id + account/date, no business number/bill key | Only Pending; POLICY REQUIRED |
| Credit Memo | Currently records Pending intent | No implemented effect | **No implemented effect**; future POLICY REQUIRED | No | Same proposed row | Same | Only Pending; POLICY REQUIRED |
| PCA | No source/config/calculation | No implemented effect; POLICY REQUIRED | No implemented effect; POLICY REQUIRED | No | No PCA audit | No PCA reference | POLICY REQUIRED |

Actual correction formula is `BillingAdjustment = SUM(all adjustment Amount for BillingReferenceId)` and `RemainingAmount = MAX(20.00, TotalBillAmount - BillingAdjustment)`. Type labels do not select a different formula: Usage/CurrentBill/Penalty/Present/Previous and discount types all aggregate by signed amount. Thus a type named Penalty is not proof that this service edits `penalties`. Existing-ID SaveUpdate can update an existing line while preserving its creation fields; the operation receipt preserves command results, but the line alone is not an append-only revision log. These are current boundaries, not redesigned here. Purpose/type allocation and future overlap with memo categories require stakeholder confirmation.

## 9. RemainingAmount interaction — Matrix E

| Mechanism | BillAmount | RemainingAmount | Snapshot | Penalty | Arrears | Collections | Ledger |
|---|---|---|---|---|---|---|---|
| Bill Adjustment | Issued water amount unchanged | Signed aggregate formula, PHP 20 floor | Unchanged | Water basis unchanged; not a penalty-row command | Future qualifying prior balance reflects change | Reflected once through remaining balance; amount shown informationally | Shared projection + adjustment history |
| Debit Memo | No change | No change | No access | No access | No effect | No integration | Deliberately excluded |
| Credit Memo | No change | No change | No access | No access | No effect | No integration | Deliberately excluded |
| PCA | No implementation | No implementation | Nullable field frozen null | Excluded from basis | No current PCA effect | Placeholder only, no monetary integration | No PCA projection/history |
| Meter Maintenance | Separate from water | Included once at posting | Frozen separate component | Excluded | Already in eligible remaining balance | Separate display, not extra balance addition | Same projection |
| Other Charge/installments | Separate | Does not add contract to water balance | Issued water snapshot unchanged | Excluded | Not added by water-arrears helper | Current collectible children added separately | Other Charges tab + persisted events |

Memo `Save` has one insert/SaveChanges; it neither loads Billing nor has a related financial transaction or formula. Its amount lives solely in the proposed memo record, not current liability. There is no hidden debit or negative-credit convention to adopt. Preserve RemainingAmount as authoritative current-bill obligation; do not add snapshot arrears or independently subtract display adjustments again.

## 10. Snapshot and ServiceInvoiceNumber interaction

No memo code reads or writes InvoiceSnapshotJson, OfficialInvoiceSnapshot or ServiceInvoiceNumber. PCA's snapshot field is provisioned but unpopulated by issuance. [BillingService.SaveUpdate](../TMCWD/TMCWD.Data/Services/BillingService.cs) rejects replacement snapshots and invoice numbers and preserves stored values; PartialPost is the issuance path. Bill adjustments, payment/cancellation, SOA and collection reads do not rewrite issued snapshots. No discovered memo/PCA code attempts snapshot mutation: current compatibility **SAFE**, because no monetary memo/PCA mechanism is connected. Future PCA issuance policy remains required. Do not patch historical nulls into charge amounts.

## 11. Penalty interaction

[PenaltyService](../TMCWD/TMCWD.Data/Services/PenaltyService.cs) uses `LatePaymentPenalty.Calculate(snapshot.BillAmount)` for actual posting, strictly after DueDate, excluding Paid/Waived and enforcing authoritative snapshot availability. Maintenance, arrears, adjustments, Other Charges, memo and PCA are not added to the base. Informational `TotalAfterDue = snapshot.TotalDue + water-only PenaltyAfterDue` creates no penalty rows. Legacy list fields named BillAmount can project TotalBillAmount for presentation; they are not the posting base. Keep the confirmed water-only rule. Whether future memo liabilities have distinct late-fee treatment is POLICY REQUIRED and cannot change this water penalty by implication.

## 12. Arrears interaction

`ReadingSheetService.ComputeBillArrearsAmount` selects earlier qualifying Unpaid/Overdue bills for the same account, validates their authoritative snapshots and nonnegative RemainingAmount, and sums **prior RemainingAmount + active persisted penalty amounts**. Invalid/missing prior evidence returns unknown (null). It does not recurse through prior TotalDue/ArrearsAmount. New invoice TotalDue includes known frozen arrears, but new live RemainingAmount starts at current water + maintenance only. Existing adjustment effects carry through prior balances. Memo/PCA have no current effect and no conflicting arrears implementation was found. Future allocation of account-wide credits/debits and later reversals must be decided before integration.

## 13. Meter Maintenance interaction

Posting freezes water BillAmount and maintenance separately; TotalBillAmount/initial RemainingAmount include each once. Maintenance policy/config is its existing service; confirmed PHP 20 effective 2025-01-01 is unchanged. The Bill Adjustment service's explicit `LegacyMeterMaintenanceAdjustmentFloor = 20.00` is an existing rule, not a memo calculation. No memo service combines maintenance or interprets defective-meter labels as maintenance adjustments. The old Billing Collections view's PCA/MMF label is a **LEGACY CONFLICT in presentation**, not proof of shared financial storage. Deployed MMFAmount is a legacy nullable column absent from the current Billing entity; current authority comes from snapshots, not that column.

## 14. Other Charges and related legacy mechanisms

[OtherChargeService](../TMCWD/TMCWD.Data/Services/OtherChargeService.cs) owns contract parents, installment creation/activation, payments and parent refresh; its reconnection fee helper creates the approved PHP 350 service fee using that model. No memo ID/type/reference is linked to parent/child records and no memo creation invokes it. **No intentional memo-to-OtherCharge mapping or persisted duplication can be demonstrated**, especially with no deployed memo table. Demo data's reconnection-related debit is sample text, not an operational fee. Do not create a second fee merely from that example.

Related mechanisms with overlapping category names remain distinct:

| Mechanism/source | Actual behavior | Relationship / classification |
|---|---|---|
| Penalty waiver / `PenaltyService` | Requests Pending/Approved/Rejected; CSR/SuperAdmin submit, OIC/SuperAdmin review; bill/request locking, persisted reviewer/time/remarks; waiver changes penalty status | KEEP; memo WAIVE PENALTY label performs none of this |
| Senior citizen discount / `PaymentService` | At collection, rounds 20% of authoritative water-only snapshot; writes discount record and signed BillingAdjustment; cancellation records negative reversal and cancellation metadata | KEEP; generic DISCOUNT/privilege memo labels are not this workflow |
| Receipt cancellation / `PaymentService` | Restores saved balance and exact settlements with cancellation audit | KEEP; CANCELLED OFFICIAL RECEIPT category does not invoke it |
| AdvancePayment / `AdvancePaymentService` | Account-linked active record CRUD; no memo/PCA posting in service | KEEP separate; credit/refund/transfer/allocation policy must not be inferred |
| PaymentCheck / existing service | Check detail records; no memo/PCA implementation | KEEP separate; bounced-check memo behavior POLICY REQUIRED |
| Service fees / OtherCharges | Existing contract/installment workflows, including reconnection | KEEP; mapping of memo/service categories POLICY REQUIRED |

Penalty waiver review has no creator-versus-reviewer equality rejection; SuperAdmin can occupy both allowed roles. This proves only current waiver behavior, not a memo maker/checker policy. No deposit application, withholding-tax, overpayment refund or payment-transfer behavior is implemented by selecting those memo labels.

## 15. Collections

[BillingService.GetCollectionBill](../TMCWD/TMCWD.Data/Services/BillingService.cs) is the common read projection:

`remaining = settled ? 0 : MAX(0, Billing.RemainingAmount)`

`AmountDue = remaining + (settled ? 0 : active persisted penalties) + collectible OtherCharge installments`

BillAmount and maintenance come from issued snapshot; BillingAdjustment comes from live Billing. [CollectionsController](../TMCWD/TMCWD.Application/Controllers/CollectionsController.cs) forwards these amounts; [collections.js](../TMCWD/TMCWD.Application/wwwroot/js/collections.js) uses server AmountDue for row subtotal, selected bill total and payment submission. Maintenance is displayed separately without adding it again. Adjustment summary is informational because already in RemainingAmount. Other Charges/penalties are their actual collectible components; after-due estimates and snapshot arrears are not added a second time.

PCA is hardcoded zero in the browser's adapted bill, with hidden summary lines; CollectionBill has no PCA member and MVC does not return one. No memo query/amount is included. No current memo/PCA double count exists because neither has a financial source. A future non-null snapshot PCA alone would not make collection correct: approved liability posting and server projection must be added together. Verdict for Phase 6K mechanisms: **NOT IMPLEMENTED** (PCA has presentation provisions only).

## 16. Account Ledger

[AccountLedgerService](../TMCWD/TMCWD.Data/Services/AccountLedgerService.cs) opens a RepeatableRead transaction, uses GetCollectionBill for every bill and sums AmountDue for TotalBalance. Bills shows water, maintenance, live adjustment/remaining/penalty/charge amounts through that shared projection. Other Charges shows contracts/installments. History uses persisted bill/contract/activation/penalty/payment/cancellation/adjustment evidence; adjustment amounts are informational and do not reconstruct a running balance.

DCM is explicitly excluded by the existing `ponytail:` comment pending deployed schema/workflow. `AccountLedgerRegression` rejects SQL containing `debit_credit_memos`. Thus earlier Phase6G canonical-report references to memo history are superseded by [the deployed-schema fix](Phase6G-Account-Ledger-HTTP500-Fix.md) and current source. PCA has no bill component/history event. Future integration point: approved server GetCollectionBill for collectible effects and AccountLedgerService for persisted memo posting/reversal events. Do not resurrect the undeployed query or independently total raw memo amounts. Verdict: **NOT IMPLEMENTED** for memo/PCA, while existing balance/ledger foundations remain implemented.

## 17. Payment and cancellation

[PaymentService](../TMCWD/TMCWD.Data/Services/PaymentService.cs) locks billing references in deterministic order in a ReadCommitted transaction and locks installment records. It validates exact collectible amount, applies any confirmed senior discount, persists `Payment.BalanceBefore = collectibleBalance`, sets water RemainingAmount to zero/Paid, and records exact penalty/installment settlement links. No memo or PCA lookup exists. A pending memo neither prevents payment nor changes its amount.

Cancellation locks bills and receipt payments, rejects repeat cancellation/newer conflicting payments, requires saved BalanceBefore, restores that balance and Unpaid status, clears receipt linkage, reverses only associated paid penalties/installments, cancels discount records and writes negative discount reversal lines plus InvoiceCancellation audit. Historical receipts without authoritative BalanceBefore reject safely. This supports existing adjusted bills, but **does not certify future memo/PCA settlement or reversal**. Enabling an independent memo liability without updating this shared path would be a LEGACY CONFLICT. No such independent live liability exists now.

## 18. Memo references and numbering

Only proposed auto-increment `Id` exists; it is not a MemoNumber/ReferenceNumber/TransactionReference or accounting sequence. No prefix, server business-number generator, immutable business-reference guard or unique memo operation key exists. DcmDate/BillMonthBasis are user-entered dates, not identifiers; AccountNumber does not identify a bill. Demo `DCM-2024-001` numbers use in-memory array length and must not be adopted. **POLICY REQUIRED**: business reference format/scope, uniqueness, issuance timing, gaps/voids and audit retention. No format invented here.

## 19. Approval / maker-checker

Actual service transition: insert -> `Pending`. No Draft, Approved, Rejected, Posted, Cancelled or Reversed command/status transition exists for operational memos. Demo approved status is not evidence. Creator == approver cannot be evaluated for a nonexistent memo approval operation. Required ownership, independence, amount limits and roles are **POLICY REQUIRED**. Do not copy penalty-waiver exceptions as memo policy.

## 20. Idempotency

Memo Save always calls Add and SaveChanges; no OperationId, Idempotency-Key, unique business reference, stored response or replay validation. Browser button disabling only prevents some same-page concurrent submissions; retries/reloads can duplicate Pending rows if enabled. **IDEMPOTENCY REMEDIATION REQUIRED**. Current duplicate liability cannot occur through this service because it has no posting logic; future posting duplication risk must be guarded before balance writes exist. Existing adjustment operation receipts and payment receipt guards are usable reference patterns, not authorization or semantics.

## 21. Concurrency

Memo has a single-row SaveChanges implicit transaction but no explicit posting transaction, bill lock, status compare-and-set or ordering shared with other finance writes. Two inserts can create duplicate Pending intents; currently memo vs payment, credit vs payment, memo vs adjustment and memo vs cancellation do not touch a common obligation. There is consequently no present memo balance race to reproduce, and **CONCURRENCY REMEDIATION REQUIRED before implementation** cannot be called READY.

Future risk: independent read/change/write loses concurrent balance updates; multiple approvals post twice; inconsistent bill/receipt/child lock ordering deadlocks; reversal after a later payment or adjustment overwrites newer obligations. Reuse established deterministic Billing/settlement locking and atomic command boundaries once semantics are approved; do not change lock ordering in discovery. Existing adjustment locks serialize against current payment/cancellation; excluded migration-based race suites are not claimed passed.

## 22. Reversal, conflicts and enablement blockers

No memo edit/delete/cancel/reverse endpoint or service exists. No posted memo audit record deletion was found. Insert service is not an upsert; a client nonzero Id is not an approved edit path. Audit retention/reversal policy remains **POLICY REQUIRED**.

| Finding | Classification | Required disposition |
|---|---|---|
| Complete-looking form but denied save; missing table | MODERNIZE / POLICY REQUIRED | Keep quarantine; distinguish unavailable from invalid input/no records |
| Denied GetAll becomes success with empty history | LEGACY CONFLICT; MODERNIZE | Preserve failure category in future read bridge |
| Route-controlled financial actor; entity overbinding | SECURITY REMEDIATION REQUIRED | Claim identity + narrow validated command before enabling |
| No billing link/posting/reference/audit lifecycle | POLICY REQUIRED / MODERNIZE | Define ownership and explicit approved posting target |
| Duplicate create/retry; no posting locks | IDEMPOTENCY REMEDIATION REQUIRED / MODERNIZE | Atomic retry-safe command and concurrent operation protections |
| Fake approved demo memo/reconnection example | REMOVE / LEGACY | Do not promote sample data into semantics or duplicate fee |
| Old PCA/MMF view and zero placeholders | LEGACY CONFLICT in presentation; REMOVE / LEGACY | Replace only with approved server projection; no guessed PCA |
| Shared DTO int IDs vs entity bigint | MODERNIZE | Align actual boundary types when touched |
| Prior report claims DCM ledger history | REMOVE / LEGACY documentation claim | Current fix/source takes precedence |
| Snapshot mutation | No discovered current memo/PCA conflict | KEEP immutability; reject historical rewrite proposals |

## 23. Required stakeholder policies and reporting dependencies

Finance/Admin must explicitly decide:

1. Whether memos are accounting postings, approval requests, or documentary references to existing Bill Adjustments; which overlapping category uses which authoritative workflow.
2. Target scope: a bill, service-charge contract, penalty, account credit, payment transfer/refund or tax; treatment of unknown bills, closed accounts and settled/historical obligations.
3. Amount sign convention and precision/limits; debit/credit posting formula, floors and allocation, including excess credits and advance balances.
4. View/create/approve/post/reject/cancel/reverse roles, assignment/data scope, SuperAdmin inheritance and creator-versus-approver independence/thresholds.
5. Approval/posting timing, allowed states, date/backdate rules, immutable records, correction versus reversal and audit retention.
6. Memo reference numbering, issuance/void rules, external references and uniqueness; explicit retry/conflict semantics.
7. PCA business source: tariff/formula or manual input; effective date, rate/consumption basis, rounding, exemptions, negative values, authorization/audit, issuance freeze and historic unknown treatment.
8. Current liability, future arrears, payment allocation/cancellation/reversal treatment and separate delinquency rules for approved memo/PCA effects; preserve water-only penalty basis.
9. Meter maintenance separation, OtherCharge/service-fee mapping and protection against double liability; preserve existing reconnection fee/gate.
10. Required report presentation: posted versus pending/void/reversed values, gross issued versus live adjusted balances, separate component amounts and auditable period cutoff.

Phase 6N Accounts Receivable/Billing reports will need authoritative current balances, issued water/maintenance and (if approved) PCA, posted debit/credit magnitudes and signed effects, original/reversal links, account/bill/invoice/memo references, status, actor/approval/posting timestamps and as-of cutoff evidence. Current [CollectionsController.BuildReportRows](../TMCWD/TMCWD.Application/Controllers/CollectionsController.cs) reports payments (`PaidAmount`, receipt, date, collector, account), without memo/PCA component attribution; it is not an AR engine. No separate memo/PCA report source or operational AR/Billing report implementation was found. Do not derive receivables by summing raw Pending memo records or issued TotalDue; future reports must reuse approved ledger/projection sources and retain historical events for as-of reporting. No reports implemented here.

## 24. Recommended Phase 6K implementation order

| Phase | Scope / prerequisite | Reviewable exit condition |
|---|---|---|
| 6K-B | Resolve financial ownership and Bill Adjustment/memo overlap first; choose smallest approved domain and reviewed persistence script | Written semantics/target/status/reference policy; no competing balance engine |
| 6K-C | Claim-derived authorization, validated request boundary, approval/audit, operation receipt and posting/reversal transaction design | All-role/ownership/forged-actor denial checks; retry and audit integrity; routes still quarantined until complete |
| 6K-D | Approved memo liability in RemainingAmount/common Collections projection and payment/cancellation together; preserve arrears/penalty/snapshot invariants | Single collectible source, no duplicate fees, exact settlement/restoration; snapshots unchanged |
| 6K-E | PCA policy, effective source, precision, issuance snapshot and matching current liability; authorized mutation audit where required | New issued values reproducible; historic nulls retained; water penalty unchanged |
| 6K-F | Ledger persisted posting/reversal events, Customer Information history and operational form states; future reporting contract | Bills/charges/history agree; unavailable/error/approval state honest; no local balance engine |
| 6K-G | Real idempotency/concurrency/reversal tests in synthetic test schema, then separately authorized development integration | Concurrent memo/payment/adjustment/cancellation cases; read invariance and no customer test writes; explicit enablement decision |

Idempotency and concurrency design belong before any financial enablement in 6K-C/D; 6K-G validates them, rather than introducing safeguards after routes are enabled. If stakeholders decide memos are references to existing corrections, reuse that behavior instead of implementing duplicate accounting logic. PCA does not need speculative tables or services until its concrete source is approved.

## 25. Verification and final verdicts

Baseline `git status --short` was clean. Only this discovery document is intended to remain changed. No source/model/API/UI/SQL behavior was edited, no production deployment evaluated, and no migrations executed.

Verification results are recorded below after execution. Runtime memo creation/approval/posting was deliberately not exercised: routes are denied, schema is absent, and this task forbids finance mutations.

| Verdict | Result |
|---|---|
| DEBIT/CREDIT MEMO INVENTORY | COMPLETE for current source and visible development schema |
| PCA INVENTORY | COMPLETE for current source and visible development schema |
| CURRENT FINANCIAL SEMANTICS | POLICY REQUIRED for future memo/PCA; actual current no-effect behavior is clear |
| BILL ADJUSTMENT BOUNDARY | POLICY REQUIRED for memo overlap; existing adjustment formula/path is clear |
| REMAININGAMOUNT INTEGRATION | NOT IMPLEMENTED for memo/PCA; established current-bill foundation unchanged |
| SNAPSHOT COMPATIBILITY | SAFE for current code; future PCA freeze policy required |
| COLLECTIONS INTEGRATION | NOT IMPLEMENTED for memo/PCA; existing balance projection implemented |
| LEDGER INTEGRATION | NOT IMPLEMENTED for memo/PCA; memo exclusion is deliberate |
| AUTHORIZATION | SECURITY REMEDIATION REQUIRED plus POLICY REQUIRED; quarantine remains effective |
| AUDIT | REMEDIATION REQUIRED for operational memos/PCA |
| IDEMPOTENCY | REMEDIATION REQUIRED for memos |
| CONCURRENCY | REMEDIATION REQUIRED before memo/PCA posting |
| ROUTES SAFE TO ENABLE NOW | **0** |
| POLICY DECISIONS REQUIRED | Explicit list in section 23 |
| PHASE 6K-A | **PASS — discovery complete; no financial behavior changed** |
| PHASE 6K DEVELOPMENT INTEGRATION | **NOT YET EVALUATED** |
| PRODUCTION | **DEFERRED / NOT EVALUATED** |

### Executed checks

- Solution build `dotnet build TMCWD/TMCWD.slnx --no-restore`: **PASS**, 0 errors, 144 existing warnings. Initial sandbox write failure for Services generated artifacts was resolved by approved elevated retry. No warning suppression or source fix was made.
- BillingRegression `--no-migrations`: **PASS**, 631 database regression checks using connection-local temporary tables in dedicated `tmcwd_test`, plus penalty rounding/water calculator/MySQL query translation checks. Initial rebuilding of referenced Application assemblies hit the running app's locks; tests were compiled using freshly built references (`--no-restore -p:BuildProjectReferences=false`) and run with `--no-build`. An attempted application-account test connection was denied access; the existing `BILLING_TEST_CONNECTION` supplied the proper test connection. No grants changed and no app stopped.
- Migration scripts and migration-based invoice/payment concurrency suites: **NOT RUN**, explicitly excluded by `--no-migrations`; this is not a pass for those suites.
- `node tests/BillingRegression/browser-regression.cjs`: **PASS**, 25 tests, zero failures/skips.
- `node tests/BillingRegression/penalty-charging-regression.cjs`: **PASS**, 5 tests, zero failures/skips.
- `node tests/BillingRegression/collections-browser-regression.cjs`: **PASS**, including 31 Ledger browser checks, collection/payment scenarios and six viewport/theme combinations.
- `node tests/BillingRegression/customer-information-browser-regression.cjs`: **PASS**, account-selection/history escaping, keyboard/focus and service-status scenarios. Both headless suites use synthetic HTTP; initial sandboxed runs stalled and were stopped, then approved elevated retries completed successfully.
- No new implementation tests were added. Mobile parsing/display/cache source and existing tests were inspected; Flutter tests were not run because no mobile code changed.
- Repeated read-only development inspection after verification returned identical column/key/index metadata and snapshot aggregate counts. This is a bounded metadata/count comparison, not a digest of every financial row.

## Appendix A. Inspected development columns

The following metadata inventory records all 134 columns for the eight requested deployed tables and the two matching adjustment tables. `NULL` in the default column means no non-null default was reported; it does not itself mean the column permits null. Relationships are application links unless explicitly listed as enforced FKs above.

| Table | Column | SQL type | Nullable | Default | Key | Extra |
|---|---|---|---|---|---|---|
| accounts | Id | bigint | NO | NULL | PRI | auto_increment |
| accounts | CustomerId | bigint | NO | NULL |  |  |
| accounts | AccountNumber | varchar(50) | NO | NULL |  |  |
| accounts | MeterNumber | varchar(50) | YES | NULL |  |  |
| accounts | Classification | int | YES | NULL |  |  |
| accounts | MeterSize | decimal(6,2) | NO | NULL |  |  |
| accounts | ZoneBookId | bigint | NO | NULL |  |  |
| accounts | UnitNo | varchar(5) | YES | NULL |  |  |
| accounts | Building | varchar(50) | YES | NULL |  |  |
| accounts | HouseNo | varchar(10) | YES | NULL |  |  |
| accounts | Street | varchar(255) | YES | NULL |  |  |
| accounts | Barangay | varchar(50) | YES | NULL |  |  |
| accounts | IsCurrentAddress | bit(1) | YES | NULL |  |  |
| accounts | Sequence | int | YES | 0 |  |  |
| accounts | Status | int | YES | NULL |  |  |
| accounts | DateCreated | datetime | NO | CURRENT_TIMESTAMP |  | DEFAULT_GENERATED |
| accounts | CreatedBy | bigint | NO | NULL |  |  |
| accounts | DateUpdated | datetime | YES | NULL |  |  |
| accounts | UpdatedBy | bigint | YES | NULL |  |  |
| billing_adjustment_operations | OperationId | varchar(36) | NO | NULL | PRI |  |
| billing_adjustment_operations | BillingReferenceId | varchar(50) | NO | NULL |  |  |
| billing_adjustment_operations | ActorId | bigint | NO | NULL |  |  |
| billing_adjustment_operations | PayloadHash | char(64) | NO | NULL |  |  |
| billing_adjustment_operations | ResultJson | longtext | NO | NULL |  |  |
| billing_adjustment_operations | DateCreated | datetime(6) | NO | NULL |  |  |
| billing_adjustments | Id | bigint | NO | NULL | PRI | auto_increment |
| billing_adjustments | BillingReferenceId | varchar(50) | NO | NULL |  |  |
| billing_adjustments | Type | int | NO | NULL |  |  |
| billing_adjustments | Amount | decimal(6,2) | NO | NULL |  |  |
| billing_adjustments | Reason | varchar(255) | YES | NULL |  |  |
| billing_adjustments | CreatedBy | bigint | NO | NULL |  |  |
| billing_adjustments | DateCreated | datetime | YES | CURRENT_TIMESTAMP |  | DEFAULT_GENERATED |
| billing_adjustments | UpdatedBy | bigint | YES | NULL |  |  |
| billing_adjustments | DateUpdated | datetime | YES | NULL |  |  |
| billings | Id | bigint | NO | NULL | PRI | auto_increment |
| billings | AccountId | bigint | NO | NULL |  |  |
| billings | JobOrderId | bigint | YES | NULL |  |  |
| billings | BillingReferenceId | varchar(50) | NO | NULL | MUL |  |
| billings | PaymentTransactionId | varchar(50) | YES | NULL |  |  |
| billings | ReadingId | bigint | YES | NULL |  |  |
| billings | BillingPeriod | datetime | YES | NULL |  |  |
| billings | MaterialsAmount | decimal(6,2) | YES | NULL |  |  |
| billings | MMFAmount | decimal(6,2) | YES | NULL |  |  |
| billings | Penalties | decimal(6,2) | YES | NULL |  |  |
| billings | OtherCharges | decimal(6,2) | YES | NULL |  |  |
| billings | BillingAdjustment | decimal(6,2) | YES | NULL |  |  |
| billings | TotalBillAmount | decimal(6,2) | NO | NULL |  |  |
| billings | RemainingAmount | decimal(6,2) | YES | 0.00 |  |  |
| billings | Status | int | NO | NULL |  |  |
| billings | CreatedBy | bigint | NO | NULL |  |  |
| billings | DateCreated | datetime | NO | CURRENT_TIMESTAMP |  | DEFAULT_GENERATED |
| billings | UpdatedBy | bigint | YES | NULL |  |  |
| billings | DateUpdated | datetime | YES | NULL |  |  |
| billings | ServiceInvoiceNumber | varchar(30) | YES | NULL | UNI |  |
| billings | InvoiceSnapshotJson | longtext | YES | NULL |  |  |
| billings | BillArrearsAmount | decimal(18,2) | YES | NULL |  |  |
| other_charge_installments | Id | bigint | NO | NULL | PRI | auto_increment |
| other_charge_installments | OtherChargeId | bigint | NO | NULL | MUL |  |
| other_charge_installments | InstallmentNumber | int | NO | NULL |  |  |
| other_charge_installments | DueBillingPeriod | datetime | NO | NULL |  |  |
| other_charge_installments | Amount | decimal(6,2) | NO | NULL |  |  |
| other_charge_installments | PaymentStatus | int | NO | NULL | MUL |  |
| other_charge_installments | SettledPaymentId | bigint | YES | NULL | MUL |  |
| other_charge_installments | ActivatedBillingReferenceId | varchar(50) | YES | NULL |  |  |
| other_charge_installments | ActivatedAt | datetime | YES | NULL |  |  |
| other_charge_installments | CreatedBy | bigint | NO | NULL |  |  |
| other_charge_installments | DateCreated | datetime | NO | NULL |  |  |
| other_charge_installments | UpdatedBy | bigint | NO | NULL |  |  |
| other_charge_installments | DateUpdated | datetime | NO | NULL |  |  |
| other_charges | Id | bigint | NO | NULL | PRI | auto_increment |
| other_charges | BillingReferenceId | varchar(50) | NO | NULL | MUL |  |
| other_charges | Type | int | NO | NULL |  |  |
| other_charges | PayableIn | varchar(50) | YES | Cash |  |  |
| other_charges | Particulars | varchar(255) | YES |  |  |  |
| other_charges | IsActive | tinyint(1) | NO | 1 |  |  |
| other_charges | Amount | decimal(6,2) | NO | NULL |  |  |
| other_charges | PaymentStatus | int | NO | NULL |  |  |
| other_charges | CreatedBy | bigint | NO | NULL |  |  |
| other_charges | DateCreated | datetime | YES | CURRENT_TIMESTAMP |  | DEFAULT_GENERATED |
| other_charges | UpdatedBy | bigint | YES | NULL |  |  |
| other_charges | DateUpdated | datetime | YES | NULL |  |  |
| other_charges | SettledPaymentId | bigint | YES | NULL |  |  |
| other_charges | InstallmentCount | int | YES | NULL |  |  |
| payments | Id | bigint | NO | NULL | PRI | auto_increment |
| payments | BillingReference | varchar(200) | NO | NULL |  |  |
| payments | PaymentReference | varchar(200) | NO | NULL | MUL |  |
| payments | MacAddress | varchar(100) | NO | NULL |  |  |
| payments | PaidAmount | decimal(6,2) | YES | NULL |  |  |
| payments | Method | int | YES | NULL |  |  |
| payments | Success | bit(1) | YES | NULL |  |  |
| payments | CreatedBy | bigint | NO | NULL |  |  |
| payments | DateCreated | datetime | YES | CURRENT_TIMESTAMP |  | DEFAULT_GENERATED |
| payments | UpdatedBy | bigint | YES | NULL |  |  |
| payments | DateUpdated | datetime | YES | NULL |  |  |
| payments | BalanceBefore | decimal(65,30) | YES | NULL |  |  |
| payments | ChargesOnly | tinyint(1) | NO | 0 |  |  |
| penalties | Id | bigint | NO | NULL | PRI | auto_increment |
| penalties | BillingReferenceId | varchar(50) | NO | NULL | MUL |  |
| penalties | Amount | decimal(6,2) | NO | NULL |  |  |
| penalties | PenaltyTypeId | bigint | NO | NULL |  |  |
| penalties | PaymentStatus | int | YES | NULL |  |  |
| penalties | CreatedBy | bigint | NO | NULL |  |  |
| penalties | DateCreated | datetime | YES | CURRENT_TIMESTAMP |  | DEFAULT_GENERATED |
| penalties | UpdatedBy | bigint | YES | NULL |  |  |
| penalties | DateUpdated | datetime | YES | NULL |  |  |
| penalties | SettledPaymentId | bigint | YES | NULL |  |  |
| reading_sheets | Id | bigint | NO | NULL | PRI | auto_increment |
| reading_sheets | Name | varchar(50) | YES | NULL |  |  |
| reading_sheets | BillingDate | datetime | YES | NULL |  |  |
| reading_sheets | DueDate | datetime | YES | NULL |  |  |
| reading_sheets | DisconnectionDate | datetime | YES | NULL |  |  |
| reading_sheets | BillingPeriodStart | datetime | YES | NULL |  |  |
| reading_sheets | ZoneBookId | int | NO | NULL |  |  |
| reading_sheets | SeqFrom | int | YES | 0 |  |  |
| reading_sheets | SeqTo | int | YES | 0 |  |  |
| reading_sheets | AssignedTo | bigint | NO | NULL |  |  |
| reading_sheets | Status | int | YES | 1 |  |  |
| reading_sheets | CreatedBy | bigint | NO | NULL |  |  |
| reading_sheets | DateCreated | datetime | YES | CURRENT_TIMESTAMP |  | DEFAULT_GENERATED |
| reading_sheets | DateUpload | datetime(6) | YES | NULL |  |  |
| reading_sheets | UpdatedBy | bigint | YES | NULL |  |  |
| reading_sheets | DateUpdated | datetime | YES | NULL |  |  |
| readings | Id | bigint | NO | NULL | PRI | auto_increment |
| readings | AccountId | bigint | NO | NULL |  |  |
| readings | ReadingSheetId | bigint | NO | NULL |  |  |
| readings | Reading | decimal(6,2) | NO | NULL |  |  |
| readings | PreviousReading | decimal(6,2) | NO | 0.00 |  |  |
| readings | IsCompleted | tinyint(1) | NO | 0 |  |  |
| readings | Status | int | YES | 1 |  |  |
| readings | CreatedBy | bigint | NO | NULL |  |  |
| readings | DateCreated | datetime | YES | CURRENT_TIMESTAMP |  | DEFAULT_GENERATED |
| readings | UpdatedBy | bigint | YES | NULL |  |  |
| readings | DateUpdated | datetime | YES | NULL |  |  |
| readings | Remarks | varchar(2000) | YES | NULL |  |  |
