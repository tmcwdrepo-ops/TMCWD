# Phase 6I-B — Development deployment verification

Date: **2026-10-09**, Asia/Manila. **Current DEVELOPMENT INTEGRATION: PASS. PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.** See the final-verification continuation below. Sections 1–20 preserve the earlier failed automated attempt and its then-current results.

## 1. Scope and classification

The stakeholder explicitly classified the current local database/accounts as **synthetic DEVELOPMENT / MOCK DATA** and authorized the specific mock SuperAdmin verification, additive maintenance migration and guarded 20250101 seed. This changes the development authorization scope from B5/B6; it does not invalidate their historical findings or approve production deployment. No production-style bootstrap, permission grant, anonymous endpoint, financial-calculation change or recovery operation was introduced.

The task could not complete: the configured development MySQL operator lacks the routine privilege needed by the reviewed seed. Its application security role and its database privileges are independent. The empty schema and verified mock account remain; no rollback was invented or performed.

## 2. Verified target before writes

Configuration/user-secrets and server SQL matched **127.0.0.1:3307 / tmcwd**, hostname **DESKTOP-Q72GRF4**, version **8.0.46**, UUID **c3f950fe-b565-11f1-b6a6-30560f67a354**, datadir `D:\Projects 20260720\TMCWD\recovery\tmcwd-20260921\data\`. No checked Process/User/Machine DefaultConnection override existed. Development configuration selected the previously reviewed local user-secrets connection. No password or hash was printed.

Before migration, `information_schema.tables` reported **0** maintenance tables. The mock administrator was exactly **Id 5 / Role 1 / Active 1 / Verified 0**. The guarded script refused materially different configuration, server identity, preexisting table or account flags.

## 3. Mock SuperAdmin preparation

Authorized development-only update changed exactly **one user row**, Id 5:

| Flag | Before | After |
|---|---:|---:|
| Role | 1 | 1 |
| Active | 1 | 1 |
| Verified | 0 | 1 |
| Eligible active+verified SuperAdmins | 0 | 1 |

Only `IsVerified=1`, `DateVerified=NOW()`, `DateUpdated=NOW()` and `UpdatedBy=5` changed, using existing verification/audit conventions. The row was transactionally locked and the update predicate required Id 5, Role 1, active, previously unverified, exact schema/port/UUID. Stored verification/update timestamp: **2026-10-09 14:40:20** (database timestamp, without asserting timezone). Name, email, password/hash, role, activation, other users and financial records were not updated. No login/reset or permanent bootstrap mechanism was added. This establishes development eligibility only, not production organizational authority.

## 4. Migration execution

**PASS.** Executed only `TMCWD/TMCWD.Data/Database/20261008_add_meter_maintenance_rates.sql` on the verified development target. It created the additive table; initial count **0**. No unrelated migration ran on `tmcwd`. Checks below were inspected before attempting the seed.

## 5. Exact resulting schema

Actual SHOW CREATE TABLE output (also retained in `.tmp/phase6ib-dev-schema.txt`):

```sql
CREATE TABLE `meter_maintenance_rates` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `Amount` decimal(12,2) NOT NULL,
  `EffectiveFrom` date NOT NULL,
  `EffectiveTo` date DEFAULT NULL,
  `CreatedBy` bigint NOT NULL,
  `DateCreated` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `UpdatedBy` bigint NOT NULL,
  `DateUpdated` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UQ_meter_maintenance_rates_effective_from` (`EffectiveFrom`),
  KEY `IX_meter_maintenance_rates_effective_interval` (`EffectiveFrom`,`EffectiveTo`),
  CONSTRAINT `CK_meter_maintenance_rates_amount` CHECK ((`Amount` >= 0)),
  CONSTRAINT `CK_meter_maintenance_rates_interval` CHECK (((`EffectiveTo` is null) or (`EffectiveTo` >= `EffectiveFrom`)))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci
  COMMENT='Effective-dated monthly Meter Maintenance billing configuration';
```

## 6. Approved seed attempt

**FAIL — DATABASE ROUTINE PRIVILEGE REQUIRED.** Attempted only the reviewed `20261008_seed_meter_maintenance_rate_20250101.sql`, with `@authorized_service_user_id=5` in that same dedicated connection after development eligibility checks.

Actual MySQL error **1370 / SQLSTATE 42000**: `alter routine command denied` for the seed helper `_seed_meter_maintenance_rate_20250101`. The script stopped at its initial DROP PROCEDURE IF EXISTS, before helper creation/call or insertion. No `SEEDED` success was returned. Follow-up metadata confirmed **0 helper routines** with that name and **0 configuration rows**.

No GRANT, new privileged user, credential reset, direct INSERT fallback or modified seed was used to bypass the failure. A designated development database operator with the reviewed script's required routine permissions must be provided before a later retry; do not broaden the application user's privileges automatically. The retired **20260101 seed was never executed**.

## 7–8. Idempotency and exact configuration

Seed second execution: **NOT RUN**, because the first failed. Idempotent ALREADY_PRESENT/no-audit-rewrite behavior is **NOT VERIFIED** on this development database.

Required row remains **20.00 / 2025-01-01 / NULL**, CreatedBy/UpdatedBy **5**. Actual table is **empty**; no row or audit identity can be reported as deployed. The approved amount/date/selection contract is unchanged.

## 9–10. Actual boundary and July sheet lookup

Read-only final lookup found:

| BillingDate | Required result | Actual matching rows |
|---|---|---:|
| 2024-12-31 | No initial rate; posting blocked | 0 |
| 2025-01-01 | 20.00 | 0 |
| 2025-07-01 | 20.00 | 0 |
| 2026-01-01 | 20.00 | 0 |
| 2026-10-09 | 20.00 | 0 |
| 2030-01-01 | 20.00 unless approved schedule supersedes it | 0 |

Sheets **2 and 5** retain BillingDate **2025-07-01**, with **0 deployed matches**. Their approved interval coverage is correct, but the operational missing-configuration blocker is **not resolved** because the seed failed. No sheet was posted or its date changed. Synthetic test configuration boundary/posting checks passed separately; they are not evidence of a deployed rate.

## 11. Financial baseline before/after

Captured before the schema migration and compared after the seed failure, and again after regressions. **PASS — identical baseline**, including raw snapshot SHA-256 comparisons retained privately (no customer or security data in console output).

| Metric | Before | After |
|---|---:|---:|
| Billings | 30 | 30 |
| Readings | 230 | 230 |
| Reading sheets | 16 | 16 |
| Payments | 8 | 8 |
| Penalties | 5 | 5 |
| Other Charges | 0 | 0 |
| Other Charge installments | 0 | 0 |
| Non-null InvoiceSnapshotJson | 13 | 13 |
| SUM TotalBillAmount | 6766.30 | 6766.30 |
| SUM RemainingAmount | 5027.30 | 5027.30 |
| SUM BillArrearsAmount | 717.00 | 717.00 |
| SUM BillingAdjustment | 34.00 | 34.00 |

This is the authorized lightweight development baseline, not independently restored production recovery or exhaustive full-row financial reconciliation. Existing issued snapshot bytes also match exactly. Intentional development changes are the single user's verification/audit fields and the empty new schema.

## 12. Migration/concurrency regressions

Reviewed runner guard requires local **tmcwd_test**, never `tmcwd`. The first attempt derived a test connection from application credentials and failed with access denied before database fixtures ran; it is not a successful test. The already configured **BILLING_TEST_CONNECTION** was then validated as local/tmcwd_test and used unchanged for the successful run.

`dotnet run --project tests/BillingRegression --no-build -- .` **PASS**, including **617 grouped temporary-table checks**, plus these **four separately reported migration/concurrency groups** (not folded into the 617 count):

1. Phase 6D real two-connection locking, unique first/last invoice serials and additive migration rerun.
2. Receipt migration rerun preserving legacy multi-bill rows and seeding shared receipt sequence.
3. Two-cashier Cash/Check/EWallet races, stale conflicts, uniqueness, ownership, cancellation/non-reuse and immutable invoices.
4. Two-cashier installment locking/reversal, untouched water principal and installment migration rerun without legacy backfill.

Concurrency uses uniquely prefixed synthetic persistent fixtures in tmcwd_test with scoped cleanup; ordinary database cases use temporary tables. Maintenance lookup/configuration/overlap/missing-rate/posting tests are in the 617 groups. The guarded initial seed's actual execution/idempotency remains blocked on development and was not replaced by a synthetic success claim.

ServiceRequestRegression: **NOT RUN — TEST HARNESS REQUIRES DEDICATED-DB GUARD**. It falls back to the application development connection and only checks local host, without the dedicated-schema restriction. Although it uses temporary shadows for selected tables, it executes migration/backfill and reads Users. No real development service fixture was altered to make it pass. No runner/source guard was changed in this deployment task.

## 13. Standard regressions

| Required command | Actual result |
|---|---|
| Solution build, `-v quiet` | PASS; 0 warnings/errors in this incremental run |
| BillingRegression build, `-v quiet` | PASS; 0 warnings/errors |
| BillingRegression run without `--no-migrations` | PASS; 617 temporary-table groups + four separately reported migration/concurrency groups |
| Browser regression | PASS; 25, 0 skipped |
| Penalty browser regression | PASS; 5, 0 skipped |
| Collections browser regression | PASS; 8 scenarios, Ledger 31 checks plus related scenarios |
| MeterReaderAuthRegression build | PASS; 0 warnings/errors |
| MeterReaderAuthRegression run | PASS; 43 assertions |

Xml remains **8.0.4**, with no dependency or financial business-logic edit. B5 nullable findings remain dated evidence; this incremental build's zero warnings does not erase them.

## 14. Authenticated development status

Existing synthetic HTTP/JWT tests passed for fresh credential login, production fingerprint validation, claim-derived posting, MVC bridge/CSRF, authorized rate/config access and unauthorized-role rejection, inactive/unverified synthetic login rejection and stale-session/token rules. These exercise the production boundary with isolated fixtures.

Live development mock-Id-5 password login/Application→Data bridge: **NOT VERIFIED**. No password was exposed, reset or guessed and no local app was started solely to claim a successful live login. Synthetic authentication success does not certify the live account workflow. B4 fingerprints and all quarantines remain enforced.

## 15. Synthetic posting verification

The passing BillingRegression cases post water **100.00**, maintenance **20.00**, TotalBillAmount and initial RemainingAmount **120.00**, with snapshot water **100.00** and maintenance **20.00**. Retry tests preserve invoice identity/snapshot and prevent duplicate billing/fee. Water-only late penalty is **10.00**, excluding maintenance; official SOA is immutable. These writes occur only in isolated test fixtures, not manually prepared sheets 2/5 or other development financial rows. Integrated posting using the development deployed rate is **NOT VERIFIED** because the rate does not exist.

## 16. Phase 6I-A arrears

Existing regression PASS verifies adjusted principal **90.00** plus active penalty **17.00** contributes **107.00** within the multiple-prior-bill fixture: total **397.00 = 120 + 90 + 17 + 50 + 120**. It verifies current snapshot arrears and unchanged prior snapshot bytes, rejecting recursive snapshot TotalDue. A new isolated single-prior-bill 107.00-only case was not separately run; the existing fixture protects that component alongside other prior bills. Historical configuration lookup does not rewrite old invoices.

## 17. Other Charges, Collections, Ledger and Adjustment

Passing isolated/browser suites protect Other Charge/installment separation, authoritative collectible children, full-payment-only water settlement, exact BalanceBefore cancellation restoration, penalty/installment settlement links and Ledger reconciliation. No maintenance row was inserted into other_charges in development; its count remains zero. Test cases require snapshot OtherCharges separation rather than moving maintenance into contract storage.

BillAdjustment remains the existing legacy **max(20.00, TotalBillAmount − BillingAdjustment)** floor, with regression coverage and no current-rate substitution. Future non-20 maintenance schedules require separate Finance policy; this task neither redesigns nor changes that floor.

## 18. Skipped/failed checks and evidence

Failed: initial seed permission gate; initial database regression credential attempt (later successfully retried with the existing dedicated test connection). Initial PowerShell script launch was blocked by local execution policy; rerun used process-only ExecutionPolicy Bypass, without changing machine policy.

Not run/verified: seed second execution, successful development rate lookup/posting, live mock credential/Application bridge, isolated 107-only arrears case, ServiceRequestRegression dedicated-schema execution and production recovery/workflows. No skipped check counts as PASS.

Local evidence under ignored `.tmp`: `phase6ib-dev-deploy.ps1`, `phase6ib-dev-deploy.log`, `phase6ib-dev-schema.txt`, private `phase6ib-dev-baseline-before.txt` / `phase6ib-dev-baseline-after.txt`, `phase6ib-dev-state.log`, and build/regression/browser/penalty/collections/reader logs with prefix `phase6ib-dev-`. Baseline files contain snapshot digests, not user hashes or personal details. The deployment script intentionally expects the original unverified actor/absent schema and **must not be blindly rerun** against the now partially completed state.

## 19. Development verdict and next step

**DEVELOPMENT INTEGRATION: FAIL.** Verified target, eligible mock actor, schema migration, unchanged financial baseline and safe regressions pass. Required successful seed, exact rate, idempotency and development configuration/posting integration do not.

Remaining development action: designate an authorized development maintenance database operator with CREATE ROUTINE / ALTER ROUTINE / EXECUTE and the reviewed script's table privileges, then recheck the current actor/schema/financial baseline before retrying only the unchanged approved 20250101 seed. Do not silently reuse a dedicated test credential for development, grant broad privileges to the application user, reset credentials or replace the guarded script with direct inserts. After successful seed, verify the exact row/audit, run the approved seed again, compare the complete row and financial baseline, and confirm actual rate/sheet lookup. No production-style bootstrap is needed for this development task.

## 20. Production qualification

**PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.** Production backup/restore and production SuperAdmin readiness are deferred because the current database/accounts are synthetic development data. B5/B6 historical recovery and actor findings remain preserved; the new development flags do not establish production organizational authorization or recovery readiness.

Production still requires independently restored/reconciled recovery, legitimate organizational SuperAdmin, exact production target/runtime verification, explicit production deployment authorization and production workflow verification. Quarantined service/provider workflows retain their separate full-application release implications. This report does not call Phase 6I-B production-ready.

## 2026-10-09 final read/test verification continuation

**DEVELOPMENT INTEGRATION: PASS. PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.** The stakeholder reports completing the approved 20250101 seed through Workbench with a privileged local development operator. This review independently read the resulting database; it did not rerun a seed, migrate the development schema, change a configuration/user or post development financial records. The earlier routine-permission failure remains historical evidence, not a current missing-rate finding.

### Deployed configuration and idempotency evidence

Fresh identity matches **127.0.0.1:3307/tmcwd**, DESKTOP-Q72GRF4, MySQL 8.0.46, UUID **c3f950fe-b565-11f1-b6a6-30560f67a354**. Mock Id 5 remains Role 1 / Active 1 / Verified 1.

Exactly **one** maintenance row exists:

| Field | Verified value |
|---|---|
| Id | 1 |
| Amount | 20.00 |
| EffectiveFrom | 2025-01-01 |
| EffectiveTo | NULL |
| CreatedBy / UpdatedBy | 5 / 5 |
| DateCreated / DateUpdated | 2026-10-09 14:53:39 / 2026-10-09 14:53:39 |

These are database-stored timestamps without an asserted timezone. With only one row, there is no conflicting or overlapping row. The complete row remained unchanged across final verification.

**Idempotency: VERIFIED using operator-reported rerun evidence plus current database state.** In response to the verification question, the stakeholder explicitly confirmed **“yes, ALREADY_PRESENT”** for the approved seed rerun. Current count remains 1, amount/date/audit fields are exact, and DateUpdated equals DateCreated. No duplicate exists. The reviewer did not witness the Workbench invocation or obtain an independent pre-rerun timestamp capture; the rerun result is attributed to the operator, not invented as a reviewer execution. No seed was executed by this final-verification task; the retired 20260101 artifact remains unused.

### Real service and API lookup

Actual `MeterMaintenanceRateService` reads the development table: 2024-12-31 throws the missing-rate exception; **2025-01-01, 2025-07-01, 2026-01-01, current covered date and 2030-01-01** each resolve the single PHP 20.00 row. Sheets **2 and 5** retain BillingDate **2025-07-01** and resolve that row exactly once. The July missing-configuration blocker is now resolved for development.

HTTP GETs against the real Data `MeterMaintenanceRateController` and service, with the development DbContext and production persisted-token callback, verify:

| Request | Actual result |
|---|---|
| Anonymous July lookup | 401 |
| Valid active verified MeterReader token | 403 |
| Valid active verified BillingOperations OIC token | 200; amount 20.00 |
| Valid mock SuperAdmin Id 5 configuration read | 200 |

The ephemeral local test host uses the existing role policy values and real signed tokens with current persisted fingerprints; tokens/security fields are never printed. Tokens are issued by the verification harness rather than a password-login UI. This proves actual controller/service/database lookup and authorization, while live password login, deployed 5178/5054 processes and the full browser Application bridge remain **NOT VERIFIED** by this continuation. No production authentication behavior changed and no POST configuration operation ran on development.

### Deployed-rate posting integration without development financial writes

Added opt-in `tests/BillingRegression/DevelopmentDeploymentVerification.cs`, reached through `--verify-development-deployment`. It requires the existing local `tmcwd_test` fixture guard and the exact read-only source target/UUID. It reads the deployed rate through the real service, then copies its amount, interval and audit fields into the connection-local temporary rate table owned by the existing synthetic test fixture. Source financial/customer/user/configuration records are never mutated.

Real `ReadingSheetService.PartialPost` then posts a new synthetic reading: water **100.00**, maintenance **20.00**, TotalBillAmount **120.00**, RemainingAmount **120.00**, snapshot water **100.00** / maintenance **20.00**; water-only penalty calculation **10.00**. Other Charge contract table remains empty for that bill. Retry posts zero additional readings, retains one bill, the same invoice identity, byte-identical snapshot and PHP 120.00 total. Existing regression cases additionally verify persisted late-penalty behavior and collection/cancellation/arrears contracts. This is deployed-value integration through an isolated copied rate, not live posting against development financial tables.

### Baseline and financial contracts

Before and after final tests, the previously captured development baseline matches exactly: **billings 30, readings 230, sheets 16, payments 8, penalties 5, Other Charges 0, installments 0, non-null snapshots 13**. Sums remain **6766.30 / 5027.30 / 717.00 / 34.00** for TotalBillAmount / RemainingAmount / BillArrearsAmount / BillingAdjustment. Every recorded raw snapshot SHA-256 is unchanged. The single maintenance row is the intended new configuration data from the manual deployment.

Phase 6I-A regression preserves the **90.00 adjusted balance + 17.00 active penalty = 107.00 contribution** within its 397.00 multi-prior-bill fixture, preserving historical snapshots rather than recursively using their TotalDue. Phase 6G children, full water settlement, exact BalanceBefore and penalty/installment reversal, Collections/Ledger/Dashboard Charging behavior remain protected. The legacy adjustment floor is still **max(20.00, TotalBillAmount − BillingAdjustment)**; future non-20 rates require separate Finance policy. No financial business logic changed.

### Actual final regressions

| Check | Result |
|---|---|
| Solution build, `-v quiet` | PASS; 0 warnings/errors in incremental run |
| BillingRegression build, `-v quiet` | PASS; 0 errors, 1 existing EF1002 warning in synthetic PaymentConcurrency code |
| `dotnet run --project tests/BillingRegression --no-build -- . --verify-development-deployment` | PASS; **618 temporary-table groups** = 617 prior + 1 deployed-rate posting/retry group; plus actual service and four HTTP GET checks |
| Invoice/payment/receipt/installment migration and concurrency | PASS; the same four separately reported groups, using synthetic tmcwd_test fixtures |
| Browser / penalty browser | PASS; **25 / 5**, no skipped cases |
| Collections / Ledger browser | PASS; **8 scenarios / 31 checks**, plus related scenarios |
| MeterReaderAuthRegression build/run | PASS; **43 assertions**, build 0 warnings/errors |

The opt-in invocation includes the standard suite and migration/concurrency paths; no production-source change was made. An initial new-harness compilation failed on an ambiguous Reading type and a nullable BillingDate; those test-only errors were corrected before the successful build/run. Failed compilation is not counted as PASS. Prior uncommitted work was preserved.

Evidence under ignored `.tmp`: `phase6ib-final-build.log`, `phase6ib-final-solution.log`, `phase6ib-final-regression.log`, `phase6ib-final-browser.log`, `phase6ib-final-penalty.log`, `phase6ib-final-collections.log`, `phase6ib-final-reader-build.log`, `phase6ib-final-reader.log`; private original snapshot baseline is retained from the earlier development review.

### Qualifications and stop point

ServiceRequestRegression remains **NOT RUN — TEST HARNESS REQUIRES DEDICATED-DB GUARD**. A separate 107.00-only fixture, real-device workflow, live credential login/Application bridge, production recovery/runtime/administrator/workflow readiness were not verified. Idempotency evidence is operator-reported as explained above. These do not erase the directly observed configuration, isolated deployed-value posting, unchanged baseline and successful required suites.

**Development integration PASS applies only to the synthetic local environment and tested flow.** Production remains **DEFERRED / NOT EVALUATED**, requiring actual recovery verification, legitimate organizational SuperAdmin, exact production target/runtime, authorization and workflow verification. Mock Id 5 and successful local tests provide no production authority. No final-verification seed or development financial write occurred. Review stops after this amendment and the checklist continuation.
