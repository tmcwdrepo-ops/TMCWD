# Other Charge cleanup safety audit — October 8, 2026

**Historical audit; gate now resolved.** The user subsequently confirmed the local `tmcwd` database and the ten listed charges plus linked history are synthetic development records. Targeted cleanup and the installment migration have now been applied. See [financial-model cleanup report](Phase6G-Financial-Model-Cleanup.md) for the current state; the audit below records the original stop decision.

Implementation stopped at the database classification gate required by the user's cleanup instructions. No cleanup script was executed or prepared for destructive use, and no financial/application code was changed in this cleanup task.

The configured application connection targets local `tmcwd`, rather than the dedicated regression database `tmcwd_test`. Local hosting and development user-secrets do not establish that its customer/account/payment records are disposable synthetic data. Classification is **unverified application data; potentially real financial history**.

Read-only inspection covered every existing OtherCharge row and its linked billing/payment context without printing customer names, particulars, credentials, or connection strings.

| Charge ID | PayableIn | Status | Active | Linked payment rows |
| --- | --- | --- | --- | --- |
| 1 | Cash | Unpaid | No | 1 |
| 2 | Cash | Unpaid | Yes | 0 |
| 3 | Cash | Unpaid | No | 1 |
| 4 | Cash | Unpaid | No | 1 |
| 5 | 3 months | Unpaid | No | 1 |
| 6 | blank | Paid | Yes | 1 |
| 8 | blank | Paid | Yes | 1 |
| 9 | blank | Unpaid | Yes | 0 |
| 10 | Cash | Paid | Yes | 1 |
| 11 | blank | Paid | Yes | 1 |

All ten values are obsolete under the newly requested rule. Every parent SettledPaymentId is NULL. Linked payment counts are reference matches, not assertions that a receipt remains successful; these links still establish financial-history risk. Four active rows are marked Paid. None of these rows can be declared disposable from the available evidence.

The application database contains 45 account rows and 35 billing rows, including 13 official invoice snapshots. The ten charge anchors themselves have no reading or official snapshot, consistent with connection stubs; this does not prove that the connection charges/payments are fictitious.

`other_charge_installments` does not exist in this database. The prior additive installment migration remains unapplied. `PayableIn` remains nullable VARCHAR(50), parent Amount DECIMAL(6,2), and charge/payment identities signed BIGINT.

The compatibility branches identified for removal after the safety gate is resolved are:

- OtherChargeService: metadata-only update/post/deactivation branches, UpdateBillingOtherCharges parent-amount rollup, parent-ID payment, parent SettledPaymentId assignment, and legacy billing-balance reconciliation.
- BillingService: parent-derived balance recovery via max(RemainingAmount, legacy charge balance).
- PaymentService: parent-only amount fallback, parent settlement/link assignment, and parent-only reversal/billing restoration.
- Collections request/UI: parent charge payment IDs alongside installment IDs.
- Account Ledger: installment-only financial rows must replace the existing unsupported/parent-oriented Other Charges presentation; no separate legacy mode should be introduced.

No branch was removed in this task because the user explicitly required inspection/classification first and a stop if any obsolete row may represent real financial history. No values were translated, deleted, backfilled, or converted into guessed schedules.

To proceed, establish whether charge IDs 1, 2, 3, 4, 5, 6, 8, 9, 10, and 11 and their linked payment/billing history are confirmed disposable development records. If any are real, an explicitly authorized historical-data treatment is required instead of reset/deletion. Confirmation must also define the treatment of linked receipt history and integrated billing balances; deleting parents alone would leave financial inconsistencies.

The only task artifacts changed are this report and the local read-only audit helper `.tmp/phase6g-inspect/Program.cs`, with its output/build artifacts under `.tmp/legacy-charge-audit*`. No application build/regression pass is claimed for compatibility removal because implementation was stopped before financial edits.
