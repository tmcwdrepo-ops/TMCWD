# Phase 6I-B — Implementation Verification and Effective-Date Amendment

**Updated:** 2026-10-08  
**Status:** Source implementation and automated regression verification complete; deployment and authenticated live workflow verification pending.  
**Scope:** Phase 6I-B database-backed Meter Maintenance implementation. Source status, automated test status, database deployment, and live workflow verification are tracked separately below.

## 2026-10-08 amendment — current approval: 2025-01-01

The stakeholder changed the approved initial date from **2026-01-01** to **2025-01-01**. The current configuration is **PHP 20.00 / 2025-01-01 / NULL**, once per eligible monthly bill, selected by `ReadingSheet.BillingDate`; SuperAdmin-only configuration authority is unchanged.

The only current initial seed is [`20261008_seed_meter_maintenance_rate_20250101.sql`](../TMCWD/TMCWD.Data/Database/20261008_seed_meter_maintenance_rate_20250101.sql). It preserves exact-row idempotence, active verified SuperAdmin audit validation, the shared configuration lock, overlap/conflict rejection and transactional insertion. It never updates/deletes existing rates or financial records. The previous 20260101 seed is **SUPERSEDED / NOT FOR DEPLOYMENT**: it raises SIGNAL and its former implementation is comment-only. Never run both initial seeds; a previously deployed open-ended 2026 row would be a conflict, not silently corrected by the new seed.

| Current billing-date boundary | Required behavior |
|---|---|
| 2024-12-31 | No initial rate; posting blocked before invoice allocation, pending reading preserved |
| 2025-01-01 | PHP 20.00, applied once |
| 2025-07-01 | PHP 20.00, applied once; prior July coverage blocker resolved by the amended interval |
| 2026-01-01 | PHP 20.00, applied once |
| Later covered dates | PHP 20.00 unless an approved non-overlapping future schedule supersedes it |

The synthetic fixture and lookup/posting boundary tests now use 2025-01-01. Existing future-schedule, immutable-snapshot, RemainingAmount-based arrears, water-only penalty, Other Charge/installment separation, legacy adjustment floor and claim-derived posting authorization tests remain in place. No financial service calculation or authorization source changed in this amendment.

**Safe amendment verification (2026-10-08): PASS.** Actual commands and results:

| Check | Amendment result |
|---|---|
| `dotnet build tests/BillingRegression/BillingRegression.csproj --no-restore -v quiet` | PASS, exit 0, zero errors; builds the affected test project and its Data/Billing/Application dependencies. Existing NU1903/EF1002 warnings remain. |
| `dotnet run --project tests/BillingRegression --no-build -- . --no-migrations` | PASS, exit 0, **132 database checks using temporary tables only in local `tmcwd_test`**, including the amended lookup/posting boundaries, future schedule, financial preservation and 22 posting-security groups. |
| `node tests/BillingRegression/browser-regression.cjs` | PASS, 25/25, no skips. |
| `node tests/BillingRegression/penalty-charging-regression.cjs` | PASS, 5/5, no skips. |
| Seed source inspection only | The retired seed has no executable DML and raises SIGNAL; the new seed contains only the approved configuration INSERT, retaining its exact-row, actor and conflict guards. Neither script executed. |

Each current boundary above passed both date lookup and posting/no-posting checks; 2027-06-01 is the later open-ended coverage fixture. Future-schedule supersession and immutable snapshots also passed. The July 2025 coverage blocker is **resolved by the approved interval**, not by a live database change.

Migration/seed execution is excluded with `--no-migrations`; the runner explicitly reports migration checks and the invoice/payment concurrency suites that execute migration SQL as NOT RUN. Historical migration/concurrency results below are not new results. Unchanged headless Collections and independent migration-executing ServiceRequest tests were not rerun for this date-only amendment. No SQL was executed against `tmcwd` or production, no operational administrator accounts were created/verified, and no persistent financial records were changed. Existing harness user/financial fixtures were confined to connection-local temporary tables.

**Deployment: NOT AUTHORIZED / NOT DONE.** Approved coverage does not mean the table/rate exists in the operational database. Backup readiness, legitimate seed-actor eligibility, separate authorization gaps and live deployment checks remain outstanding, as detailed in the checklist and posting-security remediation.

## Historical implementation record — prior 2026-01-01 approval

The remainder of this document preserves the earlier approval, original 107-check verification and source inventory as dated evidence. Its 2026 boundary expectations and seed references are superseded by the amendment above; do not use its historical deployment instructions to select a seed.

### Historical initial configuration (superseded)

The stakeholder-approved initial database configuration replaces the formerly proposed `2026-10-08` effective date:

| Field | Approved value |
|---|---|
| Amount | PHP 20.00 |
| EffectiveFrom | 2026-01-01 inclusive |
| EffectiveTo | NULL (open-ended until a separately approved schedule change) |
| Selection date | `ReadingSheet.BillingDate` |
| Applicability | Once to every eligible monthly bill |
| Configuration changes | SuperAdmin only |

The schema migration and initial rate seed remain separate scripts. The schema-only migration is [`20261008_add_meter_maintenance_rates.sql`](../TMCWD/TMCWD.Data/Database/20261008_add_meter_maintenance_rates.sql); it creates the table, audit columns, inclusive-date bounds, check constraints, and lookup/unique indexes, but does not seed or update billing records. The separate guarded seed at [`20261008_seed_meter_maintenance_rate_20260101.sql`](../TMCWD/TMCWD.Data/Database/20261008_seed_meter_maintenance_rate_20260101.sql) is idempotent for the exact approved row (`20.00`, `2026-01-01`, `NULL`) and rejects conflicting or overlapping existing rows without updating or deleting them.

**Neither the migration nor seed has been executed.** Deployment to any development or production database requires separate authorization.

## Milestone status

| Milestone | Status | Evidence |
|---|---|---|
| IMPLEMENTED IN SOURCE | PASS | Effective-dated entity/mapping, service/API, posting integration, explicit-fee calculator, and sheet-date MVC estimates are implemented. |
| TESTED | PASS | Solution build succeeded. BillingRegression passed 107 database checks using connection-local temporary tables in the dedicated local `tmcwd_test` database; concurrency/installment suites also passed. |
| DEPLOYED TO DATABASE | NOT DONE | No migration or seed was run. |
| VERIFIED IN LIVE WORKFLOW | NOT DONE | No authenticated MVC/API workflow was exercised after implementation. |

## Required selection and failure behavior

- Resolve rates by the normalized date in `ReadingSheet.BillingDate`, not by posting time, reading timestamp, due date, or “current/latest” status.
- For dates on or after 2026-01-01, the approved row applies unless a separately approved, non-overlapping future interval is effective for that billing date.
- For a date before 2026-01-01, no rate matches under the initial configuration. Block posting and require Finance review unless a separately approved historical interval is configured.
- Missing or overlapping matches must fail closed and roll back posting; do not use a hardcoded fallback, skip the fee, or issue a partial invoice.
- Configuration changes are SuperAdmin-only. Normal billing-date reads must use the appropriate billing-operation authorization.

## Boundary and preservation verification results

| Scenario | Expected behavior | Verification status |
|---|---|---|
| BillingDate `2025-12-31` | No match with only the approved initial row; posting blocked before invoice or invoice-sequence allocation, leaving pending reading unchanged. | PASS — regression |
| BillingDate `2026-01-01` | PHP 20.00 applies once in `RemainingAmount` and the frozen invoice snapshot. | PASS — regression |
| BillingDate `2026-01-02` | PHP 20.00 is selected by date lookup. | PASS — regression |
| Future approved rate schedule | Inclusive `EffectiveTo` and `EffectiveFrom` selection switches to the future amount; overlap fails closed. | PASS — regression |
| Existing posted 2026 invoice | Changing a future schedule does not alter the already-issued `InvoiceSnapshotJson`. Configuration writes also reject changes that would change the selected amount on an existing billing date. | PASS — regression |
| Phase 6I-A adjusted arrears | Qualifying prior `RemainingAmount` plus persisted active penalties; no recursive snapshot arrears/current rate lookup. | PASS — existing BillingRegression coverage |
| BillingAdjustmentService floor | Existing minimum-balance formula and PHP 20 compatibility floor remain unchanged; the constant is now named `LegacyMeterMaintenanceAdjustmentFloor`. | PASS — build and existing regression coverage |
| Other Charges and penalty | Maintenance remains outside Other Charges/installments; penalty remains 10% of water-only `BillAmount`. | PASS — existing BillingRegression coverage |
| Calculator and authorization | Water calculator returns water-only by default; explicit-fee path adds the supplied fee once. Read API uses `BillingOperations`; schedule list/write API uses `SuperAdmin`; service rejects non-SuperAdmin, inactive, or unverified actors. | PASS — regression/metadata and service checks |
| Collections, Account Ledger, cancellation, Flutter official SOA | Continue to present persisted snapshot/current balance values; no new mobile fee calculation was added. | PASS — existing BillingRegression coverage |

The regression runner confirmed the local `tmcwd_test` target and confined financial fixture writes to connection-local temporary tables. No persistent customer, development, or production financial records were changed.

## Implementation summary

- Added `MeterMaintenanceRate` entity and `UserDbContext` mapping for amount, effective dates, audit columns, check constraints, and indexes.
- Added `IMeterMaintenanceRateService`/`MeterMaintenanceRateService` with exactly-one date lookup, inclusive interval and amount validation, overlap rejection, a MySQL named lock for serialized configuration writes, active/verified SuperAdmin checks, and protection against changing selected amounts for existing billing dates.
- Added a protected API: date lookup requires `BillingOperations`; schedule listing and changes require `SuperAdmin`. Rate rows are managed through the API; no separate administration UI was added.
- `ReadingSheetService.PartialPost` resolves by the persisted `ReadingSheet.BillingDate` before invoice allocation. It stores the configured amount exactly once in `OfficialInvoiceSnapshot.MeterMaintenance`, includes it in initial bill/remaining balances, and rolls back on missing or ambiguous configuration.
- `WaterChargeCalculator` is water-only unless an explicit fee is passed. MVC sheet estimates use the selected active sheet's server-loaded billing date; generic account searches without a selected sheet remain water-only and are identified as estimates, never backed by a latest-rate fallback.
- Renamed the constant used solely by the unmodified Bill Adjustment floor to `LegacyMeterMaintenanceAdjustmentFloor`. The `BillingAdjustmentService` calculation itself was not changed.
- Removed the duplicate fixed fee from the unused `TMCWD_Water_Rates_Config.json` artifact and clarified that maintenance is configured separately.

Automated results: `dotnet build TMCWD/TMCWD.slnx` succeeded with zero errors (existing NU1903 vulnerability and nullable warnings remain); the test project built with zero errors (existing EF1002 warning remains); BillingRegression reported **107 database regression checks passed using temporary tables only**, plus passing two-connection invoice sequence/payment and installment concurrency checks.

### Exact Phase 6I-B implementation files

- `TMCWD/TMCWD.Data/Database/20261008_add_meter_maintenance_rates.sql` (new, unexecuted schema migration)
- `TMCWD/TMCWD.Data/Entities/MeterMaintenanceRate.cs` (new)
- `TMCWD/TMCWD.Data/Context/UserDbContext.cs`
- `TMCWD/TMCWD.Data/Services/IMeterMaintenanceRateService.cs` (new)
- `TMCWD/TMCWD.Data/Services/MeterMaintenanceRateService.cs` (new)
- `TMCWD/TMCWD.Data/Controllers/MeterMaintenanceRateController.cs` (new)
- `TMCWD/TMCWD.Data/Program.cs`
- `TMCWD/TMCWD.Data/Controllers/ReadingSheetController.cs`
- `TMCWD/TMCWD.Data/Services/ReadingSheetService.cs`
- `TMCWD/TMCWD.Data/TMCWD.Data.http`
- `TMCWD/TMCWD.Model/Billing/MeterMaintenanceRateDto.cs` (new)
- `TMCWD/TMCWD.Model/Billing/BillingCharges.cs`
- `TMCWD/TMCWD.Model/Billing/Requests/ReadingSheetAccountDto.cs`
- `TMCWD/TMCWD.Billing/MeterMaintenanceRateTransaction.cs` (new)
- `TMCWD/TMCWD.Billing/WaterChargeCalculator.cs`
- `TMCWD/TMCWD.Application/Program.cs`
- `TMCWD/TMCWD.Application/Controllers/BillingController.cs`
- `TMCWD/TMCWD.Application/Controllers/ReadingSheetController.cs`
- `TMCWD/TMCWD.Application/Models/PresentReadingViewModel.cs`
- `TMCWD/TMCWD.Application/Views/Billing/Reading.cshtml`
- `TMCWD/TMCWD.Application/Views/Billing/ReadingSheetDetails.cshtml`
- `TMCWD/TMCWD.Application/wwwroot/js/reading.js`
- `TMCWD_Water_Rates_Config.json`
- `tests/BillingRegression/BillingRegression.csproj`
- `tests/BillingRegression/Program.cs`
- `docs/Phase6I-B-Meter-Maintenance-Design.md`
- `docs/Phase6I-B-Implementation-Verification.md`

The separate approved seed [`20261008_seed_meter_maintenance_rate_20260101.sql`](../TMCWD/TMCWD.Data/Database/20261008_seed_meter_maintenance_rate_20260101.sql) was already present before this implementation task; it was reviewed but not changed or executed.

## Historical and invoice guarantees

This configuration controls the amount applied to a bill when it is newly posted. It does not authorize updating/recalculating any already-issued invoice, payment, snapshot, or historical balance. The exact configured amount used at posting is frozen into `OfficialInvoiceSnapshot.MeterMaintenance`; Collections, Account Ledger, payment cancellation, arrears, and Flutter official SOA continue to use persisted financial values.

Keep the Phase 6I-A corrected arrears behavior. Do not place maintenance in Other Charges. Do not change the existing `BillingAdjustmentService` minimum balance rule in this phase.

## Related design

See [Phase6I-B-Meter-Maintenance-Design.md](./Phase6I-B-Meter-Maintenance-Design.md) for the approved schema/API design, seed safeguards, adjustment-floor analysis, and implementation rationale.

## Verification verdict

**INCOMPLETE — implementation is present in source and automated regressions pass, but the migration/seed have not been authorized or deployed and authenticated live workflow verification remains outstanding.**

No migration or seed was executed. The only database writes during verification were connection-local temporary regression fixtures in the dedicated `tmcwd_test` database. No historical financial record was changed.

## Phase 6I-C handoff / remaining risks

1. Obtain separate deployment authorization, then apply the schema migration and guarded initial seed to the explicitly selected target. Verify the resulting row without changing prior invoices or balances.
2. Perform authenticated SuperAdmin schedule-management and BillingOperations date-lookup checks, then an authenticated test posting/estimate workflow in an approved non-production deployment.
3. Schedule separate Finance approval for the retained historical Bill Adjustment minimum-balance floor and its legacy no-snapshot behavior. This implementation intentionally preserves the old formula.
4. Keep direct database edits restricted; named-lock overlap guarantees apply to service-mediated writes, not unsynchronized external edits.

