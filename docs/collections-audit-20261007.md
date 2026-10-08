# Collections / Cashier audit — 7 October 2026

## Result and scope

The main Collections payment flow now displays issued invoice components separately from the current collectible balance, uses server-side staff authorization, and serializes duplicate payment references. This is not a clean bill of health for every legacy billing endpoint. Remaining risks and unsupported business rules are listed below.

No automated payment or billing records were created in normal `tmcwd`. Database regressions require local `tmcwd_test`, use connection-local temporary tables or uniquely named synthetic tables, and clean up only their own synthetic tables. No Flutter files, production schema, rates, or billing allocation rules were changed by this audit.

## Architecture and endpoint map

The page is `TMCWD/TMCWD.Application/Views/Collections/Collections.cshtml`. Its scripts call MVC actions; domain transactions use the shared HTTP client; Data API controllers call EF/MySQL services. Flutter and browser JavaScript do not connect directly to MySQL.

| UI operation | MVC / transaction | Data API / persistence |
|---|---|---|
| Search outstanding accounts | `CollectionsController.SearchAccounts`; Account/Customer/Billing/OtherCharge/OtherFeeType transactions | Account and Customer lookup; `api/Billing/GetByAccountId/{id}`; new `GET api/Billing/Collection/{reference}` → `BillingService.GetCollectionBill`; charge/type reads |
| Submit selected bills | `CollectionsController.Pay` → `PaymentTransaction.ProcessCollectionPayment` | `POST api/Payment/ProcessCollection/{legacyUserId}` → `PaymentService.ProcessCollectionPayment` |
| Collect posted miscellaneous charges | `CollectionsController.PayOtherCharges` → `OtherChargeTransaction.ProcessPayment` | `POST api/OtherCharge/ProcessPayment/{legacyUserId}` → `OtherChargeService.ProcessPayment` |
| Search/view/reprint payment receipt | `SearchInvoices` / `Invoice` → PaymentTransaction | Payment API → `PaymentService.SearchInvoices` / `GetInvoice`; persisted payment/history reads |
| Cancel payment | `CancelInvoice` → PaymentTransaction | Payment API → `PaymentService.CancelInvoice`; transaction reverses payment and restores linked balances |
| Collection monitoring / daily list | `Monitoring` / `CollectionList` → User/Payment transactions | Payment history and user reads; browser modules render persisted amounts |
| Remittance | `CurrentCollector` / `RemittanceExpected` / `SaveRemittance` → CollectionRemittanceTransaction | `POST api/CollectionRemittance/Save`; derives expected totals from successful payments and persists remittance |
| Reports | `Collectors`, `CollectionReportPdf`, `CollectionReportExcel` | Payment/Billing/Account/Customer/User reads; existing report generation |
| Batch upload/list/detail | Collections batch actions → BatchPaymentUploadTransaction | BatchPaymentUpload API/service stores upload metadata and row count; it does **not** import payment rows |
| Account ledger | `/Billing/History` from `ledger-modal.js` | BillingController plus existing billing/payment history transactions |
| Service Invoice identity lookup | Existing `GET api/collection/service-invoice/{number}` | `ServiceInvoiceController` → Billing/Account/Customer services; read-only, Collection policy |
| Post late penalties | `BillingController.PostPenaltyCharges` → PenaltyTransaction | `POST api/Penalty/PostCharges/{legacyUserId}` → `PenaltyService.PostCharges` |
| BIR administration | Existing configuration API | `BirInvoiceConfigController`, SuperAdmin policy; not a customer receipt operation |

The legacy user-ID route parameters remain for compatibility. Financial write controllers now take the actor from validated JWT claims, not those parameters. MVC derives that JWT from its trusted server-side session, using the existing configured signing key/issuer/audience and a request-scoped HTTP client. MVC and Data must have matching JWT configuration. No token is sent to browser JavaScript or printed.

## Authoritative field sources

| Value | Source and meaning |
|---|---|
| Service Invoice number | Stored `Billing.ServiceInvoiceNumber`, assigned by posting. Never replaced with BillingReferenceId or PaymentReference. Historical null is visibly unavailable. |
| Internal billing identifier | `Billing.BillingReferenceId`, used to select and settle the correct persisted bill. |
| Payment reference / receipt | `Payment.PaymentReference`; the existing cashier UI prepares `CR00` plus entered reference. Only a real successful payment POST persists it. This is not the Service Invoice number. |
| Water-only BillAmount | Frozen `InvoiceSnapshotJson.BillAmount`. PartialPost calculates it using the applicable backend water-rate rules, then freezes it. Collection does not query current rates to reconstruct it. |
| Meter Maintenance | Frozen `InvoiceSnapshotJson.MeterMaintenance`. Its posting source is the shared backend constant `BillingCharges.MeterMaintenanceFee = 20.00m`, not a database setting, Flutter value, or miscellaneous OtherCharge row. |
| Other Charges | Persisted active `other_charges` records in Unpaid/Overdue states. Existing write-time rollup updates `Billing.OtherCharges`, TotalBillAmount and RemainingAmount. Internal OtherCharges excludes maintenance. |
| Billing Adjustment | Persisted `billing_adjustments` and aggregate `Billing.BillingAdjustment`. It is already reflected in the stored balance; the UI does not subtract it again. |
| Issued TotalDue | Frozen `InvoiceSnapshotJson.TotalDue`; an invoice issuance value, not automatically the amount to collect after later adjustments/payments. |
| RemainingAmount | Stored outstanding principal balance, with the existing connection-stub safeguard `max(0, max(Billing.RemainingAmount, active posted charges))`. Paid/Waived bills return zero. It does not include informational future penalty. |
| Applied penalty | Sum of persisted Unpaid/Overdue penalty rows. Paid/Waived penalties are excluded. Collection adds this to outstanding principal exactly once. |
| Amount currently payable | Server DTO `AmountDue = RemainingAmount + applied Penalty`; zero for a settled bill. Payment revalidates under bill row locks. |
| PenaltyAfterDue / TotalAfterDue | Informational values only: rounded 10% of frozen water BillAmount; frozen TotalDue plus that penalty. They do not create rows or increase today's collectible balance. |
| PeriodFrom / PeriodTo | Frozen invoice consumption dates from previous same-account authoritative reading and current posted reading respectively. First reading has null PeriodFrom. Collection never subtracts 30 days or uses today's date. |
| Billing / due / disconnection dates | Existing authoritative linked ReadingSheet dates; legacy BillingPeriod fallback for billing date only. No invented due/disconnection date for an unlinked historical bill. |
| Customer account/name/address | Existing Account/Customer records for collection account identification; mutable collection metadata is separate from frozen official invoice issuance data. |
| PCA / arrears | No confirmed component rules were found. No new amounts or calculations were introduced. Misleading zero PCA summary rows are hidden. |

The original UI was not rerunning water-rate tiers, but it **was reconstructing water charge from RemainingAmount minus today's maintenance constant**. That loses the original breakdown after adjustments/payments and invents a historical water-only value. That reconstruction has been removed. JavaScript contains no independently hardcoded maintenance charge.

Maintenance was not proven to be double-counted on the original normal unadjusted bill path: the old subtraction/addition happened to cancel. The confirmed failure was using the wrong component source. The corrected flow shows the frozen maintenance component once, and uses server AmountDue for payment rather than summing displayed historical components. Existing customer-facing invoice grouping remains unchanged; the Collections breakdown distinguishes maintenance from miscellaneous charges.

## Confirmed mismatches and corrections

| Severity | Before | Correction / current behavior |
|---|---|---|
| High | Collection inferred water charge from mutable RemainingAmount and a current shared maintenance constant. | New nullable CollectionBill DTO reads frozen issued water/maintenance. Historical missing components remain unknown; principal can still be viewed and paid. |
| High | Displayed component totals could drive payment amount after balance adjustments. | Rows, selected totals and submitted amounts use the server's current AmountDue. Original issued components and adjustments are explanatory, not re-added/re-subtracted. |
| Critical | Legacy financial Data API routes had no specific server authentication/role boundary, despite MVC role checks. Actor IDs could come from request routes. | Collection policy on Payment, remittance and batch controllers; BillingOperations on penalty/adjustment and relevant billing/charge writes. Actors come from validated claims. Meter Reader is denied. Residual unrelated legacy reads remain below. |
| High | Payment reference existence check preceded transaction without cross-bill reference serialization. Two sessions could create the same reference for different bills. | MySQL connection-scoped GET_LOCK on a hash of database + normalized reference surrounds duplicate check and transaction in both full-bill and charge payment services. Existing bill row locks remain. |
| High | Legacy raw Payment.SaveUpdate could persist a payment without the atomic balance workflow. | Authorized endpoint returns 409 directing callers to existing ProcessCollection / posted Other Charges workflows. No live caller of raw SaveUpdate was found in the Collections flow. |
| High | Legacy Penalty.SaveUpdate accepted arbitrary amount/date state and rewrites. | New creation delegates to the existing authoritative PostCharges service; existing posted penalty edits are rejected, with waiver workflow retained. Due-date, snapshot, paid/existing-charge checks apply. |
| High | Senior-citizen water base was reconstructed from current totals/charges. | Existing 20% discount uses frozen water BillAmount. If that source is unavailable, discounted payment rejects safely; ordinary historical principal payment remains possible. |
| Medium | Payment references were presented as invoice numbers, and Service Invoice identity was absent from bill selection. | Stored ServiceInvoiceNumber is shown separately; receipt/search/reprint/cancel labels use Payment Reference/Payment Receipt. Historical SI absence is explicit. |
| Medium | Bottom reprint/cancel buttons only produced success-like toasts. | They now open the existing real receipt/reversal modal workflows. |
| Low | Reprint UI promised account-name searching, but its endpoint requires the exact payment reference. | Reprint label/placeholder now requests a payment reference. Receipt search remains reference-based; no new customer-name search implementation. |

## Rule-by-rule assessment

1. **Water and maintenance:** Collection now reads frozen components, not current rate rules. Posting still uses the shared backend maintenance constant. Later rate changes do not affect issued components.
2. **Penalty base:** existing PostCharges and its legacy creation path use `LatePaymentPenalty.Calculate(snapshot.BillAmount)`. Maintenance, miscellaneous charges, adjustments, PCA and arrears are not added to the base. Examples 170/190 → 17/207 and 900/920 → 90/1010 are exercised.
3. **Due date:** eligibility is date-based, strictly after the authoritative due date. Explicit cutoff cannot charge today's/not-yet-due bills. Day-before, exact-day and day-after tests remain in the suite.
4. **Informational versus applied:** summary may return future penalty separately, but payable balance uses only persisted collectible penalty rows. Search/SOA/Service Invoice GETs do not post penalties.
5. **Historical compatibility:** missing snapshot means unknown water/maintenance/future penalty; new penalty posting rejects. Legitimate stored principal remains payable. No backfill or inferred snapshot was added. Historical senior-discount base remains unavailable and is safely rejected.
6. **Totals and adjustments:** frozen issued TotalDue is separate from mutable principal balance and applied penalty. Current adjustment is not included in the water-only penalty base and is not subtracted twice in UI.
7. **Payments:** current Collection requires full settlement of every selected bill's revalidated amount. Partial or greater-than-balance PaidAmount rejects. Tendered cash above the total is change, not a persisted advance payment. No principal/penalty/arrears/charge partial-allocation order was invented.
8. **Remaining balance:** full payment sets principal to zero, settles associated posted charges/penalties, and cannot drive it negative. Multiple successful payment rows may exist under one reference for distinct selected bills. Historical partial-payment allocation is not implemented by this flow; stored historical balance is not reconstructed during reads.
9. **Atomicity:** main bill and charge payment writes use transactions. Injected save failure leaves no partial payment/balance change. Stale balances are rechecked under row locks and rejected if the submitted amount differs.
10. **Idempotency:** double presses are disabled in the browser. Server duplicates reject rather than returning a replayed success; they do not create another payment. Same-reference different-bill and different-reference same-bill races are exercised with real two-connection tests.
11. **Read-only:** account searches, summary reads, payment receipt lookups and Service Invoice lookup contain no financial writes. New summary uses AsNoTracking and does not rebuild the invoice snapshot. Existing remittance table creation occurs on POST, not GET.
12. **Other Charges:** collectible sums exclude inactive/draft/paid records. Write-time rollup preserves existing storage meaning. Invoice snapshots are issuance-time records; later posted charges are mutable collection state and need not rewrite the original invoice.
13. **Authorization:** MVC Cashier/SuperAdmin checks and anti-forgery remain. Data Payment, Collection summary, Service Invoice lookup, remittance and batch operations allow Cashier/SuperAdmin only. Existing CSR/OIC penalty workflows retain their staff roles through BillingOperations (1/2/6/7). BIR changes remain SuperAdmin-only. Authentication alone does not grant Meter Reader access.
14. **Snapshot and SI immutability:** lookup/payment do not write InvoiceSnapshotJson or replace ServiceInvoiceNumber. Existing posting/immutable guards and serial concurrency/migration regressions remain passing.
15. **BIR and dates:** Collection receipt is a payment receipt, not a newly generated Service Invoice. No internal SeriesWidth/SeriesPrefix/EffectiveFrom is added to customer output; admin configuration remains. Consumption periods are server-issued; no separate mobile Reading Date row or mobile reconstruction was introduced.

## Remaining risks and unresolved rules

These are explicit audit findings, not assumptions silently fixed:

- **High — legacy read authorization:** generic Billing.GetAll/GetByAccountId/GetByBillingReference and some Account/OtherCharge read APIs still lack the new Collection policy. Their cross-module role contract must be defined before tightening them globally. The new Collection summary and cashier financial writes are protected; this does not make all legacy API data private.
- **High — adjustment write path:** BillingAdjustmentService.SaveUpdate saves the ledger and balance separately, lacks the batch method's settled-bill guard/row lock, and recomputes RemainingAmount from TotalBillAmount less aggregate adjustments with a current maintenance floor. SaveBatch is transactional and rejects settled bills, but retains that floor/recomputation rule. Adjustment timing and treatment of historical partial payments/changed maintenance need a confirmed policy; this audit did not invent one.
- **Medium — mixed lock ordering:** direct charge payment locks charge rows before linked bill rows; full-bill payment locks bills before updating charge rows. Concurrent mixed operations can deadlock and fail, although transaction rollback protects committed data. Real concurrency coverage added here covers full-bill settlements/reference reuse, not this mixed race or cancellation races.
- **Medium — cancellation matching:** reversal identifies paid charges/penalties using existing cashier/timestamp matching. Timestamp precision and mixed concurrent cancellation deserve focused tests; no new cancellation allocation rule was introduced.
- **Medium — remittance:** Save derives expected successful payment totals server-side, but creates its table at request time and has no per-day duplicate guard. OtherCharges is set to zero as a report category; included charge payments still contribute through payment amounts. Deposit/remittance revision rules and denomination accounting are not certified by this audit.
- **Medium — batch uploads:** CSV upload records metadata/count only, not actual imported payments. An upload listing must not be interpreted as confirmation that those accounts were paid.
- **Low — performance:** SearchAccounts performs per-account/per-bill API queries. Correctness source was fixed without introducing an unrequested bulk API redesign.
- **Unconfirmed business rules:** PCA, arrears, partial-payment allocation order, adjustment timing, special penalty exemptions, discounted adjusted balances, advance-payment behavior, receipt-number reservation/cancellation/reuse policy, and historical reconstruction beyond stored balances remain unresolved. No new formulas were added for them.
- **Integration limit:** MVC JWT bridge requires matching deployed JWT configuration. Browser fixtures and HTTP/controller regressions are separate from real database regressions; a logged-in browser against the full deployed MVC/API chain was not exercised. Existing PaymentTransaction methods sometimes turn non-success reads into empty lists; deployments must not treat missing data caused by misconfiguration as a certified zero balance.
- **Reference comparison limit:** advisory locks use the trimmed, uppercased reference text. Database collation equivalences beyond case (for example accented identifiers) are not covered by the new race tests; the current cashier numeric receipt input avoids that case, but a broader external identifier contract needs explicit normalization rules.

## Exact inspected files

Paths below are relative to the repository. Inspection included the relevant declarations/callers/queries; this is not a claim to have audited every unrelated method in these files.

- Root `AGENTS.md`.
- `TMCWD/TMCWD.Application/Program.cs`, `Controllers/CollectionsController.cs`, `Controllers/BillingController.cs`, `Controllers/HomeController.cs`, `Views/Collections/Collections.cshtml`, `wwwroot/css/collections.css`, `wwwroot/js/collections.js`.
- All `TMCWD/TMCWD.Application/wwwroot/js/collections/` modules: `remittance.js`, `collection-monitoring.js`, `my-collection.js`, `ledger-modal.js`, `invoice-search.js`, `reprint-invoice.js`, `cancel-invoice.js`, `oc-modal.js`, `collection-report.js`, `batch-payments.js`.
- `TMCWD/TMCWD.Services/WebService.cs`, `AuthenticatedUserService.cs`.
- `TMCWD/TMCWD.Billing/BillingTransaction.cs`, `PaymentTransaction.cs`, `PenaltyTransaction.cs`, `OtherChargeTransaction.cs`, `CollectionRemittanceTransaction.cs`, `BatchPaymentUploadTransaction.cs`, `BillingAdjustmentTransaction.cs`, `WaterRateTransaction.cs`; related Account/Customer/OtherFeeType transaction call sites.
- `TMCWD/TMCWD.Data/Program.cs`, `Context/UserDbContext.cs`; `Controllers/BillingController.cs`, `PaymentController.cs`, `PenaltyController.cs`, `OtherChargeController.cs`, `BillingAdjustmentController.cs`, `CollectionRemittanceController.cs`, `BatchPaymentUploadController.cs`, `ServiceInvoiceController.cs`, `BirInvoiceConfigController.cs`.
- `TMCWD/TMCWD.Data/Services/BillingService.cs`, `IBillingService.cs`, `PaymentService.cs`, `PenaltyService.cs`, `OtherChargeService.cs`, `BillingAdjustmentService.cs`, `ReadingSheetService.cs`, `ReadingService.cs`, `BatchPaymentUploadService.cs`, `BirInvoiceConfigService.cs`, `PaymentReferenceLock.cs`.
- Relevant Data entity/model mappings: Billing, Payment, Penalty, OtherCharge, BillingAdjustment, Account, Customer, Reading, ReadingSheet, CollectionRemittance, BatchPaymentUpload; `TMCWD/TMCWD.Model/Billing/Interfaces/BillingBase.cs`, `Interfaces/PaymentBase.cs`, `BillingCharges.cs`, `LatePaymentPenalty.cs`, `OfficialInvoiceSnapshot.cs`, `CollectionBill.cs`, payment/charge request and receipt response contracts, and shared role/payment status enums.
- Regression entry points and payment/invoice concurrency helpers listed below; existing invoice migration script `TMCWD/TMCWD.Data/Database/20261006_add_service_invoice_bir_config.sql`.

## Exact files changed for this audit

Pre-existing changes elsewhere in this working tree were preserved. This list identifies this audit's edits, not everything reported dirty by Git.

Created:

- `TMCWD/TMCWD.Model/Billing/CollectionBill.cs`
- `TMCWD/TMCWD.Data/Services/PaymentReferenceLock.cs`
- `tests/BillingRegression/PaymentConcurrency.cs`
- `tests/BillingRegression/collections-browser-regression.cjs`
- `docs/collections-audit-20261007.md`

Modified:

- `TMCWD/TMCWD.Application/Program.cs`
- `TMCWD/TMCWD.Application/Controllers/CollectionsController.cs`
- `TMCWD/TMCWD.Application/Controllers/BillingController.cs`
- `TMCWD/TMCWD.Application/Views/Collections/Collections.cshtml`
- `TMCWD/TMCWD.Application/wwwroot/js/collections.js`
- `TMCWD/TMCWD.Application/wwwroot/js/collections/invoice-search.js`
- `TMCWD/TMCWD.Application/wwwroot/js/collections/reprint-invoice.js`
- `TMCWD/TMCWD.Application/wwwroot/js/collections/cancel-invoice.js`
- `TMCWD/TMCWD.Billing/BillingTransaction.cs`
- `TMCWD/TMCWD.Data/Program.cs`
- `TMCWD/TMCWD.Data/Controllers/BillingController.cs`
- `TMCWD/TMCWD.Data/Controllers/PaymentController.cs`
- `TMCWD/TMCWD.Data/Controllers/PenaltyController.cs`
- `TMCWD/TMCWD.Data/Controllers/OtherChargeController.cs`
- `TMCWD/TMCWD.Data/Controllers/BillingAdjustmentController.cs`
- `TMCWD/TMCWD.Data/Controllers/CollectionRemittanceController.cs`
- `TMCWD/TMCWD.Data/Controllers/BatchPaymentUploadController.cs`
- `TMCWD/TMCWD.Data/Services/IBillingService.cs`
- `TMCWD/TMCWD.Data/Services/BillingService.cs`
- `TMCWD/TMCWD.Data/Services/PaymentService.cs`
- `TMCWD/TMCWD.Data/Services/OtherChargeService.cs`
- `TMCWD/TMCWD.Data/Services/PenaltyService.cs`
- `tests/BillingRegression/Program.cs`
- `tests/BillingRegression/InvoiceConcurrency.cs` (reuse synthetic-table interceptor)
- `tests/BillingRegression/browser-regression.cjs`
- `tests/MeterReaderAssignedRegression/Program.cs`

AGENTS.md already contains the confirmed money/date rules; no duplicate rules were added for this audit.

## Verification and practical limits

Final check results are recorded with command/log paths below. No skipped test is counted as passing.

| Check | Result |
|---|---|
| Solution build, separate BaseOutputPath to avoid running API file locks | Passed, 206 warnings, zero errors; warnings remain visible in `.tmp/collection-final-build.log` |
| BillingRegression | 5 pure penalty/rounding checks, 2 MySQL query translation shapes, 71 database checks passed |
| Invoice numbering / migration | Real two-connection serial locking and additive migration rerun passed on synthetic local tables |
| Payment concurrency | Same reference across different bills and different references against same bill passed with real independent connections |
| HTTP authorization regression | Passed, all six reported regression groups; covers bearer expiry/roles/claims, Service Invoice/BIR and financial write boundaries using synthetic services, including rejection of raw payment creation |
| `browser-regression.cjs` | 25 passed, zero skipped |
| `penalty-charging-regression.cjs` | 5 passed, zero skipped |
| `collections-browser-regression.cjs` | 4 actual headless Edge cases: before due, after applied penalty, adjusted balance, historical balance |

The browser test loads actual Razor page markup and all Collections scripts, searches/selects bills, checks SI/component/payable displays, submits a payment with anti-forgery header, prevents duplicate presses, and reloads the paid state. Its local HTTP fixture server holds synthetic payment state. It does **not** use live MVC or claim MySQL persistence; that boundary is exercised independently by database and HTTP/controller regressions. It does not automate native print dialogs, remittance deposit submission, batch import, or mixed cancellation/charge concurrency.

Initial plain `dotnet run` regression attempts hit DLL locks from the already-running Data API. They were not counted as passes. Final runs use separate output folders and leave that process untouched. No credentials, JWTs or connection strings are included in this report/log summary.

Commands executed (the regression connection was loaded silently from the Windows User environment):

```powershell
dotnet build TMCWD/TMCWD.slnx -p:BaseOutputPath='D:/Projects 20260720/TMCWD/.tmp/collection-final-output/'
dotnet run --project tests/BillingRegression -p:BaseOutputPath='D:/Projects 20260720/TMCWD/.tmp/collection-regressions/' -- .
dotnet run --project tests/MeterReaderAssignedRegression -p:BaseOutputPath='D:/Projects 20260720/TMCWD/.tmp/collection-auth/'
node tests/BillingRegression/browser-regression.cjs
node tests/BillingRegression/penalty-charging-regression.cjs
node tests/BillingRegression/collections-browser-regression.cjs
```
