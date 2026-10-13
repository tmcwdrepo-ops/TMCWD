# Phase 6I-B — Database-Backed Meter Maintenance Configuration

**Date:** 2026-10-08  
**Implementation status:** Approved design implemented in source and regression-tested; not deployed to a database. See [Phase6I-B-Implementation-Verification.md](./Phase6I-B-Implementation-Verification.md) for milestone evidence.
**Original proposal status (historical):** Design proposal only — no application code, migration, or database was changed at that stage.  
**Scope:** Replace the hardcoded amount used for newly posted monthly bills with an effective-dated database configuration. Preserve the Phase 6G and Phase 6I-A financial contracts.

## 2026-10-08 amendment — current approval supersedes 2026-01-01

The stakeholder changed the approved initial interval from **2026-01-01** to **2025-01-01**. The current contract is **PHP 20.00 / 2025-01-01 / NULL**, once per eligible monthly bill, selected by `ReadingSheet.BillingDate`; configuration changes remain SuperAdmin-only. The earlier 2026-01-01 approval and its dated audit findings were valid when recorded and are preserved as history in the verification/checklist documents.

Use only [`20261008_seed_meter_maintenance_rate_20250101.sql`](../TMCWD/TMCWD.Data/Database/20261008_seed_meter_maintenance_rate_20250101.sql). The previous `20261008_seed_meter_maintenance_rate_20260101.sql` is **SUPERSEDED / NOT FOR DEPLOYMENT**, rejects execution with SIGNAL, and retains its former body only in a comment. Never run both initial seeds. If a previous open-ended 2026 row exists, the new guarded seed must reject the overlap without updating/deleting it; resolution would require separate authorization.

The previously observed sheets dated **2025-07-01** are now **covered by the approved interval**. Their effective-date coverage blocker is resolved in the design; actual posting remains unavailable until separately authorized deployment and all other readiness gates pass. No sheet dates, financial records, administrator accounts or existing invoice snapshots are changed by this amendment. Dates before 2025-01-01 still fail closed without a separately approved interval. This is not deployment authorization.

## Executive recommendation

Add a dedicated effective-dated `meter_maintenance_rates` table and a server-side configuration service. Do not repurpose water tariffs or BIR invoice settings:

- [`WaterRate`](../TMCWD/TMCWD.Data/Entities/WaterRate.cs) and [`WaterRateService`](../TMCWD/TMCWD.Data/Services/WaterRateService.cs) represent tariffs by classification and meter size. Their records have `EffectiveDate` and `IsActive`, but fee applicability is universal, independent from those tariff keys, and the current lookup selects active tariff rows rather than resolving a fee period.
- [`BirInvoiceConfig`](../TMCWD/TMCWD.Data/Entities/BirInvoiceConfig.cs) and [`BirInvoiceConfigService`](../TMCWD/TMCWD.Data/Services/BirInvoiceConfigService.cs) configure PTU/invoice series. They have optional date bounds but unrelated meaning and active-series behavior. They are not billing fee settings.
- No suitable generic billing-settings table or service was found.

For every new posting, select exactly one maintenance amount by the normalized `ReadingSheet.BillingDate` date. This is already the date written to `Billing.BillingPeriod`; it is the authoritative bill date, not `Reading.DateCreated`, wall-clock posting time, due date, or a rate table's current-active flag.

If there is no matching configuration, or more than one matching configuration, fail posting explicitly and roll back. Never fall back to ₱20.00, select the latest row, skip the fee, or proceed with a partial invoice.

## Proposed schema

Add a new entity/table through an additive database script in `TMCWD/TMCWD.Data/Database`, following the repository's explicit, rerunnable database-script convention. Illustrative columns:

| Column | Proposed type | Purpose |
|---|---|---|
| `Id` | `BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY` | Stable configuration identity. |
| `Amount` | `DECIMAL(12,2) NOT NULL` | Configured monthly fee in PHP; `0.00` may be allowed only if a future approved policy explicitly sets it. |
| `EffectiveFrom` | `DATE NOT NULL` | Inclusive first authoritative billing date. |
| `EffectiveTo` | `DATE NULL` | Inclusive last applicable billing date; `NULL` means no scheduled end. |
| `CreatedBy`, `DateCreated` | Existing audit-column convention | Configuration audit. |
| `UpdatedBy`, `DateUpdated` | Existing audit-column convention | Audit changes to a not-yet-effective schedule, if edits are supported. |

Constraints and indexes:

- Check `Amount >= 0` and `EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom`.
- Unique index on `EffectiveFrom`, plus an index supporting lookup by effective bounds.
- Do not add `IsActive`: applicability is determined only by the billing-date interval.
- Do not model exemptions or account-category/meter-size selectors without an approved policy requiring them.
- Foreign keys are not required for the fee lookup. Store the authenticated administrator ID in audit fields; handle any service-actor seed explicitly.

MySQL does not provide a general exclusion constraint for overlapping date ranges. The configuration write service must serialize every insert/update with a database-wide named advisory lock on the same connection used for the transaction, then check for any existing interval satisfying:

`existing.EffectiveFrom <= new.EffectiveToOrMax AND new.EffectiveFrom <= existing.EffectiveToOrMax`

Reject an overlap with a conflict; also reject duplicate starts and invalid bounds. Hold the lock until commit/rollback and release it in `finally`. Fail explicitly if lock acquisition times out or fails. Restrict configuration writes to the protected application service; do not treat the unique-start index alone as overlap protection. Read lookup must still require exactly one match, so corrupted/legacy overlapping rows fail closed instead of choosing one nondeterministically.

Gaps between intervals are allowed by the schema but are operationally invalid for billing dates in those gaps: posting then fails closed. A new scheduled rate should normally start the day after the prior inclusive end date.

## Configuration authority and initial ₱20 row

The stakeholder-approved initial configuration is:

- `Amount = 20.00`
- `EffectiveFrom = 2025-01-01`
- `EffectiveTo = NULL`
- Rate selection date: `ReadingSheet.BillingDate`
- Applies once to every eligible monthly bill
- Configuration changes: SuperAdmin only

This approval **replaces** the former proposed `2026-10-08` effective date. The entity and additive schema-only migration source now exist; neither the schema migration nor the separate guarded seed has been executed. Approval of the row is not evidence that the configuration is deployed.

1. Keep the additive schema migration and initial configuration seed as separate SQL scripts. The migration creates the table and constraints only; it must not insert rates or backfill historical bills.
2. Use the separately guarded seed artifact [`20261008_seed_meter_maintenance_rate_20250101.sql`](../TMCWD/TMCWD.Data/Database/20261008_seed_meter_maintenance_rate_20250101.sql) for exactly `20.00 / 2025-01-01 / NULL`. If that exact row already exists and there are no conflicting overlapping rows, the seed is a no-op. If any row has the same effective start with different values, or any interval overlaps this open-ended interval, abort with a clear error and do not update/delete existing rows. Never use an upsert that overwrites existing configuration.
3. The seed requires the supplied audit actor to be an active, verified SuperAdmin and records that identity and timestamps using the schema's audit convention. It is run only after separate authorization to deploy to the selected database.
4. For `ReadingSheet.BillingDate >= 2025-01-01`, this rate matches unless a separately approved future schedule supersedes it. A bill date earlier than `2025-01-01` has no matching configuration and posting must fail closed with an explicit Finance-review requirement, unless a separately approved historical rate interval covers that date.
5. Do not use migration execution time or posting time to select a rate. Do not modify, backfill, or recalculate any previously issued billing row, payment, invoice snapshot, or historical balance. In particular, 2026 invoices already posted remain unchanged; the seed supplies rates to posting, not retroactive recalculation.

Future intervals must be non-overlapping and selected by the same billing date. A future scheduled change should close the previous interval on the preceding day and begin the new interval on its approved effective date. Any separately approved historical interval must likewise be non-overlapping. Deployment requires the table and approved seed in the authorized target database before the application relies on this configuration.

## Service, model, and posting design

1. Add an entity such as `MeterMaintenanceRate` and `DbSet<MeterMaintenanceRate>` in [`UserDbContext`](../TMCWD/TMCWD.Data/Context/UserDbContext.cs); add a focused service/interface registered in [`TMCWD.Data/Program.cs`](../TMCWD/TMCWD.Data/Program.cs).
2. Provide a server query `GetForBillingDate(DateOnly billingDate)` that:
   - queries `EffectiveFrom <= billingDate` and (`EffectiveTo IS NULL OR EffectiveTo >= billingDate`);
   - materializes enough to distinguish no row, exactly one row, and multiple rows;
   - returns the single configured amount or throws a specific configuration exception for zero/multiple matches.
3. Provide configuration writes through a SuperAdmin-authorized API consistent with the existing BIR config boundary, but with immutable effective history and overlap validation. Date-based reads needed by normal billing screens must use the repository's appropriate billing/collection authorization policy, not require SuperAdmin. All changes to the schedule, including future rates, remain SuperAdmin-only. The rate table is not a new generic settings framework.
4. In [`ReadingSheetService.PartialPost`](../TMCWD/TMCWD.Data/Services/ReadingSheetService.cs), resolve the maintenance amount using `readingSheet.BillingDate.Value.Date` before issuing any service invoice number or adding a billing row. Resolve it once per sheet/date, then use that amount for each eligible reading in the same posting transaction.
5. Replace the hardcoded fee in `ComputeWaterCharge` with an explicit amount parameter, or keep that helper strictly water-only and add the resolved fee at the posting composition boundary. Prefer the latter separation: tier charge lines and `snapshot.BillAmount` remain water-only; the configured amount is added exactly once to `TotalBillAmount` and initial `RemainingAmount`, and is stored as `snapshot.MeterMaintenance`.
6. Keep `snapshot.TotalDue` behavior: current water plus current maintenance plus known `BillArrearsAmount`; do not add a separate duplicate maintenance line to arrears or Other Charges.
7. Do not alter an existing snapshot or derive its maintenance from the new rate table. The snapshot already stores the exact applied amount, so the current snapshot schema does not need a new rate ID/version field for correctness.

Configuration absence/overlap is a financial posting error, not a successful skipped bill. Return/log a clear configuration failure through the existing API error path; do not swallow it in the batch, issue an invoice number, mark a reading complete, or report partial success. The surrounding transaction must roll back any posting work if the rate cannot be resolved.

## Full dependency inventory

The design-time repository search found these runtime/test references to `BillingCharges.MeterMaintenanceFee` and its alias. The source implementation removes its runtime charge-generation use; the value survives only as the explicitly named legacy Bill Adjustment floor:

| Reference | Implementation result |
|---|---|
| [`BillingCharges.cs`](../TMCWD/TMCWD.Model/Billing/BillingCharges.cs) | `MeterMaintenanceFee` was removed. `LegacyMeterMaintenanceAdjustmentFloor` remains solely for the unchanged adjustment compatibility rule; it is not used to calculate new bills. |
| [`ReadingSheetService.cs`](../TMCWD/TMCWD.Data/Services/ReadingSheetService.cs), `PartialPost` and private `ComputeWaterCharge` | `PartialPost` resolves by `ReadingSheet.BillingDate`, persists the applied amount in the snapshot, and includes it once in bill totals. Its private helper now calculates water only. |
| [`WaterChargeCalculator.cs`](../TMCWD/TMCWD.Billing/WaterChargeCalculator.cs) | Default calculator is water-only; bill total requires an explicit maintenance amount. |
| [`BillingController.cs`](../TMCWD/TMCWD.Application/Controllers/BillingController.cs) and [`ReadingSheetController.cs`](../TMCWD/TMCWD.Application/Controllers/ReadingSheetController.cs) | Sheet estimates use the selected sheet's authoritative billing date and resolved rate. Generic searches without a selected sheet remain water-only; no “latest/current” rate fallback exists. |
| [`BillingAdjustmentService.cs`](../TMCWD/TMCWD.Data/Services/BillingAdjustmentService.cs) | The existing floor formula remains unchanged apart from referencing the explicitly named `LegacyMeterMaintenanceAdjustmentFloor`; do not substitute a configured rate into historical invoices. |
| [`Program.cs`](../tests/BillingRegression/Program.cs) | Configured rate fixtures and boundary, overlap, authorization, posting, and snapshot-preservation checks were added. |
| [`reading.js`](../TMCWD/TMCWD.Application/wwwroot/js/reading.js) | Fixed-fee comment replaced with a note that the server resolves the rate by the active sheet's billing date. |
| [`TMCWD_Water_Rates_Config.json`](../TMCWD_Water_Rates_Config.json) | Unused duplicate fee property and fixed-fee prose were removed; this artifact is not the runtime source or seed. |
| [`Phase6I-Meter-Maintenance-Audit.md`](./Phase6I-Meter-Maintenance-Audit.md) and [`collections-audit-20261007.md`](./collections-audit-20261007.md) | Update their “hardcoded runtime source” statements only after the implementation is deployed and verified; preserve historical audit observations as dated evidence rather than rewriting them as if they were false when recorded. |
| Flutter source under `TMCWD.MeterReader.Mobile` | No local constant/calculation found. Keep official SOA and invoice preview server-issued/cached; do not add a client-side configuration or fee calculator. |

Collections, Account Ledger, payment, and current Phase 6I-A arrears do not need live rate lookups:

- `BillingService.GetCollectionBill`, `AccountLedgerService`, web Collections, and ledger JavaScript read the persisted invoice snapshot for the separate maintenance component and `Billing.RemainingAmount` for collectible current principal.
- Payment and cancellation use the persisted balance/linked payment state; they must not query the current fee configuration.
- Phase 6I-A `ComputeBillArrearsAmount` uses each qualifying prior bill's stored `RemainingAmount` plus persisted active prior penalties. Preserve this exactly; never replace it with a current configured fee or recompose the frozen snapshot.
- The mobile SOA API and `OfficialSoa`/service-invoice preview read the snapshot. No schema, SQLite migration, or Flutter billing calculation is required for this change.
- The 10% penalty still uses water-only `snapshot.BillAmount`; maintenance remains excluded.
- Meter maintenance remains a bill component and must never be inserted into `other_charges` or `other_charge_installments`.

## BillingAdjustmentService minimum-balance rule

`BillingAdjustmentService.SaveUpdate` and `SaveBatch` currently calculate:

`RemainingAmount = max(BillingCharges.LegacyMeterMaintenanceAdjustmentFloor, TotalBillAmount - BillingAdjustment)`

This is an adjustment floor, not a second maintenance posting. It does not currently consult the snapshot and it can become wrong when the configured rate changes: an old bill may receive the current fee as a minimum instead of the amount frozen on that invoice.

**Recommendation for a separately approved follow-up:** for a bill with a valid authoritative snapshot, use its frozen `MeterMaintenance` as the minimum floor, never a currently configured fee. This preserves the bill's original fee despite future rate changes and keeps the adjustment floor aligned with its immutable invoice. Do not implement this behavioral change automatically in Phase 6I-B.

For historical bills with a missing, malformed, unsupported, or incomplete snapshot, the fee cannot be safely reconstructed from current configuration, `TotalBillAmount`, or adjustment totals. Do not assume zero or today's ₱20. Before changing the floor, have Finance choose and document the legacy handling. Safest proposed behavior is to reject an adjustment requiring that floor and route the case for manual review, unless policy explicitly authorizes a documented legacy fallback. Until then, preserve the existing historical adjustment behavior as an explicitly identified compatibility exception; do not let it drive new bill charges.

## Expected financial behavior

Example with a configuration amount of ₱20.00 effective on a bill date:

- Water charge lines sum to `snapshot.BillAmount = ₱100.00`.
- Snapshot `MeterMaintenance = ₱20.00`.
- Posted `TotalBillAmount` and initial `RemainingAmount` = ₱120.00.
- Collections/Account Ledger display the persisted snapshot fee and current remaining balance; they do not add a second ₱20.
- Penalty remains 10% of ₱100 water only, i.e. ₱10, not 10% of ₱120.
- Other Charges/installments remain separate.
- A later configuration of ₱25 applies only to a bill date in its configured interval. Previously issued ₱20 snapshots remain byte-for-byte unchanged.
- Phase 6I-A arrears continue to use the prior bill's actual remaining current principal plus applicable persisted active penalty. They do not query this configuration or recurse into prior snapshot arrears.

## Migration and deployment plan

1. Review/approve the schema and confirm whether configuration management needs a new administrator UI or a protected API plus operational workflow. Only SuperAdmin may change configuration.
2. The initial effective date is stakeholder-approved as 2025-01-01. Confirm the Finance-review workflow for pre-start unposted sheets; do not infer or automatically add a historical rate. A separately approved historical interval may be configured if Finance authorizes it.
3. Add the entity, DbSet, service, DI registration, protected API/model, and non-overlap validation.
4. Add an additive/rerunnable DDL script creating the table, constraints, and indexes only. Do not migrate or backfill any `Billings` rows.
5. Prepare a separate guarded seed script/operation for exactly `20.00 / 2025-01-01 / NULL`. It must be idempotent for the exact row and reject any conflicting/overlapping row without overwriting or deleting it.
6. Update `PartialPost` and all estimate callers to use explicit date-resolved configuration. Keep posting transactional and fail closed on missing/overlapping rates, including pre-2025-01-01 dates unless a separately approved historical interval exists.
7. Leave the `BillingAdjustmentService` historical floor unchanged for this phase; retain only a clearly named compatibility value if removing the original central fee constant requires it. Do not treat that value as a source for newly issued bills.
8. Verify schema in a dedicated local/test environment, deploy schema before application code, and execute the separate seed only after authorization for the selected database. Confirm interval coverage for intended billing dates before enabling posting.
9. After deployment, update the audit/config artifact documentation to reflect the runtime source and preserve prior audit findings as time-bounded.

## Regression test plan

1. **Monthly once:** configured ₱20 on a synthetic bill date yields separate snapshot water/maintenance components and one inclusion in total/remaining; retry does not create another bill or fee.
2. **Immutability:** post under ₱20, change future configuration, then assert the old snapshot JSON is byte-identical and Collections/Account Ledger/mobile SOA still show ₱20.
3. **Approved initial boundaries:** `BillingDate` 2024-12-31 has no match and posting is blocked; 2025-01-01, 2025-07-01, 2026-01-01 and later covered dates select ₱20.00.
4. **Future effective-period switch:** a future approved rate (for example, ₱25 from an explicitly approved date) supersedes ₱20 only from that date; adjacent intervals select correctly using the sheet's `BillingDate`. Posting-time clock and reading date do not affect selection.
5. **Missing config:** no interval match rejects posting before invoice number allocation/financial mutation; no fallback to a constant or successful partial batch.
6. **Overlapping config:** service rejects an overlap and concurrent writes serialize; lookup encountering legacy/corrupt overlap fails closed.
7. **Adjusted previous bill:** Phase 6I-A regression remains: prior ₱100 water + ₱20 frozen maintenance less ₱30 adjustment contributes ₱90 remaining plus exact active persisted penalty. Changing current fee does not alter this result.
8. **Payment/cancellation:** full payment and cancellation preserve exact current principal, restore the prior balance, and never query or apply the new maintenance configuration to an issued bill.
9. **Previously issued 2026 invoices:** seed/configuration changes do not update or recalculate existing billing rows, payments, invoice snapshots, or historical balances; assert snapshot bytes and financial values remain unchanged.
10. **Flutter official SOA:** API response and offline cache retain the applied snapshot amount; service invoice renders it separately; no client fee calculation or SQLite schema change.
11. **Legacy missing snapshot:** existing ledger/collection behavior remains unchanged; arrears remain unknown where required. No current fee is backfilled or used to invent historic invoice components.
12. **Adjustment floor:** maintain regression coverage for current behavior in this phase, and add separate approved tests for snapshot-based floors and no-snapshot manual-review behavior only when that change is authorized.
13. **Other charges and penalties:** no maintenance row in either Other Charges table; penalty remains 10% of water-only amount; maintenance does not enter that base.

Tests that exercise DB writes must continue using the dedicated `tmcwd_test` database and the regression harness's connection-local temporary tables. Do not run fixtures against a live or customer database.

## Risks and unresolved decisions

- **Pre-2025-01-01 periods:** the initial date is approved as 2025-01-01. Finance must decide how to handle pre-start unposted sheets; posting remains blocked absent a separately approved historical rate.
- **Configuration administration:** decide who can create/schedule rates and whether the protected API alone is operationally sufficient.
- **Legacy adjustment floor:** Finance must approve snapshot-based floor behavior and no-snapshot handling separately.
- **External JSON consumers:** repository search found no runtime use of `TMCWD_Water_Rates_Config.json`. Its duplicate fee property and fixed-fee prose have been removed; confirm no deployment/external tooling depends on this unused artifact.
- **Direct database edits:** overlap guarantees rely on all routine writes using the serialized service; operational access should prohibit unsynchronized manual edits.
- **Estimated values:** current MVC estimate endpoints do not consistently carry a billing date. Each estimate must use its associated sheet/bill date or be labeled water-only/unofficial; silently selecting the latest configuration would produce nondeterministic estimates.
- **Concurrent posting/config updates:** service writes are serialized with a database named lock and reject configuration changes that would change the selected amount on existing billing dates. Posting snapshots remain immutable.

## Exact implementation steps

1. Add `MeterMaintenanceRate` entity and a `DbSet` with date/amount precision and interval constraints.
2. Add `IMeterMaintenanceRateService` and `MeterMaintenanceRateService` with exactly-one date lookup and serialized overlap-safe create/schedule operations.
3. Add API DTO/controller: authorize reads by billing date/list under the appropriate existing billing-operation policy, and restrict configuration writes to SuperAdmin; return explicit conflict/validation errors.
4. Add the additive SQL script for `meter_maintenance_rates`; keep it schema-only. The separate guarded seed artifact is [`20261008_seed_meter_maintenance_rate_20250101.sql`](../TMCWD/TMCWD.Data/Database/20261008_seed_meter_maintenance_rate_20250101.sql); do not execute either script without separate deployment authorization.
5. Change `ReadingSheetService.PartialPost` to resolve by `ReadingSheet.BillingDate.Date`, keep water charge lines independent, persist the resolved fee in the existing snapshot, and fail/rollback if configuration is absent or ambiguous. Pre-2025-01-01 sheets remain blocked unless a separately approved historical interval matches.
6. Replace fee-embedding in `WaterChargeCalculator`; update every estimate caller to pass/resolve the explicit billing date and avoid a fixed default.
7. Preserve Phase 6I-A arrears logic exactly and keep Collections, Account Ledger, payment/cancellation, penalty, and Flutter official SOA snapshot-driven.
8. Do not change `BillingAdjustmentService`'s historical minimum floor in this implementation; isolate/document its compatibility dependency and schedule the policy decision separately.
9. Remove the runtime charge-generation use of `BillingCharges.MeterMaintenanceFee`; retain only an explicitly named legacy adjustment compatibility value if required until the historical-floor decision.
10. Update test fixtures, stale JavaScript comments, and duplicate JSON/docs only after confirming there are no external consumers.
11. Run solution build and BillingRegression with `BILLING_TEST_CONNECTION` restricted to `tmcwd_test`; add the scenarios above and verify no production/development DB writes.
12. Before release, verify the seed's exact row and idempotency/conflict behavior, 2024-12-31/2025-01-01/2025-07-01/2026-01-01 boundaries, future interval selection, API authorization, rollback on missing/overlapping rates, invoice snapshot immutability, and unchanged Phase 6G/Phase 6I-A balances.

## Design verdict

**APPROVED DESIGN; source implementation and automated regression verification are complete. Deployment remains pending** authorized schema/seed execution and authenticated live workflow verification. Finance must still decide pre-2025-01-01 unposted-period handling and the legacy adjustment-floor policy. The initial row is approved as `20.00 / 2025-01-01 / NULL`. The separate migration and seed artifacts remain unexecuted; this document does not authorize database deployment or historical financial changes.

