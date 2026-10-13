# Phase 6J — Disconnection and reconnection monitoring

Completed **2026-10-13**, Asia/Manila. DEVELOPMENT / SYNTHETIC TESTING ONLY.

**Historical report:** sections 1–26 retain the original design and evidence. Section 27 supersedes their CS OIC-only disconnection authorization and unchanged Account.Status design. Section 28 authorizes physical reconnection and supersedes the earlier reconnection quarantine. Historical rollout boundaries remain dated evidence.

**Current source and isolated workflow checks PASS. Live authenticated development integration remains NOT VERIFIED.** Physical disconnection and physical reconnection confirmation are authorized to **CS OIC and SuperAdmin**, canonical roles **6 / 1**, subject to their server-side eligibility checks. The event table was already present before the amendments; neither its rollout nor a live data repair was performed by them. Full application service-workflow readiness remains **BLOCKED BY POLICY**. Production is **DEFERRED / NOT EVALUATED**. See sections 27–28 for current evidence.

## 1. Existing implementation inventory

| Component | Actual meaning and classification before Phase 6J |
|---|---|
| Account / Customer | IMPLEMENTED + ENABLED: account identity and customer relationship; AccountStatus is Pending / Suspended / Active / Closed. These values do not prove a physical event. Multiple accounts for one customer must remain independent. |
| Billing / Reading / ReadingSheet | IMPLEMENTED + ENABLED: PartialPost persists issuance date in Billing.BillingPeriod, links a completed reading and freezes the issued snapshot. One qualifying water-bill month is not an Other Charge anchor or penalty. |
| ReadingSheet.DisconnectionDate | IMPLEMENTED + ENABLED for existing invoice/SOA scheduling. It is a scheduling field, not evidence of physical disconnection or the new second-bill clock. It remains unchanged. |
| Payment / cancellation / Bill Adjustment | IMPLEMENTED + ENABLED: authoritative current RemainingAmount, persisted penalties and collectible children; cancellation restores BalanceBefore. Adjustment audit/history/idempotency remains intact. |
| OtherCharge / OtherChargeInstallment | IMPLEMENTED + ENABLED: draft/posted parent contract and authoritative installment children. Current children are collectible; future draft children are excluded. |
| Collections / Account Ledger | IMPLEMENTED + ENABLED: GetCollectionBill is the common financial projection; Ledger sums its AmountDue. Exact-account requests and the Customer Information context fix remain intact. |
| Dashboard disconnection lookups/orders | IMPLEMENTED BUT QUARANTINED: DashboardController routes carry PendingAuthorizationDecision; DashboardTransaction and MVC wrappers exist. Reads/writes are not enabled by this phase. |
| Legacy Disconnection page / disconnection.js / DisconnectionOrder | PARTIAL, implemented but quarantined. Client supplies the date, fee and action; Accomplished sets Account.Status to Suspended. ReconnectionFee is merely an order field; no collectible child, stable confirmation receipt or reconnection audit is created. The table is absent on inspected development. Not reused as physical-event evidence. |
| ServiceRequest / Request / JobOrder | PARTIAL, implemented but quarantined at HTTP boundaries. DISCONNECTION and RECONNECTION are selectable descriptions, not an authoritative financial/physical lifecycle. Existing guarded service fixtures exercise the underlying creation/update behavior. |
| Workflow / Inspection / Installation | IMPLEMENTED BUT QUARANTINED at related Request/JobOrder/Inspection/Workflow/Dashboard boundaries. General workflow authorization and completion ownership remain unresolved. No unrelated quarantine was removed. |
| Second-bill monitoring / physical reconnection / fee-per-event linkage | NOT IMPLEMENTED before this phase. New source below supplies narrowly scoped monitoring and event persistence; deployment is still pending. |

Trace: existing CustomerSupport Disconnection view → disconnection.js → MVC wrappers → DashboardTransaction → quarantined DashboardController → disconnection_orders. New monitoring follows MVC → BillingTransaction shared HTTP client → DisconnectionMonitoringController → DisconnectionMonitoringService → existing financial services / MySQL. No persistence was moved into MVC or browser code.

## 2–6. Authoritative rule, unpaid detection and five-day clock

The retired three-month assumption is not used. A bill qualifies using current **CollectionBill.RemainingAmount**, which is derived from persisted **Billing.RemainingAmount** under existing paid/waived rules. Original TotalBillAmount, recursive arrears and snapshot TotalDue are not unpaid tests. A 190 bill adjusted by 30 remains an authoritative 160 obligation.

Monthly candidates must belong to the exact account, have a linked completed reading for that account, and have a non-deleted reading sheet. Charge anchors without a completed reading do not count. Periods are ordered by persisted BillingPeriod, never Billing.Id or invoice sequence. PartialPost stores the issuance date in BillingPeriod; monitoring deliberately does not re-read a mutable sheet scheduling date as the historical trigger. Duplicate candidate months require financial/service review and block confirmation rather than selecting an arbitrary row. Skipped months and future issuance dates do not create a qualifying adjacent pair.

When the immediately preceding consecutive monthly bill still has RemainingAmount > 0 and the next bill has issued:

```text
SubjectDate = second bill's persisted issuance date
ScheduledDisconnectionDate = SubjectDate + 5 calendar days
```

Nov 8 is Day 0; Nov 13 is Day 5. DaysRemaining is clamped at zero. Neither second DueDate nor penalty eligibility is involved. Nov 8–12 is SUBJECT FOR DISCONNECTION; Nov 13 onward is FOR DISCONNECTION while unresolved. Explicit DateOnly evaluation and UTC audit timestamps make service tests deterministic; HTTP boundaries obtain the Manila date from the established UTC+8 convention.

**A date never creates an event, fee or DISCONNECTED physical status.** Monitoring has a separate PhysicalServiceStatus: NO CONFIRMED EVENT, DISCONNECTED or RECONNECTED, derived solely from explicit event timestamps. If nobody confirms the physical work, an otherwise qualifying account remains FOR DISCONNECTION indefinitely.

Before actual disconnection, settling the qualifying prior obligation removes that pair's eligibility. Cancellation recomputes it from restored current balances. A zero-outstanding-water account cannot qualify. A stricter organizational requirement to clear every water bill before disconnection, rather than the qualifying prior bill, remains POLICY REQUIRED; this phase does not invent it.

## 7–8. Authorization, confirmation and audit

| Operation | Effective boundary |
|---|---|
| View monitoring / reconnection eligibility | Existing BillingOperations policy: canonical roles 1 / 2 / 6 / 7. These are existing financial read owners. |
| Confirm actual disconnection | New narrow ConfirmDisconnection policy: **CS OIC only**, canonical enum value 6, explicitly assigned by the user. SuperAdmin is not silently added. |
| Create reconnection fee | Server-only consequence of successful disconnection, inside its transaction. No independent client fee command. |
| Confirm physical reconnection | PendingAuthorizationDecision: anonymous 401, all authenticated roles 403 until ownership is explicitly assigned. |

New Data routes are GET `api/DisconnectionMonitoring`, GET `api/DisconnectionMonitoring/{accountNumber}`, POST `api/DisconnectionMonitoring/{accountNumber}/disconnect`, and POST `api/DisconnectionMonitoring/{accountNumber}/reconnect`. POST bodies contain only an explicit nonempty operation GUID. They do not accept an actor, physical timestamp, balance, fee, arbitrary status or billing references as authority. The Data controller derives actor identity from validated claims and timestamps from the server. Existing JWT issuer/audience/signature/expiry and persisted-user fingerprint validation remain unchanged.

MVC disconnection submission checks the authenticated session's CS OIC role, requires CSRF, and delegates through the existing per-request signed HTTP bridge. Data independently authorizes the operation. Unknown accounts return 404, invalid identities/filters 400, ineligible/reconciliation conflicts 409, missing event schema 503 and unexpected failures a safe 500 message. No raw secrets or stack traces are returned.

The service resolves account identity before opening the transaction, then locks authoritative account billings and the account. The first consistent financial/event read occurs after the financial locks are acquired; otherwise RepeatableRead could retain a stale pre-lock snapshot. Confirmation reevaluates eligibility and Day 5 under those locks. It commits the event, fee parent and immediate child atomically. Failure rolls back all writes. Legacy customer/activity/account statuses, historical Billing, invoice identity and snapshots are not rewritten.

The event records the stable operation/event GUID, exact account ID, first and second billing IDs/references, subject and scheduled dates, actual server UTC timestamp, DisconnectedBy and fee parent ID. Reconnection fields record its separate operation GUID, server observation/confirmation timestamps, ReconnectedBy and successful settlement payment IDs. ReconnectionEligibleDateUtc is observed at confirmation, not a fabricated historical payment timestamp. Reads never write eligibility timestamps.

## 9–13. PHP 350 fee, Phase 6G and reconnection gate

Actual confirmed disconnection creates one **350.00 decimal** OtherCharge with PayableIn `1`, InstallmentCount `1`, plus one immediately activated unpaid child. It links to the triggering existing bill. There is no fabricated billing or zero-value anchor. The existing OtherChargeService owns this creation; the helper requires an enclosing transaction and server actor. It does not advance an unrelated new-connection Charging workflow.

The helper resolves exactly one configured `Reconnection Fee` type by its established name and fails safely if missing/ambiguous. It never assumes a magic type ID or trusts client/configured default amounts. Development read-only inspection found existing type **31**, default **350**, so no fee-type seed was performed.

The fee never changes water BillAmount, maintenance, RemainingAmount, invoice snapshot or water-only penalty basis. It participates through the existing child collection model. Current collection projections are reused for all bills, including paid water bills which still anchor collectible children. The monitoring DTO separates the linked event's ReconnectionFeeBalance from other collectible charges for display; TotalCollectibleBalance remains the sum of authoritative CollectionBill.AmountDue, not a parallel balance engine.

Reconnection eligibility requires **zero total currently collectible balance** and a paid linked 350 parent/child with settlement evidence. This includes outstanding water RemainingAmount, active collectible penalties and current child installments; future installments, parent contract totals, historical arrears and maintenance counted again are excluded.

The tested example reconciles **397 water + 17 persisted penalty + 250 current child + 350 fee = 1,014**, identically in Collections-derived projection and Ledger. Paying the water alone leaves the fee pending; paying the fee alone cannot bypass another obligation. All required settlement yields ELIGIBLE FOR RECONNECTION while PhysicalServiceStatus remains DISCONNECTED. Only a separate valid physical confirmation can record RECONNECTED; its source service is tested, but its HTTP boundary remains quarantined pending ownership.

## 14–18. Cancellation and financial preservation

Cancellation before physical reconnection restores balances/children using existing payment logic and returns eligibility to BALANCE + RECONNECTION FEE PENDING. No stale persisted eligibility flag survives.

Cancellation of a recorded successful settlement payment after physical reconnection raises **FINANCIAL / SERVICE STATE REVIEW REQUIRED**. ActualReconnectionDateUtc and PhysicalServiceStatus remain RECONNECTED. No software-driven physical reversal occurs. The review flag persists for that recorded reversal; resolving the exception needs an organizational process, not automatic service-state changes.

Bill Adjustment current RemainingAmount, creation/update audit and durable receipts are reused without modification. The legacy adjustment floor is unchanged. Penalties remain water-only 10% under LatePaymentPenalty.Calculate; no new penalty applies to the fee. Maintenance remains in the original water obligation exactly once. Other Charges retain parent/child ownership and future-installment exclusions.

Ledger's financial/history services were not changed to require the new event table. Existing Other Charge creation/payment/cancellation history already shows fee financial events. Physical audit is displayed by monitoring; adding separate physical events to Ledger History can follow a reviewed deployment without making existing Ledger GET fail before migration. The exact requested account contract remains protected.

## 19–20. Customer Information and monitoring UI

The new `CustomerSupport/DisconnectionMonitoring` page replaces the sidebar's legacy order-entry link. It shows account/customer/address, source bill references/dates, subject/scheduled dates, nonnegative days remaining, separate financial categories, monitoring status, physical evidence and exact-account Ledger/Account links. Account-number/customer-name, zone/book and status filters use supported data. Queries exceeding 100 matching accounts fail visibly and ask for a narrower search; the bounded per-account financial reads are an explicit documented limit, not an unbounded whole-district scan.

Only the server-rendered CS OIC role receives a Confirm physical disconnection action, and only for a server-projected FOR DISCONNECTION row. The dialog explicitly asks whether physical work is completed, initially focuses Cancel, and does not submit on opening/cancellation. Data still reevaluates every submission independently.

The browser disables confirmation while in flight, blocks double-submit and retains a cryptographic operation GUID in sessionStorage across uncertain failure/reload/retry. A successful confirmation response, or an authenticated monitoring response containing that exact confirmed event ID, acknowledges the identity. This also prevents a lost response from leaving an old key attached to a later genuine disconnection cycle. Storage/crypto failure prevents an unidentifiable submission. Database identities/constraints are authoritative; browser storage is only a retry aid. Reconnection buttons and unrelated quarantined actions are not exposed.

Customer Information's existing selected-account setter also refreshes a read-only service indicator by exact AccountNumber. Responses are versioned to prevent stale status updates. Missing schema/access displays monitoring unavailable while the existing account and Ledger remain usable. Monitoring's View Account link includes the exact number; MVC accepts that optional selector only if it belongs to the specified customer's account list, with no default-account substitution on an unknown explicit selection.

## 21–22. Concurrency, idempotency and migration

Same disconnection GUID/account/actor returns the original event/audit without new liability. Reusing it for a different account or actor conflicts. Distinct competing confirmations for an already disconnected account do not create another event/fee. Reconnection has a separate durable operation identity and preserves the original successful response/audit on replay. A later genuine monthly cycle may create one new event/fee while retaining older-event replay.

Six real two-connection races hold the winner open and verify the competitor waits:

1. Same-key disconnection → one event/fee and original ID on replay.
2. Different-key disconnections → one event/fee; competing new intent conflicts.
3. Payment wins → resolved qualifying obligation; confirmation rejected.
4. Disconnection wins → one confirmed event/fee; subsequent payment preserves physical evidence.
5. Cancellation wins before reconnection → reconnection rejected, payment pending.
6. Reconnection wins before cancellation → physical timestamp preserved, manual review flagged.

These implement existing lock serialization and the user-defined physical/financial distinction. No new Finance business priority was invented.

Additive artifact: [20261012_add_service_disconnection_events.sql](../TMCWD/TMCWD.Data/Database/20261012_add_service_disconnection_events.sql), prepared during the Oct 12 work. It creates only `service_disconnection_events`; no financial columns are altered and no historical events are guessed/backfilled. Primary GUID identity, unique active account via a stored generated column, unique account/second-bill source, unique fee linkage and unique reconnection operation protect lifecycle multiplicities. Financial records are not cascaded/deleted by the script. CREATE IF NOT EXISTS supports additive rerun, not repair of an incompatible existing table.

The exact script was executed twice through the existing synthetic table interceptor against owned prefixed tables in **tmcwd_test**. Those ordinary fixture tables are cleaned up by their owner; normal regression tables remain connection-local temporary tables. **No new development migration was applied. No startup migration was added.** Missing event schema fails closed before physical/fee mutation.

**DEVELOPMENT MIGRATION REQUIRED.** Separately review/authorize this exact script and exact target, reconfirm server identity/schema/baseline and the one configured fee type, coordinate Data/MVC deployment, then exercise real authenticated CS OIC submission/retry and denied-role behavior. Do not infer events from legacy Suspended/Closed flags or order descriptions. Reconnection-role authorization is a separate policy decision.

## 23. Actual verification

| Check | Result |
|---|---|
| Solution / final BillingRegression builds using isolated artifacts | PASS, zero errors; existing warnings retained, no warning suppression or dependency changes |
| Full BillingRegression with development read-only, adjustment and disconnection options | PASS; **654** temporary-table groups in the summary, including **25** deterministic Phase 6J service groups |
| Real HTTP Phase 6J boundaries | PASS: exact-account reads, other-role denial, CS OIC confirmation/replay, claim-derived actor despite body 999, server timestamp/350 amount, stale fingerprint denial, actual MVC CSRF/signed-client bridge |
| Existing optional adjustment workflow | **21/21 PASS**, zero failures; reported separately after the main summary |
| Existing invoice/payment/installment migration-concurrency suites | PASS; no `--no-migrations` exclusions in the final full run |
| Existing adjustment concurrency | Four race cases PASS; additive rerun PASS |
| Phase 6J migration / concurrency | Exact additive script rerun PASS; **six** held-transaction races PASS |
| Main / penalty / adjustment browser tests | **25 / 5 / 12 PASS**, none skipped |
| Existing Collections/Ledger browser | **8 scenarios / 31 Ledger checks**, plus **6** light/dark viewport cases PASS |
| Customer Information browser | **8** account/status/Ledger scenarios PASS |
| New monitoring browser | **16** scenario groups PASS: six read-only viewport/theme cases, six CS OIC dialog viewport/theme cases, empty/error/filter recovery, double-submit/uncertain reload/retry acknowledgement, lost-response acknowledgement/later-cycle identity, unavailable storage |
| MeterReaderAuthRegression | Build and **43 assertions PASS** |
| Guarded ServiceRequestRegression | Build and **44 assertions PASS**; unrelated HTTP workflows remain quarantined |
| Flutter | NOT RERUN: mobile production source/contracts were not changed |
| Live authenticated UI / operational physical work | NOT VERIFIED / NOT EXECUTED |

Browser sizes were **1440×900**, **768×1024**, **390×844**, light and dark. Tests execute the actual monitoring markup/scripts and shared Ledger with synthetic HTTP; they verify safe text, exact account requests/headers, loading/error/empty/retry states, status/date/days, overflow containment, dialog focus/fit and absence of unauthorized physical buttons. They are not live authenticated deployment evidence.

An early same-key race exposed a pre-lock RepeatableRead snapshot; that source defect was corrected before all six races passed. Initial fixture route/layout waits and tablet overflow were corrected before passing browser runs. Failed intermediate runs are not counted as passes.

Evidence is retained under ignored `.tmp/phase6j-*` logs/artifact directories and `.tmp/phase6j-development-inspection.json`. Final verification commands include:

```powershell
dotnet build TMCWD/TMCWD.slnx --artifacts-path .tmp/phase6j-artifacts
dotnet build tests/BillingRegression --artifacts-path .tmp/phase6j-artifacts
dotnet .tmp/phase6j-artifacts/bin/BillingRegression/debug/BillingRegression.dll . --verify-adjustment-workflow --verify-disconnection-workflow --verify-development-deployment
node tests/BillingRegression/disconnection-browser-regression.cjs
node tests/BillingRegression/customer-information-browser-regression.cjs
```

## 24. Development preservation and remaining decisions

Read-only identity: **127.0.0.1:3307/tmcwd**, MySQL **8.0.46**, server UUID **c3f950fe-b565-11f1-b6a6-30560f67a354**. Counts remained **30 billings / 230 readings / 16 sheets / 8 payments / 5 penalties / 0 Other Charges / 0 installments / 13 issued snapshots**. Current sums remained **6766.30 / 5045.55 / 717.00 / 15.75** for TotalBillAmount / RemainingAmount / BillArrearsAmount / BillingAdjustment. Every inspected raw issued-snapshot digest and relevant schema stayed identical before/after the final gate.

These current sums differ from the historical C1 report; no claim is made that historical totals still describe today's development state. Inspection found `billing_adjustment_operations` already present before Phase 6J. Neither its rollout nor prior financial changes were performed by this task. The new event table and legacy disconnection_orders remain absent; no Phase 6J development event, fee, payment, cancellation, user/customer edit or schema write occurred. Git was not reset/cleaned and unrelated changes were preserved.

Remaining: separately authorized development event migration/coordinated rollout; real authenticated CS OIC UI/network verification; assignment of physical reconnection ownership; legacy physical-state reconciliation before enabling confirmations for uncertain accounts; organizational resolution of post-reconnection payment reversals; any stricter pre-disconnection settlement/approval/race-priority policy. General service workflows retain their existing quarantines.

## 25–26. Separate final verdicts

| Gate | Verdict |
|---|---|
| PHASE 6J DISCONNECTION MONITORING | **PARTIAL** development rollout; source/synthetic checks PASS |
| SECOND-BILL SUBJECT-FOR-DISCONNECTION RULE | **PASS** source/fixtures |
| 5TH-DAY DISCONNECTION RULE | **PASS** source/fixtures; never automatic physical disconnection |
| ACTUAL DISCONNECTION CONFIRMATION | **PASS** source/HTTP/fixtures; CS OIC only; live rollout not verified |
| PHP 350 RECONNECTION FEE | **MIGRATION REQUIRED** for development; single atomic fee source/fixtures PASS |
| ALL-BALANCE RECONNECTION GATE | **PASS** source/fixtures |
| COLLECTIONS / LEDGER RECONCILIATION | **PASS** source/fixtures |
| DISCONNECTION IDEMPOTENCY | **PASS** source/HTTP/real races |
| RECONNECTION IDEMPOTENCY | **PASS** underlying service/fixtures; public confirmation remains POLICY REQUIRED |
| LIVE AUTHENTICATED UI | **NOT VERIFIED** |
| FULL APPLICATION SERVICE-WORKFLOW READINESS | **BLOCKED BY POLICY** |
| PRODUCTION | **DEFERRED / NOT EVALUATED** |

Work stops at source, safe tests and the reviewed migration artifact. Development deployment or production readiness is not declared complete.

## 27. Stakeholder amendments — SuperAdmin authorization and operational status

Amended **2026-10-13**, Asia/Manila. Source and synthetic testing only. No live data correction or new migration was performed by this amendment.

> Confirmed physical disconnection changes Account.Status from Active to Suspended. User-facing service status displays DISCONNECTED. Confirmed physical reconnection restores Account.Status to Active. Calendar eligibility alone never changes Account.Status.

The stakeholder additionally authorized **Customer Service OIC and SuperAdmin** to confirm physical disconnection. The earlier CS OIC-only rule above is superseded. The Data ConfirmDisconnection policy now requires either canonical enum role 6 or 1. MVC checks the same authenticated-session roles, and the actual Razor view exposes its permission flag for those two roles only. JavaScript then shows confirmation only on FOR DISCONNECTION rows. Current, First Bill Unpaid and Subject for Disconnection rows offer no physical-confirmation button to either role. Cashier, Customer Representative, Meter Reader, Engineer and Guest receive no button and a direct authorized-format POST is denied with 403. Anonymous API requests receive 401; MVC anonymous requests with valid CSRF also receive 401. Existing CSRF enforcement is retained.

The stakeholder's SuperAdmin operational-authority principle applies to this established action. Unrelated PendingAuthorizationDecision boundaries and the separately unresolved physical reconnection ownership are unchanged; this amendment does not globally unquarantine service workflows.

### Atomic account-state ownership

The existing locked transaction now commits the disconnection event, exactly one 350 fee parent/child and Account.Status = Suspended together. Account UpdatedBy and DateUpdated are server-owned; creation audit is preserved. Replays do not repeatedly save/update the account. The underlying reconnection transaction records the physical reconnection timestamp/actor and restores Active in its same final save, only after the existing all-collectible-balance gate passes. Payment, eligibility evaluation and cancellation never reactivate or suspend the account independently.

There is no new AccountStatus value, schema column or monetary formula. Pending / Suspended / Active / Closed remain the existing enum. Injected failures after final event/account SQL saves verify rollback of all disconnection state and of reconnection/Active restoration, in addition to the existing early-fee failure check.

### Presentation and reading/collection boundaries

Ledger retains its persisted operational status internally, exposes separate physical evidence, and displays **Disconnected** when the latest authoritative confirmed event has no actual reconnection timestamp. It never infers physical state from Suspended alone. An actual reconnection returns the operational Active label and separate RECONNECTED physical evidence. Financial Ledger projection/calculation is unchanged. A pre-Phase-6J missing event table preserves the old Ledger with physical evidence marked UNAVAILABLE; malformed or other database failures are not silently treated as missing schema.

Customer Information's exact-account status response now includes OperationalAccountStatus and displays it alongside the existing physical/service indicator. Shared event lookup belongs to the existing Data monitoring service; MVC/browser code still do not access persistence.

The existing MVC reading checks and mobile reading roster require Active accounts. Inspection also found that the shared web Data ReadingService.SaveCore did not explicitly reject Suspended accounts, so a narrow Suspended guard closes that direct-API path. Regression coverage verifies both web save and mobile sync rejection without a new reading, restoration of the active roster/normal reading after physical reconnection, and preservation of historical readings. Other existing account-status behavior and billing posting rules were not redesigned.

AccountService.Search has no Active-only filter. A suspended/disconnected account therefore remains searchable for Collections and can settle water, active penalties, current children and its linked fee. The existing 1,014 reconciliation fixture is still payable after suspension, excludes future children and leaves operational status Suspended on payment alone. Cancellation after reconnection retains Active/RECONNECTED and raises the existing manual-review flag.

### Claim audit and cross-role idempotency

The Data action continues deriving actor ID from the validated NameIdentifier claim. There is no hardcoded admin ID, role-as-actor conversion or client/route userId authority. Real HTTP tests use persisted synthetic CS OIC user **2 / role 6** and SuperAdmin user **3 / role 1**, with forged body userId/disconnectedBy 999, fee and timestamp. Both API and actual MVC-to-signed-API submissions audit the corresponding user ID. Revoked fingerprints for both identities are denied before writes.

Both first-actor orderings are tested: CS OIC → SuperAdmin and SuperAdmin → CS OIC. Same-actor replay succeeds; another actor reusing the first actor's GUID conflicts, and a new GUID for an already active event also conflicts. These 409 outcomes preserve the original audit rather than impersonating the first actor. Every path leaves one event, one 350 fee/child and Suspended. Two additional held-transaction cross-role races verify the same invariant, bringing the Phase 6J concurrency suite to eight races.

### Amendment verification and development safety

| Check | Actual result |
|---|---|
| Final solution / BillingRegression build | PASS; zero errors, warnings not suppressed |
| Full fixture gate with adjustment, disconnection and development-read options | **661** temporary-table groups PASS, including actual rendered MVC permissions, both authorized API/MVC actors, both denied-role boundaries, two revoked fingerprints and operational-state regressions |
| Optional existing adjustment workflow | **21/21 PASS**; separately reported after the main summary |
| Phase 6J concurrency | **8 PASS**: six original races plus both cross-role orderings, now checking operational status as well as event/fee/audit |
| Existing invoice/payment/installment and adjustment concurrency/migration suites | PASS; none skipped in the final full run |
| Main / penalty / adjustment browser | **25 / 5 / 12 PASS**, none skipped |
| Collections/Ledger browser | Existing **8 scenarios / 31 checks / 6 viewport-theme cases PASS** |
| Monitoring browser | **21 scenario groups PASS**: 16 previous groups plus five required role/state visibility matrix groups; actual shared Ledger displays Disconnected after confirmation |
| Customer Information browser | **8 exact-account/status/Ledger scenarios PASS** |
| Flutter | NOT RERUN; no mobile production source or DTO contract changed |
| Live authenticated deployment / corrective data write | NOT VERIFIED / NOT EXECUTED |

Read-only development identity remained the same reviewed `127.0.0.1:3307/tmcwd`, MySQL 8.0.46 and server UUID. Its current pre-amendment baseline is **32 billings / 232 readings / 18 sheets / 8 payments / 6 penalties / 1 Other Charge / 1 installment / 15 snapshots**, sums **7146.30 / 5425.55 / 924.00 / 15.75**. Counts, sums, every inspected raw issued-snapshot digest, account status/audit digests, physical-event digests and inspected schemas match before/after the final tests. This baseline reflects activity/deployment preceding the amendment, not writes performed by it. In particular the new event table is now present; the original report's absent-table evidence remains historical.

No new schema is needed for this amendment. Existing confirmations produced by the old source are **not automatically backfilled**: their event evidence now supports the Disconnected Ledger label, but an existing Active operational row is not silently rewritten by GET or a rejected cross-actor attempt. Such operational inconsistencies need separately reviewed, explicitly authorized reconciliation; the source change atomically fixes new confirmations. No live row, including the reported account, was repaired merely to test this code.

Evidence: ignored `.tmp/phase6j-amendment-build.log`, `solution.log`, `regression.log`, `browser.log`, `penalty.log`, `adjustment-browser.log`, `collections.log` and the read-only inspection artifact; interactive tool output records the final monitoring and Customer Information browser runs. Original regression evidence above remains dated historical evidence.

| Required amendment verdict | Result |
|---|---|
| SUPERADMIN CONFIRM-DISCONNECTION AUTHORIZATION | **PASS** API, MVC, rendered permissions and browser |
| CS OIC CONFIRM-DISCONNECTION AUTHORIZATION | **PASS** API, MVC, rendered permissions and browser |
| OTHER-ROLE DENIAL | **PASS**; anonymous API 401, other roles 403 |
| CLAIM-DERIVED DISCONNECTED-BY AUDIT | **PASS**; persisted user ID, no client/role/hardcoded actor |
| CROSS-ROLE IDEMPOTENCY | **PASS** sequential HTTP and real two-connection races |
| ACCOUNT STATUS ON PHYSICAL DISCONNECTION | **PASS** source/fixtures |
| LEDGER DISCONNECTED DISPLAY | **PASS** source/fixtures/browser |
| COLLECTIONS WHILE DISCONNECTED | **PASS** source/fixtures and preservation browser |
| READING BLOCK WHILE DISCONNECTED | **PASS** web/mobile server paths |
| ATOMIC EVENT + FEE + ACCOUNT STATUS | **PASS** including final-SQL rollback |
| RECONNECTION RESTORES ACTIVE | **POLICY BLOCKED** at public confirmation boundary; underlying transaction/restoration/rollback fixtures PASS |

Live authenticated amendment rollout remains NOT VERIFIED. Full general service-workflow readiness remains BLOCKED BY POLICY; production remains DEFERRED / NOT EVALUATED.

## 28. Phase 6J-F — physical reconnection authorization and verification

Amended **2026-10-13**, Asia/Manila. SOURCE / SYNTHETIC TESTING ONLY.

> Physical reconnection confirmation is authorized to Customer Service OIC and SuperAdmin. Financial settlement alone does not restore service. An authorized staff member must confirm the actual physical reconnection.

The narrow Data **ConfirmReconnection** policy allows canonical roles 6 and 1 only. The existing reconnection endpoint uses that policy instead of PendingAuthorizationDecision. MVC independently checks the same authenticated session roles, requires CSRF and submits the unchanged operation-GUID request through BillingTransaction and the signed API bridge. Anonymous API/MVC confirmations are 401; Cashier, Customer Representative, Meter Reader, Engineer, Guest and other roles are 403. Both actors' revoked fingerprints remain denied. No unrelated quarantine was removed.

The existing bill-first/account lock order is preserved. Inside that transaction the service rechecks financial eligibility and **Suspended** operational status, explicitly locks and revalidates the active event, then records the server UTC reconnection time, validated claim actor, operation GUID and settlement payment evidence while restoring Active and server audit fields in the same save/commit. Matching same-actor replay is resolved before the new-operation Suspended guard, so a successful retry still returns the original result after activation. Another actor's same GUID or a new GUID against the already reconnected event conflicts. Reconnection creates no new fee and does not change financial projections or history.

Monitoring exposes Confirm Physical Reconnection only for an ELIGIBLE FOR RECONNECTION row and an authorized server-rendered permission flag. It reuses the existing modal, Cancel focus, keyboard trap, in-flight close guard and double-submit protection. The dialog shows exact account/customer, zero outstanding balance, paid fee and the physical-work warning. Reconnection keys are retained across uncertain retries and scoped to the account **and disconnection event**, preventing an older successful operation from being reused for a later cycle. An authoritative read of that completed event acknowledges its pending browser identity. This needs no request/response or schema change.

Customer Information retains its simplified two-card layout. Its existing exact-account read displays Active / RECONNECTED and the server-confirmed date inside Meter & Service. Ledger returns to Active using the existing authoritative physical evidence. Financial Ledger bills, Other Charges and History are compared before/after reconnection and remain identical, including the paid fee. Existing web/mobile reading rejection while Suspended and restoration after Active pass. Cancellation before reconnection revokes eligibility; cancellation afterward retains Active / RECONNECTED and raises FINANCIAL / SERVICE STATE REVIEW REQUIRED.

### Actual verification

- Solution build: **PASS**, zero errors. Final BillingRegression build: **PASS**, zero errors; existing warnings and unavailable NuGet audit-feed warnings were not suppressed.
- Full guarded regression with adjustment, disconnection and development-read options: **663 temporary-table groups PASS**, exit 0. Includes actual API and MVC-to-signed-API reconnection for persisted CS OIC ID **2** and SuperAdmin ID **3**, forged actor/time rejection, denied roles, CSRF, replay and both cross-role orderings.
- Existing adjustment workflow: **21/21 PASS**, reported separately after the baseline summary.
- Phase 6J concurrency: **10 real two-connection races PASS**. The two added reconnection cross-role races hold the winner open and prove the competitor waits, retaining the original actor, one event/fee and Active. Existing migration reruns and invoice/payment/installment/adjustment races also pass; none were excluded.
- Monitoring browser: **39 scenario groups PASS**, including six reconnection dialog viewport/theme cases, role/state visibility, uncertain retry, double-submit, success refresh, completed-event acknowledgement of a lost response, shared Ledger Active and a fresh later-cycle GUID.
- Customer Information browser: **22 scenario groups PASS**, including Active / RECONNECTED / confirmed date. Existing Collections/Ledger browser: **8 scenarios / 31 Ledger checks / 6 viewport-theme cases PASS**.
- Viewports: **1440×900, 768×1024, 390×844**, light and dark. Browser HTTP is synthetic; these are not live login or field-work evidence.

Evidence: ignored `.tmp/phase6jf-build.log`, `phase6jf-solution.log`, `phase6jf-regression.log`, `phase6jf-customer-browser.log`, `phase6jf-collections-browser.log`; monitoring browser output and screenshots under its owned ignored profile directory.

Read-only development preservation passed before/after the final gate: **127.0.0.1:3307/tmcwd**, MySQL **8.0.46**, UUID **c3f950fe-b565-11f1-b6a6-30560f67a354**; **32 billings / 232 readings / 18 sheets / 8 payments / 6 penalties / 1 Other Charge / 1 installment / 15 snapshots**; sums **7146.30 / 5425.55 / 924.00 / 15.75**. Inspected account/event audits, issued-snapshot digests and schema also stayed unchanged. No development operational confirmation, settlement, cancellation, corrective row write or migration was executed. Existing historical operational inconsistencies are not silently repaired by a read or replay.

| Phase 6J-F verdict | Result |
|---|---|
| PHYSICAL RECONNECTION AUTHORIZATION | **PASS** source/API/MVC/browser |
| CS OIC RECONNECTION | **PASS** fixtures/HTTP |
| SUPERADMIN RECONNECTION | **PASS** fixtures/HTTP |
| OTHER ROLE DENIAL | **PASS** API/MVC/rendered permissions |
| ALL-BALANCE RECONNECTION GATE | **PASS**; unpaid obligations, water-only settlement and paid fee with another obligation remaining reject |
| RECONNECTION CLAIM AUDIT | **PASS**; persisted IDs 2 / 3, server UTC timestamps |
| RECONNECTION IDEMPOTENCY | **PASS** same-actor replay, both cross-role orders and real races |
| ACCOUNT STATUS RESTORED ACTIVE | **PASS**, including final-SQL rollback |
| CUSTOMER INFORMATION RECONNECTED STATE | **PASS** synthetic browser |
| LEDGER ACTIVE AFTER RECONNECTION | **PASS** service/browser; financial history preserved |
| READING RESTORED AFTER RECONNECTION | **PASS** existing web/mobile server fixtures |
| POST-RECONNECTION CANCELLATION REVIEW | **PASS** service and real cancellation/reconnection races |
| LIVE AUTHENTICATED END-TO-END | **NOT VERIFIED** |
| PHASE 6J DEVELOPMENT INTEGRATION | **PARTIAL / NOT VERIFIED live**; source, synthetic and read-only preservation gates PASS |
| FULL APPLICATION SERVICE-WORKFLOW READINESS | **BLOCKED BY POLICY**, separate Phase 6S scope |
| PRODUCTION | **DEFERRED / NOT EVALUATED** |

Remaining manual development check: use a reviewed development/mock account with authoritative disconnection evidence and Suspended status; settle all required balances through normal Collections; verify eligibility; log in as CS OIC or SuperAdmin and confirm only after actual field work; inspect actor/time, Active / RECONNECTED in Customer Information and Ledger, restored reading eligibility and unchanged paid fee history. No LIVE PASS is claimed until this is actually performed. Flutter and broad unrelated service-workflow suites were not rerun; no mobile production contract was changed.
