# Phase 6I-B4 — Read, session and dependency security

Review date: **2026-10-09 (Asia/Manila)**. This supplements the B3, B2, posting-security and pre-deployment reports. Historical findings remain valid for their recorded source/configuration state.

**Source read/session remediation: PASS. Dependency/operational readiness: INCOMPLETE. Deployment: NO-GO.** No deployment, migration, seed, operational user creation/verification, persistent operational write or backup operation is authorized or performed. The approved maintenance interval remains **PHP 20.00 / 2025-01-01 / NULL**, selected by `ReadingSheet.BillingDate`. The retired 20260101 seed remains NOT FOR DEPLOYMENT.

## Findings and policies

Legacy Account/Customer reads exposed names, contact information, addresses, account and meter identifiers anonymously. Generic Billing reads exposed balances, arrears/adjustments, invoice identifiers, payment status and historical periods. Reading, sheet/template, advance-payment/check, charge/reference and service/provider lookups also lacked adequate read authorization. GET is not a public contract. No intentional public customer/invoice lookup was found.

The inventory below comes from **actual ASP.NET controller route metadata**, including nested routes, Unicode controller names and the POST query `Reading/GetRangeByReadingSheetIds`. There are **162 query routes: 161 GETs and one POST query**. Every row has real HTTP anonymous and inappropriate-role rejection checks. Development OpenAPI is outside the controller inventory and now inherits the authenticated fallback. Credential login POSTs are the two explicit `AllowAnonymous` exceptions; they return identity/fingerprint or an issued token only after credential and persisted eligibility checks. There are **no public Data customer/account/financial GETs**.

| Classification | Count | Authorization evidence |
|---|---:|---|
| BILLING OPERATIONS | 78 | Existing SuperAdmin/CSR/CustomerServiceOIC/Cashier policy: roles 1, 2, 6, 7 |
| COLLECTION | 20 | Existing SuperAdmin/Cashier policy: roles 1, 7 |
| SUPERADMIN | 6 | Existing role 1 policy |
| CUSTOMER SERVICE | 3 | Existing SuperAdmin/CSR/OIC policy: roles 1, 2, 6 |
| METER READER | 3 | Existing role 4 policy plus persisted reader eligibility and sheet assignment |
| BLOCKED — POLICY REQUIRED | 52 | PendingAuthorizationDecision; no role, including SuperAdmin, bypasses denial |
| AUTHENTICATED / PUBLIC — INTENTIONALLY SAFE | 0 controller queries | No blanket authenticated-only sensitive read or public-record exception introduced |

Account/Customer and financial history are used through shared transactions by `CustomerSupportController` (Customer Information/forms), `AccountController`, `BillingController`, `CollectionsController` and `ReadingSheetController`, and their account/customer view components. BillingOperations is the existing union of the proven CustomerService and Collection roles, so a new role matrix is unnecessary. Account/Customer writes retain their narrower CustomerService policy. Collections' bill/report, payment, advance and check reads retain/use Collection; Account Ledger uses BillingOperations. Reading/sheet/template/rate/zone reads use BillingOperations. Mobile calls existing services internally through its own assigned-sheet endpoints; it does not need unrestricted legacy Account/Customer/Billing routes.

Users lookups are used by Administration, reader assignment, reading-sheet name resolution, Customer Information and Collections collector/report name resolution. Ordinary BillingOperations users receive **Id and Name only**. SuperAdmin gets the existing password-free profile where administration needs role, email, active/verified and dates. Name/email/search administrative queries are SuperAdmin-only. Nonadmin `GetUsersByRole` permits only the established meter-reader assignment list; other role lists return 403. The assignment list remains server-filtered by role.

Unresolved request/job/inspection/material/workflow/inventory/provider data is denied instead of assigning speculative service permissions. This deliberately makes those legacy read-dependent forms, including Engineering Inventory and related service screens, unavailable pending read-policy review. All Dashboard service queries are denied; their four previously quarantined reconciliation/backfill GETs remain denied. The separately protected official service-invoice Collection endpoint remains available under its existing contract. No quarantined workflow was re-enabled.

## Exact source changes

Read authorization was added at query actions in these Data controllers (all their read routes are enumerated below): `Account`, `Customer`, `Billing`, `BillingAdjustment`, `Reading`, `ReadingSheet`, `ReadingSheetTemplate`, `OtherCharge`, `AdvancePayment`, `PaymentCheck`, `ChargeType`, `OtherFeeType`, `InspectionType`, `WaterRate`, `Tariff`, `ZoneBook`, `Endpoint`, `EWalletTransaction`, `WebHook`, `ApprovalHistory`, `DebitCreditMemo`, `Dashboard`, `Finding`, `InspectionReport`, `Inventory`, `JobOrder`, `Material`, `Recommendation`, `Request`, `RequestDetail`, `RequestFile`, `Workflow` and `ÌnpectionTypeDetail`. `UsersController` additionally restricts lookups and projects directory responses. Existing Payment, Penalty, BatchPaymentUpload, BirInvoiceConfig, MeterMaintenanceRate, MobileReadingSheet and ServiceInvoice policies were inspected and exercised by the query inventory; their read policies were retained.

Additional files:

- `TMCWD/TMCWD.Data/Program.cs`: authenticated fallback; production JWT persisted validation callback; existing policy role mappings unchanged.
- `TMCWD/TMCWD.Data/Services/PersistedTokenValidation.cs`: fresh persisted identity/security validation before controller activation.
- `TMCWD/TMCWD.Utility/Encryption/UserSecurityStamp.cs`: shared domain-separated HMAC fingerprint generation.
- `TMCWD/TMCWD.Data/Controllers/AuthController.cs`: explicit credential-only anonymous exceptions; fingerprint issuance following validated credentials/eligibility; Data meter-reader JWT includes fingerprint.
- `TMCWD/TMCWD.Data/Entities/Users.cs` and `TMCWD/TMCWD.Model/Administrator/User.cs`: password and remember-token JSON suppression. Strict password/user command DTOs remain the authorized write contracts.
- `TMCWD/TMCWD.Administration/ApplicationLoginTransaction.cs`: retain credential-issued fingerprint separately from the public user model; missing fingerprint fails login.
- `TMCWD/TMCWD.Services/AuthenticatedUserService.cs`: separate server-session fingerprint state/key.
- `TMCWD/TMCWD.Application/ApiClientRegistration.cs`: copy the original login fingerprint into the short-lived bridge token; never fetch a fresh fingerprint for a stale session.
- `TMCWD/TMCWD.Application/Controllers/HomeController.cs`, `TMCWD/TMCWD.Application/Program.cs`: store/restore that fingerprint separately in server session.
- `TMCWD/TMCWD.Application/Controllers/AuthController.cs`: general/reader Application JWTs carry the Data credential-issued fingerprint.
- `tests/BillingRegression/ReadSessionSecurity.cs`: actual route inventory, production-validator HTTP, current financial reads/posting, stale JWT/session and credential/reader compatibility tests.
- `tests/BillingRegression/PostingSecurity.cs`, `MvcBoundarySecurity.cs`, `README.md`: invoke/document B4 and adapt older synthetic session fixtures.
- `tests/MeterReaderAuthRegression/Program.cs`: fingerprint-aware synthetic credential response, claim assertion and exact assertion count.

No financial persistence service, calculation, invoice allocation, database script, package reference or runtime installation was changed by B4.

## Password and secret serialization

Data/User model password and remember-token properties are JSON-ignored, including accidental future serialization of those types. Users GETs use explicit projections. Dashboard user projections were inspected: they select names/IDs/assignment metadata rather than password-bearing entities; unresolved Dashboard/provider reads are denied. `Data/Test/TestUser` is not a controller or mapped API endpoint. No query returns raw passwords, hashes, remember tokens, JWT signing keys or authentication fingerprints.

Successful credential login returns the necessary issued token or a keyed login fingerprint used by the trusted Application issuer. The fingerprint is **not the stored password hash**, is not independently accepted as a bearer credential, and cannot be calculated without the signing secret. It is not added to `UserProfileDto`, ordinary user-directory responses or the session's serialized User. Issued login tokens are the explicit authentication contract exception to withholding token material from normal reads. Tests never print passwords, hashes, fingerprints or tokens.

## Token/session revocation design and implementation

JWT signature/issuer/audience/expiry checks alone do not revoke stale roles or passwords. The MVC bridge formerly minted a new five-minute JWT from cached session identity/role, while Application-issued tokens last 30 days and Data reader tokens last 12 hours.

| Direction | Decision |
|---|---|
| A: persisted eligibility recheck | Implemented on every validated Data token, including mutations and sensitive reads. Active, verified, known role and claim role must match the current user. |
| B: security/version stamp | Implemented without new schema: HMAC-SHA256 over existing user ID, normalized stored password hash, role, active/verified flags and persisted update/verification timestamps. Domain separation and fixed-time comparison prevent exposing/comparing the hash as a public credential. |
| C: short expiry/refresh alone | Insufficient: cached MVC sessions can mint new JWTs; preserves existing lifetimes instead. |
| D: new revocation storage | Not needed for the implemented committed user-security changes. Separate policy/schema would be needed for per-device/session logout or revoking one token while retaining unchanged user state. |
| E: combined A+B | Selected: one fresh, no-tracking user lookup per request, no revocation cache and no migration. |

The Data bearer callback rejects missing/duplicate/invalid identity, role or fingerprint claims, absent users, inactive/unverified users, unknown/mismatched roles and changed fingerprints with **401**. Valid current identities outside an endpoint's allowed policy receive **403**. Anonymous requests receive **401**. The current actor remains server-validated claim identity; forged route/query userId remains non-authoritative. Rejected requests do not reach financial mutations.

Data credential login computes the fingerprint from the stored record only after password and eligibility validation. Application login retains it separately; Application-issued JWTs and the MVC session bridge carry that **original** value. The bridge cannot refresh a revoked session by signing a fresh token. Password/role/eligibility changes therefore take effect on the **next Data request after commit**, rather than waiting 12 hours/30 days/session expiry. Existing immutable invoices and posting transaction/rollback behavior are unchanged.

Operational behavior and limits:

- Pre-B4 tokens have no fingerprint and fail closed; pre-B4 MVC sessions do not mint authenticated bridge tokens. A coordinated source release requires fresh login. Mobile cached/offline work remains device-local; authenticated network operations require renewed login.
- Existing `DateUpdated`/`DateVerified` participate, so ordinary profile edits also require renewed login. Normal edit/revert does not revive the original token. Security edits must use server services that maintain audit timestamps; privileged direct SQL restoring the entire historical security state/timestamps and database restore/key management require separate operational controls.
- Validation does not abort requests already authenticated before a concurrent security-change commit. It adds one read by primary key per signed request; database availability and actual deployment latency/load are not certified. A failed identity lookup cannot authorize a mutation.
- Global signing-key rotation invalidates tokens/fingerprints. Per-device logout, credential throttling, MD5 migration, administrator reset/verification and peer-administrator management remain separate decisions. Existing password compatibility is preserved.
- No token-revocation schema/design approval blocker is needed for the implemented global user-security changes. Session-specific revocation remains outside scope, and no such feature is claimed.

## NU1903 disposition — recommendation, not an applied upgrade

**DEPENDENCY UPDATE REQUIRES SEPARATE APPROVAL.** Administration directly pins `System.Security.Cryptography.Xml 8.0.3` alongside `Microsoft.AspNetCore.Mvc 2.3.9` and `Microsoft.AspNetCore.WebUtilities 10.0.7`. Its restored graph contains **MVC 2.3.9 → MVC.ViewFeatures 2.3.0 → Antiforgery 2.3.0 → DataProtection 2.3.0 → Xml >= 8.0.2**; the direct reference resolves Xml **8.0.3**, which depends on **Pkcs 8.0.1**. Removing the explicit pin alone is unsafe: the retained transitive minimum is also affected.

Application directly references Administration; BillingRegression and MeterReaderAuthRegression reference Application. Current inspected net10.0 Application, Data and those test assets do **not** resolve Xml as a NuGet package: the ASP.NET shared framework supplies it. The installed ASP.NET **10.0.12** Xml assembly has file version **10.0.1226.42308**, product **10.0.12**. Administration's library graph still resolves the vulnerable NuGet package and produces five NU1903 warnings. No repository direct SignedXml/EncryptedXml usage was found; framework DataProtection still matters. A standalone library consumer or different publish/runtime graph could load the pinned assembly. Production published output and host runtime are **NOT VERIFIED**; the local shared-framework observation is not a vulnerability waiver.

Microsoft's five advisories identify Xml 8.0.0–8.0.3 as affected and **8.0.4** as the first patched 8.x version: [CVE-2026-50648](https://github.com/dotnet/runtime/security/advisories/GHSA-23rf-6693-g89p), [CVE-2026-50525](https://github.com/dotnet/runtime/security/advisories/GHSA-8q5v-6pqq-x66h), [CVE-2026-47302](https://github.com/dotnet/runtime/security/advisories/GHSA-cvvh-rhrc-wg4q), [CVE-2026-47304](https://github.com/dotnet/runtime/security/advisories/GHSA-g8r8-53c2-pm3f), [CVE-2026-50527](https://github.com/dotnet/runtime/security/advisories/GHSA-mmjf-rqrv-855v). The advisory package/Patched versions sections are authoritative for this proposal; some runtime-version tables use different version numbering.

Smallest proposed servicing change in `TMCWD/TMCWD.Administration/TMCWD.Administration.csproj`:

```diff
- <PackageReference Include="System.Security.Cryptography.Xml" Version="8.0.3" />
+ <PackageReference Include="System.Security.Cryptography.Xml" Version="8.0.4" />
```

Recommended baseline: **8.0.4**, subject to separately authorized restore/audit and compatibility verification (or a later verified patched 8.x servicing release). Net10 alignment or removal of the old MVC package requires a broader dependency review. The package page for 8.0.4 was unavailable to the browsing tool; advisories establish its patched status, but its complete restored/published graph and compatibility were not tested here. Thus a servicing-only safe change was **not proven**, and no csproj/assets/package upgrade was applied.

Required after approval: restore and vulnerability audit of the complete solution/library graph; inspect changed Pkcs/BCL dependencies and published DLL/deps/runtimeconfig selection; run solution and affected test builds; verify credential login/JWT/session bridge, anti-forgery, data-protection key persistence/restart/unprotect behavior, reader authentication and the safe financial/browser suites below. Verify the real host runtime and assess advisories before release. B4's `--no-restore` builds cannot prove the proposed update safe.

## Verification results

Final source verification passed. **617 grouped temporary-table checks** comprise the prior **274** groups plus **343 B4 groups**: **324** query-rejection groups (anonymous and inappropriate role across 162 routes) and **19** staff/profile, financial posting/read/SOA, stale JWT/MVC session, password, credential/reader and eligibility groups. Groups may contain several requests/assertions; 617 is not a count of individual HTTP requests. Recorded commands:

| Command | Result |
|---|---|
| `dotnet build TMCWD/TMCWD.slnx --no-restore -v quiet` | PASS, zero errors; NU1903 remains |
| `dotnet build tests/BillingRegression/BillingRegression.csproj --no-restore -v quiet` | PASS, zero errors; existing NU1903/nullable/EF1002 warnings remain |
| `dotnet run --project tests/BillingRegression --no-build -- . --no-migrations` | PASS, **617 grouped checks**, temporary tables only; migration suites explicitly NOT RUN |
| `node tests/BillingRegression/browser-regression.cjs` | PASS, 25 tests, 0 failed/skipped |
| `node tests/BillingRegression/penalty-charging-regression.cjs` | PASS, 5 tests, 0 failed/skipped |
| `node tests/BillingRegression/collections-browser-regression.cjs` | PASS, 8 Collections scenarios, 31 Ledger checks, plus Other Charges/cancellation/Dashboard scenarios |
| `dotnet build tests/MeterReaderAuthRegression/MeterReaderAuthRegression.csproj --no-restore -v quiet` | PASS, zero errors, 5 NU1903 warnings |
| `dotnet run --project tests/MeterReaderAuthRegression --no-build` | PASS, 43 assertions |

B4 uses a separate real HTTP/Kestrel host with the production `PersistedTokenValidation` callback and actual current synthetic records. Earlier B2/B3 hosts retain their original signature/role-policy synthetic claims; they do not themselves model persisted security stamps. B4 fills that qualification explicitly. Every rejected B4 request checks unchanged financial/workflow state plus password/remember-token fields. Tests remain sequential connection-local temporary fixtures in **tmcwd_test**; no operational user, customer, billing, payment, reading, snapshot or balance is changed. Dedicated test credentials are not printed or committed.

The first solution build failed because the sandbox could not write existing obj artifacts; the approved build retry passed. Earlier B4 runs passed 615, 616 and 617 groups as coverage was completed; only the final 617-group run qualifies the finished source, including explicit official SOA and assigned-reader download assertions. These are not skipped checks represented as successes. Collections launched headless Edge under approved tool permission. Migration SQL, seed SQL and migration-dependent invoice/payment concurrency suites are **NOT RUN**. Standalone service migration/backfill regression, deployment publish, dependency upgrade compatibility, real-device end-to-end and live authenticated operational posting are **NOT RUN / NOT VERIFIED**.

Financial contracts remain unchanged: maintenance PHP 20 by the approved billing-date interval; issued snapshot immutability; RemainingAmount arrears; water-only 10% penalty; Other Charge separation/installments; collection allocation and cancellation restoration; full-payment-only water bill rule; BillAdjustment PHP 20 legacy floor; invoice sequence/history. No maintenance Other Charges entry is created.

## Readiness and remaining blockers

The last operational identity evidence is the dated B3 read-only verification: **127.0.0.1:3307/tmcwd**, server **DESKTOP-Q72GRF4**, MySQL **8.0.46**, maintenance table absent, active verified SuperAdmin count **0** (Id 5 active/unverified), sheets **2 and 5** with BillingDate **2025-07-01**. B4 did **not** repeat operational SQL or assert new live success. July 2025 remains covered by the approved initial interval; coverage does not establish an installed configuration.

Independently restorable/reconciled backup evidence remains **NOT VERIFIED**. A workspace backup-name search found no matching backup artifact; that is not proof about external backups or a successful independent restore. No backup was created/overwritten/restored/deleted. The legitimate eligible seed actor, actual deployment configuration/runtime, coordinated fresh-login behavior and live authenticated workflow remain **NOT VERIFIED**. Migration/approved 20250101 seed and deployment remain separately unauthorized.

High dependency risk remains pending the approved servicing fix and actual publish/runtime disposition. MD5 compatibility, throttling, account-administration policy and required unavailable service/inventory/provider workflows remain operational follow-ups. All **40 B2/B3 denied mutation routes remain denied**; 52 query routes are now classified BLOCKED — POLICY REQUIRED (including the four previously denied mutating GETs). These are containment, not usable workflows or inferred service permissions.

Before any deployment: approve/dispose of dependency and unavailable-workflow risks; verify an independently restored/reconciled backup and an existing legitimate eligible actor; verify the target and coordinated Application/Data release with fresh authenticated sessions and reader tokens; obtain separate migration/seed authorization; reconcile financial baselines; apply only the approved 20250101 seed after the additive migration when authorized; run separately approved post-deployment checks. B4 does not authorize these operations or declare Phase 6I-B complete.

## Complete Data query inventory

The following runtime-derived rows identify the effective policy after narrower existing method/class policies are combined. There is no public-record exception. The Users response projection described above applies in addition to the listed route policy.

| Method / route | Classification | Effective policy |
|---|---|---|
| `GET /api/{jobOrderId}/ApprovalHistory/GetAll` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{jobOrderId}/Finding/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{jobOrderId}/Finding/GetAll` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{jobOrderId}/Material/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{jobOrderId}/Material/GetAll` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{jobOrderId}/Material/GetByJobOrderId` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{requestId}/JobOrder/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{requestId}/JobOrder/GetAll` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{requestId}/JobOrder/GetByRequestDetailId/{requestDetailId}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{requestId}/JobOrder/GetByRequestId` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{requestId}/Recommendation/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{requestId}/Recommendation/GetByRequestId` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{requestId}/RequestDetail/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/{requestId}/RequestDetail/GetByRequestId` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Account/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Account/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Account/GetByAccountNumber/{accountNumber}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Account/GetByCustomerId/{customerId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Account/GetByMeterNumber/{meterNumber}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Account/GetByZoneAndBook/{zone}/{book}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Account/GetByZoneBookAndSequence/{zone}/{book}/{seqFrom}/{seqTo}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Account/Search` | BILLING OPERATIONS | BillingOperations |
| `GET /api/AdvancePayment/Get/{id}` | COLLECTION | Collection |
| `GET /api/AdvancePayment/GetActiveByAccount/{accountId}` | COLLECTION | Collection |
| `GET /api/AdvancePayment/GetAll` | COLLECTION | Collection |
| `GET /api/AdvancePayment/GetByAccount/{accountId}` | COLLECTION | Collection |
| `GET /api/BatchPaymentUpload/Get/{id}` | COLLECTION | Collection |
| `GET /api/BatchPaymentUpload/GetByDateRange` | COLLECTION | Collection |
| `GET /api/Billing/AccountLedger/{accountNumber}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Billing/Collection/{billingReferenceId}` | COLLECTION | Collection |
| `GET /api/Billing/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Billing/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Billing/GetByAccountId/{accountId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Billing/GetByBillingReference/{billingReferenceId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Billing/GetByJobOrderId/{jobOrderId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Billing/GetPaymentTransactionId/{paymentTransactionId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Billing/History/{accountNumber}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/BillingAdjustment/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/BillingAdjustment/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/BillingAdjustment/GetByReference/{reference}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/BirInvoiceConfig` | SUPERADMIN | SuperAdmin |
| `GET /api/BirInvoiceConfig/active` | SUPERADMIN | SuperAdmin |
| `GET /api/ChargeType/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ChargeType/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ChargeType/GetByClassification/{classificationId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/collection/service-invoice/{serviceInvoiceNumber}` | COLLECTION | Collection |
| `GET /api/Customer/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Customer/GetByName/{firstname}/{lastname}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Customer/GetCustomers` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Customer/GetCustomersFromIds` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Customer/Search/{searchString}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Dashboard/AccountTransferAccounts` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/ApplicationPayment/{applicationId:long}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/ApplicationTicket/{applicationId:long}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/ConnectOrder/{requestId:long}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/DisconnectionAccounts` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/DisconnectionOrders` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/HouseTapping/{requestId:long}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/ServiceApplications/{category}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/ServiceInspection/{requestId:long}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/ServiceRequestAssignees` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/ServiceRequestsByDescription` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/ServiceSummary` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Dashboard/WorkOrder/{workOrderType}/{requestId:long}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/DebitCreditMemo/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/DebitCreditMemo/GetAll` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Endpoint/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Endpoint/GetByType/{type}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Endpoint/GetByTypeAndName/{type}/{name}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/EWalletTransaction/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/EWalletTransaction/GetByPaymentReference/{reference}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/EWalletTransaction/GetByProcessedBy/{processBy}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/ÌnpectionTypeDetail/GetDetail` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/InspectionReport/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/InspectionReport/GetByRequestId/{requestId}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/InspectionType/Get/{id}` | CUSTOMER SERVICE | CustomerService |
| `GET /api/InspectionType/GetNewRequestType` | CUSTOMER SERVICE | CustomerService |
| `GET /api/InspectionType/GetTypes` | CUSTOMER SERVICE | CustomerService |
| `GET /api/Inventory/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Inventory/GetAll` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Inventory/GetByName/{name}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/MeterMaintenanceRate` | SUPERADMIN | SuperAdmin |
| `GET /api/MeterMaintenanceRate/by-billing-date/{billingDate}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/OtherCharge/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/OtherCharge/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/OtherCharge/GetByReference/{reference}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/OtherFeeType/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/OtherFeeType/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/OtherFeeType/GetByName/{name}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Payment/Get/{id}` | COLLECTION | Collection |
| `GET /api/Payment/GetByBillingReference/{referenceId}` | COLLECTION | Collection |
| `GET /api/Payment/GetByCashier/{userId}` | COLLECTION | Collection |
| `GET /api/Payment/GetByDateRange` | COLLECTION | Collection |
| `GET /api/Payment/GetByMacAddress/{macAddress}` | COLLECTION | Collection |
| `GET /api/Payment/GetByMethod/{methodId}` | COLLECTION | Collection |
| `GET /api/Payment/GetByPaymentReference/{referenceId}` | COLLECTION | Collection |
| `GET /api/Payment/Invoice/{invoiceNumber}` | COLLECTION | Collection |
| `GET /api/Payment/SearchInvoices` | COLLECTION | Collection |
| `GET /api/PaymentCheck/Get/{id}` | COLLECTION | Collection |
| `GET /api/PaymentCheck/GetAll` | COLLECTION | Collection |
| `GET /api/PaymentCheck/GetByReference/{reference}` | COLLECTION | Collection |
| `GET /api/Penalty/ActiveEntries` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Penalty/ChargeEntries` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Penalty/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Penalty/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Penalty/GetByReference/{reference}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Penalty/PendingWaivers/{userId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/reading-sheet/mobile/{id:int}/accounts/{accountId:int}/soa` | METER READER | MeterReader |
| `GET /api/reading-sheet/mobile/{id:int}/download` | METER READER | MeterReader |
| `GET /api/reading-sheet/mobile/assigned` | METER READER | MeterReader |
| `GET /api/Reading/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Reading/GetAccountCurrentReading/{accountId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Reading/GetAccountPreviousReading/{accountId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Reading/GetByAccount/{accountId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Reading/GetByAccountAndBillingPeriod/{accountId}/{billingPeriod}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Reading/GetByReader/{readerId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Reading/GetByZoneAndBook/{zone}/{book}` | BILLING OPERATIONS | BillingOperations |
| `POST /api/Reading/GetRangeByReadingSheetIds` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheet/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheet/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheet/GetByAssignedTo/{assignedTo}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheet/GetByBillingDate/{zone}/{book}/{billingDate}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheet/GetByZoneAndBook/{zone}/{book}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheet/GetByZoneBookAndAssignedTo/{zone}/{book}/{assignedTo}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheet/GetCurrentByAssignedTo/{zone}/{book}/{assignedTo}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheet/GetReadingSheetAccounts` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheetTemplate/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheetTemplate/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ReadingSheetTemplate/GetByUser/{userId}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Request/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Request/GetByCustomerId/{customerId}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Request/GetByUserId/{userId}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Request/GetRequests` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Request/SearchRequest/{searchString}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/RequestFile/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/RequestFile/GetAll/{jobOrderId}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Tariff/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Tariff/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Tariff/GetByClassification/{classification}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Users/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Users/GetByEmail/{email}` | SUPERADMIN | SuperAdmin |
| `GET /api/Users/GetByName/{name}` | SUPERADMIN | SuperAdmin |
| `GET /api/Users/GetUsers` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Users/GetUsersById` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Users/GetUsersByRole/{role}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/Users/SearchUser/{searchString}` | SUPERADMIN | SuperAdmin |
| `GET /api/WaterRate/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/WaterRate/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/WaterRate/GetByClassification/{classification}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/WaterRate/GetByClassificationAndMeterSize/{classification}/{meterSize}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/WebHook/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/WebHook/GetAll` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Workflow/Get/{id}` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/Workflow/GetAll` | BLOCKED — POLICY REQUIRED | PendingAuthorizationDecision |
| `GET /api/ZoneBook/Get/{id}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ZoneBook/GetAll` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ZoneBook/GetBooksByZone/{zone}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ZoneBook/GetByIds` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ZoneBook/GetByWeek/{week}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ZoneBook/GetByZone/{zone}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ZoneBook/GetByZoneAndBook/{zone}/{book}` | BILLING OPERATIONS | BillingOperations |
| `GET /api/ZoneBook/GetZones` | BILLING OPERATIONS | BillingOperations |
