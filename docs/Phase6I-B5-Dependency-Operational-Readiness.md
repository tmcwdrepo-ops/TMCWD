# Phase 6I-B5 — Dependency and operational readiness

Review date: **2026-10-09**. This supplements B3/B4 and the pre-deployment checklist; earlier findings remain valid for their inspection dates.

**Dependency-source verdict: PASS. Controlled local deployment: NO-GO. Full-application production readiness: NOT ESTABLISHED.** No migration, seed, backup, restore, deployment, account-security change or operational financial write was performed. The only production-source change in B5 is the approved Xml package version.

## 1. Xml servicing

`TMCWD/TMCWD.Administration/TMCWD.Administration.csproj` changes `System.Security.Cryptography.Xml` **8.0.3 → 8.0.4**, with no unrelated package upgrade, warning suppression or authentication/business-logic change. Restore/build/regressions succeeded; there was no dependency conflict requiring rollback. Microsoft identifies 8.0.4 as patched for the advisory affecting versions through 8.0.3. [Microsoft runtime advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-23rf-6693-g89p).

## 2. Exact dependency and publish graphs

Before/after resolved graph snapshots: `.tmp/phase6ib5-graphs-before.json` and `.tmp/phase6ib5-graphs-after.json`.

| Graph | Actual result |
|---|---|
| Administration | Only resolved package difference: Xml 8.0.3 removed, Xml 8.0.4 added. Pkcs remains 8.0.1; other BCL/package versions unchanged. |
| Application / Data | No resolved NuGet package-set change; updated Administration project reference metadata. Xml supplied by ASP.NET shared framework. |
| BillingRegression / MeterReaderAuthRegression | No resolved NuGet package-set change; updated Administration reference metadata. |
| Local Application / Data Debug publish | Neither `.deps.json` lists a NuGet Xml runtime entry; neither output copies `System.Security.Cryptography.Xml.dll`. |

The retained legacy dependency chain is MVC 2.3.9 → ViewFeatures 2.3.0 → Antiforgery 2.3.0 → DataProtection 2.3.0 → Xml >= 8.0.2. The approved direct 8.0.4 reference wins in Administration. The pin was not removed and legacy packages were not migrated.

Both inspected publish runtime configurations request .NET/ASP.NET Core 10.0.0 with shared-framework servicing. The local installed ASP.NET runtime is 10.0.12; its Xml assembly file version is 10.0.1226.42308. These are local Debug publish inspections, not deployment or certification of a production runtime. Actual deployment-host runtime/version remains a release gate.

## 3. Actual builds and tests

| Executed check | Result |
|---|---|
| `dotnet restore TMCWD/TMCWD.slnx` | PASS |
| `dotnet build TMCWD/TMCWD.slnx -v quiet` | PASS; 0 errors, 18 existing nullable warnings, no NU1903 |
| `dotnet build tests/BillingRegression/BillingRegression.csproj -v quiet` | PASS; 0 warnings/errors |
| `dotnet run --project tests/BillingRegression --no-build -- . --no-migrations` | PASS; **617 grouped temporary-table checks**, isolated `tmcwd_test` fixtures |
| `node tests/BillingRegression/browser-regression.cjs` | PASS; **25** |
| `node tests/BillingRegression/penalty-charging-regression.cjs` | PASS; **5** |
| `node tests/BillingRegression/collections-browser-regression.cjs` | PASS; **8 Collections scenarios, 31 Ledger checks**, and related Other Charges/cancellation/Dashboard scenarios |
| `dotnet build tests/MeterReaderAuthRegression/MeterReaderAuthRegression.csproj -v quiet` | PASS; 0 warnings/errors |
| `dotnet run --project tests/MeterReaderAuthRegression --no-build` | PASS; **43 assertions** |
| Application/Data local publish, `--no-build --no-restore`, Debug | PASS; dependency selection inspected |
| Additional isolated DataProtection fixture | PASS; protect/persist, independent-process reload/unprotect, wrong-purpose rejection |

The existing suites exercise credential login, password-change invalidation, production persisted JWT validation, stale MVC bridge denial, anti-forgery, reader authorization and financial contracts. RemainingAmount-based arrears, immutable issued snapshots, water-only penalties, Other Charges separation, collection/cancellation behavior, invoice identity and the existing adjustment floor remain protected. No financial calculation changed.

The extra harness is under ignored `.tmp/Phase6IB5DataProtection`; it uses the Administration reference graph and a new synthetic key directory, not any operational key ring. Two independent processes verified persistence compatibility. Its keys are not a database backup.

Evidence logs: `.tmp/phase6ib5-restore.log`, `phase6ib5-build.log`, `phase6ib5-regression-build.log`, `phase6ib5-regression.log`, `phase6ib5-browser.log`, `phase6ib5-penalty-browser.log`, `phase6ib5-collections-browser.log`, `phase6ib5-reader-build.log`, `phase6ib5-reader.log`, `phase6ib5-data-protection-build.log`, `phase6ib5-data-protection.log` and both `phase6ib5-publish-*.log`, all beneath `.tmp`.

Initial sandbox network/output-directory failures were retried with authorized escalation; only completed successful runs above count as PASS. Migration SQL and migration-dependent invoice/payment concurrency are explicitly **NOT RUN** under `--no-migrations`. Standalone service migration/backfill, real-device end-to-end, live operational posting and deployed-host authentication were not run.

## 4. Remaining advisories

Current NuGet vulnerability audits with transitive dependencies and `--no-restore --format json`: solution **9 projects**, BillingRegression **1**, MeterReaderAuthRegression **1**; each reports **0 problems and 0 vulnerable packages**. Evidence: `.tmp/phase6ib5-vulnerability-audit.json`, `phase6ib5-regression-audit.json`, `phase6ib5-reader-audit.json`. The five earlier Xml NU1903 warnings disappear; no NU1901–NU1904 or NU1603/NU1605 appears in the completed restore/build logs. This is a dated feed result, not assurance against future advisories or an uninspected production runtime. Existing nullable warnings remain separate technical debt.

## 5. Fresh read-only target identity

| Property | Verified value |
|---|---|
| Selected database / configured host / port | **tmcwd / 127.0.0.1 / 3307** |
| Server hostname / version | **DESKTOP-Q72GRF4 / MySQL 8.0.46** |
| Server UUID | `c3f950fe-b565-11f1-b6a6-30560f67a354` |
| Database charset / collation | utf8mb4 / utf8mb4_0900_ai_ci |
| `lower_case_table_names` / event scheduler | 1 / ON |
| Visible base tables | **66; all InnoDB** |
| Visible routines / triggers / events / views | 0 / 0 / 0 / 0, subject to credential visibility |
| Maintenance table | **Absent**, count 0 |

SQL mode is `ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION`; session timezone SYSTEM. Metadata evidence: `.tmp/phase6ib5-target-readonly.log` and `phase6ib5-target-metadata.log`.

Application and Data share the local-development user-secrets ID `TMCWD.Data-local-development`. Development user secrets override JSON connection settings. The process/user/machine scopes contain no checked DefaultConnection environment override (`ConnectionStrings__DefaultConnection`, CUSTOMCONNSTR, MYSQLCONNSTR and ASPNETCORE/DOTNET-prefixed variants). This does not inspect another already-running process's private environment. Development launch settings use Data port 5178 and Application 5054; MVC API target is `http://localhost:5178`. No credential was disclosed.

## 6. Backup plan — proposed, NOT EXECUTED

Separate explicit authorization is required before credential-profile setup, backup, service quiescence or restore. Use existing authorized database operators, not a newly created privileged account. A DBA must establish complete object visibility and the privileges needed for tables, views, routines, triggers and events; application credential backup privileges were not tested.

Proposed private destination: **`D:\TMCWD-Backups\local-development\tmcwd`**, outside Git, on encrypted storage with restrictive NTFS access. It has not been created. Retain at least **30 days and until release/recovery approval, whichever is longer**; deletion requires separate authorization. Keep an independently stored encrypted copy and verify its checksum. Dumps contain sensitive customer, financial and credential-hash data; do not commit, email or expose their contents. Recoverable application configuration/secrets and operational DataProtection keys require a separate controlled recovery arrangement; a logical database dump does not contain them.

Before capture, recheck host/port/UUID, table engines and full object inventory. Under separately approved maintenance, quiesce all operational writers and schema changes, including relevant scheduled writers, while capturing the original reconciliation baseline and dump. InnoDB `--single-transaction` provides a consistent snapshot, but concurrent DDL can invalidate the capture; routines and events require explicit options. The Windows `--result-file` option avoids shell encoding conversion and must be guarded against overwrite. [MySQL dump documentation](https://dev.mysql.com/doc/refman/8.0/en/mysqldump.html).

Use an existing authorized backup user in a password-prompted `mysql_config_editor` login path named `tmcwd_backup`; never put a password in command text. Login paths are obfuscated, not strongly encrypted, so protect `.mylogin.cnf` permissions. Configure a separate existing restore operator/profile `tmcwd_restore_validation` for port 3308. Neither profile was created here. [MySQL credential-profile documentation](https://dev.mysql.com/doc/refman/8.0/en/mysql-config-editor.html).

**Future authorized PowerShell command; do not run as part of B5:**

```powershell
$mysqlBin = 'C:\Program Files\MySQL\MySQL Server 8.0\bin'
$backupRoot = 'D:\TMCWD-Backups\local-development\tmcwd'
if (!(Test-Path -LiteralPath $backupRoot -PathType Container)) { throw 'Approved private destination required.' }
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmssfffZ')
$nonce = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$artifact = Join-Path $backupRoot "tmcwd_${stamp}_pre_phase6ib_${nonce}.sql"
if (Test-Path -LiteralPath $artifact) { throw 'Refuse overwrite.' }
& "$mysqlBin\mysqldump.exe" --login-path=tmcwd_backup --protocol=TCP --host=127.0.0.1 --port=3307 --single-transaction --quick --routines --triggers --events --hex-blob --default-character-set=utf8mb4 --set-gtid-purged=OFF --no-tablespaces --column-statistics=0 --skip-add-drop-table --skip-add-locks --skip-lock-tables "--result-file=$artifact" tmcwd
if ($LASTEXITCODE -ne 0) { throw 'Dump failed; preserve partial artifact for review.' }
if (!(Test-Path -LiteralPath $artifact) -or (Get-Item -LiteralPath $artifact).Length -eq 0) { throw 'Missing/empty dump.' }
$checksumFile = "$artifact.sha256"
if (Test-Path -LiteralPath $checksumFile) { throw 'Refuse checksum overwrite.' }
$checksum = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
[System.IO.File]::WriteAllText($checksumFile, $checksum, [System.Text.UTF8Encoding]::new($false))
```

Do not add `--force`, `--databases` or `--all-databases`. The plan avoids a dump-created original database selector, GTID changes and table-drop statements. Record UTC, version, UUID, selected schema, options, exit code, byte length, checksum and frozen baseline reference in a protected manifest. Rehash the independent copy. Checksum success alone is not restoration evidence. This is a logical snapshot, not a point-in-time/binlog recovery plan.

## 7. Isolated restore and reconciliation plan — NOT EXECUTED

Use a separately authorized **MySQL 8.0.46 instance on 127.0.0.1:3308**, separate data directory, **event_scheduler=OFF**, and database **tmcwd_restore_test**. Verify a different server UUID from section 5, compatible SQL mode/case behavior/collation, absence of that restore database and no connection to operational integrations. Do not change the original server's event scheduler. No application/background worker should point to the restored copy.

A DBA must review dump routines, definers and schema-qualified references before import. Do not automatically replace `tmcwd` throughout a dump: stored financial text/snapshots must remain intact. Stop on incompatible definitions or import errors; do not ignore failures. The separate instance prevents restored event/routine definitions from reaching the original schema.

**Future authorized restore commands**, only after identity, absence and checksum gates:

```powershell
# $mysqlBin and $artifact refer to the validated tool directory and verified dump.
& "$mysqlBin\mysql.exe" --login-path=tmcwd_restore_validation --protocol=TCP --host=127.0.0.1 --port=3308 --batch --execute="SELECT @@server_uuid, @@port, VERSION(), @@event_scheduler; SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name='tmcwd_restore_test';"
# Operator must verify the returned identity, event scheduler OFF and count 0.
& "$mysqlBin\mysql.exe" --login-path=tmcwd_restore_validation --protocol=TCP --host=127.0.0.1 --port=3308 --execute="CREATE DATABASE tmcwd_restore_test CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;"
if ($LASTEXITCODE -ne 0) { throw 'Restore schema creation failed.' }
if ((Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash -ne [System.IO.File]::ReadAllText("$artifact.sha256").Trim()) { throw 'Checksum mismatch.' }
$stdoutLog = "$artifact.restore.stdout.log"
$stderrLog = "$artifact.restore.stderr.log"
if ((Test-Path -LiteralPath $stdoutLog) -or (Test-Path -LiteralPath $stderrLog)) { throw 'Refuse log overwrite.' }
$restore = Start-Process -FilePath "$mysqlBin\mysql.exe" -ArgumentList @('--login-path=tmcwd_restore_validation','--protocol=TCP','--host=127.0.0.1','--port=3308','--default-character-set=utf8mb4','--binary-mode','--database=tmcwd_restore_test') -RedirectStandardInput $artifact -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog -WindowStyle Hidden -Wait -PassThru
if ($restore.ExitCode -ne 0) { throw 'Restore failed; preserve artifacts/logs for review.' }
```

Never drop/reuse an existing restore database, restore over `tmcwd`, run financial posting, or start an operational application to verify restoration. These proposed isolated DDL/import commands are **not authorization to execute them**.

Capture these exact row counts using read-only SQL on the frozen original and restored schema, selected through the connection's database argument:

```sql
SELECT 'users' AS TableName, COUNT(*) AS RowCount FROM users
UNION ALL SELECT 'customers', COUNT(*) FROM customers
UNION ALL SELECT 'accounts', COUNT(*) FROM accounts
UNION ALL SELECT 'billings', COUNT(*) FROM billings
UNION ALL SELECT 'readings', COUNT(*) FROM readings
UNION ALL SELECT 'reading_sheets', COUNT(*) FROM reading_sheets
UNION ALL SELECT 'payments', COUNT(*) FROM payments
UNION ALL SELECT 'penalties', COUNT(*) FROM penalties
UNION ALL SELECT 'other_charges', COUNT(*) FROM other_charges
UNION ALL SELECT 'other_charge_installments', COUNT(*) FROM other_charge_installments;
```

Required protected baseline and restored comparisons:

- All 66 existing base tables, complete schema/index/constraint/collation definitions, and full routine/trigger/event/view inventories, not merely the visible application-credential catalog.
- Counts above and payment receipts, invoice cancellations, invoice sequence/configuration and adjustment records where present.
- User role/active/verified distributions, with no password/hash or personal information printed in normal logs.
- Exact decimal totals for Billing.TotalBillAmount, RemainingAmount, Arrears, BillingAdjustment and OtherCharges grouped by status; Payment.PaidAmount grouped by success/method/charges-only; Penalty.Amount by payment status; OtherCharge.Amount by status/active; installment amounts by status. Confirm mapped column names against schema before preparing these SELECTs; never round totals to make comparisons match.
- Null/nonempty/valid-JSON issued snapshot counts; every invoice identifier, duplicate-identifier distribution and associated billing/account/reading references. Compare SHA-256 of the raw snapshot bytes, preserving preexisting null/legacy snapshots without rewriting them.
- Complete ordered row-content comparison for every table, including financial balances, snapshots, audit fields, timestamps and security fields. Perform sensitive-field comparison privately; counts and aggregate totals alone cannot establish restoration correctness.

Preserve the original point-in-time baseline with its checksum. If original writes resume, compare the restored copy against that baseline, not a changing live database. Require zero unexplained schema, row-content, identifier, snapshot or financial-total differences. Record actual import success and reconciliation evidence before marking recovery PASS. No original baseline capture, isolated restore or reconciliation was performed in B5.

## 8. Actual backup status

**NOT VERIFIED — deployment blocker.** Workspace backup-name inspection found no candidate; an external backup may exist, but none was independently restored/reconciled for this review. No backup was created, overwritten, restored or deleted. MySQL client/dump utility versions were inspected only. Proposed destination and commands are a plan, not successful backup evidence.

## 9. Eligible SuperAdmin

Fresh SELECT confirms **Id 5, Role 1 (SuperAdmin), active 1, verified 0**; eligible active+verified SuperAdmin count **0**. **BLOCKED — ELIGIBLE SEED ACTOR REQUIRED.** No account was created, activated, verified, reset or assigned a different role.

Before separately authorized deployment, an authorized TMCWD administrator must establish a legitimate, active, verified SuperAdmin through the approved account-security process; independently confirm organizational authority for this configuration and record the explicit actor ID. Database flags alone do not prove that authority. Re-query eligibility immediately before the seed; never substitute an arbitrary user or bypass the guard. This report does not authorize those account changes.

## 10. Migration source readiness

`20261008_add_meter_maintenance_rates.sql` is additive `CREATE TABLE IF NOT EXISTS`, InnoDB, decimal **(12,2)** amount, DATE bounds, required audit IDs, primary key, unique EffectiveFrom and interval index. Checks reject negative amounts and reversed bounds; MySQL 8.0.46 enforces them. There is no historical invoice/payment/billing DML.

**Source review PASS with limitations:** rerunning does not validate or repair a differently defined existing table; cross-row overlap protection is implemented by serialized service/seed checks, not a SQL check constraint. Inspect exact schema before any future execution. Broader request validation and the previously documented configuration/posting race remain separate risks; quiesce configuration changes during deployment/posting. Migration execution and schema creation remain untested here.

## 11. Approved 20250101 seed readiness

`20261008_seed_meter_maintenance_rate_20250101.sql` inserts only **PHP 20.00 / 2025-01-01 / NULL**, requiring explicit `@authorized_service_user_id` with exactly one matching active, verified Role 1 user. It obtains `tmcwd:meter_maintenance_rates` advisory lock, uses a transaction, rejects overlapping/conflicting intervals and multiple exact matches, and treats the sole exact row as already present. It never updates/deletes existing configuration or historical invoices, billings, payments, snapshots or balances. Rollback/release/resignal protects failures.

**Source review PASS with operational limitations:** helper procedure DROP/CREATE/CALL/DROP involves DDL privileges, implicit commits, reserved-name collision and error cleanup considerations. Use a dedicated deployment connection after actor review and backup verification. Actor eligibility is checked before the lock/transaction, so freeze relevant administrator/configuration changes during execution and recheck afterward. A future schedule overlapping the open-ended initial interval is rejected; approved schedule changes require the existing controlled service workflow.

Sheets **2 and 5**, BillingDate **2025-07-01**, Status **1**, remain covered by the approved interval. The earlier date-coverage blocker is resolved by the amendment; actual posting remains blocked by absent deployed configuration and operational gates. No sheet date or financial record changed. Selection remains ReadingSheet.BillingDate, once per eligible monthly bill; configuration changes remain SuperAdmin-only.

## 12. Retired 20260101 seed

`20261008_seed_meter_maintenance_rate_20260101.sql` remains **SUPERSEDED / NOT FOR DEPLOYMENT**. Its sole executable statement is `SIGNAL SQLSTATE '45000'`; the former body is entirely block-commented. No executable INSERT or helper call remains, even if a client continues after the error. Only the approved 20250101 seed belongs in a future deployment sequence. Neither seed was executed.

Reviewed SHA-256 artifact fingerprints:

| Artifact | SHA-256 |
|---|---|
| Migration | `DC7D3D0E417E2BC60BD522AAA091FA78E814D6B2F53D41C5396A3E8A85553FFA` |
| Approved 20250101 seed | `37ACBE68D655849928F51EFDC92FC47769DBD5CC89D835077B75C348C23E4B7D` |
| Retired 20260101 seed | `93BAFCDD01FA9EBC72C091F0DC9349B9338C76B128E61068B23C8AA1E40CE871` |

## 13. Coordinated fresh-login release checklist

For a later separately authorized coordinated Application + Data release:

1. Verify target/runtime and recovery evidence, and publish the fresh-login notice before release.
2. Require all users to log in again. Cached pre-B4 MVC sessions cannot be reused to mint privileged bridge tokens; old API JWTs fail closed.
3. Meter Readers must reauthenticate before new online operations. Expired authentication must not delete downloaded readings, pending drafts or offline partitions.
4. Confirm valid fresh login/bridge/assigned-reader access and rejected old-token behavior in the actual deployment environment before opening approved operations.
5. Retain current fingerprints, claim-derived actor identity, CSRF and transactional guards. Do not weaken validation to avoid re-login.

These live release checks were not run. Already validated in-flight requests are not retrospectively canceled; global per-device logout/revocation is not introduced by this package servicing.

## 14. Quarantined workflows

All **40 B2/B3 denied mutation routes** remain denied. B4's **162 query routes** retain classification/protection, including **52 blocked queries** (four overlap previously quarantined mutating Dashboard GETs). Source authorization was not modified; the relevant regression suites passed.

Request/Detail/File, JobOrder, Dashboard service flows, DebitCreditMemo, Material, Inventory, Recommendation, Workflow, InspectionTypeDetail, Reading SaveRange, EWalletTransaction, WebHook, generic Billing SaveUpdate and other quarantined operations remain unavailable. They require approved role/ownership/approval/provider-trust policies; no permission or SuperAdmin bypass was invented.

## 15. Controlled Phase 6I-B deployment verdict and sequence

**NO-GO.** Dependency-source servicing, fresh local target identity and SQL source review pass. Independently restored/reconciled backup evidence and an eligible organizationally authorized seed actor are missing. Source PASS does not authorize migration, seed or posting.

Required sequence after separate authorizations: approve maintenance/recovery work → verify exact target and full object inventory → capture original baseline and consistent backup → verify independent isolated restoration/reconciliation → establish/recheck legitimate eligible actor → verify coordinated release/runtime and fresh-login behavior → obtain explicit migration/seed authorization → verify reviewed artifact hashes and apply additive migration then **only 20250101 seed** → verify schema/exact configuration and unchanged historical financial baselines → run separately authorized synthetic post-deployment checks. Never use operational financial posting as an unapproved deployment test. Preserve the existing checklist's post-deployment financial-contract tests; migration-dependent concurrency remains outstanding.

## 16. Full-application production qualification

**NOT READY / NOT ESTABLISHED separately from maintenance calculation verification.** Quarantined service/provider/inventory workflows may block a full production release even when the maintenance configuration is ready. Backup/actor gates, actual production target/runtime, live coordinated session behavior, unavailable-workflow policy decisions and previously recorded account-security/race/validation limitations require disposition. B5 does not declare Phase 6I-B complete and grants no deployment authority.
