# Two-cashier Collections hardening — 2026-10-07

## 1. Existing architecture

Collections uses browser JavaScript → MVC CollectionsController → Billing transaction HTTP clients → Data API → EF/MySQL services. A receipt can cover several bills, with one `payments` row per bill. Previously the browser supplied the receipt reference, cash-on-hand was editable and locally incremented, and charge posting/payment/cancellation did not consistently acquire bill locks first.

## 2. Cashier identity

CurrentCollector is already request/session scoped through AuthenticatedUserService; there is no global current-collector row to replace. Data payment, charge-payment and remittance writes derive identity from validated JWT claims. `payments.CreatedBy` remains the collecting cashier. Receipt headers use the same identity. Names are resolved from Users for display. Personal history rejects another cashier ID; date-range reports force the authenticated cashier unless the caller is SuperAdmin.

## 3. Shared reference allocator

PaymentReferenceAllocator inserts a `payment_receipts` header inside the payment transaction. MySQL AUTO_INCREMENT allocates its shared ID; the reference is `CR` plus the ID padded to at least six digits. Cash, Check and EWallet, including Other Charges payments, share this allocator. Browser InvoiceNumber is retained only as an ignored compatibility field. Check/provider references are separate ExternalReference values. Rollbacks may leave serial gaps; references are never reset or reused.

As explicitly approved, uniqueness is on `payment_receipts.PaymentReference`, **not** on bill-level `payments.PaymentReference`, because several legitimate bill lines share one receipt. The migration preserves those rows and backfills one header per legacy reference. Mixed-cashier/mixed-method legacy groups fail safely rather than acquiring an invented owner.

## 4. Double-payment prevention

Payments lock and reread the selected bill rows, reject Paid/Waived bills, and compare the submitted full-balance amount against the current authoritative balance, applicable posted charges, penalty and existing discount rules. A paid or changed bill returns 409: “The bill balance changed or has already been paid. Refresh and try again.” No second payment/header commits. Partial payments remain unsupported.

## 5. Transactions and lock ordering

Financial writes use ReadCommitted transactions and sorted bill-reference locks first. This keeps post-lock queries from using an older repeatable-read snapshot. Other Charges then lock selected charge IDs in order; cancellation locks its bill rows before the exact receipt's payment rows. Penalty posting, waiver submission and waiver approval also acquire bill locks before changing penalty state. Receipts and bill lines commit together with balance updates and settlement links. Indexes narrow bill, receipt, charge and penalty reference lookup locks. Unrelated bills can proceed on independent connections while another bill's payment is paused. There is no new application-wide mutex or automatic retry strategy; failed transactions surface and roll back.

## 6. Cash-on-hand

Confirmed formula: sum of today's successful Cash bill-line amounts for the authenticated cashier. Canceled payments have Success=false and are excluded. Check/EWallet and another cashier's receipts are excluded. The UI reads this value from RemittanceExpected, is read-only, refreshes after successful payment/cancellation, and discards stale responses. Unavailable totals do not masquerade as zero.

## 7. Daily totals

RemittanceExpected returns own daily Cash (`bills`), Check, Online, totalCollected and paymentCount. Payment count uses distinct receipt references rather than bill-line count. Monitoring is scoped to today's successful own Cash receipts. Existing supervisor date-range reports retain combined/per-cashier access. My Collection continues using own history.

## 8. Remittance

Existing remittance records remain cashier/date scoped. Data overwrites CollectorId from claims and derives expected cash/online amounts from that cashier's successful daily payments. Cash denomination counts remain separately recorded. As confirmed, remittance does not deduct from displayed daily cash receipts or implicitly close a shift. No new deduction/session accounting rule was invented.

## 9. Cancellation

Cancellation targets the exact persisted receipt, allows its collecting cashier or SuperAdmin, and locks bills before payments. New payments store BalanceBefore and ChargesOnly; settled penalties/charges link to their exact payment ID. Cancellation restores that recorded balance and only those linked items, independent of timestamp precision. A later settlement/current paid receipt mismatch blocks unsafe reversal. Canceled rows, header and audit survive; repeated cancellation is rejected without a second reversal. Historical receipts lacking the required balance snapshot are rejected for manual review rather than guessed.

## 10. Reprint

Reprint/search use persisted references and original CreatedBy, amount, method and timestamp. The receipt includes the resolved original cashier and separate external reference when present. Retrieval is read-only and does not allocate references, update balances, create payments or post penalties.

## 11. Files changed for this task

- Data: `Context/UserDbContext.cs`; `Entities/PaymentReceipt.cs` (new), `Payment.cs`, `Penalty.cs`, `OtherCharge.cs`; `Services/PaymentReferenceAllocator.cs` (new), `PaymentService.cs`, `OtherChargeService.cs`, `PenaltyService.cs`; obsolete `Services/PaymentReferenceLock.cs` removed; `Controllers/PaymentController.cs`, `OtherChargeController.cs`.
- Contracts: `Model/Billing/PaymentConflictException.cs` (new), `InvoiceRecord.cs`, `Requests/CollectionPaymentRequest.cs`, `OtherChargePaymentRequest.cs`.
- Web/client: `Billing/PaymentTransaction.cs`, `OtherChargeTransaction.cs`; `Application/Controllers/CollectionsController.cs`, `Models/CollectionOtherChargePaymentRequest.cs`, `Views/Collections/Collections.cshtml`, `wwwroot/js/collections.js`, `collections/oc-modal.js`, `cancel-invoice.js`, `reprint-invoice.js`.
- Migration: `Data/Database/20261007_add_collection_receipts.sql` (new).
- Tests: `tests/BillingRegression/Program.cs`, `PaymentConcurrency.cs`, `collections-browser-regression.cjs`; `tests/MeterReaderAssignedRegression/Program.cs`, `MvcApiAuthentication.cs`.
- This report. Paths in the first four bullets are relative to `TMCWD/TMCWD.*`. Earlier authentication/audit edits already present in the working tree are preserved.

## 12. Schema and deployment

The additive, rerunnable migration creates the receipt header with unique reference, adds nullable payments.BalanceBefore, payments.ChargesOnly, penalties.SettledPaymentId and other_charges.SettledPaymentId, and adds reference lookup indexes. Existing values and bill lines remain. Legacy numeric CR references seed the next shared serial. No startup migration runs.

**The application database migration was not applied.** Before deploying the new Data API, review/back up the database, stop all collection writers, apply `20261007_add_collection_receipts.sql` through the established controlled database-script process, resolve any ambiguous legacy-reference groups, then deploy/restart Data and MVC. Do not run collection writers with the new API against the old schema. The automated regression applied/reran this script only against uniquely named synthetic tables inside local `tmcwd_test`.

## 13. Added regression coverage

Real independent DB connections test different-account concurrent payments, same-bill contention, a stale second cashier, shared CR000126/CR000127 allocation after legacy CR000125, Cash/Check/EWallet ownership, database unique constraint, cancellation racing with payment, and Other Charges racing with stale full-balance collection. Tests also cover own/combined daily totals, cancellation exclusion and non-reuse, repeated cancellation, remittance ownership with forged browser values, unchanged invoice/reference/snapshot data, and zero financial mutation on reprint/search.

Temporary-table tests protect rollback of both payment and header, full-balance-only behavior, one header across multiple bill lines, and timestamp-independent exact charge/penalty reversal. JWT HTTP tests protect 401/403, role restrictions, claims overriding route IDs, personal-report ownership, supervisor combined reports and 409 propagation. MVC tests cover two independent current collectors, daily Cash/Check/Online totals and receipt counts across multi-bill receipts. Actual Razor/JavaScript runs in headless Edge cover server-issued references, reset, duplicate presses, server cash totals, all payment methods and a stale second-cashier refresh.

## 14. Verification

Commands use isolated BaseOutputPath because the development servers hold the ordinary DLLs open. BILLING_TEST_CONNECTION is loaded securely from the Windows User environment and never printed. No normal application data is used for write tests.

- `dotnet build TMCWD/TMCWD.slnx`: passed; no errors. Existing package/compiler warnings remain.
- `dotnet run --project tests/BillingRegression -- .`: 73 temporary-table database checks passed, plus existing invoice concurrency/migration and new receipt migration/two-cashier concurrency checks.
- `dotnet run --project tests/MeterReaderAssignedRegression`: passed, including actual JWT middleware, MVC cashier totals, personal/supervisor reporting and collection/charge payment authorization/conflicts.
- `node tests/BillingRegression/browser-regression.cjs`: 25 passed, zero failed/skipped.
- `node tests/BillingRegression/penalty-charging-regression.cjs`: 5 passed, zero failed/skipped.
- `node tests/BillingRegression/collections-browser-regression.cjs`: 7 actual headless Edge scenarios passed using synthetic HTTP responses.

## 15. Remaining manual browser checks

Use an authorized test deployment and synthetic accounts after applying the migration there. No real customer payment is needed for these checks.

1. Open two independent browser profiles; sign in as two different Cashiers. Verify each teller name and own daily cash total.
2. Load the same unpaid synthetic bill in both. Pay it in profile A, then submit B's stale payment. A gets the persisted CR reference; B gets the controlled conflict and refreshed bill, with no second receipt.
3. Pay different synthetic accounts simultaneously. Both succeed with distinct shared CR references, correct ownership and zero bill balances.
4. Repeat with Check and Online; verify their external references stay separate and physical cash does not increase.
5. Reprint each reference: original cashier/amount/method remain, totals and balances stay unchanged. Cancel A's receipt as A; B's receipt remains intact, A's balance is restored and only A's cash total drops. B cannot cancel A's receipt; SuperAdmin retains permission.
6. Save separate remittance counts in each profile. Verify their respective CollectorId/date and expected totals, with remittance shown separately from daily cash receipts.
7. Reset/select the next account: prior PaymentReference is cleared and reads “Generated upon payment.” Search/refresh never consumes a receipt serial.

## 16. Business-rule limits

Confirmed daily cash receipt semantics are implemented. Opening balances, shift/session close, remittance/turnover deductions and historical cancellation recovery still require explicit TMCWD rules or authoritative historical information. No such values are inferred. No billing rates, penalties, Service Invoice numbering, invoice snapshots, mobile workflow or partial-payment allocation was redesigned.
