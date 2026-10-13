# Phase 6I-B Pre-Deployment Safety Checklist

Review date: 2026-10-08 (Asia/Manila). Recommendation: **NO-GO**.

## 2026-10-09 Phase 6I-B2 readiness recheck

**Recommendation remains NO-GO.** This dated recheck supplements the historical review and effective-date amendment below. See [Financial / Service Authorization Hardening](./Phase6I-B2-Financial-Service-Authorization-Hardening.md) for the complete 80-row endpoint inventory, exact source changes, test results and policy decisions.

| Former finding / deployment gate | Current status | Fresh evidence / qualification |
|---|---|---|
| Actual database identity | RESOLVED for inspected local target | Read-only recheck: 127.0.0.1:3307, database tmcwd, server DESKTOP-Q72GRF4, MySQL 8.0.46. Production/running process overrides are NOT VERIFIED. No secrets disclosed. |
| July 2025 initial-rate coverage | RESOLVED | Active sheets 2 and 5 remain Status 1 / BillingDate 2025-07-01 and are covered by the approved PHP 20 / 2025-01-01 / NULL interval. No sheet or financial row changed. |
| Reading Sheet posting/lifecycle authorization | RESOLVED in source | Existing BillingOperations and server-derived actor remain protected; retry and rollback regressions pass. Live session deployment verification remains NOT VERIFIED. |
| Reading/payment/configuration mutation gaps | RESOLVED for proven named routes | Web Reading entry reuses server roster/baseline/date/status controls; Advance Payment and Payment Check use Collection; charge configuration uses SuperAdmin. Claim actor replaces route/body authority. Accidental persistence-service API aliases removed. |
| Bulk Reading, memo and service ownership policies | STILL BLOCKED | SaveRange, memo, Request, JobOrder, twelve Dashboard POST actions and four mutating GET actions are denied for every role by PendingAuthorizationDecision. Finance/Admin must approve action/assignment/approval rules before these workflows can operate. This is runtime quarantine, not complete workflow remediation. |
| E-wallet/provider/webhook trust | STILL BLOCKED | Writes denied for every role; e-wallet write is POST only. Provider contract/signature/replay/trusted actor decisions are required. No provider authentication format invented. |
| Additional repository-wide mutation gaps | STILL BLOCKED | Exposed Users role/password mutations, Account/Customer and legacy service-detail/metadata writes remain Critical/High findings. Generic protected Billing write still needs a body-field integrity review. Do not expose the API as safe for deployment. |
| Additive maintenance schema | STILL BLOCKED / undeployed | Table count remains zero. Source CREATE TABLE IF NOT EXISTS is additive; rerun does not repair an incompatible existing table. No migration executed. |
| Approved seed and retired artifact | RESOLVED at source level; runtime NOT VERIFIED | 20250101 artifact inserts only the exact approved configuration or no-ops; rejects overlap; active verified SuperAdmin required. Retired 20260101 artifact signals error and has no executable DML. Neither was executed. |
| Eligible legitimate seed actor | STILL BLOCKED | Eligible active verified SuperAdmin count remains zero. Existing Id 5 is active, unverified. No account was created, activated or verified. |
| Independently restorable backup | STILL BLOCKED / NOT VERIFIED | No verified source identity, integrity, secure storage, recovery instructions, isolated restore and reconciliation evidence found in inspected repository/project-parent material. No backup operation performed. |
| Source build / permitted regressions | RESOLVED for performed checks | Solution and BillingRegression builds PASS; 190 temporary-table checks PASS, including 58 new B2 grouped security checks; browser 25 + penalty browser 5 PASS; Collections 8 scenarios and Account Ledger 31 checks plus related scenarios PASS. |
| Migration/concurrency/standalone service/live workflow checks | NOT VERIFIED | Migration-dependent suites and standalone ServiceRequestRegression NOT RUN; live service/provider/MVC deployment workflows NOT VERIFIED. Synthetic denied-route tests do not certify disabled workflows. |
| Existing package advisories / legacy adjustment policy | STILL BLOCKED for disposition | NU1903 high-severity advisories remain; approved BillAdjustment PHP 20 legacy floor remains unchanged and requires its separate policy review. |

The approved financial calculations, issued snapshots, arrears, water-only penalties, installments, allocation, cancellation and invoice numbering were preserved. Operational queries were read-only; writing tests used only synthetic connection-local temporary tables in tmcwd_test. No operational posting, persistent financial change, administrator-account change, migration, seed, backup or deployment was performed.

Required sequence remains: resolve exposed Critical/High mutations and approved quarantined policies; verify an independently restored/reconciled backup and legitimate eligible seed actor; verify the actual deployment target and live authenticated flow; obtain separate deployment approval; apply and verify the additive migration, then **only** the approved 20250101 seed; verify row/audit identity and unchanged financial baselines; run separately approved post-deployment checks before enabling operational posting. The current task authorizes none of these deployment steps.

## 2026-10-08 amendment — current deployment checklist

The stakeholder replaced the initial **2026-01-01** effective date with **2025-01-01**. Current approval: **PHP 20.00 / 2025-01-01 / NULL**, once per eligible monthly bill, selected by `ReadingSheet.BillingDate`; configuration changes remain SuperAdmin-only. This amendment does not authorize deployment.

- **Current initial seed:** [`20261008_seed_meter_maintenance_rate_20250101.sql`](../TMCWD/TMCWD.Data/Database/20261008_seed_meter_maintenance_rate_20250101.sql). It accepts only the exact approved row, inserts only when absent, rejects conflicting/overlapping intervals and requires a legitimate active verified SuperAdmin actor. Existing rows and financial history are never updated/deleted.
- **Retired seed:** `20261008_seed_meter_maintenance_rate_20260101.sql` is **SUPERSEDED / NOT FOR DEPLOYMENT**. It raises an error before any data change and contains its former implementation only as a historical comment. Never run both initial seeds, even with a SQL client's continue-on-error option. If a prior 2026 row exists, the new seed rejects it as overlapping; separately authorized resolution is required.
- **July operational coverage: RESOLVED BY APPROVED INTERVAL.** Previously observed active sheet IDs 2 and 5, BillingDate 2025-07-01, are covered by 2025-01-01 through NULL. No separate historical rate interval or sheet-date alteration is needed for these dates. This is interval analysis of the prior observation, not a fresh operational database query or a claim that posting can already succeed.
- **Posting boundary:** the previously reported missing authorization was subsequently remediated and regression-tested; see [Posting Security Remediation](./Phase6I-B-Posting-Security-Remediation.md). Claim-derived identity and BillingOperations remain unchanged by the date amendment.
- **Still NO-GO:** no independently restorable backup is verified, no eligible seed actor has been established, no schema/seed deployment has been performed, and other reported authorization/configuration risks and live checks remain open. Prior database/actor observations below were not re-queried during this amendment.

After separate authorization and readiness sign-off, the deployment sequence is: verify actual target and restorable backup; quiesce posting/configuration writes and capture financial baselines; apply the additive schema artifact; verify schema; run **only the new 20250101 seed** with an already legitimate active verified SuperAdmin; verify the exact row, audit identity and no financial changes; complete approved isolated post-deployment checks before resuming operations. None of these database steps is authorized or performed by this amendment.

Current required boundary tests are **2024-12-31 rejects**, **2025-01-01 / 2025-07-01 / 2026-01-01 select PHP 20**, and later covered dates select PHP 20 unless a separately approved future schedule supersedes it. Post-deployment SQL tests must also cover exact-row seed rerun, invalid actor, prior-2026-row conflict and other overlaps without altering history. Source/build/regression checks here never execute a migration or seed.

**Amendment verification:** the regression project/dependency build passed with zero errors; **132 local `tmcwd_test` temporary-table checks**, **25 general browser tests** and **5 penalty browser tests** passed. All amended lookup/posting boundaries, financial preservation and posting authorization checks passed. Seed idempotence/conflict behavior was inspected in source only, not exercised by executing the seed. Migration/concurrency suites requiring migration SQL were explicitly NOT RUN. See [Implementation Verification](./Phase6I-B-Implementation-Verification.md) for exact commands and limits.

## Historical pre-deployment inspection — prior 2026-01-01 approval

All original findings below are retained as they were valid when recorded. The dated amendment above supersedes their seed selection, pre-start boundary and July 2025 coverage findings; the subsequent posting-security report resolves the original posting endpoint finding. Historical observations are not current successful deployment evidence.

Scope: read-only source, configuration, database metadata and selected operational metadata inspection. Only this report was created. No migration, seed, posting, configuration write, financial record change, backup operation, or Git reset/clean was performed. Existing working-tree changes were preserved. This review concerns the current working tree, not a verified release commit.

Previous build PASS and 107 BillingRegression database checks PASS are historical evidence from `Phase6I-B-Implementation-Verification.md`, not results rerun by this review. Database-writing regression runners were deliberately not executed.

## 1. Verified database identity

| Item | Observed evidence |
|---|---|
| Configuration source | Data development user-secrets, using the project's `TMCWD.Data-local-development` ID; development launch profile sets `ASPNETCORE_ENVIRONMENT=Development` |
| Client host / port | `127.0.0.1:3307` |
| Configured / selected database | `tmcwd` / `SELECT DATABASE()` returned `tmcwd` |
| Server hostname / port | `DESKTOP-Q72GRF4` / `@@port = 3307` |
| Server version | `8.0.46`, MySQL Community Server - GPL |
| Maintenance table | `information_schema.tables` count = **0** |
| Existing prerequisite columns | `billings.InvoiceSnapshotJson` = LONGTEXT; `billings.BillArrearsAmount` = DECIMAL(18,2); installment `Id` and `SettledPaymentId` = BIGINT |

The expected local development target is verified by a successful TCP connection and server SELECTs. `billings` and `users` are InnoDB. Production identity and the environment of any already-running application process are not established by these checks. Deployment must repeat identity checks in its actual environment; `Program.cs` reads normal ASP.NET configuration overrides and performs server auto-detection.

Read-only queries included server/database identity, table/column metadata, eligible SuperAdmin identities/flags, and pre-effective-date active sheet metadata. No customer details, credential values, hashes or tokens are included here.

## 2. SQL migration safety

Artifact: `TMCWD/TMCWD.Data/Database/20261008_add_meter_maintenance_rates.sql`.

- **Source inspection satisfactory:** one `CREATE TABLE IF NOT EXISTS`, new table only, no financial DML, backfill, trigger, startup migration or existing table alteration.
- BIGINT identity/audit IDs, DECIMAL(12,2) money, DATE interval bounds, InnoDB and utf8mb4 agree with the entity and `UserDbContext` mapping.
- Unique effective-start index and composite `(EffectiveFrom, EffectiveTo)` index are present. Nonnegative amount and inclusive interval-order checks are present; NOT NULL covers amount/start.
- Idempotence is conditional: an existing table makes the statement a no-op; it does **not** verify or repair an incompatible existing schema. Compare `SHOW CREATE TABLE`, indexes and enforced constraints before treating a rerun as successful.
- Overlapping intervals are not prevented by the unique-start index or CHECK constraints. The application and seed must use their shared advisory lock and overlap checks; unrestricted direct SQL can bypass this protection.
- MySQL 8.0.46 supports the syntax and enforced CHECK constraints. CHECK enforcement requires MySQL 8.0.16 or later, per the [official MySQL documentation](https://dev.mysql.com/doc/refman/8.0/en/show-create-table.html). Compatibility was inspected, not tested by executing DDL.

## 3. Seed safety

Artifact: `TMCWD/TMCWD.Data/Database/20261008_seed_meter_maintenance_rate_20260101.sql`.

- Exactly approved row: PHP **20.00**, **2026-01-01** inclusive, open-ended NULL end.
- An exact existing row with no conflicting intervals produces `ALREADY_PRESENT`; it does not overwrite amount, dates or audit fields. A duplicate exact row or a different overlapping configuration aborts.
- Since the seed interval is open-ended, any other interval reaching 2026-01-01 or later is a conflict, including a scheduled future rate. Earlier closed non-overlapping intervals are allowed.
- Actor must exist in `users`, Role = 1 (matching the canonical SuperAdmin enum), active and verified. No automatic actor fallback exists.
- Shared `tmcwd:meter_maintenance_rates` lock serializes cooperating writers. Insert is inside a transaction; SQL exceptions roll back, release an acquired lock and rethrow. Existing invoices, billings, payments and balances are never updated by the seed.
- The script also drops/creates/calls/drops a specifically named helper procedure. It is additive with respect to financial tables, but is **not entirely DDL-free or transactional as a script**. Check routine-name collision, CREATE/ALTER ROUTINE and execution privileges, and client DELIMITER support. Failure can leave the helper routine behind if the client stops before cleanup. Do not run concurrently with another invocation of that routine.
- **BLOCKED:** eligible seed actors = **0**. The sole observed SuperAdmin, ID **5**, is active = 1 and verified = 0. Do not select it as seed actor or change its verification flag as a deployment shortcut. Establish a legitimate verified actor through the authorized administration process first.

## 4. SuperAdmin authorization and DI

`MeterMaintenanceRateController` applies BillingOperations to date lookup and SuperAdmin to schedule list/write. Writes derive actor ID from the validated NameIdentifier claim. `MeterMaintenanceRateService.SaveUpdate` rechecks the user's persisted role, active and verified flags. External requests use explicit DTOs rather than EF graphs.

Data `Program.cs` validates JWT signature, algorithm, issuer, audience and expiry, and orders authentication before authorization. It registers the rate service as scoped. Application `Program.cs` registers the transaction; `ApiClientRegistration` bridges the server session to a short-lived bearer token. Matching Application/Data signing configuration and authenticated end-to-end behavior remain deployment checks.

**HIGH / release blocker:** Data `ReadingSheetController.PartialPostReadingSheet` (line 136) has neither action nor controller authorization, takes `userId` from the query, and calls posting directly. `Program.cs` has no fallback authorization policy/global authorization filter. `ReadingSheetService.PartialPost` validates sheet state but does not validate an authenticated actor. Thus the API posting boundary does not protect a financial write or its audit identity. CORS does not substitute for authorization. This is an existing adjacent defect, not an assertion that the new configuration endpoint is unprotected. Require an authorized fix and rejection tests before exposing this release.

## 5. Backup and recovery readiness

**NOT VERIFIED / NO-GO.** No independently restorable database backup or restore-test evidence was available in inspected repository files, documentation, backup/dump filename searches, or immediate workspace/parent directory entries. This does not prove no external backup exists; none could be verified here.

Required evidence before deployment:

- Backup identifies this server/database and a sufficiently recent consistent recovery point, with all required tables/data and relevant routines/schema objects.
- Artifact location, integrity verification, retention/access owner and recovery instructions are documented securely.
- Successful restore to a separate isolated database/server is recorded, including financial and schema reconciliation. A backup filename or successful dump exit alone is insufficient.
- A recovery owner, maintenance window and application rollback plan exist. MySQL DDL cannot be treated as an ordinary rollback of this entire script.

No backup was created, overwritten, restored or deleted. Any backup/restore work requires separate authorization.

## 6. Existing financial data and workflow protections

| Workflow | Source finding / verification limit |
|---|---|
| Reading Sheet posting | Locks sheet and wraps writes in a transaction. Resolves exactly one rate by persisted `ReadingSheet.BillingDate.Date` once for new bills, before invoice allocation. Missing/overlapping rates throw and prevent commit. Recovery of already-persisted bills does not require a new rate. API maps configuration failures to 409. Missing table will fail posting, not silently use PHP 20. |
| Invoice composition | Water charge lines sum to water-only `BillAmount`; resolved maintenance is included once in `TotalBillAmount`, initial `RemainingAmount` and snapshot `MeterMaintenance`. Snapshot `TotalDue` adds known arrears once. No rate-derived historical backfill. |
| Phase 6I-A arrears | `ComputeBillArrearsAmount` retains qualifying prior unpaid/overdue `RemainingAmount` plus active persisted penalties; invalid/missing prior snapshots yield unknown arrears. No current maintenance lookup or recursive frozen arrears addition. |
| Bill Adjustment | SaveUpdate/SaveBatch retain `max(LegacyMeterMaintenanceAdjustmentFloor, TotalBillAmount - BillingAdjustment)` with PHP 20. No current-rate lookup. Historical compatibility is preserved, but future amounts different from PHP 20 need separately approved Finance handling. |
| Collections / payment / cancellation | `BillingService.GetCollectionBill` reads snapshot components and current persisted remaining principal; active collectible installment children remain separate. `PaymentService` settles persisted principal and links exact penalties/installments; cancellation restores recorded `BalanceBefore` and linked children. No live maintenance-rate lookup. |
| Account Ledger | Reuses `GetCollectionBill`, totals `AmountDue`, and uses collectible installment children. Contract totals/future children do not become immediate principal. Snapshot maintenance remains an invoice component. |
| Dashboard / Service Requests | Dashboard contains existing service-request/application reconciliation and charge creation paths; financial integration creates separate charge/installment records. No maintenance-rate lookup was found in these paths. Some dashboard GET paths call backfill/reconciliation helpers, so live GETs were **not** used as read-only inspection probes. |
| Phase 6G installments | `PartialPost` retains installment activation in the posting transaction. Collection/cancellation continue using exact linked children. Maintenance is not inserted into `other_charges` or installment tables. |
| Penalties / official dates | Posting freezes prior authoritative non-deleted reading date and current reading date. Penalty source remains water-only snapshot `BillAmount` through `LatePaymentPenalty.Calculate`. Posting eligibility uses due dates strictly before the effective posting date; informational after-due values do not insert penalties. Sheet billing/due/disconnection dates remain server-owned. |
| Flutter official SOA | API returns server snapshot amount/components, dates, invoice/QR identity and informational penalty; Flutter displays issued/cached values. No new local calculation/schema migration. Existing preview presents maintenance separately; confirm required customer-facing Other Charges grouping in the authenticated presentation test without changing internal `Billing.OtherCharges`. |
| Web estimates | Sheet-bound controllers resolve the sheet's billing date. Generic estimates without a selected sheet remain water-only. Missing/overlapping configurations do not become successful full bill estimates. |

These are source findings, not live workflow passes or a full database reconciliation. Historical snapshot preservation is also guarded by `BillingService.SaveUpdate`; the migration/seed contain no existing financial row updates.

Operational boundary: active sheet IDs **2** and **5** both have `BillingDate = 2025-07-01`, Status = 1. The approved seed does not cover them. Their new postings will fail closed and sheet-bound estimates cannot resolve a full bill. Finance must decide whether to leave them blocked or separately approve a non-overlapping historical interval. Do not change dates to bypass the approved policy.

## 7. Required deployment sequence

This is a future sequence, **not authorization to execute it**.

1. Close release blockers: verify restorable backup, establish eligible legitimate seed actor, protect posting authorization/audit identity, and document treatment of the two older active sheets.
2. Identify the exact reviewed release artifact; retain all new/untracked implementation files in the release. Confirm actual deployment configuration, server/database identity and prerequisite Phase 6G/6I-A schema.
3. Arrange a maintenance window: stop posting and configuration writes. Capture baseline counts/totals and issued snapshot/invoice identity evidence using approved read-only reconciliation.
4. After separate deployment authorization, run only the schema migration in the explicitly selected database. Verify actual table definition, precision, enforced CHECKs and indexes. Do not bundle unrelated cleanup scripts.
5. Supply the verified SuperAdmin ID in the same SQL session and run the separate guarded seed with an appropriate client and routine privileges. Abort on conflict; never replace it with an overwriting upsert.
6. Read back exactly one approved row and valid audit identity. Check no overlap and coverage of intended billing dates; compare financial baselines before resuming writers.
7. Enable the application only after schema and seed are verified. Complete isolated authenticated tests and obtain Finance/release sign-off before operational posting resumes.
8. If anything fails, stop posting, preserve diagnostic evidence and use the authorized recovery plan. Do not drop tables, reseed with guessed actors, or edit historical invoices.

## 8. Required post-deployment tests

Perform write tests only in a separately authorized synthetic/isolated environment. Do not use live customer posting as a smoke test.

- Migration rerun preserves schema/data; exact seed rerun is a no-op; conflicting same-start/overlapping seed rejects without financial changes. Invalid actor seed rejects.
- Read-only schema/index/constraint verification; exactly one PHP 20 row effective 2026-01-01, audit actor remains legitimate.
- Anonymous request = 401; authenticated non-SuperAdmin write = 403; inactive/unverified actor rejected; valid SuperAdmin write succeeds in isolation. Test real session-to-API JWT validation and BillingOperations role access.
- Posting authorization rejects anonymous/wrong-role callers and ignores client-supplied identity after its fix.
- 2025-12-31 lookup/posting rejects; 2026-01-01 and covered later dates resolve PHP 20. Missing/overlap/lock-timeout/DB failure cannot allocate committed invoices, change balances or complete readings.
- Synthetic water PHP 100 posts total/remaining PHP 120; snapshot water = 100, maintenance = 20, penalty = 10, after-due total = snapshot TotalDue + 10. Repeated posting preserves invoice number and snapshot.
- Confirm prior authoritative consumption period and sheet dates, unpaid/overdue adjusted arrears plus active penalties, paid/waived exclusions, unknown arrears, and historical snapshot immutability.
- Phase 6G collectible children/activation, contract/future exclusions, exact collection settlement and cancellation balance restoration; reconcile ledger and Collections.
- Dashboard/Service Request charge totals remain separate; official Flutter SOA online/offline cache, preview, grouping, dates, invoice footer and QR agree with server-issued values.
- Run relevant existing BillingRegression/browser/penalty/ServiceRequest suites and affected-layer builds in their approved environments. Record actual outcomes and warnings; no skipped check counts as PASS.
- Compare pre/post deployment financial baselines before authorized tests; require unchanged existing invoice snapshots, payment rows and balances.

## 9. Blockers and remaining risks

| Priority | Finding / required action |
|---|---|
| Blocker | Independently restorable backup is unverified. Provide restoration evidence before deployment. |
| Blocker | Zero active verified SuperAdmin seed actors. Establish legitimate audit identity before seed. |
| High / blocker | Unprotected Data API posting endpoint and client-controlled audit ID. Resolve through authorized code work and behavior tests before release exposure. |
| Operational gate | Two active pre-2026 sheets cannot post under the approved configuration. Obtain Finance disposition; no implicit historical fallback. |
| Medium | API validation only rejects negative amounts and reversed bounds. Missing/default start dates, dates below MySQL DATE range, amounts outside DECIMAL(12,2), and extra decimal places are not explicitly rejected at the request boundary. DB rejection/rounding may occur instead of clear 400. Review before broader schedule administration; exact approved seed is unaffected. Zero is accepted by source although any zero-fee policy needs separate approval. |
| Medium | Configuration writes serialize with each other, but posting does not share their advisory lock. A concurrent schedule edit and first posting for that date can evade the existing-posted-date guard. Frozen invoice values remain intact, but schedule consistency is not proven under that race. Quiesce configuration changes during posting until separately reviewed/tested. |
| Medium | Historical adjustment floor remains PHP 20. Do not approve a different/zero future rate without assessing adjustment behavior. |
| Residual | Direct database edits bypass application overlap checks. Restrict operational permissions and require the service/guarded deployment route. |
| Residual | DDL no-op does not validate existing schema; helper routine DDL has collision/cleanup implications; live authorization and migration/seed execution have not been exercised here. |
| Residual | Existing implementation verification records NU1903/nullable/EF1002 warnings. Their resolution was not reassessed; previous PASS does not establish current dependency safety. |

The design document retains a stale “Design proposal only” status alongside its implemented-source status. Treat the implementation verification document and observed absent database table as the deployment evidence; neither document proves release readiness.

## 10. Final recommendation

**NO-GO for deployment.** Verified local identity and satisfactory additive SQL intent do not overcome missing verified recovery, unavailable legitimate seed actor, and an unprotected financial posting boundary. The maintenance table is demonstrably not deployed; live authenticated workflow verification is outstanding. Close the blockers and operational gates, then seek explicit deployment authorization and execute the sequence above.

Review stopped after creating this report. No business logic or database data was changed. Tests involving writes, migration/seed execution, backups/restores and live financial workflows remain unperformed by this review.

## 2026-10-09 Phase 6I-B3 critical boundary amendment

This dated amendment supplements the earlier findings; it does not rewrite the historical B2 or original read-only review. See [Phase6I-B3-Critical-API-Boundary-Hardening.md](./Phase6I-B3-Critical-API-Boundary-Hardening.md) for the exact changed files, supported policies, 83-row current/retired endpoint inventory and test qualifications.

**Source boundary readiness: PASS for the scoped B3 fixes and enforced quarantines. Deployment recommendation remains NO-GO.** Users administration now requires SuperAdmin and a persisted active verified claim actor; strict commands preserve server security/audit metadata, password hashes are not returned, password changes use a current-password-proved body-only self command, and the former arbitrary password URL is removed. Administrative resets, verification and peer-administrator edits remain blocked pending account-security policy. Web credential validation moved to Data; inactive/unverified general login is rejected without changing any operational account.

Account/Customer POSTs use CustomerService (SuperAdmin/CSR/OIC), strict DTOs and claim identity. Account numbering/status/roster/ownership/history are protected, supported customer/address/creation flows remain, and meter/classification changes cannot bypass linked/historical service workflows. Generic Billing writes are denied; the legitimate single-payment Other Charge zero anchor now uses a narrow transactional server-owned command. InspectionType uses SuperAdmin; ReadingSheetTemplate uses BillingOperations with reader/zone/range validation. Related MVC forms enforce anti-forgery and retired mutating GETs cannot write.

All 22 B2 quarantines remain. B3 denies 18 additional routes (16 shared graph/configuration mutations, Inventory and generic Billing), yielding **40 denied routes**. Service/provider/inventory operations depending on them are intentionally unavailable until the corresponding role, ownership, approval and trust rules are approved and implemented.

Actual safe verification: solution and BillingRegression builds PASS with zero errors; **274 grouped temporary-table checks in tmcwd_test PASS**, including **84 new B3 groups** beyond B2's 190; browser **25** and penalty browser **5** PASS; Collections **8 scenarios** and Account Ledger **31 checks**, plus related browser scenarios PASS; synthetic MeterReaderAuthRegression PASS. Actual MVC HTTP tests cover anti-forgery denial, retired GETs and signed user/customer/account writes. Migration-dependent concurrency, standalone service migration/backfill tests, device end-to-end and live deployment authentication were **NOT RUN / NOT VERIFIED**. No migration or seed executed; failed intermediate fixture/test-host runs are documented and not counted as PASS.

Fresh read-only identity remains **127.0.0.1:3307 / tmcwd**, server **DESKTOP-Q72GRF4**, **MySQL 8.0.46**; no process connection override was present. Maintenance table count **0**. Active verified SuperAdmin count **0**; Id **5** is active and unverified. Sheets **2 and 5** remain Status **1**, BillingDate **2025-07-01**, covered by the approved **PHP 20.00 / 2025-01-01 / NULL** interval. Rate-date coverage is resolved; deployed configuration availability is not. No operational user, customer, billing, payment, reading, snapshot or balance was changed.

Independently restorable/reconciled backup evidence remains **NOT VERIFIED**. Xml **8.0.3** remains pinned in Administration and produces NU1903; its old MVC/DataProtection dependency also requests Xml. Current web/test restore graphs use the shared-framework assembly, but the actual production publish/runtime graph remains unverified. The B3 report recommends a separately authorized patched servicing upgrade or evaluated net10.0 alignment; no dependency version changed. Legacy hash compatibility and global token/session revocation remain separate security work.

Before deployment: resolve required unavailable-workflow and account-security policies, dispose of remaining dependency/security risks, verify an independently restored/reconciled backup and an already legitimate eligible actor, verify the real target and live authenticated workflow, and obtain separate migration/seed authorization. Then verify schema and apply only the approved **20250101** seed after the migration, reconcile unchanged financial baselines, and run separately approved post-deployment tests. The **20260101** seed remains **SUPERSEDED / NOT FOR DEPLOYMENT**. B3 does not authorize deployment or declare Phase 6I-B complete.

## 2026-10-09 Phase 6I-B4 read/session/dependency amendment

This supplements all historical findings without rewriting them. See [Phase6I-B4-Read-Session-Dependency-Security.md](./Phase6I-B4-Read-Session-Dependency-Security.md) for the complete 162-row runtime-derived Data query inventory, exact source changes, dependency proposal and test qualifications.

**Scoped read/session source remediation: PASS. Dependency/operational readiness: INCOMPLETE. Deployment remains NO-GO.** All 162 Data query routes (161 GETs and one POST query) reject anonymous and inappropriate-role requests. Account/Customer/Billing/Reading lookups use the existing BillingOperations roles; Collection and SuperAdmin reads retain narrower policies. Mobile assigned-sheet/SOA access retains MeterReader plus ownership checks. Ordinary user directories return IDs/names; administrative email/security profile lookups are restricted. Password and remember-token fields are JSON-suppressed in entity/shared user types. Only credential-validated login may return its issued authentication token/fingerprint; normal queries return neither.

Schema-free persisted token validation now compares current active/verified/role state and a keyed login fingerprint incorporating the existing password hash and update/verification timestamps. Application-issued tokens and the five-minute MVC bridge carry the original credential-issued fingerprint. Password, role, deactivation, verification and normal profile changes invalidate old authorization on the next Data request after commit. Existing pre-B4 sessions/tokens require fresh login; ordinary profile edits also require renewed login. Existing 12-hour Data reader and 30-day Application lifetimes remain, and cached/offline mobile work is unchanged. No user-security migration was introduced. Already authenticated concurrent requests and per-device logout are not represented as globally revoked.

All 40 B2/B3 denied mutation routes remain denied. The inventory classifies 52 queries as BLOCKED — POLICY REQUIRED, including the four previously quarantined mutating Dashboard GETs. Unresolved service/provider/inventory read-dependent screens are deliberately unavailable until a read policy is approved; no service-workflow permission was invented or enabled.

Actual final checks: solution and BillingRegression builds PASS with zero errors; **617 grouped temporary-table checks in tmcwd_test PASS** (274 prior groups + 343 B4 groups); browser **25** and penalty browser **5** PASS; Collections **8 scenarios** and Ledger **31 checks**, plus related browser scenarios PASS; MeterReaderAuthRegression build PASS and run **43 assertions PASS**. B4 uses the actual production persisted-token callback with current synthetic records. It verifies valid staff posting, immutable financial reads, official SOA/download, stale JWT/MVC denial, password-change revocation, ineligible login rejection and real credential/session/reader bridges. Rejected requests preserve financial and user state. Migration-dependent concurrency, standalone service migration/backfill, dependency-upgrade/publish compatibility, real-device end-to-end and live operational authenticated posting remain NOT RUN / NOT VERIFIED. No operational financial/user records or snapshots were changed.

**DEPENDENCY UPDATE REQUIRES SEPARATE APPROVAL:** Xml 8.0.3 remains pinned in Administration, with the old MVC/ViewFeatures/Antiforgery/DataProtection path requesting Xml >= 8.0.2. Removing the pin alone is unsafe. The report proposes the exact 8.0.3 -> 8.0.4 csproj servicing change and required restore/audit/publish/data-protection/regression checks; it was not applied or proven compatible. Local net10.0 web/test graphs use the shared-framework implementation (installed ASP.NET 10.0.12), which does not certify the production publish/runtime graph. Five NU1903 warnings remain.

B4 did not repeat operational SQL. The last dated B3 evidence remains 127.0.0.1:3307/tmcwd, MySQL 8.0.46, maintenance table absent and no eligible active verified SuperAdmin (Id 5 active/unverified). Current operational actor/configuration status therefore remains NOT VERIFIED for deployment. Sheets 2 and 5 with BillingDate 2025-07-01 are covered by PHP 20.00 / 2025-01-01 / NULL; that resolves interval coverage only. Backup independent restore/reconciliation, deployment target/runtime and the coordinated fresh-login live workflow remain unverified.

No migration, seed, deployment, operational account creation/verification or backup operation was performed. Before deployment, close dependency and required unavailable-workflow risks, verify independent recovery and an existing eligible actor, verify target/runtime and live authenticated Application/Data/mobile flow, and obtain separate migration/seed authorization. Apply only the approved 20250101 seed after the migration when separately authorized; 20260101 remains SUPERSEDED / NOT FOR DEPLOYMENT. This amendment does not declare Phase 6I-B complete.

## 2026-10-09 Phase 6I-B5 dependency and operational readiness amendment

This supplements earlier dated evidence without rewriting it. See [Phase6I-B5-Dependency-Operational-Readiness.md](./Phase6I-B5-Dependency-Operational-Readiness.md) for dependency graphs, actual verification, proposed backup/isolated-restore commands and reconciliation gates.

**Dependency-source servicing: PASS. Controlled local deployment: NO-GO. Full-application production readiness: NOT ESTABLISHED.** The only production-source change is the expressly approved Administration Xml **8.0.3 → 8.0.4** reference. Pkcs remains 8.0.1; no other resolved package version changes. The five earlier Xml NU1903 warnings disappear. Current solution and both regression-project transitive audits report zero problems/vulnerable packages. Local Debug Application/Data publishes use the ASP.NET shared-framework Xml assembly rather than copying a NuGet Xml DLL; the actual deployment runtime still requires verification.

Actual checks: restore PASS; solution build PASS (0 errors, 18 existing nullable warnings); BillingRegression and MeterReaderAuthRegression builds PASS (0 warnings/errors); **617 grouped temporary-table checks in tmcwd_test**, browser **25**, penalty browser **5**, Collections **8 scenarios**, Ledger **31 checks** plus related scenarios, and reader **43 assertions** PASS. An additional synthetic DataProtection fixture passed protect/persist, independent-process reload/unprotect and wrong-purpose rejection. No operational key ring was touched. Migration-dependent concurrency, standalone service migration/backfill, real-device end-to-end and live deployed authentication/posting remain unverified.

Fresh read-only identity confirms **127.0.0.1:3307/tmcwd**, **DESKTOP-Q72GRF4**, **MySQL 8.0.46**, 66 visible InnoDB tables, and no checked process/user/machine connection override. Maintenance table remains absent. Eligible active verified SuperAdmin count remains **0**; Id **5** is active and unverified: **BLOCKED — ELIGIBLE SEED ACTOR REQUIRED**. Organizational authorization must be independently established; no administrator was created/verified/changed. Sheets **2 and 5**, BillingDate **2025-07-01**, remain covered by the approved **20.00 / 2025-01-01 / NULL** interval, resolving date coverage only.

Backup recovery remains **NOT VERIFIED**. B5 prepares, but does not execute, a private consistent logical dump, checksum/independent-copy retention plan and separate MySQL-instance restoration to **tmcwd_restore_test**, with events disabled and original/restored schema, rows, snapshots, invoice identity and financial totals reconciled before PASS. No backup was created, overwritten, restored or deleted.

SQL source review confirms additive migration, guarded exact-row idempotent 20250101 seed and retired 20260101 execution abort with no executable insert. Schema rerun limitations, helper procedure DDL/cleanup implications and configuration/actor concurrency limitations remain documented. All **40 mutation quarantines** and **52 blocked query classifications** remain enforced. Coordinated Application/Data release requires fresh login, rejected old JWT/MVC sessions and reader reauthentication for online work while preserving offline cached readings.

No migration, seed, deployment, operational financial posting or account-security change occurred. Close recovery and eligible-actor blockers, verify actual runtime/live coordinated behavior, resolve required full-application workflow policies and obtain separate deployment authorization before proceeding. This amendment does not declare Phase 6I-B complete.

## 2026-10-09 Phase 6I-B6 recovery and seed-actor amendment

This supplements all historical findings. See [Phase6I-B6-Recovery-SeedActor-Readiness.md](./Phase6I-B6-Recovery-SeedActor-Readiness.md) and the reusable [reconciliation manifest](./Phase6I-B6-Reconciliation-Manifest.md).

**BLOCKED — RECOVERY ENVIRONMENT. BLOCKED — SUPERADMIN BOOTSTRAP POLICY. Deployment remains NO-GO.** Fresh read-only SQL matches B5: 127.0.0.1:3307/tmcwd, UUID c3f950fe-b565-11f1-b6a6-30560f67a354, MySQL 8.0.46, 66 visible InnoDB tables and zero visible routines/triggers/events/views subject to privileges. No checked connection override exists. The actual source datadir is recovery/tmcwd-20260921/data; this is the live original and must not be reused for isolation.

Port 3308 remains unreachable; separate instance identity/datadir/scheduler and restore-database absence are unverified. Successful outside-sandbox masked login-path inspection confirms tmcwd_backup and tmcwd_restore_validation absent. Proposed outside-Git backup destination is absent/unapproved. Backup privileges and running applications' effective restore targets remain unverified. No backup/restore gate is inferred satisfied; no data-copy operation ran. Historical recovery SQL files exist but provide no current independent restore/reconciliation evidence.

Fresh eligible active+verified SuperAdmin count remains 0; Id 5 is Role 1, active and unverified. Current supported user administration preserves verification, creates unverified users and requires an already eligible SuperAdmin; login/fingerprint checks reject unverified accounts. No legitimate initial verification/bootstrap workflow was found in inspected source. The B6 report proposes a separately approved, identity-bound, local/manual, audited and one-time fail-closed mechanism without implementing or executing it. Database flags do not prove organizational authority.

A reusable reconciliation report covers all tables/definitions/indexes/constraints/collations/objects, financial totals including BillArrearsAmount, invoice and receipt identity, exact raw InvoiceSnapshotJson hashes and private full-row audit/timestamp/security comparisons. It is preparation only, with no original financial baseline or restored comparison captured. Require zero unexplained differences before recovery PASS.

No migration, seed, administrator/security change, operational financial write, backup, restore or deployment occurred. B5 source/package/test evidence is preserved; no new source behavior changed. Obtain separate environment/operator/bootstrap policy approvals, satisfy gates, then obtain explicit data-copy authorization and actual recovery evidence. Only after both recovery and legitimate eligible-actor blockers close may separate controlled-deployment authorization be considered. The 20260101 seed remains forbidden; only 20250101 is the approved initial artifact.
## 2026-10-09 DEVELOPMENT integration amendment

The stakeholder now explicitly classifies 127.0.0.1:3307/tmcwd and its accounts as synthetic DEVELOPMENT MOCK DATA, and authorizes this specific development verification/migration/seed task. Historical production recovery findings remain preserved. See [Phase6I-B-Development-Deployment-Verification.md](./Phase6I-B-Development-Deployment-Verification.md).

**DEVELOPMENT INTEGRATION: FAIL. PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.** Exact source host/port/schema/UUID/version/datadir was verified before writes. Mock Id 5 changed only verification and related audit/timestamps: Role 1 / Active 1 / Verified 0 → 1; eligible development SuperAdmin count is now 1. No role/password/name/email/activation or other account was changed.

The approved additive migration succeeded and its exact schema was inspected; the new maintenance table is empty. The approved 20250101 seed, with explicit eligible development actor 5, failed at helper DROP PROCEDURE IF EXISTS with MySQL 1370: the configured operator lacks ALTER ROUTINE. No helper or configuration row was created, no rerun/idempotency success occurred, and no privilege grant/direct INSERT fallback was attempted. The retired 20260101 seed was not executed.

Pre/post development baseline remains identical: billings 30, readings 230, sheets 16, payments 8, penalties 5, Other Charges/installments 0/0, non-null snapshots 13; billing sums TotalBillAmount 6766.30, RemainingAmount 5027.30, BillArrearsAmount 717.00 and BillingAdjustment 34.00. Raw issued snapshot digests also match. July sheets 2/5 retain 2025-07-01 but currently have zero deployed rate matches; approved interval coverage does not substitute for the missing seed.

Actual checks: solution and both regression builds PASS; BillingRegression 617 temporary-table groups plus four separately reported invoice/payment/receipt/installment migration-concurrency groups PASS using validated preconfigured local tmcwd_test credentials. An initial attempt with application-derived test credentials failed access before fixtures; the later dedicated-connection run succeeded. Browser 25, penalty browser 5, Collections 8 scenarios, Ledger 31 plus related checks, reader 43 assertions PASS. Live mock login/bridge and development deployed-rate posting remain unverified. ServiceRequestRegression was not run because its default connection/guard remains unsafe for the required dedicated schema. Financial calculations, Xml 8.0.4 and B2/B3/B4 security/quarantines are unchanged.

Development completion requires an authorized maintenance database operator for the unchanged guarded seed, exact row/idempotency/baseline verification and actual development rate lookup. Do not reset credentials or grant application privileges automatically. Production backup/restore and production administrator readiness are DEFERRED because this target/accounts are synthetic development data; production requires separate recovery, organizational authority, target/runtime/workflow verification and deployment approval. No backup/restore or production deployment occurred.
## 2026-10-09 DEVELOPMENT final-verification continuation

**DEVELOPMENT INTEGRATION: PASS. PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.** This follows the manually completed privileged Workbench 20250101 seed and preserves the earlier automated permission failure. See the dated continuation in [Phase6I-B-Development-Deployment-Verification.md](./Phase6I-B-Development-Deployment-Verification.md).

Fresh read-only target identity matches 127.0.0.1:3307/tmcwd and the reviewed UUID/MySQL 8.0.46. Exactly one deployed row exists: Id 1, Amount 20.00, EffectiveFrom 2025-01-01, EffectiveTo NULL, CreatedBy/UpdatedBy 5, DateCreated/DateUpdated 2026-10-09 14:53:39. No overlap/duplicate exists. The stakeholder explicitly confirms the approved manual seed rerun returned ALREADY_PRESENT; this is operator evidence, not reviewer execution or an independently captured pre-rerun timestamp comparison. The complete current row stayed unchanged through final checks.

Actual development service boundaries succeed, with missing 2024-12-31 safely rejected and covered dates resolving PHP 20.00. Sheets 2/5 remain dated 2025-07-01 and resolve exactly one rate: the development July configuration blocker is resolved. Real controller/service HTTP GETs against the development DbContext and production persisted-token callback return anonymous 401, valid reader 403, authorized OIC 200/20.00 and SuperAdmin read 200. Verification-issued tokens do not establish live password-login/Application bridge completion.

An opt-in regression copies the read-only deployed rate into the existing tmcwd_test temporary fixture. Real posting produces water 100.00 + maintenance 20.00 = total/remaining 120.00; water-only penalty is 10.00; retry preserves the invoice/snapshot and prevents duplication. Final BillingRegression PASS: 618 temporary-table groups plus four separately reported migration/concurrency groups. Browser 25, penalty 5, Collections 8 scenarios, Ledger 31 plus related checks and reader 43 assertions PASS. Solution/reader builds pass; BillingRegression has one preexisting synthetic concurrency EF1002 warning. No financial/authentication business logic changed.

Original development counts, sums and every recorded raw snapshot digest remain identical. Phase 6I-A 90 + 17 contribution, Phase 6G child/collection/cancellation/ledger behavior, immutable snapshots and legacy adjustment floor remain protected. No seed, configuration update, account change or financial/customer write occurred on tmcwd during this final task. The retired 20260101 seed remains unused.

Live credential/Application bridge, standalone ServiceRequestRegression dedicated-DB execution, real-device and production readiness remain unverified. Production recovery, organizational administrator, target/runtime/workflow verification and deployment authorization remain deferred requirements; mock administrator eligibility is development-only. Historical B5/B6 and failed development attempt evidence are not erased.