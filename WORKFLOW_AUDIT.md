# TMCWD workflow audit

Date: 2026-10-05 (Asia/Manila)

The system is **not fully connected yet**. The main pages load, but existing
Charging/Tapping records, inventory actions, older reading workflows, and some
legacy pages have confirmed gaps. This is an audit with targeted local fixes,
not certification that every save/payment operation works in production.

## Scope and evidence

- Inspected dashboard dispatch, Razor links, browser requests, MVC controllers,
  transaction classes, API routes, and affected persistence services.
- Discovered 208 literal MVC references and 220 transaction/API references.
  This count includes repeated references, not unique endpoints. Dynamic URLs,
  concatenated URLs, compiled-out controllers, and required record parameters
  were reviewed separately where relevant; static discovery is not exhaustive.
- Used the supplied administrator account for login and read-only page checks.
  No credentials are included in this report or test source.
- Probed 34 page URLs. Used headless Edge for rendered pages and all nine
  dashboard cards, including opening the first record in each nonempty queue.
- Baseline: existing Application on port 5054 and Data API on port 5178.
  Verified fixes using a separate Application instance on port 5055 with the
  existing Data API. The original application was not restarted.
- No forms were submitted to modify persistent customer/financial records.
  Database regression writes used connection-local temporary tables.

## Dashboard cards

Every category reaches `/Admin/GetServiceApplications`, then
`DashboardTransaction.GetServiceApplications`, then
`GET api/Dashboard/ServiceApplications/{category}`. All nine list requests
returned HTTP 200. Subsequent record loading is summarized below.

| Card | Records during audit | Record workflow | Result |
|---|---:|---|---|
| Online Applications | 0 | Application review -> `GetApplicationTicket` -> `ApplicationTicket/{id}` | Empty queue loads; record/transition not exercised |
| New | 0 | New review -> ticket details; `AdvanceStage` sends to inspection | Empty queue loads; transition not submitted |
| Inspection | 2 | `GetServiceInspection` -> `ServiceInspection/{id}` | Both detail requests return 200; first workspace opens |
| Charging | 3 | `Billing/GetChargeTypes` and `GetOtherChargesByAccount` -> Other Charges | First workspace gets 400; two records lack linked accounts and account numbers |
| Payment | 2 | `GetApplicationPayment` -> `ApplicationPayment/{id}` | Both detail requests return 200; first workspace opens |
| Installation | 1 | Prelinked service-request form -> `SaveInstallationServiceRequest` -> `ServiceRequest/{userId}` | Form opens; create/complete checked with temporary-table regressions |
| Connect | 0 | `GetConnectOrder` -> `ConnectOrder/{id}`; approval via `SaveConnectOrder` | Empty queue loads; record/approval not exercised |
| Tapping | 2 | `GetHouseTapping` -> `HouseTapping/{id}` | Both details return MVC 502 because API returns 400 for missing linked accounts |
| Tickets | 10 | `GetApplicationTicket` -> `ApplicationTicket/{id}` | All ten detail requests return 200; first workspace opens |

Do not manufacture customer/account links to clear the failures. Reconcile the
affected records against their actual applications before advancing them.

## Page results after local fixes

"Clean" means HTTP 200 with no observed startup JavaScript exception or local
HTTP failure in the headless smoke check. It does not mean all buttons or saves
have been exercised. Required-record pages need valid IDs for meaningful use.

| Page URL(s) | Result |
|---|---|
| `/Home/Index` | Clean; administrator login redirects to `/Admin` |
| `/Admin/Index` | Clean startup; card failures described above |
| `/Admin/Users` | HTTP 500: `Views/Admin/Users.cshtml` is absent |
| `/Admin/AddEditUser` | Clean after correcting the nullable checkbox helper |
| `/Admin/InspectionTypes`, `/Admin/AddEditInspectionType`, `/Admin/OtherFeeTypes` | Clean; shared-helper loading fixed for list pages |
| `/CustomerSupport/Index`, `/CustomerSupport/CustomerInformation` | Clean |
| `/CustomerSupport/AddEditCustomer`, `/CustomerSupport/AddEditAccount` | Clean startup; saves not submitted |
| `/CustomerSupport/Requests`, `/CustomerSupport/AddEditRequest` | Clean after restoring shared helpers |
| `/CustomerSupport/NewConnection`, `/CustomerSupport/Disconnection`, `/CustomerSupport/AccountTransfer`, `/CustomerSupport/ServiceRequest` | Clean startup; form routes traced; persistent submissions not made |
| `/Billing/ReadingSheet`, `/Billing/Reading`, `/Billing/BillAdjustment` | Clean startup; reading endpoint gaps remain below |
| `/Billing/OtherCharges`, `/Billing/DebitCreditMemo`, `/Billing/Penalty`, `/Billing/PenaltyCharging` | Clean startup; relevant billing regressions pass |
| `/Collections/Index` | Clean startup; financial actions checked only where covered by temporary-table regressions |
| `/ReadingSheet/Index` | Clean after restoring `Collapsible` helper |
| `/Account/Index` without `customerId` | HTTP 500: downstream transaction rejects missing customer ID; valid customer navigation requires that parameter |
| `/JobOrder/Index` without IDs | Renders, then `GetJobOrder` returns 500; also calls nonexistent `cmbMaterials.setDropdown` |
| `/Engineering/Index` | Empty view inherits login layout; login script crashes because login form elements are absent |
| `/Engineering/Inventory`, `/Engineering/AddEditInventory` | Clean startup; inventory actions remain broken below |
| `/Billing/ReadingSheetDetails`, `/Billing/StatementOfAccount`, `/Billing/Collections` | HTTP 404: view files exist but no matching MVC action; these are not current sidebar destinations |

The sidebar's Announcements entry is a hash link, not a connected MVC/API page.

## Outstanding findings, in priority order

1. **Access control is inconsistent.** Before signing in, dashboard, customer,
   job-order and inventory page requests returned 200, while Billing and
   Collections correctly rejected anonymous access. The Data dashboard summary
   also responds without authentication. Review server-side guards and API
   authorization; hiding links is not protection.
2. **Charging/Tapping records have missing account links.** Two of three Charging
   records and both Tapping records cannot load their account workspaces.
   `HouseTapping` explicitly rejects a missing link; MVC wraps that as 502 and
   loses the useful API error text.
3. **Reading status operations have incomplete API wiring.**
   `ReadingTransaction.UpdateStatusByReadingSheetIds` calls an absent API action,
   despite an existing `ReadingService` implementation. Reading-sheet deletion
   and bulk deletion invoke it alongside a separate sheet update. A missing
   endpoint can leave sheet/readings inconsistent; empty-list fallback responses
   can conceal the failure. Do not live-test these mutations until fixed.
4. **Older reading lookup routes are absent.** The API lacks
   `ReadingSheet/GetByBillingDateAndAssignedTo` and `Reading/GetByReadingSheetId`,
   both called by `AccountController.GetByZoneBookAndSequence`. The older
   `create_reading.js` also omits the billing date and assignee in one lookup.
   Additional absent transaction routes are `ReadingSheet/GetByZoneBookId`,
   `GetByBillingPeriodStart`, and `GetRangeReadingSheets`. Reuse existing service
   methods if those workflows are retained.
5. **Legacy request-detail replacement is not atomic.**
   `CustomerSupportController.SaveRequestDetails` deletes existing details and
   then validates/saves replacements through another API call. Invalid data or a
   failed second request can discard the earlier details. Validate before delete
   and perform replacement in one database transaction.
6. **Inventory actions are incomplete.** `Inventory.cshtml` calls missing
   `/Engineering/DeactivateInventory`. Its quantity callback references an
   undefined `currentUserId`; the controller also accepts acting-user metadata
   from query parameters. Use the authenticated user and protected mutation
   actions when repairing this flow.
7. **Legacy pages need correction.** Missing Users view, missing job-order
   dropdown helper, missing required-ID validation, and the empty Engineering
   view prevent those paths from working reliably. Account deactivation also
   discards its intended customer-specific redirect by failing to return it.
8. **Unused/incomplete API clients remain.** `OtherCharge/GetByClassificationId`,
   `EWalletTransaction/SaveUpdate`, the single-ID overload of
   `Reading/GetByZoneAndBook`, and `Account/GetByZoneBookId` lack matching routes.
   `AdvancePayment.SaveUpdate` omits its API prefix and posts to a controller
   action declared as GET. Single request-detail deletion omits the parent route.
   Some have no active caller; remove unused paths or complete them when needed.
   `ReadingController.cs` is explicitly excluded from Application compilation.
9. **Orphan features are not working pages.** The standalone reading-details
   script only logs confirmation for Complete/Partial Post; it does not call an
   endpoint. Its view currently has no standalone action. Treat obsolete views
   and scripts as legacy artifacts, not completed workflows.

## Fixes applied and verified

- Loaded existing `site.js` in the shared inside layout before page scripts.
  This restores `DataTable`, `WebClient`, `Alert`, and `Collapsible` for the
  affected older pages without adding a dependency.
- Fixed the Add/Edit User nullable checkbox expression and submit-button markup.
  Verified that the page renders and starts without a browser exception.
- Corrected Advance Payment account lookup, billing-reference lookup (with URL
  escaping), charge-type classification lookup, and reader lookup route names.
- Corrected single request-detail save and bulk detail-delete URLs. Fixed inverted
  validation that rejected valid details, including whitespace handling.
- Added the browser's `URLSearchParams` to the penalty regression VM harness;
  the page itself did not need a loading fix.
- Added database-free HTTP capture checks in `tests/WorkflowRegression`.

## Verification and limits

- Isolated Application build: passed, zero errors. Existing nullable warnings
  and NuGet vulnerability warnings remain. The ordinary build could not replace
  the running application's locked executable.
- Browser regression suite: 23 passed; penalty charging suite: 5 passed.
- Workflow regression: 7 real transaction-request checks and 6 invalid-input
  checks passed, using a mock HTTP handler and no database writes.
- Billing regression: five pure calculation cases, query translation, and 24
  temporary-table database checks passed.
- Service-request regression: all five reported groups passed, including
  migration reruns, create/edit/complete, linked installation, and rollback.
- Headless browser rerun confirms the shared-helper and user-form fixes. The
  job-order helper error, empty Engineering page, and Charging/Tapping account
  gaps remain. Existing dashboard PDF/ledger behavior checks also passed.
- No full production write-cycle test, concurrent-posting test, or CSR/OIC/Cashier
  account login test was performed. Empty queues prevented record-level testing
  for Online Applications, New, and Connect. Successful page loads do not prove
  every modal, search, upload, export, or mutation works.

Reproduce the new checks from the repository root:

```powershell
dotnet run --project tests/WorkflowRegression
```

Detailed route and runtime evidence is in `.tmp/workflow-route-audit.json`,
`.tmp/workflow-smoke.json`, and `.tmp/workflow-browser.json`. The baseline
runtime files have the `-baseline` suffix. Audit scripts read login credentials
from process environment variables; do not put credentials in source files.
