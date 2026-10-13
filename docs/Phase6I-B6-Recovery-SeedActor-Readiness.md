# Phase 6I-B6 — Recovery and seed-actor readiness

Review: **2026-10-09**, Asia/Manila. This supplements B5/B4 and preserves historical findings. Ponytail full: readiness analysis and reusable reconciliation report only; production source, dependencies, financial logic and security state are unchanged.

**BLOCKED — RECOVERY ENVIRONMENT. BLOCKED — SUPERADMIN BOOTSTRAP POLICY. Phase 6I-B: NO-GO FOR DEPLOYMENT.** No backup/restore or account change is authorized by B6. Even with every technical gate satisfied, execution would still require separate data-copy authorization.

## 1. Fresh source identity

Read-only configuration and SQL reconfirmed the exact B5 target:

| Item | Actual evidence |
|---|---|
| Configured host / selected schema / server port | **127.0.0.1 / tmcwd / 3307** |
| UUID | **c3f950fe-b565-11f1-b6a6-30560f67a354** |
| MySQL version | **8.0.46** |
| Visible base-table inventory | **66, all InnoDB** |
| Visible routines / triggers / events / views | **0 / 0 / 0 / 0**, subject to operator privilege visibility |
| Actual server data directory | `D:\Projects 20260720\TMCWD\recovery\tmcwd-20260921\data\` |

No checked DefaultConnection override exists in Process/User/Machine scopes (ConnectionStrings, CUSTOMCONNSTR, MYSQLCONNSTR and ASPNETCORE/DOTNET-prefixed variants). Development configuration/user-secrets selection remains as documented in B5; secrets were not printed. The data directory is the **live original**, not an isolated backup to initialize, move, delete or restore over. Source UUID matches B5, so the unexpected-target STOP condition did not occur.

## 2. Restore-instance availability

TCP connection attempts to **127.0.0.1:3308 timed out**, both inside and outside the sandbox. No reachable isolated MySQL instance could be verified. This does not prove no stopped instance exists; it establishes that the required usable validation environment is unavailable.

Different UUID/version/data directory, scheduler OFF and absence of `tmcwd_restore_test` are therefore **NOT VERIFIED**. No restore database was dropped or created. Repository application settings contain no 3308 target; the current source connection remains 3307. Another running Application/Data process's private effective configuration was not inspected, so the no-operational-process-to-3308 gate remains **NOT VERIFIED**, not inferred from repository configuration.

Actual restore: **NOT RUN — ISOLATED MYSQL INSTANCE REQUIRED**.

### Proposed isolated provisioning, requiring separate approval

An authorized local DBA should prepare a new private directory outside Git, for example `D:\TMCWD-RecoveryValidation\mysql-3308`, with an empty new `data` subdirectory and restricted access. Never reuse the original data directory or its my.ini. Provisioning and operator credentials are not authorized or performed here.

Proposed isolated `my.ini`:

```ini
[mysqld]
basedir=C:/Program Files/MySQL/MySQL Server 8.0
datadir=D:/TMCWD-RecoveryValidation/mysql-3308/data
port=3308
bind-address=127.0.0.1
mysqlx=0
event_scheduler=OFF
lower_case_table_names=1
character-set-server=utf8mb4
collation-server=utf8mb4_0900_ai_ci
log-error=D:/TMCWD-RecoveryValidation/mysql-3308/mysql-error.log
```

After approved directory/config creation and an explicit empty-directory check, initialize **only that new instance** using MySQL 8.0.46:

```powershell
# Future DBA operations only; NOT EXECUTED by B6.
$mysqlBin = 'C:\Program Files\MySQL\MySQL Server 8.0\bin'
$isolatedIni = 'D:\TMCWD-RecoveryValidation\mysql-3308\my.ini'
& "$mysqlBin\mysqld.exe" "--defaults-file=$isolatedIni" --initialize
if ($LASTEXITCODE -ne 0) { throw 'Initialization failed; do not retry over existing data.' }
Start-Process -FilePath "$mysqlBin\mysqld.exe" -ArgumentList @('--defaults-file="D:\TMCWD-RecoveryValidation\mysql-3308\my.ini"') -WindowStyle Hidden
```

`--initialize` generates a temporary root credential; keep its error log private and let the DBA complete the prompted local secure setup. Do not use insecure initialization, hardcoded passwords, startup TMCWD verification or changes to the 3307 service. [MySQL initialization documentation](https://dev.mysql.com/doc/refman/8.0/en/data-directory-initialization.html).

The authorized DBA must then establish a least-privilege restore operator for the new instance, match source SQL mode/timezone, verify UUID differs, port 3308, exact separate datadir and scheduler OFF, and confirm `tmcwd_restore_test` absent. Inspect actual running Application/Data effective targets privately. Do not create the restore database until separate restore authorization. Provider integrations/background application workers must remain disconnected. Restore compatibility, routine definers and schema-qualified references still require the B5 review.

## 3. Tooling

Installed under `C:\Program Files\MySQL\MySQL Server 8.0\bin`:

| Tool | Actual version command |
|---|---|
| mysql.exe | **8.0.46**, Win64 x86_64 |
| mysqldump.exe | **8.0.46**, Win64 x86_64 |
| mysql_config_editor.exe | **8.0.46**, Win64 x86_64 |

Only version commands and masked-profile inspection ran; no dump/import command ran.

## 4. Backup operator/profile readiness

The first sandbox `mysql_config_editor print --all` crashed (exit -1073741819); its inferred absence result was not accepted. An approved read-only retry outside the sandbox completed with **exit 0**. Output was captured privately, and only profile-name presence was reported:

- `tmcwd_backup`: **absent**.
- `tmcwd_restore_validation`: **absent**.

No login path was created/modified and no credentials were printed. Required operators' authentication/privileges cannot be verified without those profiles; the normal application's visible catalog does not prove complete backup coverage. **BLOCKED — AUTHORIZED BACKUP OPERATOR REQUIRED.** A separately authorized DBA must designate existing approved operators or obtain separate account-provisioning authority, securely configure prompted profiles, and verify table/view/routine/trigger/event visibility and required dump/restore privileges. Organizational authority must accompany technical permissions.

## 5. Execution gates

| B6 gate | Status |
|---|---|
| Exact 3307/tmcwd identity | PASS |
| Separate safe MySQL 3308 instance | NOT VERIFIED / unreachable |
| `tmcwd_restore_test` absent | NOT VERIFIED; no restore connection |
| Authorized backup profile/operator | FAIL; required profile absent |
| Authorized restore profile/operator | FAIL; required profile absent |
| Approved private destination outside Git | NOT ESTABLISHED; B5 proposed directory does not exist |
| No artifact overwritten | No artifact created; future unique-path gate outstanding |
| No operational application uses restore instance | NOT VERIFIED for running processes |

Gates are not satisfied. **Do not improvise and do not execute backup/restore.** B5's reviewed dump/checksum/isolated-import commands remain a proposed procedure, not an executable authorization. This review cannot report READY FOR BACKUP/RESTORE AUTHORIZATION yet.

## 6–7. Actual backup and restore status

Backup: **NOT RUN**. Restore: **NOT RUN — ISOLATED MYSQL INSTANCE REQUIRED**. Independent restoration/reconciliation: **NOT VERIFIED**.

Historical files exist in `recovery/tmcwd-20260921`, including `tmcwd_recovered.sql`, source SQL and recovery documentation. B5's backup-name scan did not establish their readiness. Their existence does not certify current database coverage, secure storage, checksum, independent restoration or reconciliation against a frozen current baseline. No historical dump was executed or rewritten, and no personal/security row contents were printed. The directory also contains the live original server's data; it must never be treated as a disposable restore fixture.

## 8. Reconciliation preparation

Prepared [Phase6I-B6-Reconciliation-Manifest.md](./Phase6I-B6-Reconciliation-Manifest.md), a reusable report with read-only catalog/count-command generation, complete schema/index/constraint/collation/object comparison, exact financial aggregates, invoice/receipt multiplicities and raw snapshot hashes. It covers every base table and private complete-row audit/timestamp/security comparison, with zero unexplained differences required.

Source mapping corrections for future execution: arrears is **BillArrearsAmount**; the official snapshot's raw storage is **InvoiceSnapshotJson**; bill status column is **Status**. Payments can share a PaymentReference across allocations; uniqueness belongs to receipt headers, so child multiplicities must be preserved rather than flagged indiscriminately. Snapshot hashes and identifiers belong in the private manifest, not normal console logs.

No source baseline capture or financial reconciliation SQL ran in B6. The full-row comparator is specified for the later approved operation, not implemented or tested here. The manifest explicitly requires actual comparator validation and complete evidence before PASS; no skipped comparison counts as successful recovery.

## 9. Fresh SuperAdmin eligibility

Read-only SELECT: **Id 5, Role 1, Active 1, Verified 0**. Eligible active+verified SuperAdmin count: **0**. No role/password/activation/verification/timestamp/audit state was changed.

**BLOCKED — ELIGIBLE SEED ACTOR REQUIRED.** Database flags alone do not establish the administrator's actual identity or organizational authorization.

## 10. Existing verification/bootstrap workflow

Traced Administration MVC → `UserTransaction` → Data `UsersController` → `UserService`, login/token validation, verification fields, UI, startup and repository SQL/documentation:

- `AdminController.SaveUser` is a CSRF-protected form operation routed through UserTransaction, not direct SQL. The AddEditUser view has a legacy hidden DateVerified field; this is not a supported verification control.
- `UserTransaction.SaveUpdate` constructs the strict UserWriteRequest without verification fields. `UsersController.SaveUpdate` requires a currently persisted active verified SuperAdmin, preserves server verification state and rejects peer-SuperAdmin changes. New accounts are explicitly unverified with DateVerified null.
- UserService persists a server-loaded tracked user; it supplies no verify/approve/bootstrap command. Self password change needs a valid eligible authenticated identity and proves current password; it cannot recover an unverified administrator.
- General Data credential login and reader login reject unverified accounts. Persisted token validation likewise rejects them. A stale privileged token cannot unlock this state.
- No supported verification UI/action, initial verified-administrator bootstrap, account approval command or startup user seed was found in current production source. Historical recovery SQL carries recovered state, not a reviewed one-time identity-bound bootstrap. TestUser is a lookup, not an initializer; synthetic regression fixtures are not operational account procedures. The reviewed recovery documentation provides no verification/bootstrap policy.

Therefore no supported current application path can verify the first eligible SuperAdmin. Do not use hidden form fields, generic entity binding, test fixtures or historical restores as a bypass. **BLOCKED — INITIAL SUPERADMIN BOOTSTRAP POLICY REQUIRED.** This is a finding about inspected repository workflows, not a claim that no external organizational procedure exists.

## 11. Smallest proposed one-time bootstrap — design only

Require a separately approved **local/manual maintenance command**, with no HTTP endpoint or startup behavior. Do not implement it in B6. The organizational owner must designate the exact existing administrator ID and independently confirm the person, account and authority; Id 5 must not be selected merely because it is the only current role-1 row.

Proposed fail-closed contract:

1. Supply explicit target database UUID/schema and designated user ID, an approval/change-ticket reference and identified operator; no default account selection, arbitrary discovery or embedded credentials. Use separately authorized local database access with secret prompting/protected configuration.
2. Require verified recovery, paused relevant account/configuration changes, expected source UUID, existing Role=SuperAdmin, Active=true, Verified=false, zero already eligible SuperAdmins, and independent private identity confirmation. Reject all mismatches.
3. Take an exclusive maintenance lock and transaction; lock/recheck the exact designated row and eligible-administrator predicate. Provisioning lock coverage must be enforced by the maintenance window; an advisory lock alone cannot stop arbitrary SQL writers.
4. Change only verification and its approved audit/timestamp metadata. Preserve role, password, activation, financial data and unrelated users. Define who may populate UpdatedBy/verification audit metadata before implementation; never invent an already authenticated application actor in this bootstrap deadlock.
5. Persist an approved one-time completion/audit marker atomically with the state change; bind it to UUID and designated ID. Its schema/storage and append-only protection require explicit approval. Reject subsequent execution when the marker exists or eligibility/state differs. An external log alone is insufficient for transactional one-time enforcement.
6. Record approval/operator/UTC/before-after flags and result without credentials, hashes or PII. Failure rolls back. Archive the manual tool after completion, restrict access and require fresh login; do not relax B4 fingerprints.

This proposal requires organizational/bootstrap audit policy and implementation authorization, including any necessary audit-marker schema. It is **not executable SQL**, not permission to verify Id 5, and not a permanent anonymous recovery API. No bootstrap source, account or marker was created.

## 12. Exact remaining manual and authorization steps

1. Obtain DBA approval to provision the separate 3308 instance and approved private backup storage; create no source-server substitute for isolation.
2. Establish authorized backup/restore operators and prompted login paths; prove complete catalog and dump/restore privileges without exposing credentials.
3. Verify every gate in section 5, including actual running application targets. Only then report READY FOR BACKUP/RESTORE AUTHORIZATION and request separate data-copy authorization.
4. After that authorization, execute the reviewed B5 procedure under a consistent frozen baseline, verify checksum/independent copy, restore solely to isolated `tmcwd_restore_test`, and complete the manifest with zero unexplained differences.
5. Obtain the organization's existing external administrator-recovery procedure, or approve the minimal bootstrap policy/design separately, then authorize its implementation/execution. Confirm the designated actor is genuinely TMCWD-authorized, Role SuperAdmin, active, verified and known by explicit ID.
6. Immediately before any future authorized seed, re-query exact actor state and authority, freeze relevant admin/configuration changes and pass that explicit ID in the guarded seed session. Never bypass seed validation.
7. Obtain separate coordinated release/migration/seed authorization only after both recovery and legitimate-actor blockers are closed. Preserve fresh-login, runtime and full-application quarantine qualifications from B5.

## 13. Frozen source and deployment artifacts

Migration and both seed artifacts: **NOT EXECUTED**. Approved initial configuration remains **PHP 20.00 / 2025-01-01 / NULL**, once per eligible monthly bill selected by ReadingSheet.BillingDate. July 2025 coverage remains resolved by the approved interval, not by deployed data. The 20260101 seed remains SUPERSEDED / NOT FOR DEPLOYMENT.

No changes to immutable snapshots, RemainingAmount-based arrears, water-only penalty, Other Charge installments, collection allocation, cancellation restoration, adjustment PHP 20 floor, invoice numbering, B2/B3/B4 authorization/fingerprints, quarantined routes or Xml 8.0.4. B5 test results remain dated B5 evidence; no build/regression rerun was needed for these documentation-only artifacts. Existing dirty Git state was preserved.

## 14. Verdict

**BLOCKED — RECOVERY ENVIRONMENT** (unverified isolated server, absent profiles, unestablished approved destination). **BLOCKED — SUPERADMIN BOOTSTRAP POLICY** (zero eligible actors and no supported initial verification path found).

**NO-GO FOR DEPLOYMENT.** Independently restored/reconciled recovery and a legitimate active verified organizationally authorized administrator must both actually exist before readiness can advance. No data-copy, provisioning, bootstrap, migration, seed or posting operation occurred. Review stops after reconciliation preparation and documentation.
