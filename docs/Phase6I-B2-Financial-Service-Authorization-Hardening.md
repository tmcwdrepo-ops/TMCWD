# Phase 6I-B2 Financial / Service Authorization Hardening

Review completed: 2026-10-09 (Asia/Manila), following source work and tests begun on 2026-10-08. **INCOMPLETE / NO-GO.** This is source remediation and a read-only readiness review, not deployment approval. Earlier findings remain historical evidence.

## Scope and policy evidence

- Data `Program.cs` already defines **BillingOperations**: SuperAdmin (1), CSR (2), OIC (6), Cashier (7). The web Present Reading / Reading Sheet workflow uses `ReadingTransaction` through the authenticated session API bridge. Web reading entry uses this existing staff boundary. MeterReader (4) continues to use the separate mobile policy, database user eligibility, assignment and active sequence roster checks; it receives no access to web reading writes.
- **Collection** already permits Cashier (7) / SuperAdmin (1). MVC Collections checks those roles. Legacy `CashPayment`, `CheckPayment`, `EWalletPayment` call `AdvancePaymentTransaction`; `CheckPayment` calls `PaymentCheckTransaction`. Their clients already use POST. The active atomic collection flow remains authoritative; this task does not revive the disabled legacy Payment write.
- **SuperAdmin** already defines the administrative configuration boundary. `AdminController` owns Other Fee Type management; the SuperAdmin login destination is Admin. Charge Type is configuration metadata, not cashier transaction input. Writes use this existing administrative boundary; staff lookup actions are unchanged. There is no new administrative role grant.
- No reliable **memo creation/approval policy** exists: Billing MVC checks login only, while `DebitCreditMemoService` creates a Pending memo. Being on a Billing page does not establish approval authority.
- Dashboard DTO lists identify potential inspectors (Engineer / SuperAdmin), approvers (Engineer / OIC / SuperAdmin), recommenders (Engineer / CSR / OIC / SuperAdmin) and field personnel (Engineer / SuperAdmin). The write actions do not enforce those assignments, and Admin MVC actions generally check only a session user. These lists do not prove which role may submit, approve, activate an account or advance a stage. Request and JobOrder callers likewise do not establish a complete server permission matrix.
- `EWalletLogTransaction` posts internally, but the legacy `EWalletPayment` flow does not establish a provider callback authentication contract. ECPay service/configuration names are insufficient evidence of a validated provider identity. `WebHookService` stores records; no downstream consumer that converts those records into financial postings was found. No webhook signature, replay or provider verification boundary was found on the write action.

## Changes and exact boundaries

**FIXED IN THIS TASK**:

1. `ReadingController.POST SaveUpdate/{userId}`: BillingOperations; positive integer actor from validated NameIdentifier, ignoring the route actor. For an existing reading, reject account/sheet movement. Reuse `ReadingService.Sync` with staff assignment bypass selected only by this authorized controller. The client cannot set previous reading, completion/status, audit identity or arbitrary stored fields. Active roster, sequence, authoritative reading date, stored baseline, duplicate handling, posted-data protection and sheet lock/transaction remain in the existing service. Mobile retains `requireAssignment = true`; a query parameter cannot override it.
2. `AdvancePaymentController.POST SaveUpdate/{userId}` and `PaymentCheckController.POST SaveUpdate/{userId}`: Collection and positive claim identity. Advance Payment GET write mapping removed; existing POST client already matches. Services overwrite new-row creator/updater/date fields and preserve established existing-row update behavior and linkage.
3. `OtherFeeTypeController.POST SaveUpdate/{userId}`, `DELETE Delete/{id}` and `ChargeTypeController.POST SaveUpdate/{userId}`: SuperAdmin; require positive claim identity even for delete. Services load existing configuration and copy editable fields, preserving original creator/date rather than attaching forged whole-entity audit fields. Charge Type new-row updater is also server-derived. Reads remain available as before. Delete behavior is unchanged; no new audit table was invented.
4. `AdvancePaymentService` and `ChargeTypeService`: remove accidental MVC controller/routing/action attributes. These persistence classes had externally discoverable `/api/AdvancePaymentService/...` and `/api/ChargeTypeService/...` aliases bypassing their real controllers. No repository client uses those aliases. Their former write routes now return 404.

**FORMALLY BLOCKED FROM EXPOSURE** pending a policy/provider decision:

- `Reading.POST SaveRange`: legacy MVC `CreateForReadingByZoneBookId` sends raw entities without a ReadingSheetId. The service directly AddRange'd them, with no roster/status/actor checks. Do not restore this obsolete bulk boundary until an approved sheet-bound bulk contract and atomic validation are supplied. Normal Present Reading and mobile upsert are retained.
- DebitCreditMemo Save; Request SaveUpdate; JobOrder SaveUpdate; all twelve Dashboard POST mutations; four Dashboard GET actions that invoke persistence reconciliation/backfill.
- EWalletTransaction SaveUpdate (now explicit POST; GET removed), and WebHook SaveUpdate.

Data `Program.cs` adds **PendingAuthorizationDecision**, requiring authentication and an assertion that is always false. It applies only to the listed actions, not globally. Anonymous requests receive 401; every authenticated role receives 403, including SuperAdmin and identities missing an actor. Controllers/services are not invoked. This is a release quarantine, not a speculative permission grant. These workflows are intentionally unavailable until approved policy/trust rules are implemented. Their retained service bodies still contain client actor inputs and other risks; the denial policy must not be replaced with a permissive assertion.

Changed production files (all beneath `TMCWD/TMCWD.Data`): `Program.cs`; controllers `Reading`, `AdvancePayment`, `PaymentCheck`, `OtherFeeType`, `ChargeType`, `DebitCreditMemo`, `EWalletTransaction`, `WebHook`, `Dashboard`, `Request`, `JobOrder`; services `IReadingService`, `ReadingService`, `AdvancePaymentService`, `PaymentCheckService`, `OtherFeeTypeService`, `ChargeTypeService`. Test changes: `tests/BillingRegression/PostingSecurity.cs`, `Program.cs`. Documentation: this report and the dated checklist recheck. No Application/mobile production source, financial calculator, posting service, maintenance artifact or migration was changed in this task. Pre-existing working-tree changes were preserved.

## Vulnerabilities and remaining decisions

| Severity | Finding / disposition |
|---|---|
| High, fixed | Anonymous raw Reading writes and forged actors/status/baseline; web entry now uses the existing bounded sync path. Bulk insertion is denied. |
| High, fixed | Unauthenticated payment/configuration writes and GET mutations; policy, claim actor and explicit POST now protect proven routes. |
| High, fixed | Persistence services discovered as alternative API controllers; aliases removed and HTTP-tested. |
| High, quarantined | Dashboard inspection approval can create customer/account, generate account identity, create a zero-water billing stub and a separate registration Other Charge; other writes change application stages, transfer/disconnect accounts and service statuses. All named mutations are denied pending role/assignment/approval decisions. Maintenance was not moved into these charges. |
| High, quarantined | Dashboard GET ServiceSummary, ServiceApplications, WorkOrder call account-number backfill; ServiceSummary and ServiceRequestsByDescription call completed-request reconciliation. These are not safe reads. They are denied until reconciliation is an explicit authorized write and reads are side-effect-free. |
| High, unresolved outside named fixes | Users SaveUpdate accepts roles, active/verified fields; ChangePassword accepts target/actor/password in the URL without authorization. These remain exposed. Administrative account management and self-service password/reset contracts require separate hardening. Do not expose this API as deployment-ready. No accounts were created/verified in the operational database. |
| High, unresolved outside named fixes | Account/Customer and legacy request-detail, finding, inspection, material, recommendation, approval-history, workflow/configuration writes remain unprotected. Some attach entire entities and trust route/body audit fields. They can bypass quarantined service workflows through shared tables. See complete inventory. |
| High, unresolved data boundary | Billing SaveUpdate has BillingOperations and claim identity but attaches a body entity after protecting snapshot/invoice number. It can still accept mutable balance/status/account fields and original audit fields. Authorization alone does not certify that generic financial write. Existing legacy payment classes use parallel writes across endpoints rather than one transaction; no atomicity improvement or revival was made. |
| Remaining review | Existing protected endpoints were inspected, not exhaustively security-certified. Bill Adjustment / Other Charge / Penalty retain their existing business validation and transaction rules. Bulk request/file mutations lack actor/ownership checks. Charge deletion referential/usage rules remain a separate configuration integrity question. |
| Existing dependency risk | Builds report NU1903 high-severity advisories for System.Security.Cryptography.Xml 8.0.3; existing nullable and EF1002 warnings remain. This task did not update packages or suppress warnings. |

Finance/Admin must supply: memo creator/reviewer role matrix; CSR/OIC/Engineer/SuperAdmin permissions for each service action and approval versus entry; assignment and ownership rules; permitted legacy Request/JobOrder edits; atomicity/idempotency requirements for account activation/registration charges; explicit authority and execution point for backfill/reconciliation. The provider owner must supply ECPay/webhook sender, signed fields/algorithm/key management or other authentication, replay/idempotency rules, trusted internal actor mapping, and whether records may trigger settlements. No signature format was invented.

## Complete mutation / write-verb inventory

This table lists every POST/PUT/PATCH/DELETE action in Data controllers, including two nonmutating POST actions, plus the four confirmed GET writes. Controller-level and action-level authorization were inspected. Two former persistence-service write aliases are listed separately. Lookup-only controllers Endpoint, Tariff, WaterRate and ZoneBook contain no write actions. No global fallback authorization protects the exposed legacy rows below.

**BLOCKED — POLICY REQUIRED (exposed)** means an unresolved finding, not a runtime denial. **BLOCKED (denied)** means this task actually applies PendingAuthorizationDecision. **PROTECTED** describes the existing role boundary, not certification of all body fields or transaction semantics.

| Method / route | Classification | Boundary / remaining risk |
|---|---|---|
| POST `/api/Account/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | HIGH: unauthenticated account/customer whole-entity persistence remains exposed. |
| POST `/api/AdvancePayment/SaveUpdate/{userId}` | FIXED IN THIS TASK | Collection; positive claim actor; server audit; POST only. |
| POST `/api/{jobOrderId}/ApprovalHistory/Save/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/auth/meter-reader/login` | OUT OF SCOPE / JUSTIFIED | Login validates credentials/reader eligibility; issues JWT, no persisted mutation. |
| POST `/api/BatchPaymentUpload/Save/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/BillingAdjustment/SaveUpdate/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/BillingAdjustment/SaveBatch/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Billing/SaveUpdate/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/BirInvoiceConfig/active` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ChargeType/SaveUpdate/{userId}` | FIXED IN THIS TASK | SuperAdmin; positive claim identity; stored creator preserved; reads unchanged. |
| POST `/api/CollectionRemittance/Save` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Customer/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | HIGH: unauthenticated account/customer whole-entity persistence remains exposed. |
| POST `/api/Dashboard/NewConnection/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/DisconnectionOrder/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/AccountTransfer/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveServiceRequest/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/AccomplishServiceRequest/{requestId:long}/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/ServiceRequest/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveServiceInspection/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveConnectOrder/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveHouseTapping/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveWorkOrder/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/ActivateAccount/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/AdvanceStage/{userId:long}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/DebitCreditMemo/Save/{userId}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/EWalletTransaction/SaveUpdate/{userId}` | BLOCKED — PROVIDER TRUST REQUIRED (denied) | PendingAuthorizationDecision; no provider contract invented. |
| POST `/api/{jobOrderId}/Finding/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/InspectionReport/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/InspectionType/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/Inventory/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/{requestId}/JobOrder/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/{jobOrderId}/Material/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| PUT `/api/{jobOrderId}/Material/UpdateQuantityOrNewUnitCost/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| DELETE `/api/{jobOrderId}/Material/Delete/{id}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/MeterMaintenanceRate` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/reading-sheet/mobile/{id:int}/readings` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/OtherCharge/SaveUpdate/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| PUT `/api/OtherCharge/Deactivate/{id}/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/OtherCharge/PostToCollection/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/OtherCharge/ProcessPayment/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/OtherFeeType/SaveUpdate/{userId}` | FIXED IN THIS TASK | SuperAdmin; positive claim identity; stored creator preserved; reads unchanged. |
| DELETE `/api/OtherFeeType/Delete/{id}` | FIXED IN THIS TASK | SuperAdmin; positive claim identity; stored creator preserved; reads unchanged. |
| POST `/api/PaymentCheck/SaveUpdate/{userId}` | FIXED IN THIS TASK | Collection; positive claim actor; server audit; POST only. |
| POST `/api/Payment/SaveUpdate/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Payment/ProcessCollection/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Payment/CancelInvoice/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Penalty/SubmitWaiver/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Penalty/ReviewWaiver/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Penalty/PostCharges/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Penalty/SaveUpdate/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Reading/SaveUpdate/{userId}` | FIXED IN THIS TASK | BillingOperations/claim; existing sync roster/baseline/date validation; no movement. |
| POST `/api/Reading/SaveRange` | BLOCKED — POLICY REQUIRED (denied) | Raw legacy bulk contract denied. |
| POST `/api/Reading/GetRangeByReadingSheetIds` | OUT OF SCOPE / JUSTIFIED | POST query only; no persisted mutation. |
| POST `/api/ReadingSheet/SaveUpdate/{userId}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ReadingSheet/UpdateZoneProgress` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| PATCH `/api/ReadingSheet/UpdateReadingSheetsStatus/{userId}/{status}` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ReadingSheet/PartialPostReadingSheet` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ReadingSheet/CompleteReadingSheet` | PROTECTED | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ReadingSheetTemplate/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| PUT `/api/ReadingSheetTemplate/Deactivate/{id}/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/{requestId}/Recommendation/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/Request/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (denied) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/{requestId}/RequestDetail/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/{requestId}/RequestDetail/SaveMultiple/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| DELETE `/api/{requestId}/RequestDetail/Delete/{id}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| DELETE `/api/{requestId}/RequestDetail/DeleteDetails` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/RequestFile/SaveUpdate/{userId}/{jobOrderId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/RequestFile/SaveRange` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/Users/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | CRITICAL: role/eligibility or target password mutation remains anonymously exposed. |
| PUT `/api/Users/ChangePassword/{id}/{userId}/{newPassword}` | BLOCKED — POLICY REQUIRED (exposed) | CRITICAL: role/eligibility or target password mutation remains anonymously exposed. |
| POST `/api/WebHook/SaveUpdate` | BLOCKED — PROVIDER TRUST REQUIRED (denied) | PendingAuthorizationDecision; no provider contract invented. |
| POST `/api/Workflow/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| DELETE `/api/Workflow/Delete/{id}` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| POST `/api/ÌnpectionTypeDetail/SaveUpdate` | BLOCKED — POLICY REQUIRED (exposed) | No server authorization; route/body audit, ownership and transaction review remains; outside named fixes. |
| GET `/api/Dashboard/ServiceSummary` | BLOCKED — POLICY REQUIRED (denied) | Retained backfill/reconciliation never invoked. |
| GET `/api/Dashboard/ServiceApplications/{category}` | BLOCKED — POLICY REQUIRED (denied) | Retained backfill/reconciliation never invoked. |
| GET `/api/Dashboard/ServiceRequestsByDescription` | BLOCKED — POLICY REQUIRED (denied) | Retained backfill/reconciliation never invoked. |
| GET `/api/Dashboard/WorkOrder/{workOrderType}/{requestId:long}` | BLOCKED — POLICY REQUIRED (denied) | Retained backfill/reconciliation never invoked. |
| Former GET `/api/AdvancePaymentService/SaveUpdate/{userId}` | FIXED IN THIS TASK | Controller discovery metadata removed; HTTP 404. |
| Former POST `/api/ChargeTypeService/SaveUpdate/{userId}` | FIXED IN THIS TASK | Controller discovery metadata removed; HTTP 404. |

Inventory: 74 write-verb actions (72 mutations + 2 nonmutating POSTs), 4 GET mutations, 2 removed mutation aliases: 80 rows.

## Actual validation

Results are recorded after the final checks below. Database-writing tests are restricted to local `tmcwd_test` with canonical connection-local temporary tables, including new advance-payment, payment-check and charge-type fixtures. No deployment seed or migration is executed.

| Check | Actual result |
|---|---|
| `dotnet build TMCWD/TMCWD.slnx --no-restore -v quiet` | PASS, zero errors; existing dependency/nullability warnings. Sandbox output-file denial was retried with approved build access. |
| `dotnet build tests/BillingRegression/BillingRegression.csproj --no-restore -v quiet` | PASS, zero errors; existing NU1903 / EF1002 warnings. |
| `dotnet run --project tests/BillingRegression --no-build -- . --no-migrations` | PASS: **190 grouped database regression checks**, temporary tables only in `tmcwd_test`; **58 new B2 grouped security checks** beyond the previous 132. Final output retained in `.tmp/phase6ib2-regression-final.log`. |
| Pure checks printed before database suite | PASS: **5** penalty/rounding cases, explicit water/maintenance calculation and both MySQL eligibility query shapes. These are separate from the 190 count. |
| `node tests/BillingRegression/browser-regression.cjs` | PASS: **25 tests**, zero failed/skipped. |
| `node tests/BillingRegression/penalty-charging-regression.cjs` | PASS: **5 tests**, zero failed/skipped. Browser total for these two commands: **30**. |
| `node tests/BillingRegression/collections-browser-regression.cjs` | PASS: **8 Collections scenarios**, **31 Account Ledger checks**, plus the script's Other Charges, cancellation and Dashboard Charging scenarios. The stalled sandboxed Edge run was stopped; the approved headless Edge retry completed. |
| Standalone ServiceRequestRegression / migration-dependent concurrency suites | **NOT RUN**, as qualified below. |
| Scoped source whitespace review | PASS with CR-at-EOL handling for Windows files. Whole-tree default `git diff --check` reports pre-existing/unrelated whitespace issues; those files were preserved. |

Initial new-test runs failed on invalid synthetic check fields, an already seeded fee ID, a detached fixture reference, and an assumed reading ID. The fixtures were corrected and the final entire permitted regression run passed. Failed intermediate runs are not counted as successful runs.

New HTTP/JWT checks exercise the six proven write endpoints (anonymous, wrong role, six malformed/missing actor identities, authorized forged route/body identity); all 22 quarantined routes for anonymous/all enumerated roles/invalid identities; removed GET methods and service aliases; roster/account movement; creator preservation/updater derivation; unchanged payment linkage; actual mobile assignment/roster boundary with a forged `requireAssignment=false` query; and the actual signed MVC API client for reading entry and posting. Rejected requests compare synthetic persisted state. Existing rollback-after-SQL-save, retry, invoice allocation, mobile, financial and browser regressions remain included. A denied route has no authorized-success result; it intentionally denies every role.

Standalone ServiceRequestRegression is **NOT RUN**: it defaults to the application connection, has no dedicated-database restriction or no-migrations switch and executes service migration/backfill SQL. Invoice/payment concurrency migration suites are also **NOT RUN** under `--no-migrations`. Browser tests use synthetic HTTP and no application database. Live authenticated installation/service/provider/MVC sessions are **NOT VERIFIED**. Successful synthetic tests do not certify the quarantined service workflow.

## Financial contracts

No approved financial calculation changed: PHP 20 maintenance from 2025-01-01 by ReadingSheet.BillingDate, once monthly; issued snapshots; RemainingAmount arrears; water-only 10% penalty and due-date rule; installment-only Other Charge collections; allocation and cancellation restoration; full water-bill payment; BillAdjustment legacy PHP 20 floor; invoice numbering. Web reading input now uses the existing authoritative server validation/calculation path. No maintenance was moved into persisted service Other Charges. No financial posting was performed outside synthetic temporary fixtures.

## Fresh read-only deployment review

Read-only queries on 2026-10-08, repeated on **2026-10-09**, confirmed the configured development target **127.0.0.1:3307 / tmcwd**, server **DESKTOP-Q72GRF4**, **MySQL 8.0.46**. Configuration came from Data local-development user-secrets; no secrets were printed. No `ConnectionStrings__DefaultConnection` environment override was present in this inspection process. This verifies that local target, not an unspecified production environment or a separately overridden running process.

- `meter_maintenance_rates` table count: **0**. Schema/seed remain undeployed. Neither was executed.
- Eligible active AND verified role-1 seed actors: **0**. Existing SuperAdmin Id 5 remains active and unverified. No account was changed.
- Active sheets **2 and 5**, Status 1, BillingDate **2025-07-01**: **covered** by the approved 2025-01-01/open-ended configuration. Former date-coverage blocker **RESOLVED**; actual configured-rate availability remains blocked until separately authorized deployment.
- Backup evidence: repository/docs/script search and immediate project-parent file metadata found no evidence satisfying source identity, integrity, secure location, recovery instructions, isolated successful restore and financial reconciliation. A backup elsewhere may exist but is **NOT VERIFIED / STILL BLOCKED**. No dump, backup, restore or filesystem deletion was performed.

### Migration and seed source inspection

`20261008_add_meter_maintenance_rates.sql` contains only additive CREATE TABLE IF NOT EXISTS: InnoDB, DECIMAL(12,2), DATE bounds, amount >= 0 CHECK, end >= start CHECK, unique EffectiveFrom and interval index. MySQL 8.0.46 supports these CHECK constraints. No historical financial DML. Rerun creates a missing table or leaves an existing table untouched; it does not verify/repair an existing incompatible schema. There is no database exclusion constraint for arbitrary interval overlap: the API/seed use the same named lock and explicit overlap validation; manual SQL that ignores that lock can still race or introduce bad periods.

`20261008_seed_meter_maintenance_rate_20250101.sql` requires an explicitly selected positive active verified SuperAdmin actor, acquires `tmcwd:meter_maintenance_rates`, runs exact-row/conflict checks in a transaction, inserts only 20.00 / 2025-01-01 / NULL if absent, or reports ALREADY_PRESENT for exactly that row. Any other interval reaching the initial date or later rejects; no UPDATE/DELETE or historical financial DML exists. The procedure handler rolls back, releases the lock and resignals. Actor legitimacy still needs operator confirmation; the SQL cannot establish organizational authorization from a role flag alone. Actor status is checked before the transaction/lock and is not row-locked against concurrent revocation. Helper procedure CREATE/DROP requires corresponding privileges and deployment without unrelated pending work because DDL commits implicitly. Existing schema shape and operator database selection must be verified before authorization. Runtime seed idempotency/error handling were **NOT EXECUTED / NOT VERIFIED** in this task.

The retired `20261008_seed_meter_maintenance_rate_20260101.sql` begins with SIGNAL SQLSTATE 45000 and retains its old body only inside a non-executable ordinary comment. It cannot insert a rate even if a client continues after the SIGNAL. Source inspected; neither artifact executed. Only the 20250101 seed may enter a later approved deployment sequence.

## Recommendation and required sequence

**PASS for tested, proven source boundaries and enforced quarantine; INCOMPLETE for remaining authorization and operational workflow readiness. Final recommendation: NO-GO.** Unprotected shared-table/user mutation routes, unresolved service/provider policies, lack of an eligible seed actor, missing restore evidence and live-flow verification prevent controlled deployment approval. A migration/seed source review alone does not satisfy these gates.

Before any deployment: resolve and harden the exposed inventory findings; approve/implement quarantined boundaries with claim actor, assignment, transaction and denial tests; address dependency risks; verify the target and independently restored/reconciled backup; identify an already legitimate active verified SuperAdmin without changing accounts under this task; run authorized live-flow and isolated SQL checks. Then request separate deployment authorization for migration followed by the 20250101 seed only, inspect the exact persisted row, and run the approved post-deployment boundary/authorization/rollback/retry/snapshot/collection/ledger/mobile tests using authorized fixtures. Do not post operational July sheets merely to verify coverage. Stop here; no deployment is authorized.
