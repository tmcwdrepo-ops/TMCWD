# Phase 6G implementation and verification — October 8, 2026

**Historical implementation report.** The subsequent [financial-model cleanup report](Phase6G-Financial-Model-Cleanup.md) supersedes the legacy compatibility and deployment status below: development cleanup and migration are applied, and parent-only financial compatibility is removed. Phase 6G still awaits the canonical unified Account Ledger.

The new installment workflow is implemented and tested. Deployment is pending: the additive migration has not been applied to the application database. The separate Penalty Charging browser suite has one failure in an unchanged path, so this report does not declare Phase 6G fully complete.

## Read-only database audit

`other_charges.PayableIn` is nullable `varchar(50)`; `Amount` is `decimal(6,2)`. Parent and payment IDs, including nullable parent `SettledPaymentId`, are signed `bigint`. Billing references are `varchar(50)` and billing periods are nullable `datetime` physically. No foreign keys were returned for `other_charges` or `payments`.

The ten existing parent rows contain five `Cash`, one `3 months`, and four blank values. No automatic conversion, backfill, legacy schedule generation, or application-data update was performed.

## Explicit migration

Run `TMCWD/TMCWD.Data/Database/20261008_add_other_charge_installments.sql` before deploying the updated Data API and MVC application together. It is additive, rerunnable, and never invoked at startup. Its exact DDL was executed twice against isolated synthetic test tables.

The migration adds nullable `other_charges.InstallmentCount INT`. NULL preserves legacy semantics; a new validated draft stores its installment count. This marker is necessary to distinguish new drafts before child rows are generated from legacy numeric-looking metadata.

The child table `other_charge_installments` contains:

| Column | Physical type | Nullable |
| --- | --- | --- |
| Id | BIGINT AUTO_INCREMENT, primary key | No |
| OtherChargeId | BIGINT | No |
| InstallmentNumber | INT | No |
| DueBillingPeriod | DATETIME | No |
| Amount | DECIMAL(6,2) | No |
| PaymentStatus | INT | No |
| SettledPaymentId | BIGINT | Yes |
| ActivatedBillingReferenceId | VARCHAR(50) | Yes |
| ActivatedAt | DATETIME | Yes |
| CreatedBy / UpdatedBy | BIGINT | No |
| DateCreated / DateUpdated | DATETIME | No |

There is a unique constraint on `(OtherChargeId, InstallmentNumber)`, an index on `(PaymentStatus, DueBillingPeriod)`, and an index on `SettledPaymentId`. The unique index also supports parent lookup. Links follow existing service-enforced integrity conventions; no foreign keys or duplicated account identity were added.

## Validation and posting

The physical PayableIn column remains unchanged. Entity/shared legacy contracts now use nullable strings to accurately read historical NULL values. New requests require an ASCII-digit positive integer string that fits Int32. Blank, whitespace, signed values, zero, negatives, decimals, and text are rejected. All existing new-charge composers use blank numeric inputs with min=1 and step=1; no charge-type filtering was introduced.

The server also validates the selected fee type and the parent's actual monetary storage limits: positive amount, at most two decimal places, maximum 9999.99. Plans that would create zero-cent installments are rejected rather than inventing a settlement rule for zero-value rows. Posting rejects schedules exceeding supported DateTime months.

New charges are saved as Draft. Posting locks the billing anchor and parent, validates the count and amount again, creates exactly N uniquely numbered rows, and changes the parent to Unpaid in one transaction. Retrying posting never generates another schedule. Posted plans cannot be edited through SaveUpdate.

For N>1 the anchor must have an existing, account-matching completed reading and a valid BillingPeriod. A synthetic stub cannot activate such a plan. MVC creation returns the clear existing-billing-period validation message when no real billing exists; posting independently enforces the rule. N=1 retains safe stub support and still creates one child.

Periods are stored as the first day of the authoritative anchor's billing month, followed by AddMonths(sequence-1). Installment 1 activates immediately, with its activating billing reference recorded. Future rows use PaymentStatus.Draft; activated rows use Unpaid. No new overdue-date or waiver workflow was added.

Amounts use decimal arithmetic: base = decimal.Floor(total/count*100m)/100m; the final row = total-base*(count-1). Thus 1000/4 is four 250.00 rows and 1000/3 is 333.33, 333.33, 333.34.

## Monthly activation and Collections

PartialPost activates still-scheduled children for the account with DueBillingPeriod <= the newly posted billing month, inside its existing transaction. Anchor locks are acquired in sorted order before individual installment locks. Activation records the authoritative billing reference and actor/time. Calendar passage alone never activates a row; delayed billing catches up earlier scheduled rows. Retries never reset paid rows or create children.

Collections uses active posted parents' Unpaid/Overdue children. Draft, future, paid, and inactive rows are excluded. A parent with children never contributes its contractual Amount again. Legacy parents without children retain their existing conservative rollup/receipt compatibility path, including exclusion on settled legacy billing anchors; their metadata is not reinterpreted.

The existing CollectionBill response now includes the exact collectible rows used for its OtherCharges total. MVC uses that same response for the balance and modal rows, avoiding a second independently timed financial read. Paid water bills are still considered when they anchor newly activated installments.

New installment amounts are added separately to the live water/legacy balance and persisted water penalty. They are never rolled into Billing.RemainingAmount or BillArrearsAmount. Existing legacy billing reconciliation is preserved rather than mass-rewriting historical integrated balances. Combined water payments still settle their water obligation normally.

The real backend regression verifies October water/maintenance/penalty 207 + November water/maintenance 190 + installment 250 = 647, including a combined receipt and reversal. November RemainingAmount stays 190 before collection and after reversal; its water arrears remain 207.

## Payment, cancellation, and invoice protection

Other-charge-only requests carry separate legacy parent IDs and installment IDs. The backend rejects a parent-ID payment for a child-based plan, locks/rechecks selected individual children, calculates the amount from database rows, and rejects stale or future selections. Combined collection likewise locks and settles only currently collectible children. Receipt ownership and server receipt-number allocation are preserved.

Each settled child stores its own SettledPaymentId. Parent SettledPaymentId remains NULL for new plans; aggregate parent status becomes Paid only when every child is Paid. A paid installment cannot reappear, and future siblings remain untouched.

Cancellation locks the receipt/billing context and restores only child rows linked to the cancelled payment, then recomputes parent state. Repeated cancellation remains rejected. Installment-only reversals leave water principal/status untouched, including when the original water bill is already paid. Existing later-payment conflict protection remains in force.

Posting, activation, payment, and reversal never assign InvoiceSnapshotJson or change invoice identity/numbering, BIR/PTU, or QR information. Regressions compare immutable snapshot content across these operations. Water arrears and penalty calculation code were not changed. New installment-only collection does not settle an unrelated water penalty.

## Verification

- `dotnet build TMCWD/TMCWD.slnx --nologo`: attempted; running TMCWD.Data/Application processes lock the default output assemblies. Initial sandbox cache access also required an escalated retry.
- `dotnet build TMCWD/TMCWD.slnx --nologo --artifacts-path .tmp/phase6g-artifacts`: passed, zero errors; existing solution warnings remain.
- `dotnet run --project tests/BillingRegression --artifacts-path .tmp/phase6g-artifacts -- .`: passed, 81 temporary-table database regression groups, five pure penalty calculations, query translation checks, invoice concurrency/migration checks, receipt concurrency/migration checks, and new real two-cashier installment/migration checks. Uses the already configured dedicated local tmcwd_test connection; no live customer records are written.
- `node tests/BillingRegression/collections-browser-regression.cjs`: passed in headless Edge. Covers seven existing collection scenarios, new blank numeric/count validation, safe text rendering, draft exclusion, 647 reconciliation, installment-ID payment, and receipt cancellation.
- `node tests/BillingRegression/browser-regression.cjs`: passed, 25 checks including receipt/cancellation wiring.
- `node tests/BillingRegression/penalty-charging-regression.cjs`: four passed, one failed at line 39 with TypeError reading an undefined request's body. That suite and penalty implementation were not edited for this task.

Verification logs are in `.tmp/phase6g-*.log`. Remaining work is explicit migration/deployment, and resolution of the unchanged penalty-browser failure in its own scope. Overdue promotion and legacy schedule backfill are intentionally outside this implementation.

## Exact product/test files changed by this task

Existing dirty source changes were retained. Paths below describe this task's additions/edits; `.tmp/phase6g-*` contains local audit/edit helpers, logs, and isolated build artifacts.

- TMCWD/TMCWD.Application/Controllers/BillingController.cs
- TMCWD/TMCWD.Application/Controllers/CollectionsController.cs
- TMCWD/TMCWD.Application/Models/CollectionOtherChargePaymentRequest.cs
- TMCWD/TMCWD.Application/Views/Admin/Index.cshtml
- TMCWD/TMCWD.Application/Views/Billing/Collections.cshtml
- TMCWD/TMCWD.Application/Views/Billing/OtherCharges.cshtml
- TMCWD/TMCWD.Application/Views/Collections/Collections.cshtml
- TMCWD/TMCWD.Application/wwwroot/js/dashboard.js
- TMCWD/TMCWD.Application/wwwroot/js/other-charges.js
- TMCWD/TMCWD.Application/wwwroot/js/collections/oc-modal.js
- TMCWD/TMCWD.Billing/OtherChargeTransaction.cs
- TMCWD/TMCWD.Data/Context/UserDbContext.cs
- TMCWD/TMCWD.Data/Controllers/OtherChargeController.cs
- TMCWD/TMCWD.Data/Database/20261008_add_other_charge_installments.sql
- TMCWD/TMCWD.Data/Entities/OtherCharge.cs
- TMCWD/TMCWD.Data/Entities/OtherChargeInstallment.cs
- TMCWD/TMCWD.Data/Services/BillingService.cs
- TMCWD/TMCWD.Data/Services/OtherChargeService.cs
- TMCWD/TMCWD.Data/Services/PaymentService.cs
- TMCWD/TMCWD.Data/Services/ReadingSheetService.cs
- TMCWD/TMCWD.Model/Billing/CollectionBill.cs
- TMCWD/TMCWD.Model/Billing/OtherCharge.cs
- TMCWD/TMCWD.Model/Billing/Interfaces/IOtherCharge.cs
- TMCWD/TMCWD.Model/Billing/OtherChargeInstallments.cs
- TMCWD/TMCWD.Model/Billing/Requests/OtherChargePaymentRequest.cs
- tests/BillingRegression/Program.cs
- tests/BillingRegression/PaymentConcurrency.cs
- tests/BillingRegression/OtherChargeInstallmentsRegression.cs
- tests/BillingRegression/collections-browser-regression.cjs
- docs/Phase6G-Other-Charges.md
