# Phase 6I-B3 — Critical API Boundary Hardening

Review date: **2026-10-09**, Asia/Manila. **Source boundary readiness: PASS for the scoped fixes and enforced quarantines. Deployment: NO-GO.** This report supplements the historical [B2 review](./Phase6I-B2-Financial-Service-Authorization-Hardening.md); its findings were valid when recorded.

## Root risks and scope

The remaining anonymously writable Users API accepted security fields and arbitrary password targets, returned password hashes, and placed a password in a URL. Account/Customer writes accepted whole persistence entities. Billing's authenticated generic write still allowed client control over balances, payment status, account/reading linkage, period and audit fields. Unprotected child tables could bypass B2's Request/JobOrder/Dashboard quarantines. Inventory pricing could feed those material workflows.

The changes secure established administrative/customer/billing operations and deny unresolved service operations. No production/development financial or customer row was written, no administrator account was created/verified operationally, and no migration, seed, backup, restore or deployment was executed. All database-writing verification used connection-local synthetic temporary tables in `tmcwd_test`. Existing working-tree changes were preserved.

## Users and password security

Repository evidence is `HomeController`'s SuperAdmin destination, `AdminController`'s user form and the canonical shared `UserRole` enum. `POST /api/Users/SaveUpdate/{userId}` uses the existing **SuperAdmin** policy and additionally checks that the claim-derived actor is an existing active verified role-1 user. The route actor is ignored. Its strict `UserWriteRequest` contains only target ID, name, email, optional explicit role/active flags, and an initial plaintext password for creation. The Data API performs the existing `StringEncyption.Encrypt` hash operation. No password hash is returned.

Administrative staff-role and active-flag edits are explicit supported form operations. Self role/active changes and edits to another SuperAdmin are denied. Creation never verifies an account; verification fields, audit IDs/timestamps and remember-token fields are not accepted. An existing account's password cannot be updated through this generic form. Duplicate email is rejected. Name/email and permitted fields map into the stored row, preserving creation and verification metadata.

The shared UserService now saves only tracked changed fields for existing users, rather than marking the whole user row modified. A stale profile edit therefore cannot restore a concurrently changed password. Detached whole-user updates are rejected. A two-context synthetic regression verifies this behavior in addition to the HTTP/JWT tests.

The existing `UserTransaction.ChangePassword` establishes a current-password/confirmation self-service contract. It now uses **POST `/api/Users/password`**, with current/new/confirmation passwords in the body. Only the positive validated claim identity selects the account; current password and active/verified eligibility are checked before writing. Unknown target/audit body fields and any query arguments are rejected. The former PUT URL is removed (**404**). Administrative resets and account verification remain **BLOCKED — ACCOUNT SECURITY POLICY REQUIRED**; there is no invented reset or verification privilege.

Users lookup endpoints now require authentication and return `UserProfileDto`, excluding password/hash/remember token. Web login previously fetched a hash and compared it in MVC; it now sends a body to **POST `/api/auth/login`** for server-side credential validation and receives a password-free profile. Numeric mobile identifiers resolve directly at that boundary; the Application login endpoint no longer anonymously fetches a user profile to resolve an ID. Login rejects inactive/unverified/unknown-role accounts. Existing MD5 compatibility, token signing configuration and mobile token lifetime remain unchanged. This means the currently unverified operational administrator cannot authenticate through the new general-login command; no account was changed to work around that gate.

The MVC user form no longer contains a hash/password hidden field. New-password hashing occurs once in Data. User and inspection-type form writes and deactivation require POST and anti-forgery. Corresponding password-free login and signed MVC-to-Data flows were tested using synthetic identities.

## Account and Customer authority

Callers were traced through `CustomerSupportController`'s Customer Information/contact forms, customer creation/deactivation and account forms, `AccountController`, `CustomerController`, and shared `CustomerTransaction`/`AccountTransaction`. Their domain and canonical CustomerRepresentative/CustomerServiceOic roles establish **CustomerService**: SuperAdmin (1), CSR (2), OIC (6). Cashier, Engineer, Meter Reader and Guest are not granted customer authority.

Both POST SaveUpdate routes require this policy and a positive claim actor. Strict request DTOs reject creation/audit timestamps, account number, status, zone/book, sequence, balances and unrelated financial fields. Customer changes map only names, contact fields and the explicit active flag into the existing row. Account updates map address/current-address and validated manual meter/classification metadata; ownership, account number, status, zone/book, sequence and original creation metadata remain stored values. A changed CustomerId is rejected. Meter/classification changes are allowed only for a pending manual account with no reading, billing or service-application linkage; otherwise they require the approved service workflow and reject without saving.

New accounts validate the customer, receive the existing `ACCTyyyy-MM-guid` number format on the server, Pending status, unassigned zone/sequence and claim-derived audit fields. Initial reading is not an editable persistence field; the authoritative installation baseline remains linked tapping data. MVC account creation now passes the supplied address instead of omitting the house/address value required by its existing shared validation. Account closure/status and roster changes remain unavailable pending the approved workflow; the two former MVC deactivation GETs return **409** without writing.

Shared clients send the narrow DTOs, so the normal forms cannot bypass the API using stored whole-entity fields. MVC account/customer writes enforce anti-forgery, the Customer Information AJAX calls forward the token, and customer deactivation uses POST. Protected-field attempts and actual signed MVC creation/contact updates were tested. Existing issued invoice snapshots are not rewritten when contact/address information changes.

## Billing body-integrity decision

The whole `Billing` body could control Id, AccountId, JobOrderId, BillingReferenceId, PaymentTransactionId, ReadingId, BillingPeriod, MaterialsAmount, Penalties, OtherCharges, BillingAdjustment, TotalBillAmount, PaymentStatus, CreatedBy/DateCreated, UpdatedBy/DateUpdated, RemainingAmount and BillArrearsAmount. The existing service rejected changed snapshot and invoice-number values, but authorization alone did not protect the remaining fields. Meter Maintenance is represented in the issued snapshot, not a separately editable Billing property.

All callers were searched. The active MVC `SaveOtherCharge` flow used the generic route solely to create a zero-value initial billing anchor when a single-payment charge had no current bill. Other generic references are legacy Cash/Check/EWallet payment classes with no active ProcessPayment callers; their generic payment write was already disabled. Consequently **POST `/api/Billing/SaveUpdate/{userId}` is denied by PendingAuthorizationDecision**, including all authenticated roles.

The supported use moves to **POST `/api/Billing/ChargeAnchor`**, with only AccountId accepted. Existing BillingOperations roles and claim audit identity apply. The persistence service verifies and locks the account in a transaction, returns an existing eligible unpaid/nonwaived bill unchanged, or creates a server-numbered zero-value Unpaid anchor with no reading, invoice number or issued snapshot. Repeated requests return the existing anchor. No client balance/status/period/linkage/audit/snapshot fields are accepted. MVC continues requiring an existing metered period for installments; Other Charge calculation, creation and settlement are unchanged. Tests confirm zero values, claim identity, retry, nonexistent-account rollback and unchanged issued invoice/snapshot/balance.

## Shared-table bypasses and quarantine

RequestDetail, RequestFile, ApprovalHistory, Finding, InspectionReport, Material, Recommendation, Workflow and InspectionTypeDetail can mutate the request/job-order graph or its approval/material/configuration data. Shared CustomerSupport transactions call these routes; Workflow has an administrative transaction, but no proven per-action ownership/approval policy makes its generic graph writes safe. Assignment dropdowns and domain location alone do not authorize entry, approval, deletion or pricing. Their **16 mutation routes are denied**. Inventory's whole-item pricing/stock write is also denied pending the inventory/material authority decision.

InspectionType's standalone administration form has clear SuperAdmin ownership: its POST uses that policy, claim identity and stored-row allowlisting of Name, WithDetail, IsRequiredAccount, IsNew and IsActive. Original creation fields are preserved; updater/date are server-owned. ReadingSheetTemplate's existing reading-sheet CRUD workflow uses BillingOperations, claim identity, existing stored-field mapping, and new validation for an active verified Meter Reader, existing zone/book and valid sequence bounds. Template creation/deactivation does not alter issued sheets or invoices. MVC template writes now enforce anti-forgery and forward the token.

All **22 B2 quarantines remain denied**, including the four Dashboard mutating GETs, raw Reading SaveRange, memo, Request, JobOrder, service mutations, EWalletTransaction and WebHook. B3 adds **18 denied routes** (16 graph/configuration writes, Inventory and generic Billing): **40 denied routes total**. Anonymous requests return 401; every canonical authenticated role returns 403 before controller/service execution. No provider signature or service role policy was guessed.

Intentional operational limitations: legacy request detail/file entry, approvals, recommendations, findings, inspection reports, material/stock/pricing edits, Workflow/InspectionTypeDetail changes, service activation/transfer/installation/reconciliation, memo and provider workflows cannot complete through these denied routes. UI pages depending on them remain unavailable; browser rendering tests do not certify these disabled workflows. Some old client route mismatches (ApprovalHistory Save and single RequestDetail Delete) also remain unavailable and were not repurposed.

## Exact endpoint changes

| Endpoint | Result |
|---|---|
| POST `/api/Users/SaveUpdate/{userId}` | SuperAdmin + persisted actor eligibility; strict user command, stored security metadata |
| POST `/api/Users/password` | Authenticated self, current password, body only; no arbitrary target |
| PUT `/api/Users/ChangePassword/{id}/{userId}/{newPassword}` | Removed, 404 |
| All seven Users lookup actions | Authenticated, password-free profile DTO |
| POST `/api/auth/login` | New server-side body credential validation; no persisted mutation |
| POST `/api/Account/SaveUpdate/{userId}`, POST `/api/Customer/SaveUpdate/{userId}` | CustomerService, strict commands, server audit and authority validation |
| POST `/api/Billing/SaveUpdate/{userId}` | Denied for every role |
| POST `/api/Billing/ChargeAnchor` | Narrow existing-flow command, BillingOperations, transactional server values |
| POST `/api/InspectionType/SaveUpdate/{userId}` | SuperAdmin, claim actor, allowlist |
| POST `/api/ReadingSheetTemplate/SaveUpdate/{userId}`, PUT `/api/ReadingSheetTemplate/Deactivate/{id}/{userId}` | BillingOperations, claim actor, assignment/roster validation |
| RequestDetail 4; RequestFile 2; ApprovalHistory 1; Finding 1; InspectionReport 1; Material 3; Recommendation 1; Workflow 2; InspectionTypeDetail 1; Inventory 1 | Denied; exact full routes below |

MVC endpoints changed: Admin SaveUser/DeactivateUser/SaveUpdateInspectionType/DeactivateInspectionType; CustomerSupport SaveCustomerInformation/SaveAccountInformation/SaveCustomer/DeactivateCustomer; Account SaveAccount/AddAccount/SaveUpdateAccount; Customer SaveUpdateCustomer; ReadingSheet SaveReadingSheetTemplate/DeactivateReadingSheetTemplate now validate anti-forgery. Customer/user/inspection deactivation is POST. Account and CustomerSupport DeactivateAccount GET actions return conflict without mutation. Application Auth login/meter-reader login and Billing SaveOtherCharge use the revised shared API flow.

## Exact files changed in B3

Paths below are relative to the repository; earlier phase changes in these files were preserved.

- `TMCWD/TMCWD.Model/Administrator/UserCommands.cs`; `TMCWD/TMCWD.Model/CustomerSupport/CustomerCommands.cs`; `TMCWD/TMCWD.Model/Billing/ChargeAnchorRequest.cs`.
- `TMCWD/TMCWD.Data/Program.cs`.
- `TMCWD/TMCWD.Data/Controllers/UsersController.cs`, `AuthController.cs`, `AccountController.cs`, `CustomerController.cs`, `BillingController.cs`, `InspectionTypeController.cs`, `ReadingSheetTemplateController.cs`, `RequestDetailController.cs`, `RequestFileController.cs`, `ApprovalHistoryController.cs`, `FindingController.cs`, `InspectionReportController.cs`, `MaterialController.cs`, `RecommendationController.cs`, `WorkflowController.cs`, `ÌnpectionTypeDetailController.cs`, `InventoryController.cs`.
- `TMCWD/TMCWD.Data/Services/AccountService.cs`, `BillingService.cs`, `IBillingService.cs`, `InspectionTypeService.cs`, `ReadingSheetTemplateService.cs`, `UserService.cs`.
- `TMCWD/TMCWD.Administration/ApplicationLoginTransaction.cs`, `UserTransaction.cs`; `TMCWD/TMCWD.CustomerSupport/AccountTransaction.cs`, `CustomerTransaction.cs`; `TMCWD/TMCWD.Billing/BillingTransaction.cs`.
- `TMCWD/TMCWD.Application/Controllers/AdminController.cs`, `AuthController.cs`, `AccountController.cs`, `CustomerController.cs`, `CustomerSupportController.cs`, `ReadingSheetController.cs`, `BillingController.cs`.
- `TMCWD/TMCWD.Application/Views/Admin/AddEditUser.cshtml`, `AddEditInspectionType.cshtml`; `Views/Account/Index.cshtml`; `Views/CustomerSupport/AddEditCustomer.cshtml`, `AddEditRequest.cshtml`, `CustomerInformation.cshtml`; `Views/Billing/ReadingSheet.cshtml`.
- `TMCWD/TMCWD.Application/wwwroot/js/site.js`, `create_temp.js`.
- `tests/BillingRegression/Program.cs`, `PostingSecurity.cs`, `CriticalBoundarySecurity.cs`, `MvcBoundarySecurity.cs`, `README.md`; `tests/MeterReaderAuthRegression/Program.cs`.
- `docs/Phase6I-B3-Critical-API-Boundary-Hardening.md`; `docs/Phase6I-B-PreDeployment-Checklist.md`.

## Updated complete mutation inventory

The 80 historical rows are retained with current classifications; the removed password route remains listed as retired. Three current rows are added: body password change, server-side login and ChargeAnchor. **83 inventory rows**: **76 current write-verb actions** (73 mutation-capable actions, including denied actions, plus 2 credential logins and 1 query-only POST), 4 denied mutating GETs and 3 removed aliases/routes. Lookup-only Endpoint/Tariff/WaterRate/ZoneBook and persistence services contain no additional discovered write routes. No global fallback policy was introduced; anonymous credential login and the query-only POST remain explicitly justified exceptions.

PROTECTED — EXISTING identifies an existing boundary, not blanket certification of body fields, role revocation, remittance DDL or every financial operation. FIXED IN 6I-B / B2 / B3 identifies the source phase. BLOCKED entries below are actual enforced denials.

| Method / route | Current classification | Current boundary / qualification |
|---|---|---|
| POST `/api/Account/SaveUpdate/{userId}` | FIXED IN 6I-B3 | CustomerService + claim actor; strict command, stored authority and creation metadata. |
| POST `/api/AdvancePayment/SaveUpdate/{userId}` | FIXED IN 6I-B2 | Collection; positive claim actor; server audit; POST only. |
| POST `/api/{jobOrderId}/ApprovalHistory/Save/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/auth/meter-reader/login` | OUT OF SCOPE — JUSTIFIED | Login validates credentials/reader eligibility; issues JWT, no persisted mutation. |
| POST `/api/BatchPaymentUpload/Save/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/BillingAdjustment/SaveUpdate/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/BillingAdjustment/SaveBatch/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Billing/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/BirInvoiceConfig/active` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ChargeType/SaveUpdate/{userId}` | FIXED IN 6I-B2 | SuperAdmin; positive claim identity; stored creator preserved; reads unchanged. |
| POST `/api/CollectionRemittance/Save` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Customer/SaveUpdate/{userId}` | FIXED IN 6I-B3 | CustomerService + claim actor; strict command, stored authority and creation metadata. |
| POST `/api/Dashboard/NewConnection/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/DisconnectionOrder/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/AccountTransfer/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveServiceRequest/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/AccomplishServiceRequest/{requestId:long}/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/ServiceRequest/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveServiceInspection/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveConnectOrder/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveHouseTapping/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/SaveWorkOrder/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/ActivateAccount/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/Dashboard/AdvanceStage/{userId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/DebitCreditMemo/Save/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/EWalletTransaction/SaveUpdate/{userId}` | BLOCKED — PROVIDER TRUST REQUIRED (DENIED) | PendingAuthorizationDecision; no provider contract invented. |
| POST `/api/{jobOrderId}/Finding/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/InspectionReport/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/InspectionType/SaveUpdate/{userId}` | FIXED IN 6I-B3 | SuperAdmin, positive claim actor, stored-field allowlist and server audit. |
| POST `/api/Inventory/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/{requestId}/JobOrder/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/{jobOrderId}/Material/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| PUT `/api/{jobOrderId}/Material/UpdateQuantityOrNewUnitCost/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| DELETE `/api/{jobOrderId}/Material/Delete/{id}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/MeterMaintenanceRate` | FIXED IN 6I-B | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/reading-sheet/mobile/{id:int}/readings` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/OtherCharge/SaveUpdate/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| PUT `/api/OtherCharge/Deactivate/{id}/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/OtherCharge/PostToCollection/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/OtherCharge/ProcessPayment/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/OtherFeeType/SaveUpdate/{userId}` | FIXED IN 6I-B2 | SuperAdmin; positive claim identity; stored creator preserved; reads unchanged. |
| DELETE `/api/OtherFeeType/Delete/{id}` | FIXED IN 6I-B2 | SuperAdmin; positive claim identity; stored creator preserved; reads unchanged. |
| POST `/api/PaymentCheck/SaveUpdate/{userId}` | FIXED IN 6I-B2 | Collection; positive claim actor; server audit; POST only. |
| POST `/api/Payment/SaveUpdate/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Payment/ProcessCollection/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Payment/CancelInvoice/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Penalty/SubmitWaiver/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Penalty/ReviewWaiver/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Penalty/PostCharges/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Penalty/SaveUpdate/{userId}` | PROTECTED — EXISTING | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/Reading/SaveUpdate/{userId}` | FIXED IN 6I-B2 | BillingOperations/claim; existing sync roster/baseline/date validation; no movement. |
| POST `/api/Reading/SaveRange` | BLOCKED — POLICY REQUIRED (DENIED) | Raw legacy bulk contract denied. |
| POST `/api/Reading/GetRangeByReadingSheetIds` | OUT OF SCOPE — JUSTIFIED | POST query only; no persisted mutation. |
| POST `/api/ReadingSheet/SaveUpdate/{userId}` | FIXED IN 6I-B | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ReadingSheet/UpdateZoneProgress` | FIXED IN 6I-B | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| PATCH `/api/ReadingSheet/UpdateReadingSheetsStatus/{userId}/{status}` | FIXED IN 6I-B | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ReadingSheet/PartialPostReadingSheet` | FIXED IN 6I-B | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ReadingSheet/CompleteReadingSheet` | FIXED IN 6I-B | Existing policy/claim boundary; see field/ownership/transaction qualifications above. |
| POST `/api/ReadingSheetTemplate/SaveUpdate/{userId}` | FIXED IN 6I-B3 | BillingOperations, claim actor, active verified reader and valid zone/sequence validation. |
| PUT `/api/ReadingSheetTemplate/Deactivate/{id}/{userId}` | FIXED IN 6I-B3 | BillingOperations, claim actor, active verified reader and valid zone/sequence validation. |
| POST `/api/{requestId}/Recommendation/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/Request/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision: 401 anonymous / 403 all authenticated roles; no service invocation. |
| POST `/api/{requestId}/RequestDetail/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/{requestId}/RequestDetail/SaveMultiple/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| DELETE `/api/{requestId}/RequestDetail/Delete/{id}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| DELETE `/api/{requestId}/RequestDetail/DeleteDetails` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/RequestFile/SaveUpdate/{userId}/{jobOrderId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/RequestFile/SaveRange` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/Users/SaveUpdate/{userId}` | FIXED IN 6I-B3 | SuperAdmin + persisted active verified actor, strict command, server audit/security fields. |
| PUT `/api/Users/ChangePassword/{id}/{userId}/{newPassword}` | FIXED IN 6I-B3 | Retired URL, HTTP 404; replaced by body-only self password command. |
| POST `/api/WebHook/SaveUpdate` | BLOCKED — PROVIDER TRUST REQUIRED (DENIED) | PendingAuthorizationDecision; no provider contract invented. |
| POST `/api/Workflow/SaveUpdate/{userId}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| DELETE `/api/Workflow/Delete/{id}` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| POST `/api/ÌnpectionTypeDetail/SaveUpdate` | BLOCKED — POLICY REQUIRED (DENIED) | PendingAuthorizationDecision; 401 anonymous / 403 every authenticated role, before service invocation. |
| GET `/api/Dashboard/ServiceSummary` | BLOCKED — POLICY REQUIRED (DENIED) | Retained backfill/reconciliation never invoked. |
| GET `/api/Dashboard/ServiceApplications/{category}` | BLOCKED — POLICY REQUIRED (DENIED) | Retained backfill/reconciliation never invoked. |
| GET `/api/Dashboard/ServiceRequestsByDescription` | BLOCKED — POLICY REQUIRED (DENIED) | Retained backfill/reconciliation never invoked. |
| GET `/api/Dashboard/WorkOrder/{workOrderType}/{requestId:long}` | BLOCKED — POLICY REQUIRED (DENIED) | Retained backfill/reconciliation never invoked. |
| Former GET `/api/AdvancePaymentService/SaveUpdate/{userId}` | FIXED IN 6I-B2 | Controller discovery metadata removed; HTTP 404. |
| Former POST `/api/ChargeTypeService/SaveUpdate/{userId}` | FIXED IN 6I-B2 | Controller discovery metadata removed; HTTP 404. |
| POST `/api/Users/password` | FIXED IN 6I-B3 | Claim-selected active verified self, current-password proof, body-only command; queries/target fields rejected. |
| POST `/api/auth/login` | FIXED IN 6I-B3 | Server body credential validation and password-free profile; no persisted mutation. |
| POST `/api/Billing/ChargeAnchor` | FIXED IN 6I-B3 | BillingOperations; only AccountId, transactional server-owned zero anchor or unchanged eligible existing bill. |

## Actual verification

| Command/check | Actual result |
|---|---|
| `dotnet build TMCWD/TMCWD.slnx --no-restore -v quiet` | PASS, zero errors; existing NU1903/nullability warnings |
| `dotnet build tests/BillingRegression/BillingRegression.csproj --no-restore -v quiet` | PASS, zero errors; existing NU1903/EF1002 warnings |
| `dotnet run --project tests/BillingRegression --no-build -- . --no-migrations` | PASS: **274 grouped database checks**, all connection-local temporary tables in `tmcwd_test`; **84 new B3 groups** beyond B2's 190 |
| Pure checks before database suite | PASS: 5 penalty/rounding cases, explicit maintenance/water calculation, both MySQL query shapes; separate from 274 |
| `node tests/BillingRegression/browser-regression.cjs` | PASS: **25 tests**, 0 failed/skipped |
| `node tests/BillingRegression/penalty-charging-regression.cjs` | PASS: **5 tests**, 0 failed/skipped |
| `node tests/BillingRegression/collections-browser-regression.cjs` | PASS: **8 Collections scenarios**, **31 Account Ledger checks**, plus Other Charges, cancellation and Dashboard Charging scenarios |
| `dotnet build tests/MeterReaderAuthRegression/MeterReaderAuthRegression.csproj --no-restore -v quiet`; `dotnet run --project tests/MeterReaderAuthRegression --no-build` | PASS: credential/input/reader-role/signed identity/expiry/legacy-login suite; synthetic HTTP, no database |
| `node --check .../wwwroot/js/site.js`; `node --check .../wwwroot/js/create_temp.js` | PASS |
| Migration-dependent invoice/payment concurrency, standalone ServiceRequestRegression, live authenticated deployment, Flutter/device end-to-end | **NOT RUN / NOT VERIFIED** |

Final evidence is retained in `.tmp/phase6ib3-build.log`, `phase6ib3-regression-build.log`, `phase6ib3-regression-final.log`, `phase6ib3-browser.log`, `phase6ib3-penalty-browser.log`, `phase6ib3-collections-browser.log`, `phase6ib3-auth-build.log`, `phase6ib3-auth.log` and the package-graph logs. These are local output artifacts, not deployment evidence.

Real HTTP bearer tests cover anonymous/wrong-role/invalid identity, forged audit fields, user escalation and peer administrators, explicit legitimate staff edits, initial password hashing without auto-verification, retired password URL, self password/confirmation/eligibility, password-free profiles and server login, strict customer/account commands, supported creation/contact/address changes, prohibited account authority changes, narrow anchor integrity/retry/issued snapshot preservation, inspection/template audit mapping and assignment validation, and every B3 quarantine for every canonical role. Rejected calls compare stored synthetic user/customer/account/billing/payment/reading/invoice sequence/configuration/shared-table state. Four actual MVC HTTP groups cover anti-forgery denial across 14 actions, retired GETs, signed user form mapping and signed customer/account creation. The test host uses ephemeral anti-forgery keys without modifying operational key storage.

Intermediate failed runs are not counted as PASS: a detached synthetic fixture reference, an inappropriate fallback credential lacking test-database permission, and a test host's filesystem-backed anti-forgery keys were corrected. The passing database run used the existing dedicated test connection. Sandbox build-output denials and the stalled sandbox Edge launch were retried with approved access. Standalone ServiceRequestRegression remains excluded because it can use the application connection and execute service migration/backfill SQL; `--no-migrations` excludes migration-dependent concurrency checks.

## Financial invariants

The preserved regression suite covers PHP **20.00** maintenance, **2025-01-01** initial date, sheet BillingDate selection and once-monthly inclusion; immutable official snapshots; RemainingAmount-based arrears; water-only rounded 10% penalties strictly after due date; internal Other Charges separation and installment activation/settlement; Collections allocation and full water-bill payment; cancellation restoration; BillAdjustment's existing PHP 20 legacy floor; invoice numbering and rollback/retry. No maintenance amount was moved into persisted service Other Charges. No historical invoice, balance, payment or operational reading date changed. The new anchor reproduces the former permitted zero-value anchor behavior with server-owned fields; it does not introduce a calculation.

## Remaining risks and policy blockers

- **No anonymously exposed Critical/High mutation remains in the enumerated Data API inventory for Users/password, Account/Customer, generic Billing or the quarantined child graph.** Denial is containment, not an approved usable service workflow.
- **HIGH dependency risk:** Administration still resolves Xml 8.0.3 and its five NU1903 advisories. Actual production host/publish graph and advisory disposition require review; no upgrade was authorized.
- **HIGH account-security follow-up:** administrator reset, verification and peer-administrator management are blocked. Existing MD5 password compatibility was deliberately preserved; a hash migration, login throttling and token/session revocation design require separate work. Existing role-bearing JWTs/sessions can outlive role/active/password changes; only user administrative writes/password operations and login perform the added persisted eligibility checks. B3 is not a global token-revocation implementation.
- **Service/inventory/provider policy:** quarantined actions need approved per-role action and ownership rules, safe request commands, atomicity/idempotency and provider trust before re-enabling. No SuperAdmin exception to PendingAuthorizationDecision exists.
- **Existing authenticated surfaces:** the PROTECTED inventory rows retain their prior financial contracts. B3 does not certify all legacy authenticated overposting/ownership cases or all MVC proxy anti-forgery routes. Batch upload is metadata, and CollectionRemittance retains runtime table-readiness DDL; neither was invoked. Legacy Account/Customer lookup and generic Billing Get/GetAll-style reads still lack a read-access policy and can expose customer/financial information anonymously; that visibility finding and remaining legacy MVC/configuration methods require separate review. These lookup-only routes are outside the mutation inventory; only Users' hash-bearing reads were changed in B3. No unresolved route is represented as a successful live test.
- **Operational gates:** backup restore/reconciliation unverified; eligible seed actor absent; schema/seed undeployed and unauthorized; live authenticated deployment workflow unverified; migration-dependent concurrency and device verification outstanding.

## NU1903 recommendation only

`TMCWD.Administration.csproj` directly pins **System.Security.Cryptography.Xml 8.0.3**. Its restored graph also requests Xml through **Microsoft.AspNetCore.Mvc 2.3.9 → Microsoft.AspNetCore.DataProtection 2.3.0 → Xml 8.0.2 minimum**, so deleting the direct pin alone could resolve an affected older package in that library graph. The pin also brings Pkcs 8.0.1. No repository application code directly calls SignedXml/EncryptedXml; framework data-protection use remains relevant.

Application references Administration, and BillingRegression/MeterReaderAuthRegression reference Application. In the inspected current `net10.0` web/test restore graphs, Xml is **not a resolved transitive NuGet package**: the ASP.NET Core shared-framework reference supplies it. Data has no resolved Xml package. No Xml DLL was found beside the current Application output. The installed shared runtimes include **10.0.12**; this local graph observation is not proof of a production deployment's effective runtime or published assemblies.

Microsoft's five build-linked advisories identify the affected Xml branch through 8.0.3 and patched versions **8.0.4 / 9.0.18 / 10.0.10**: [CVE-2026-50648](https://github.com/dotnet/runtime/security/advisories/GHSA-23rf-6693-g89p), [CVE-2026-50525](https://github.com/dotnet/runtime/security/advisories/GHSA-8q5v-6pqq-x66h), [CVE-2026-47302](https://github.com/dotnet/runtime/security/advisories/GHSA-cvvh-rhrc-wg4q), [CVE-2026-47304](https://github.com/dotnet/runtime/security/advisories/GHSA-g8r8-53c2-pm3f), [CVE-2026-50527](https://github.com/dotnet/runtime/security/advisories/GHSA-mmjf-rqrv-855v). For Xml package versions, use the advisories' Package/Patched versions sections; some .NET 8 runtime tables describe runtime versions, not the Xml package version.

Recommendation: separately authorize the smallest supported servicing change, **8.0.4 or a later verified patched 8.x package**, then restore/audit/build and test login, anti-forgery/data-protection and any library/publish consumer. A separately evaluated alignment to the project's net10.0 family is also available; NuGet publishes [Xml 10.0.12](https://www.nuget.org/packages/System.Security.Cryptography.Xml/10.0.12), beyond the advisories' 10.0.10 fix. That path may change Pkcs/BCL crypto dependencies and has greater graph/behavior impact than an 8.x servicing patch. Retiring the old MVC package/pin needs its own dependency review and verification. No package reference, lock file, runtime installation or restore graph was changed in this task; compatibility of an upgrade was not tested.

## Fresh read-only readiness and deployment verdict

On **2026-10-09**, configuration from Data local-development user-secrets and read-only queries confirmed **127.0.0.1:3307 / tmcwd**, server **DESKTOP-Q72GRF4**, **MySQL 8.0.46**. No process `ConnectionStrings__DefaultConnection` override was present; a different running process or production configuration is not verified. No secret was disclosed.

- `meter_maintenance_rates` table count: **0**; migration and seed remain undeployed.
- Existing SuperAdmin **Id 5**: active **1**, verified **0**; eligible active verified SuperAdmin count: **0**. No operational user was created, activated or verified.
- Active Reading Sheets **2 and 5**, Status **1**, BillingDate **2025-07-01**: covered by the approved **2025-01-01 / NULL** interval. The former date-coverage blocker remains resolved; actual rate availability remains blocked until separately authorized deployment.
- Repository/docs/backup filename searches and immediate project-parent metadata still provide no independently restorable backup evidence. An external backup may exist, but source identity, consistency, integrity, recovery instructions, successful isolated restore and reconciliation are **NOT VERIFIED**. No backup operation occurred.

**PASS for scoped B3 source authorization/body boundaries and enforced containment; deployment NO-GO.** Required next steps are policy decisions for unavailable workflows, dependency/security disposition, independently restored/reconciled backup evidence, an already legitimate eligible seed actor, actual-target/live-flow verification and separate migration/seed authorization. The only approved initial artifact remains the guarded **20250101** seed; **20260101 is SUPERSEDED / NOT FOR DEPLOYMENT**. Any future approved deployment must verify schema, then apply migration and only that seed, reconcile unchanged financial baselines, and run separately approved boundary/rollback/retry/collection/ledger/mobile checks. This task does not deploy or declare Phase 6I-B complete.


