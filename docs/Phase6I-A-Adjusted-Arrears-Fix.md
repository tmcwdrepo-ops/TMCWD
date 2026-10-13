# Phase 6I-A — Correcting Adjusted Prior-Bill Arrears

**Date:** 2026-10-08  
**Scope:** Use the persisted outstanding current-bill balance for qualifying prior bills when calculating `BillArrearsAmount`. This is a focused fix; it does not complete Phase 6I.

## Root cause

`ReadingSheetService.ComputeBillArrearsAmount` previously summed the original immutable snapshot's `BillAmount + MeterMaintenance` for every qualifying Unpaid/Overdue prior bill. A subsequent billing adjustment changes `Billing.RemainingAmount`, but does not rewrite that issued snapshot. Consequently, adjusted principal could be counted again in a later invoice's arrears.

## Modified methods

- [`ReadingSheetService.ComputeBillArrearsAmount`](../TMCWD/TMCWD.Data/Services/ReadingSheetService.cs): retains account, strict prior billing-date, and Unpaid/Overdue qualification; requires an authoritative version 1 invoice snapshot with numeric water and maintenance fields; then adds `Billing.RemainingAmount` plus only the qualifying persisted active penalties supplied by the existing posting query.
- Regression coverage in [`Program.cs`](../tests/BillingRegression/Program.cs): exercises the posting path, balance updates through `BillingAdjustmentService`, payment cancellation, and frozen snapshots.

The adjustment service, payment service, invoice snapshot structure, fee amount/applicability, and payment allocation rules were not changed. In particular, the historical adjustment floor remains unresolved policy-dependent behavior and was not modified.

## Calculation examples

| Prior-bill state | Old arrears contribution | Corrected contribution |
|---|---:|---:|
| Unadjusted: ₱100 water + ₱20 maintenance; ₱120 remaining | ₱120 | ₱120 |
| Unpaid, adjusted: ₱100 water + ₱20 maintenance − ₱30 adjustment; ₱90 remaining | ₱120 | ₱90 |
| Same adjusted bill with ₱17 persisted active penalty | ₱137 | ₱107 (₱90 principal + ₱17 penalty) |
| Zero remaining balance and no active penalty | ₱120 | ₱0 |

`RemainingAmount` is already the current-bill-only outstanding amount, inclusive of the bill's maintenance component after adjustment. The computation does not subtract `BillingAdjustment` again. It does not use `snapshot.ArrearsAmount`, `snapshot.TotalDue`, other charges, or future installments, and therefore avoids recursive arrears, maintenance duplication, or Other Charge duplication. A valid snapshot is still required before using the stored balance; a missing, malformed, unsupported-version, or incomplete component snapshot leaves calculated arrears unknown (`null`) rather than inferring a value.

## Regression scenarios and results

Automated validation run on October 8, 2026:

- Solution build: **PASS**, exit code 0.
- BillingRegression: **PASS**, exit code 0.
- The regression runner reported **101 database regression checks passed using temporary tables only**, plus its two-connection payment and installment concurrency/migration checks.

Targeted arrears coverage added/updated:

| Scenario | Result |
|---|---|
| Unadjusted prior balance of ₱120 | PASS |
| Adjusted unpaid prior balance of ₱90 | PASS |
| Adjusted prior balance plus exactly ₱17 persisted active penalty | PASS |
| Multiple prior billing periods; snapshot arrears and `TotalDue` do not recurse | PASS |
| Paid prior bill and its settled penalty excluded | PASS |
| Cancelled payment restores the Unpaid bill's ₱120 remaining principal and includes it once | PASS |
| Missing and malformed eligible snapshots leave arrears unknown | PASS |
| Zero remaining balance contributes no principal | PASS |
| Future Other Charge installment excluded | PASS |
| Same-period, future-period, and other-account bills excluded | PASS |
| Previously issued prior snapshots remain byte-for-byte unchanged | PASS |

The audit regression also confirms that collection still rejects partial and excess water-bill payments; this change adds no partial-payment support or allocation behavior.

## Remaining risks

- The district policy authority for the ₱20 fee and its applicability remains undocumented in the repository, as recorded in the [Phase 6I audit](./Phase6I-Meter-Maintenance-Audit.md).
- `BillingAdjustmentService` still applies the existing current-constant minimum balance floor. Its applicability to historical invoices remains a policy question and was explicitly outside this task.
- If persisted `RemainingAmount` is itself corrupt but nonnegative, this method cannot independently reconstruct the authoritative collectible amount without violating the no-recalculation rule. Existing adjustment/payment writes remain the source of that balance.
- The solution build retained existing NU1903 dependency-advisory warnings; BillingRegression emitted an existing EF1002 test-code warning. Neither blocked validation.

## Phase 6G contract

Phase 6G behavior remains intact:

- `Billing.RemainingAmount` continues to mean the current bill's outstanding balance only.
- Existing issued invoice snapshots remain immutable.
- Collections continues to use issued snapshot components.
- Persisted penalties are separate and retain the 10% water-only penalty base.
- Other Charges remain collectible through their installment rows and are excluded from water-bill arrears.

## Phase 6I-A verdict

**PASS — Phase 6I-A adjusted-arrears correction and requested regression validation completed.**

This verdict applies only to Phase 6I-A. It does not declare the broader Phase 6I audit complete or resolve its outstanding policy questions.
