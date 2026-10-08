# Phase 6G financial-model cleanup — October 8, 2026

The confirmed local synthetic development database was cleaned, the existing installment migration was applied, and parent-only Other Charges collection/payment compatibility was removed. At the time of this cleanup report, the canonical unified Account Ledger remained the next task. It is now complete; see [the canonical Account Ledger completion report](Phase6G-Canonical-Account-Ledger.md) for implementation, regression and Development host verification.

## Development database verification

The configured application connection was verified as local `tmcwd`, MySQL 8.0.46. The user explicitly confirmed that its accounts, billings, charges, payments, receipts and related history are synthetic development records. Neither credentials nor customer names were printed. The ten audited IDs, payment statuses, active flags, deprecated PayableIn values and billing links matched the preceding audit. Before deployment, both `other_charge_installments` and `other_charges.InstallmentCount` were absent.

The dedicated local `tmcwd_test` database was used for regressions; synthetic temporary/prefixed tables isolate tests from application data. Cleanup authorization was not generalized to that database or to production.

## Targeted cleanup and counts

Executed manually: `TMCWD/TMCWD.Data/Database/20261008_cleanup_legacy_other_charges_dev.sql`.

This script is marked DEVELOPMENT / SYNTHETIC DATA ONLY and requires an explicit session confirmation plus the exact database name. Its enforced CHECK guards reject changed charge/stub/payment states, shared receipts and unexpected dependent financial records. Execution used a verified localhost connection and a stop-on-error runner that closes/rolls back on failure. The financial deletions and application snapshot updates ran in one transaction. It is not a startup migration. It intentionally rejects rerunning against a changed or already-cleaned charge set rather than treating new rows as disposable.

| Table/context | Before | Removed/cleared | After |
| --- | ---: | ---: | ---: |
| other_charges | 10 | 10 | 0 |
| payments | 11 | 3 | 8 |
| payment_receipts | 11 | 3 | 8 |
| Billings | 35 | 5 | 30 |
| Official invoice snapshots | 13 | 0 | 13 |
| Accounts | 45 | 0 | 45 |
| Linked application charging snapshots | 4 | 4 | 0 |
| Linked application payment snapshots | 3 | 3 | 0 |

Exact charge IDs removed: **1, 2, 3, 4, 5, 6, 8, 9, 10, 11**.

Payment IDs removed: **5, 6, 7**. Three receipts were removed by their exclusive payment references: `OCO-1790830722476`, `CR005`, `CR006`. These receipts had no unrelated payment lines. There were no dependent check, wallet, e-wallet, cancellation, penalty, waiver, discount or billing-adjustment rows for the removed contexts. No remittance table existed in this application database.

Billing stubs removed: **8, 9, 10, 15, 16**. Each had ReadingId=0, JobOrderId=0, no official snapshot or invoice number, no materials, penalties, adjustments or arrears, and no legitimate financial dependency beyond the obsolete charges. Their integrated OtherCharges/TotalBillAmount balances were respectively 0, 2000, 0, 2000 and 3815; the only outstanding RemainingAmount was 2000 on stub 15. Removing these proven charge-only contexts removes the obsolete balance without inventing or recomputing any water balance. Unrelated water bill rows were not updated or deleted.

Applications **2, 10, 11, 14** had their explicitly linked charging snapshots cleared. Three linked payment snapshots were cleared. Application 2 returned from Payment to Charging; completed applications retained their workflow state. Account/customer/inspection/installation records were preserved. Post-checks found no removed references in any remaining charging, payment, inspection, installation, connect, tapping or done snapshots.

The first script preparation attempted a stored procedure, but the configured database principal lacks routine-creation privileges; no data changed during that failed attempt. The executed script instead uses a temporary CHECK guard table, available in the verified MySQL version, and no stored routine remains.

## Installment migration and final schemas

Applied successfully: `TMCWD/TMCWD.Data/Database/20261008_add_other_charge_installments.sql`. The existing script was reused without a competing schema or backfill. Migration rerun behavior is covered by the isolated database regression.

Final physical `other_charges` schema:

| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint | No |
| BillingReferenceId | varchar(50) | No |
| Type | int | No |
| PayableIn | varchar(50) | Yes |
| Particulars | varchar(255) | Yes |
| IsActive | tinyint(1) | No |
| Amount | decimal(6,2) | No |
| PaymentStatus | int | No |
| CreatedBy | bigint | No |
| DateCreated | datetime | Yes |
| UpdatedBy | bigint | Yes |
| DateUpdated | datetime | Yes |
| SettledPaymentId | bigint | Yes |
| InstallmentCount | int | Yes |

Parent Amount is the total contractual amount. InstallmentCount is the schedule count. Parent SettledPaymentId remains physically present but is never payment authority; supported writes leave it null.

Final physical `other_charge_installments` schema:

| Column | Type | Nullable |
| --- | --- | --- |
| Id | bigint, auto-increment primary key | No |
| OtherChargeId | bigint | No |
| InstallmentNumber | int | No |
| DueBillingPeriod | datetime | No |
| Amount | decimal(6,2) | No |
| PaymentStatus | int | No |
| SettledPaymentId | bigint | Yes |
| ActivatedBillingReferenceId | varchar(50) | Yes |
| ActivatedAt | datetime | Yes |
| CreatedBy | bigint | No |
| DateCreated | datetime | No |
| UpdatedBy | bigint | No |
| DateUpdated | datetime | No |

Verified indexes: primary Id; unique `UQ_other_charge_installment_sequence (OtherChargeId, InstallmentNumber)`; `IX_other_charge_installment_due (PaymentStatus, DueBillingPeriod)`; `IX_other_charge_installment_payment (SettledPaymentId)`. The existing deployment approach uses service-enforced links without adding foreign keys.

## Removed compatibility and final behavior

- OtherChargeService: removed metadata-only posting, UpdateBillingOtherCharges, parent rollups, parent-ID payments, parent settlement assignment, and old billing-balance reconciliation/closure. Draft management remains; edits require a draft without children. Posting creates exactly N children atomically or rejects invalid plans. Reposting an existing valid plan remains idempotent.
- BillingService: removed parent-derived balance recovery and mixed parent/child totals. Billing.RemainingAmount remains water-side only.
- PaymentService: removed parent-derived amounts, direct parent settlement and cancellation restoring parent Amount into billing totals/balances. Existing water and penalty calculations are retained.
- DashboardController: removed GET-time paid-parent/billing repair. Automatic registration fees now create a count=1 Draft plan rather than a Cash/Unpaid parent. A missing or invalid configured default leaves fee entry to the existing Charging composer.
- Request models and browser: Other Charges payments contain InstallmentIds only. Obsolete OtherChargeIds payment fields are rejected during JSON parsing. Parent IDs remain only for draft metadata/posting operations. Combined water collection keeps billing references and also requires the exact collectible child IDs per bill; missing, future, duplicate or stale child selections are rejected.

PayableIn accepts only an ASCII positive Int32 integer. Null, blank, zero, negative, decimal, whitespace/sign-prefixed, overflow and deprecated text values are rejected. All existing composers retain blank numeric inputs with min=1, step=1 and required. The physical nullable varchar is unchanged. Amounts use decimal, cent precision and the existing last-installment remainder policy.

Collections Other Charges = **SUM collectible child Amount**, restricted to active Unpaid/Overdue parents and Unpaid/Overdue children. Draft/future, Paid, inactive and invalid plans cannot be collected. A posted parent without the required child ledger produces a safe conflict instead of a parent fallback. Collection amount due is outstanding water balance + applicable persisted penalty + collectible installments; maintenance retains its existing water-side storage meaning and is counted once.

Dashboard's Charging queue card remains an application count. The Charging workspace money total is **SUM collectibleAmount**, returned through the same BillingService/OtherChargeService query as Collections. Draft/future contract totals contribute zero. Parent amounts remain visibly labeled total contractual amounts, and the workspace reloads authoritative amounts after posting.

Installment #1, monthly PartialPost activation, catch-up, exact cents, retry behavior and immutable invoice snapshots are preserved. Payment links the exact children to SettledPaymentId, and parent PaymentStatus is recomputed from all children. Cancellation restores only those children and recomputes the parent; installment-only cancellation does not reopen a paid water bill. The common anchor locks, stale cashier protections, receipt allocation and cancellation ownership rules remain.

For Account Ledger preparation, the existing parent-by-reference response now includes InstallmentCount and ordered child records with sequence, due month, amount, status, settlement ID and current payment reference. Existing payment/cancellation history services remain available. No legacy ledger mode or full ledger UI redesign was added.

## Integrity and verification

Executed `20261008_verify_other_charge_installments.sql` read-only. All thirteen anomaly counters were **zero**, including invalid posted plans, orphan children, invalid child settlement links, obsolete parent settlement links, dangling penalty links, missing/orphan receipts and every audited removed development context. Additional checks found no obsolete application snapshots. The cleaned application database currently has zero plans/children; nonempty posted-plan invariants are exercised by the synthetic regressions.

| Check | Result |
| --- | --- |
| `dotnet build TMCWD/TMCWD.slnx --nologo --artifacts-path .tmp/phase6g-clean-artifacts` | Passed, 0 errors; final run reported 14 package vulnerability-lookup warnings |
| `dotnet run --project tests/BillingRegression --artifacts-path .tmp/phase6g-clean-artifacts -- .` | Passed, 79 database regression groups, five penalty/currency cases, MySQL query translation and invoice/receipt/installment concurrency/migration checks |
| `node tests/BillingRegression/collections-browser-regression.cjs` | Passed, 11 actual headless Edge scenarios including combined collection carrying child IDs, charge-only payment/cancellation, and Dashboard child-only totals |
| `node tests/BillingRegression/browser-regression.cjs` | Passed, 25 tests; zero skipped |

Headless Edge required execution outside the filesystem sandbox. Browser HTTP data was synthetic and did not touch the application database. Temporary-table restrictions exposed a repeated-table query in the new integrity check; the check now uses one grouped ledger join. A browser test caught an intermediate bill mapping dropping installment IDs; both combined collection mappings now retain them. Obsolete parent-flow tests were removed or rewritten, rather than preserving that behavior to satisfy tests.

The separate Penalty Charging browser suite was **not rerun** in this cleanup. Its previously reported unchanged browser-suite failure was not modified as part of this task.

## Exact files changed in this continuation

Product code:

1. `TMCWD/TMCWD.Data/Services/OtherChargeService.cs`
2. `TMCWD/TMCWD.Data/Services/BillingService.cs`
3. `TMCWD/TMCWD.Data/Services/PaymentService.cs`
4. `TMCWD/TMCWD.Data/Entities/OtherCharge.cs`
5. `TMCWD/TMCWD.Data/Controllers/OtherChargeController.cs`
6. `TMCWD/TMCWD.Data/Controllers/BillingController.cs`
7. `TMCWD/TMCWD.Data/Controllers/DashboardController.cs`
8. `TMCWD/TMCWD.Model/Billing/OtherCharge.cs`
9. `TMCWD/TMCWD.Model/Billing/OtherChargeInstallments.cs`
10. `TMCWD/TMCWD.Model/Billing/Requests/OtherChargePaymentRequest.cs`
11. `TMCWD/TMCWD.Model/Billing/Requests/CollectionPaymentRequest.cs`
12. `TMCWD/TMCWD.Billing/OtherChargeTransaction.cs`
13. `TMCWD/TMCWD.Application/Models/CollectionOtherChargePaymentRequest.cs`
14. `TMCWD/TMCWD.Application/Controllers/BillingController.cs`
15. `TMCWD/TMCWD.Application/Controllers/CollectionsController.cs`
16. `TMCWD/TMCWD.Application/wwwroot/js/collections.js`
17. `TMCWD/TMCWD.Application/wwwroot/js/collections/oc-modal.js`
18. `TMCWD/TMCWD.Application/wwwroot/js/dashboard.js`
19. `TMCWD/TMCWD.Application/Views/Admin/Index.cshtml`
20. `TMCWD/TMCWD.Application/Views/Collections/Collections.cshtml`
21. `TMCWD/TMCWD.Application/Views/Billing/Collections.cshtml`

Scripts, tests and reports:

22. `TMCWD/TMCWD.Data/Database/20261008_cleanup_legacy_other_charges_dev.sql` (new)
23. `TMCWD/TMCWD.Data/Database/20261008_verify_other_charge_installments.sql` (new)
24. `tests/BillingRegression/Program.cs`
25. `tests/BillingRegression/OtherChargeInstallmentsRegression.cs`
26. `tests/BillingRegression/PaymentConcurrency.cs`
27. `tests/BillingRegression/collections-browser-regression.cjs`
28. `tests/BillingRegression/browser-regression.cjs`
29. `docs/Phase6G-Financial-Model-Cleanup.md` (new)
30. `docs/Phase6G-Other-Charges.md` (historical-status notice)
31. `docs/Other-Charge-Cleanup-Audit.md` (resolved-gate notice)

The existing installment SQL was executed, not edited. Auxiliary local files: `.tmp/phase6g-inspect/Program.cs`, `.tmp/phase6g-remove-legacy.cjs`, `.tmp/phase6g-remove-remaining.cjs`, `.tmp/phase6g-update-tests.cjs`, `.tmp/phase6g-simplify-service.cjs`; verification logs/artifacts use `.tmp/phase6g-clean-*` and `.tmp/legacy-charge-audit`. Unrelated dirty work was preserved. No reset, restore, clean, stash manipulation, commit or publication was performed.

## Remaining Phase 6G work

Implement the canonical unified Account Ledger across every page/modal/entry point using bill, parent metadata, child installment and payment/cancellation history contracts. Restart the running web/API applications to load the rebuilt code; existing development app processes were not killed. This task completes financial-model cleanup, not the full phase.
