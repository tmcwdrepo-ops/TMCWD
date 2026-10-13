# Phase 6I-B Posting Security Remediation

Date: 2026-10-08 (Asia/Manila).

**Subsequent effective-date amendment (2026-10-08):** the approved initial start changed from 2026-01-01 to 2025-01-01. The historical July-sheet coverage blocker below is now resolved by the approved interval; it is retained as a dated finding. Use only the new 20250101 seed after separate deployment authorization. The security remediation, historical test results and remaining backup/actor/authorization gates are unchanged. See the current amendment in [Implementation Verification](./Phase6I-B-Implementation-Verification.md).

**Posting-security remediation: PASS within the scope below. Phase 6I-B deployment: INCOMPLETE / NO-GO.**

This report supersedes the posting-endpoint finding in the dated [pre-deployment checklist](./Phase6I-B-PreDeployment-Checklist.md) for the current working tree. It does not supersede that checklist's other blockers or authorize deployment.

## Root cause

The Data API's `ReadingSheetController.PartialPostReadingSheet` lacked authorization and accepted `userId` from the request query. `ReadingSheetService.PartialPost` trusted its internal actor argument. Data `Program.cs` validates bearer tokens when authentication is requested, but has no global fallback authorization policy: authentication middleware alone did not protect this action. A caller could reach financial posting without a validated identity and supply another actor ID for billing/reading audit fields.

The adjacent sheet creation/update, bulk status, completion and progress write actions had the same missing boundary protection. These were secured as part of the sheet posting lifecycle; unrelated financial APIs were inspected and documented below rather than changed.

## Authorization policy and workflow

Reused the existing **BillingOperations** policy without changing its definition:

| Allowed role | Canonical enum value |
|---|---|
| SuperAdmin | 1 |
| CustomerRepresentative | 2 |
| CustomerServiceOic | 6 |
| Cashier | 7 |

This is the policy already used for Billing writes, Bill Adjustments, penalty posting, Other Charge management and maintenance-rate lookup. The portal supports these billing/collection staff through the existing login/session workflow. There was no narrower established sheet-posting policy to reuse. SuperAdmin-only is reserved for configuration changes; MeterReader, Engineer, Guest and unknown roles are not granted posting rights. No new policy or role grant was introduced.

The Application's existing `AddSessionApiClient` registration signs a short-lived bearer token from the trusted server session, including NameIdentifier and role. Data's existing JWT configuration validates signature, HS256 algorithm, issuer, audience and expiry, then applies BillingOperations before the controller executes. The authenticated MVC session workflow therefore remains usable.

## Exact modified endpoints

All paths below are under `/api/ReadingSheet` and now require BillingOperations and a positive integer NameIdentifier before entering a mutation service.

| Endpoint | Identity handling |
|---|---|
| POST `PartialPostReadingSheet?readingSheetId=...` | Removed the `userId` action parameter. Extra/forged `userId` query values are ignored; posting uses the validated claim. |
| POST `CompleteReadingSheet?readingSheetId=...` | Removed the `userId` action parameter. Completion receives the validated claim. |
| POST `SaveUpdate/{userId}` | Retained route compatibility; replaces the route actor with the claim before invoking the existing service. New-sheet CreatedBy is set by that service from this actor. |
| PATCH `UpdateReadingSheetsStatus/{userId}/{status}?ids=...` | Retained route compatibility; replaces the route actor with the claim before invoking the existing service. |
| POST `UpdateZoneProgress` | Requires a valid claim before entering the existing progress service. No actor input/audit parameter exists on that service. |

Anonymous requests return **401**. Authenticated disallowed roles return **403**. Allowed-role tokens with missing, malformed, nonpositive or overflowing identity return **401** before mutation. Existing invalid-operation/configuration conflicts remain **409** on partial posting.

The posting service remains an internal API with an actor argument; external callers cannot supply the authoritative actor through these protected actions. Existing sheet status/completion audit storage behavior was not expanded: those services do not currently use every supplied actor argument. This task does not invent audit columns or alter lifecycle rules.

## Exact modified files

| File | Change made in this task |
|---|---|
| `TMCWD/TMCWD.Data/Controllers/ReadingSheetController.cs` | Protects the five sheet write actions; centralizes claim parsing; removes posting/completion actor inputs. |
| `TMCWD/TMCWD.Billing/ReadingSheetTransaction.cs` | Posting/completion callers no longer accept or send an actor ID. Their API 401/403 responses propagate as status-bearing exceptions. |
| `TMCWD/TMCWD.Application/Controllers/ReadingSheetController.cs` | Updates both callers and returns Data 401/403 to the MVC client; preserves successful and existing conflict behavior. |
| `tests/BillingRegression/PostingSecurity.cs` | Adds actual HTTP/JWT/controller/service tests against isolated temporary financial fixtures and tests the actual MVC session client/controller path. |
| `tests/BillingRegression/Program.cs` | Runs security checks and adds `--no-migrations` to exclude migration-executing paths for this authorized run. |
| `tests/BillingRegression/BillingRegression.csproj` | References the existing Application project to test its actual client registration/controller. No package dependency added. |
| `tests/BillingRegression/README.md` | Documents the security coverage and no-migration invocation/exclusions. |
| `tests/BillingRegression/penalty-charging-regression.cjs` | Repairs a stale fixture URL assertion: the existing script supplies a posting-date query. Tests now assert the endpoint, absence of a billing-period filter, and date format. Penalty application code is unchanged. |
| `docs/Phase6I-B-Posting-Security-Remediation.md` | This report. |

No changes were made by this task to Data `Program.cs`, `ReadingSheetService`, billing calculation services, SQL migration/seed artifacts, authentication secrets, or mobile code. Pre-existing working-tree changes were preserved; repository diffs against HEAD include earlier Phase 6 work and are not this task's change inventory.

## Financial behavior preserved

The controller validates authorization/identity before calling posting. The existing posting transaction, sheet lock, invoice allocation, maintenance selection by ReadingSheet.BillingDate, snapshot freezing, reading completion, rollback, retry behavior and installment activation remain unchanged.

- Maintenance remains PHP 20 under the approved synthetic effective-dated configuration and appears once in bill principal/snapshot.
- Penalties retain the water-only snapshot base and strict after-due posting rules.
- Phase 6I-A retains adjusted prior RemainingAmount plus active persisted penalties for qualifying arrears, including unknown-snapshot handling.
- Phase 6G retains collectible installment children, separate Other Charge liabilities, exact payment/cancellation links and ledger reconciliation.
- The legacy adjustment floor, posted statuses, financial rounding, invoice identity and issued snapshots were not changed.

## Actual tests and outcomes

All database-writing verification used **local `tmcwd_test` only**, enforced by the runner's host/database guards. Existing fixture setup created connection-local TEMPORARY tables from the canonical EF model and synthetic records; no persistent customer, billing, payment, rate or user records were changed. No actual privileged user was created. Temporary tables disappear when their connection closes. HTTP tests hosted a separate loopback server with a random test signing key; no token or secret was printed.

| Command / check | Actual final outcome |
|---|---|
| `dotnet build TMCWD/TMCWD.slnx --no-restore -v quiet` | **PASS**, exit 0, zero errors. Existing NU1903 warnings remain. |
| `dotnet build tests/BillingRegression/BillingRegression.csproj --no-restore -v quiet` | **PASS** after the final test fixture change, exit 0, zero errors. Existing NU1903 and EF1002 warnings remain; earlier full dependency compilation also emitted existing nullable warnings. |
| `dotnet run --project tests/BillingRegression --no-build -- . --no-migrations` | **PASS**, final run exit 0: **129 database regression checks**, including **22 new posting-security groups**, all using temporary tables. Also passed the existing pure money/calculator and MySQL query-translation checks. |
| `node tests/BillingRegression/browser-regression.cjs` | **PASS**, 25/25 tests, no skips. |
| `node tests/BillingRegression/penalty-charging-regression.cjs` | **PASS**, final run 5/5, no skips, after correcting the stale URL fixture. Initial run and independent repeat were 4/5; those failures were not counted as passes. |
| `node tests/BillingRegression/collections-browser-regression.cjs` | **PASS**, exit 0: eight Collections scenarios, Other Charges and cancellation workflows, Dashboard Charging, and 31 Account Ledger browser checks. Synthetic HTTP only, no application database. |

The initial sandboxed build failed on existing build-output access permissions; approved execution outside the sandbox succeeded. The initial sandboxed headless browser run stalled and was stopped, not counted as PASS; its authorized rerun completed successfully. One intermediate expanded security-test run failed because its synthetic account was not active for completion; the fixture was corrected, rebuilt, and the final complete run passed.

Security assertions exercised:

- Anonymous posting = 401; Engineer/MeterReader/Guest/unknown-role posting = 403.
- Missing, empty, nonnumeric, zero, negative and overflowing identities = 401.
- Expired tokens and tokens signed with the wrong key = 401.
- All four permitted billing roles successfully invoke the real posting service through HTTP.
- `userId=999` cannot impersonate another actor: bill CreatedBy/UpdatedBy and reading UpdatedBy remain the signed actor **1**.
- Rejected requests preserve bills, readings, sheet state, invoice sequence, charges, installment children, penalties and payments.
- Posting retry preserves invoice identity and snapshot; an injected exception after SQL save rolls back bill, reading completion and invoice sequence.
- Every secured lifecycle endpoint rejects anonymous, disallowed-role and missing-identity requests without state changes.
- The actual MVC session-to-API registration signs the correct identity; MVC posting/completion forward 401/403, and authorized MVC posting **and final completion** succeed with the real services.

The HTTP harness uses the real production controllers/services and the same JWT validation settings and BillingOperations role set in a synthetic test host. It does not boot a deployed application, exercise actual login credentials or prove production environment configuration. Existing login/session bridge source was inspected; live deployment authentication remains a separate check.

### Explicitly unperformed checks

`--no-migrations` did not execute migration SQL, seed SQL, legacy synthetic schema-upgrade paths, or the invoice/payment concurrency suites that execute migrations. The runner reports these as **NOT RUN**, not PASS. The independent ServiceRequestRegression runner also executes migration SQL and was not run; existing financial-service integration cases within BillingRegression passed. No backup creation/restore or operational posting was performed.

## Other authorization gaps: separate review findings

These are source-confirmed gaps, not exploit attempts against operational data. They remain unchanged because their role/ownership/workflow remediation exceeds this sheet-posting task. Data has no fallback authorization policy to protect them implicitly.

| Boundary | Finding / next review |
|---|---|
| Reading `POST SaveUpdate/{userId}` and `POST SaveRange` | No controller/action authorization. Single-save actor comes from the route; bulk save directly adds supplied entities. These can alter/create the authoritative reading inputs and statuses used by billing. Review roles, assignment and server-owned fields before release exposure. |
| DebitCreditMemo `POST Save/{userId}` | No authorization; the service persists a financial memo with the supplied audit actor. Review legitimate memo creation/approval roles. |
| AdvancePayment `GET SaveUpdate/{userId}` | No authorization, and GET is mapped to a persistence mutation. Review payment authority, claim identity and an explicit write method. |
| PaymentCheck `POST SaveUpdate/{userId}` | No authorization; persists check/payment metadata with a client-supplied actor. Review Collection ownership and financial linkage. |
| EWalletTransaction `GET SaveUpdate/{userId}` | No authorization; GET maps to a persisted transaction write using client-supplied identity. Review provider/collection authority and explicit write method. |
| Dashboard `POST SaveServiceInspection/{userId}` | No authorization; its installation path can create account/billing stubs and a registration-fee Other Charge. Other Dashboard service/application mutation actions and some GET reconciliation/backfill paths are also unprotected. Review domain roles, actor identity, write-on-GET behavior and transaction ownership together. |
| JobOrder `POST SaveUpdate/{userId}` and Request `POST SaveUpdate/{userId}` | No authorization; client-supplied actors control workflow writes adjacent to service charging. Domain ownership/role review required. |
| OtherFeeType `POST SaveUpdate/{userId}`, `DELETE Delete/{id}`; ChargeType `POST SaveUpdate/{userId}` | No authorization. These alter charge metadata/defaults used by financial workflows. Configuration authority review required. |
| WebHook `POST SaveUpdate` | No authentication/provider signature boundary found on the persistence action. It currently stores webhook records; financial/provider interpretation needs a separate review. |

Inspected protected boundaries:

- Billing `SaveUpdate`: BillingOperations with claim-derived actor. Collection/AccountLedger reads use their existing policies.
- Bill Adjustments `SaveUpdate`/`SaveBatch`: controller BillingOperations and claim-derived actor.
- Payments `ProcessCollection`/`CancelInvoice`: controller Collection, claim-derived actor, existing cancellation ownership checks. Legacy Payment SaveUpdate is disabled with 409.
- Penalties `PostCharges`/`SaveUpdate` and waiver actions: controller BillingOperations and claim-derived actor, with existing service validation/waiver role checks.
- Other Charges `SaveUpdate`/`Deactivate`/`PostToCollection`: BillingOperations and claim-derived actor; `ProcessPayment`: Collection and claim-derived actor.
- Collection Remittance and Batch Payment Upload writes: Collection and claim-derived actor.
- BIR configuration writes: SuperAdmin and claim-derived actor. Maintenance configuration writes: SuperAdmin and persisted active/verified role recheck. These were not changed.
- Mobile reading upload: MeterReader policy, server-derived reader identity, assignment/roster checks; it does not perform web financial posting.
- WaterRate controller currently exposes lookup actions only; no external write action was found there.

Unauthenticated financial read endpoints also remain in legacy controllers; this report's mutation inspection does not certify their customer-data exposure or the whole repository's security.

## Remaining blockers and verdict

**PASS: the confirmed sheet-posting authorization/audit-impersonation defect is remediated and regression-tested in source.** The full financial behavior suite permitted without migration execution passes. Sheet lifecycle writes share that protected boundary.

**INCOMPLETE / NO-GO: Phase 6I-B deployment is not complete or authorized.** Remaining gates include:

1. Independently restorable backup evidence remains unverified, as recorded by the pre-deployment review.
2. That review found no active verified SuperAdmin seed actor; none was created or changed here.
3. The maintenance migration/seed remain undeployed and unexecuted by this task.
4. The two previously observed active sheets dated 2025-07-01 remain outside the approved seed interval and need Finance disposition.
5. The separate authorization gaps above require review before exposing the relevant endpoints.
6. Live environment/session authentication verification, migration/concurrency checks, and post-deployment tests remain outstanding. Existing dependency advisories and Phase 6I-B configuration/adjustment risks in the checklist remain unresolved.

Backup, seed-actor and historical-sheet observations above are carried forward from the prior read-only checklist, not newly verified against `tmcwd` in this remediation. No deployment, migration, seed, backup operation, privileged-user creation, persistent database change or financial calculation change was performed.

