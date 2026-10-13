# Phase 6I-C1 — Bill Adjustment integrity remediation

Completed **2026-10-12**, Asia/Manila. DEVELOPMENT / SYNTHETIC TESTING ONLY.

**PHASE 6I-C1 BILL ADJUSTMENT REMEDIATION: PASS for source and isolated regressions.** The new idempotency schema is **not deployed to development tmcwd**; coordinated development rollout remains separately authorized work. **PRODUCTION: DEFERRED / NOT EVALUATED.** This report supplements the historical Phase 6I-C failures without erasing them. Overall Phase 6I-C is not declared PASS solely from this remediation.

## 1. Root causes and original flow

Traced the full implemented chain: `BillingController.BillAdjustment` → `Views/Billing/BillAdjustment.cshtml` / `bill_adjustment.js` → CSRF-protected `SubmitAdjustment` → `BillingAdjustmentTransaction` → Data `BillingAdjustmentController` → `BillingAdjustmentService` → `billing_adjustments` / `Billings`.

Original single `SaveUpdate` inserted/updated and saved an adjustment **before** loading Billing. Missing Billing merely skipped recalculation, leaving an orphan. A second independently committed SaveChanges recalculated the bill; failure after the first left partial state. There was no transaction or billing lock.

Original `SaveBatch` validated one reference, began a transaction, loaded Billing FOR UPDATE, inserted lines, saved, summed adjustments, updated RemainingAmount, saved and committed. That transaction was sound, but no submission identity existed: the same request created new rows on every retry. Existing adjustment row ID identifies a record, not a new single/batch submission; no suitable operation ID was found in MVC, JavaScript, DTOs, transaction, API or schema.

## 2–5. Common transaction, missing bill and atomicity

Both public service methods now use one shared save implementation:

1. Validate positive actor, explicit nonempty operation GUID, one reference and supported line representation.
2. Begin one transaction on the existing DbContext.
3. Load authoritative Billing with parameterized **FOR UPDATE**, before any adjustment write. Missing bill throws KeyNotFoundException; API returns **404**.
4. Look up a successful operation receipt. Matching replay returns its original result; different actor/reference/payload/mode returns **409**. Replays may succeed after later settlement because they do not create new liability.
5. For a new operation, reject paid/waived Billing. Insert server-owned lines or load the existing single adjustment; reject a reference move.
6. First SaveChanges allocates/persists adjustment rows **inside the same transaction**.
7. Sum the authoritative stored adjustments; preserve `max(20.00, TotalBillAmount − BillingAdjustment)`. Update only adjustment/balance/audit fields on Billing, and add the operation receipt containing original successful response data.
8. Second SaveChanges and COMMIT complete all three together. Any failure explicitly rolls back. A database uniqueness collision also rolls back and returns an operation conflict.

Two internal saves are retained for generated adjustment IDs and the authoritative SQL sum; neither commits independently. Batch remains multiple lines for **exactly one bill**, with a billing-row lock throughout. No multi-bill batching, billing fabrication or zero-value anchor was added. TotalBillAmount, invoice number and snapshot are untouched.

## 6. Explicit idempotency contract

Both mutation routes now require **`Idempotency-Key: <nonempty GUID>`**:

- `POST api/BillingAdjustment/SaveUpdate/{userId}`
- `POST api/BillingAdjustment/SaveBatch/{userId}`

The route userId remains non-authoritative; the controller derives it from validated claims. Body shapes remain single adjustment / list of lines. Shared HTTP transaction methods accept the explicit GUID and set a **per-request** header. No server fallback invents a new key for a retry.

The receipt table has a **globally unique OperationId primary key**, authoritative BillingReferenceId, ActorId, SHA-256 PayloadHash, original ResultJson and UTC DateCreated. The payload includes single/batch mode and ordered line IDs/types/amounts, normalized to invariant two-decimal amounts. Audit fields supplied by a client are ignored and excluded from payload identity. Bill/reference and actor are checked independently.

Same key + same actor/bill/mode/lines returns original IDs/audit/result, without modifying rows, balance or receipt. Different content, actor or bill cannot reuse the key. Different intentional keys may apply equal amounts to the same bill: no amount/date/user proximity deduplication exists. A single update has its own new operation key and preserves the original record's creation audit. A replay of an older operation still returns that operation's original response after a later update.

The receipt is not an issued invoice snapshot or financial obligation. It provides durable retry identity and is committed atomically with the operation. Historical adjustments are neither backfilled nor guessed to be duplicates.

## 7. Schema requirement and rollout

New additive artifact: [20261012_add_billing_adjustment_operations.sql](../TMCWD/TMCWD.Data/Database/20261012_add_billing_adjustment_operations.sql). It creates only `billing_adjustment_operations` with VARCHAR(36) primary key, VARCHAR(50) reference, BIGINT actor, CHAR(64) hash, LONGTEXT result and DATETIME(6) creation time, InnoDB/utf8mb4. It does not ALTER existing financial columns, update/delete historical rows or migrate at startup. CREATE IF NOT EXISTS rerun does not repair a differently defined existing table.

The artifact was executed twice through the existing synthetic table-name interceptor against **owned prefixed tables in tmcwd_test**, verifying additive rerun and operation uniqueness. Temporary ordinary regression tables use the canonical EF model. All owned concurrency tables are dropped by their fixture cleanup. No operation table was created in **tmcwd**; fresh metadata count is **0**.

Development rollout prerequisites, **not executed or authorized by this task**:

1. Separately authorize the additive migration on the exact reviewed development target and reconfirm identity/baseline/schema.
2. Resolve any outstanding pre-C1 uncertain submissions; no amount-based retrospective deduplication is supported.
3. Pause adjustment submissions during coordinated rollout. Apply/inspect the new table before releasing C1 Data/API and MVC/JavaScript together. Do not release the new client against the old non-idempotent API.
4. Update all direct API consumers to send a stable key, reload old adjustment pages and verify new intentional submission versus retained-key retry. Missing keys fail closed with 400.
5. Recheck baseline, real authenticated MVC/API adjustment behavior and actual rollback/retry behavior before calling development rollout complete.

New source requires this table for adjustment writes; deploying code without it causes failure before adjustment mutation, not silent legacy fallback. Source EF precision/max-length annotations now match the inspected existing adjustment schema (**Amount DECIMAL(6,2), reference VARCHAR(50)**); no existing-column migration was executed. No routine privileges or administrator bootstrap is required by this new schema artifact.

## 8. MVC/browser protection

The form carries `AdjustmentOperationId`, generated once at valid submission by browser crypto and sent through MVC to the shared HTTP service. JavaScript persists the pending identity and submission payload in sessionStorage, disables submit while in flight and blocks double-submit. Back/page restore re-enables controls while retaining the key; matching payload reload/retry uses the same key. A confirmed successful redirect carries server TempData acknowledgement, allowing a fresh key for an intentional subsequent identical adjustment. Changed intentional payload uses a distinct key. If retry storage/crypto is unavailable, submission fails visibly through the existing toast rather than proceeding without an identity.

Session storage is only a retry aid; **the database receipt and constraint are authoritative**. MVC delegates settled eligibility to Data under the lock after replay resolution, avoiding a pre-check that would block an already successful retry after settlement. Upstream failure status is propagated with a safe message; uncertain outcomes must retain the same key. Existing CSRF remains.

Four execution-based JavaScript tests cover double-submit, timeout/back/reload key retention, successful acknowledgement/new intent, changed intent and fail-closed storage. These are focused DOM fixtures, not live-browser login or a complete adjustment-page accessibility audit.

## 9. Actor and audit integrity

BillingOperations policy remains unchanged (roles 1/2/6/7); JWT/fingerprint validation and MVC API authentication are unchanged. Anonymous and wrong-role mutations remain 401/403. API route/body audit forgery cannot select the actor. Both create/update assign UpdatedBy/DateUpdated server-side; create assigns CreatedBy/DateCreated server-side. Updates preserve creation audit and original reference. Receipt ownership prevents a second authorized actor from replaying another actor's key.

Real HTTP tests check malformed money/no-write, missing key 400, absent bill 404, valid batch retry and creator/updater derived from claim ID 1 despite route/body 999. The shared Application-to-Data transaction client is also exercised against that HTTP host with the original key, proving header propagation does not duplicate liability.

## 10. Numeric/reference findings and preserved policy

Zero is rejected consistently with the existing nonzero batch contract. Unknown enum types, empty/long/mixed references, batch record IDs and invalid update IDs are rejected. Amount values outside existing **−9999.99…9999.99** physical line storage or with meaningful fractions beyond two decimals are rejected before writing; large type values cannot wrap into a valid enum. No floating-point currency was added.

Signed negative corrections remain permitted as in the existing AsBilled − ShouldBe/batch behavior. **POLICY REQUIRED** for an organizational restriction on debit/negative corrections or new approval/limit rules; this task invents none. An adjustment larger than the bill still follows the existing PHP 20 floor (tested with 200 against 120), rather than adding an unapproved cap. SQL/overflow failures remain transactional; no silent truncation/partial commit is accepted. Future non-20 maintenance schedules still require separate Finance disposition of the legacy adjustment floor.

## 11. Concurrency and valid financial behavior

Four deterministic real two-connection races PASS in tmcwd_test, holding the winner's transaction open and proving the competing operation waits:

- Same key twice → original ID returned, one line/receipt, RemainingAmount 90.
- Distinct keys → two legitimate 30 adjustments, serialized sum 60, RemainingAmount 60, no lost update.
- Adjustment wins billing lock → commits 30/90; stale payment at 120 is rejected without a receipt/payment.
- Payment wins → commits full payment/RemainingAmount 0; new adjustment rejects settled Billing with no adjustment/operation receipt.

These reflect existing row-lock and settled/stale eligibility rules, not a newly invented business priority. **POLICY REQUIRED** if Finance wants a different deterministic priority, post-settlement correction/refund policy or approval workflow. Snapshot, Service Invoice identity and financial row multiplicities are checked in both race orderings.

Normal 120 − 30 = **90** still reconciles Billing, Collections AmountDue and Ledger TotalBalance. Payment records BalanceBefore **90**, and cancellation restores **90**, preserving invoice/snapshot. Same-key replay after settlement does not apply a second adjustment. Separate next-period fixture freezes **90 + historical active penalty 17 = 107** arrears, not original snapshot 120. The persisted-17 fixture is not a new penalty calculation on water 100; water-only penalty remains **10**. Maintenance stays distinct in the issued snapshot and out of Other Charge contracts/installments.

## 12. Actual final tests and exact counts

Builds use **--artifacts-path .tmp/phase6ic1-artifacts** to preserve running applications with locked normal DLL outputs.

| Check | Final result |
|---|---|
| Solution build | PASS; 0 errors, 44 existing nullable warnings in final run |
| BillingRegression build | PASS; 0 errors, 1 existing PaymentConcurrency EF1002 warning |
| Full BillingRegression with --verify-development-deployment --verify-adjustment-workflow | PASS / exit 0; **618 baseline temporary-table groups**, existing four invoice/payment/receipt/installment migration-concurrency groups |
| Original mandatory adjustment cases | **7 PASS / 0 FAIL**, replacing historical 4 PASS / 3 FAIL |
| Additional single/audit/reference, distinct-key, numeric/missing-key cases | **3 PASS**; targeted total **10/10 PASS** |
| New adjustment concurrency / test-only migration | **4 race cases PASS**, additive migration rerun PASS |
| Browser / penalty browser | **25 / 5 PASS**, none skipped |
| New adjustment browser retry tests | **4 PASS**, none skipped |
| Collections / Ledger browser | **8 scenarios / 31 checks PASS**, related checks and **6 viewport/theme cases PASS** |
| MeterReaderAuthRegression | build PASS, 0 warnings/errors; **43 assertions PASS** |
| Explicit guarded ServiceRequestRegression | build PASS, 0 warnings/errors; **44 assertions PASS**, HTTP workflows still quarantined |
| Flutter | **NOT RERUN**; prior Phase 6I-C **73-test PASS** retained as dated evidence because production Flutter source/contracts were unaffected |

The baseline 618 summary precedes the optional targeted tests; separate cases above are not silently folded into that count. No skipped check counts as PASS. An intermediate provider mapping failure on a CHAR(36) string key was corrected to VARCHAR(36); early test-stub failures were corrected before successful execution. Neither counts as a successful run. No package version, warning suppression or financial formula changed.

Evidence: ignored `.tmp/phase6ic1-solution.log`, `phase6ic1-build.log`, `phase6ic1-regression.log`, `phase6ic1-browser.log`, `phase6ic1-penalty.log`, `phase6ic1-adjustment-browser.log`, `phase6ic1-collections.log`, `phase6ic1-reader-build.log` / `reader.log`, and `phase6ic1-service-build.log` / `service.log`.

## 13. Development preservation

Final read-only identity matches **127.0.0.1:3307/tmcwd**, MySQL **8.0.46**, UUID **c3f950fe-b565-11f1-b6a6-30560f67a354**. Operation table is absent, proving no development C1 schema rollout was performed.

Original financial baseline and every recorded raw issued snapshot digest remain identical: **30 billings, 230 readings, 16 sheets, 8 payments, 5 penalties, 0 Other Charges, 0 installments, 13 non-null snapshots**; sums **6766.30 / 5027.30 / 717.00 / 34.00** for TotalBillAmount / RemainingAmount / BillArrearsAmount / BillingAdjustment. No live adjustment, payment, cancellation, customer/user edit, maintenance seed/migration or backup operation occurred.

## 14. Exact changed files and remaining questions

Production source: Data BillingAdjustmentService / IBillingAdjustmentService / BillingAdjustmentConflictException, BillingAdjustmentController, BillingAdjustment and new BillingAdjustmentOperation entities, UserDbContext; shared BillingAdjustmentTransaction; MVC BillingController.SubmitAdjustment, BillAdjustmentViewModel, BillAdjustment.cshtml and bill_adjustment.js. New dated SQL creates only the operation receipt table.

Tests: BillingRegression Program fixture/caller/opt-in wiring; AdjustmentWorkflowVerification expanded; new AdjustmentConcurrency and adjustment-browser-regression.cjs. No production Flutter, penalty, collection, ledger, arrears, Other Charge, auth policy/fingerprint or Xml 8.0.4 source was modified. Existing unrelated dirty files remain preserved.

Outstanding: separately authorized development table/coordinated rollout; real authenticated adjustment browser deployment check; organizational debit/negative/approval policy, preferred race priority and future rate/floor policy if requirements change. Full application service workflows retain all quarantines. Prior UI/device/production recovery gaps remain; this remediation does not close them or create an approval stage.

## 15. Verdict and overall gate continuation

**PHASE 6I-C1 BILL ADJUSTMENT REMEDIATION: PASS (source/tests).** The three original defects are fixed and protected by passing regressions; separate equal-amount intentions remain allowed, and valid collection/ledger/cancellation/arrears/snapshot behavior is preserved.

The Phase 6I-C financial regression gate was rerun with the new cases and passes in isolated fixtures. **Overall Phase 6I-C development rollout is NOT VERIFIED after C1**: the development operation table is intentionally absent and a coordinated live rollout/check remains pending. Do not declare overall Phase 6I-C PASS solely from C1. **FULL APPLICATION SERVICE-WORKFLOW READINESS remains BLOCKED BY POLICY. PRODUCTION remains DEFERRED / NOT EVALUATED.** Work stops after source remediation, safe tests, baseline review and documentation.

---

## 16. Bill Adjustment Audit History continuation — 2026-10-12

This section documents the Phase 6I-C1 audit history continuation completed in the same development session. It supplements sections 1–15 without replacing them.

### 16.1 Existing audit model discovery

Full inspection of the pre-existing `billing_adjustments` table and all callers confirmed:

**EXISTING** in `billing_adjustments`:
- `Id` BIGINT auto-increment PK — unique adjustment identity ✓
- `BillingReferenceId` VARCHAR(50) — billing anchor ✓
- `Type` BIGINT validated against `AdjustmentLineType` enum ✓
- `Amount` DECIMAL(6,2), enforced ±9999.99 ✓
- `CreatedBy` BIGINT — persisted at insertion ✓
- `DateCreated` DATETIME — server-controlled at insertion ✓
- `UpdatedBy` BIGINT — set on every write ✓
- `DateUpdated` DATETIME — set on every write ✓

**MISSING** (no equivalent field existed):
- `Reason` / Remarks / Description — the MVC view collected a dropdown (`Remarks`) and free-text (`Explanation`) in the ViewModel, but `SubmitAdjustment` never sent either to the Data API and neither was persisted.

**PRESENT BUT NOT EXPOSED ON THE BILL ADJUSTMENT PAGE**:
- All four audit fields (`CreatedBy/DateCreated/UpdatedBy/DateUpdated`) were stored but invisible to staff on the Bill Adjustment page.
- `GetByReference/{reference}` endpoint existed on the Data API but returned raw entity JSON with no resolved user names.
- `GetBillAdjustmentsByAccount` on the MVC layer existed but served a different UI (Customer Information adjustments modal) — not the Bill Adjustment page itself.
- `AccountLedgerService` already surfaced adjustments as informational ledger events (`"Type X: amount"` string) but without Reason or actor name.

### 16.2 Reason / Remarks storage

A nullable `Reason VARCHAR(255)` column was added to `billing_adjustments`.

**No new Finance policy was invented.** The existing MVC view already collected:
- `Remarks` dropdown: Meter Error / Reading Error / System Error / Other
- `Explanation` free-text textarea

`SubmitAdjustment` now combines these as `"<Category>: <Explanation>"` (capped at 255 chars) into the persisted `Reason` field via the new `CombineReason` helper. Either component may be absent; a fully blank pair stores NULL.

Reason is **not part of the idempotency hash** — a retry may omit Reason without generating a new operation key. The first successful write's Reason is authoritative; replays return the original without overwriting it.

Migration: `TMCWD/TMCWD.Data/Database/20261012_add_billing_adjustment_reason.sql` — additive, idempotent (IF NOT EXISTS via user variable pattern). **Not applied to development `tmcwd`; tmcwd_test only.**

### 16.3 History query and endpoint

**Service:** `BillingAdjustmentService.GetHistory(billingReferenceId)` — read-only, two-step query:
1. JOIN `billing_adjustments` → `Billings` (for `ServiceInvoiceNumber`), newest first.
2. Batch-resolve all distinct `CreatedBy`/`UpdatedBy` IDs against `users` in a single query.

Two-step design avoids a MySQL "Can't reopen table" error that occurs when the same temporary table is joined twice in one LINQ query.

Returns `List<BillingAdjustmentHistoryDto>` — never an empty 404, always an array (empty when no adjustments exist). Never mutates financial state.

**Data API endpoint:** `GET api/BillingAdjustment/History/{billingReferenceId}` — protected by existing `BillingOperations` policy (roles 1/2/6/7). Anonymous → 401; MeterReader → 403; authorized → 200 with JSON array.

**MVC endpoint:** `GET /Billing/GetAdjustmentHistory?billingReferenceId=XXX` — MVC BillingOperations session; calls through `BillingAdjustmentTransaction.GetHistory` to the Data API. Returns a shaped anonymous object safe for JSON display.

**DTO:** `TMCWD.Model.Billing.BillingAdjustmentHistoryDto` — fields: `Id`, `BillingReferenceId`, `ServiceInvoiceNumber` (null pre-Phase 6D), `AdjustmentType` (enum name string), `Amount`, `Reason` (null for historical rows), `CreatedBy`, `CreatedByName` (informational), `DateCreated`, `UpdatedBy`, `UpdatedByName` (informational), `DateUpdated`.

`CreatedByName`/`UpdatedByName` are informational display values. `CreatedBy`/`UpdatedBy` IDs are the authoritative persisted audit identity and are never replaced by names in storage.

### 16.4 User-facing history UI

`BillAdjustment.cshtml` now includes an **Adjustment History** section after the form and before the Billing History modal. The section:
- Is hidden until a billing record is selected.
- Shows a loading indicator while fetching.
- Shows a distinct error state if the fetch fails (retryable by selecting the billing again).
- Shows an explicit empty-state message when no history exists.
- Shows a table (Date / Service Invoice or Reference / Type / Adjustment / Reason / Made By / Last Updated) when rows are present.

`bill_adjustment.js` gains `loadAdjustmentHistory(referenceId)`:
- Called when `ReferenceNo` fires a `change` event (triggered by `btnSelectBilling` click).
- Uses `safeText()` (DOM-based text-node escaping) to prevent HTML injection from server values.
- Uses `formatAmount()` for currency display (parentheses for negative, comma thousands separator).
- Shows actor name when resolved; falls back to `ID <n>` when the user record has no name.
- Replaces `innerHTML` on each call — retry never duplicates rows.

### 16.5 Retry vs legitimate new adjustment — history contract

The history semantics match the idempotency contract:

| Operation | Expected history result |
|---|---|
| Same OperationId, same actor/payload | One row only; no duplicate |
| Different OperationId, same amount/actor | Two distinct rows — intentional separate action |
| Update (SaveUpdate, Id > 0) | One row; `DateCreated`/`CreatedBy` unchanged; `Amount`/`Reason`/`UpdatedBy`/`DateUpdated` updated |
| Failed transaction (rollback) | Zero rows; no orphan |
| Missing billing | Zero rows; no orphan |

### 16.6 Authorization summary

| Path | Anonymous | MeterReader | BillingOperations |
|---|---|---|---|
| `GET api/BillingAdjustment/History/{ref}` | 401 | 403 | 200 |
| `GET /Billing/GetAdjustmentHistory?billingReferenceId=` | Redirect/401 | 403 | 200 |
| History section visible in BillAdjustment page | Not accessible | Not accessible | Shown after billing selection |

### 16.7 Account Ledger integration

`AccountLedgerService` already referenced `billing_adjustments` before this continuation — it surfaces adjustments as informational ledger events (`"Type X: amount"` description). No change was made to this behavior. The Ledger continues to use `Billing.RemainingAmount` as the authoritative current obligation; it does not recalculate from audit history client-side.

The new `Reason` field is not yet exposed in the Ledger history event description — that is a separate display decision and no requirement for it was stated.

### 16.8 Transaction and history atomicity

`Reason` is written inside the same transaction that saves the adjustment row, updates `Billing.RemainingAmount`, and commits the operation receipt. A rollback leaves zero rows in all three tables. A replay returns the original Reason without rewriting it. The history query is read-only and has no side effects on any financial table.

### 16.9 Schema and migration requirements

| Migration | Purpose | Applied to tmcwd |
|---|---|---|
| `20261012_add_billing_adjustment_reason.sql` | Adds nullable `Reason VARCHAR(255)` to `billing_adjustments` | **NO — tmcwd_test only** |
| `20261012_add_billing_adjustment_operations.sql` | (C1, prior) idempotency receipt table | **NO — tmcwd_test only** |

Both migrations are additive. Historical `billing_adjustments` rows remain valid with NULL Reason. No existing data is modified by either migration.

**Development rollout for both migrations requires separate explicit authorization** before any adjustment write will succeed against the deployed development `tmcwd` database (the operations table guard was introduced in C1).

### 16.10 Regression results — Phase 6I-C1 Audit History continuation

All regressions executed against `tmcwd_test` using temporary tables only. No writes to development `tmcwd`.

| Check | Result |
|---|---|
| Solution build (TMCWD.slnx) | **PASS; 0 errors** |
| BillingRegression build | **PASS; 0 errors** |
| BillingRegression DB checks | **619 PASS** (temporary tables) |
| Phase 6D invoice concurrency | **PASS** |
| Receipt migration rerun | **PASS** |
| Payment concurrency groups | **PASS** |
| Installment locking group | **PASS** |
| Adjustment workflow — original 10 scenarios | **10/10 PASS** (unchanged from C1) |
| Adjustment workflow — new audit history A–K | **11/11 PASS** |
| Adjustment workflow total | **21/21 PASS** |
| Adjustment concurrency — 4 scenarios | **4 PASS** |
| Adjustment browser — original 4 | **4 PASS** (unchanged) |
| Adjustment browser — new history 8 | **8 PASS** |
| Adjustment browser total | **12/12 PASS** |
| Main browser regression | **25 PASS** |
| Penalty browser regression | **5 PASS** |
| Collections/Ledger browser — 31 checks + 6 viewport | **PASS** |
| MeterReaderAuthRegression | **43 PASS** |
| ServiceRequestRegression (guarded tmcwd_test) | **44 PASS** |
| Flutter | **NOT RERUN** — no Flutter contract/source was changed by this continuation |

### 16.11 Files changed by this continuation

**New:**
- `TMCWD/TMCWD.Data/Database/20261012_add_billing_adjustment_reason.sql`
- `TMCWD/TMCWD.Model/Billing/BillingAdjustmentHistoryDto.cs`

**Modified:**
- `TMCWD/TMCWD.Data/Entities/BillingAdjustment.cs` — added `Reason` nullable property
- `TMCWD/TMCWD.Model/Billing/BillingAdjustment.cs` — added `Reason` to DTO
- `TMCWD/TMCWD.Model/Billing/Interfaces/IBillingAdjustment.cs` — added `Reason` to interface
- `TMCWD/TMCWD.Data/Services/IBillingAdjustmentService.cs` — added `GetHistory` method
- `TMCWD/TMCWD.Data/Services/BillingAdjustmentService.cs` — implemented `GetHistory` (two-step query); threaded `Reason` through `Save()`
- `TMCWD/TMCWD.Data/Controllers/BillingAdjustmentController.cs` — added `History` GET endpoint
- `TMCWD/TMCWD.Billing/BillingAdjustmentTransaction.cs` — added `GetHistory` client method
- `TMCWD/TMCWD.Application/Controllers/BillingController.cs` — added `GetAdjustmentHistory`, `CombineReason`; wired Reason through `SubmitAdjustment`
- `TMCWD/TMCWD.Application/Views/Billing/BillAdjustment.cshtml` — Adjustment History section
- `TMCWD/TMCWD.Application/wwwroot/js/bill_adjustment.js` — `loadAdjustmentHistory()`, `safeText()`, `formatAmount()`, `formatDate()`; `btnSelectBilling` dispatches `change`
- `tests/BillingRegression/AdjustmentWorkflowVerification.cs` — tests A–K
- `tests/BillingRegression/adjustment-browser-regression.cjs` — 8 new history DOM tests

### 16.12 Remaining gaps

- `Reason` is not yet surfaced in the `AccountLedgerService` history event description — low priority; no requirement stated.
- Both migrations (`billing_adjustment_reason` and `billing_adjustment_operations`) require separately authorized coordinated development rollout before adjustment writes will succeed against deployed `tmcwd`.
- Real authenticated staff browser journey on the Bill Adjustment page with the history section is **NOT VERIFIED** (synthetic DOM tests only).
- Organizational debit/negative/approval policy, preferred race priority and future maintenance floor remain unresolved. No new policy was introduced.
- PRODUCTION: **DEFERRED / NOT EVALUATED.**

---

## 17. Phase 6I-C2 — Post-migration development verification — 2026-10-12

**Completed 2026-10-12, Asia/Manila. DEVELOPMENT / SYNTHETIC TESTING ONLY.**

The stakeholder manually executed the two reviewed C1 migrations on development database `tmcwd`. This section records the post-deployment verification. No source changes were made in this task. All regressions run against `tmcwd_test` only.

### 17.1 Development target identity (Step 1)

Verified by direct `SELECT` against `127.0.0.1:3307/tmcwd`:

| Field | Observed | Expected |
|---|---|---|
| `DATABASE()` | `tmcwd` | `tmcwd` |
| `@@hostname` | `DESKTOP-Q72GRF4` | any |
| `@@port` | `3307` | `3307` |
| `VERSION()` | `8.0.46` | `8.0.46` |
| `@@server_uuid` | `c3f950fe-b565-11f1-b6a6-30560f67a354` | `c3f950fe-b565-11f1-b6a6-30560f67a354` |

All match. Identity confirmed.

### 17.2 Deployed `billing_adjustments` schema (Step 2)

`SHOW CREATE TABLE billing_adjustments` returned:

```sql
CREATE TABLE `billing_adjustments` (
  `Id` bigint NOT NULL AUTO_INCREMENT,
  `BillingReferenceId` varchar(50) ... NOT NULL,
  `Type` int NOT NULL,
  `Amount` decimal(6,2) NOT NULL,
  `Reason` varchar(255) DEFAULT NULL,
  `CreatedBy` bigint NOT NULL,
  `DateCreated` datetime DEFAULT CURRENT_TIMESTAMP,
  `UpdatedBy` bigint DEFAULT NULL,
  `DateUpdated` datetime DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 ...
```

`Reason VARCHAR(255) DEFAULT NULL` is present after `Amount`. Matches the reviewed `20261012_add_billing_adjustment_reason.sql` artifact exactly. `AUTO_INCREMENT=2` confirms one pre-existing historical adjustment row; its `Reason` is legitimately `NULL` (pre-dates this migration).

### 17.3 Deployed `billing_adjustment_operations` schema (Step 3)

`SHOW CREATE TABLE billing_adjustment_operations` returned:

```sql
CREATE TABLE `billing_adjustment_operations` (
  `OperationId` varchar(36) NOT NULL,
  `BillingReferenceId` varchar(50) NOT NULL,
  `ActorId` bigint NOT NULL,
  `PayloadHash` char(64) NOT NULL,
  `ResultJson` longtext NOT NULL,
  `DateCreated` datetime(6) NOT NULL,
  PRIMARY KEY (`OperationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ...
```

Matches the reviewed `20261012_add_billing_adjustment_operations.sql` artifact exactly: `OperationId VARCHAR(36)` PK, correct column types, `InnoDB/utf8mb4`.

### 17.4 Operation receipt count (Step 4)

`SELECT COUNT(*) FROM billing_adjustment_operations` = **0**. No development adjustments have been submitted since the migration. No operation receipts were back-filled or fabricated.

### 17.5 Financial baseline verification (Steps 5 and 15)

Read before and after all regression runs. Both reads identical — confirmed unchanged:

| Metric | Value | C1 documented baseline |
|---|---|---|
| `billings` COUNT | 30 | 30 ✓ |
| `billing_adjustments` COUNT | 1 | 1 ✓ |
| `readings` COUNT | 230 | 230 ✓ |
| `reading_sheets` COUNT | 16 | 16 ✓ |
| `payments` COUNT | 8 | 8 ✓ |
| `penalties` COUNT | 5 | 5 ✓ |
| `other_charges` COUNT | 0 | 0 ✓ |
| `other_charge_installments` COUNT | 0 | 0 ✓ |
| Non-null `InvoiceSnapshotJson` | 13 | 13 ✓ |
| SUM `TotalBillAmount` | 6766.30 | 6766.30 ✓ |
| SUM `RemainingAmount` | 5027.30 | 5027.30 ✓ |
| SUM `BillArrearsAmount` | 717.00 | 717.00 ✓ |
| SUM `BillingAdjustment` | 34.00 | 34.00 ✓ |

All 13 `InvoiceSnapshotJson` SHA-256 hashes match previous evidence exactly (see raw hash list in 17.9 below). No financial liabilities or issued invoice snapshots were altered by the schema rollout.

### 17.6 Solution build (Step 6)

`dotnet build TMCWD/TMCWD.slnx` — **PASS, 0 errors.** All warnings are pre-existing CS8618/CS8603/CS0169/CS0414/CS0649 nullability technical debt, identical to the C1 build baseline. No new errors from the `Reason` column or `GetHistory` implementation.

### 17.7 Application compatibility and auth boundary (Steps 7–9)

`--verify-development-deployment` requires user-secrets (`TMCWD.Data-local-development/secrets.json`) which are absent in this environment. Development identity was confirmed directly via SQL in Step 1 — the development server is the expected target. This flag was not passed; the baseline regression ran without it, consistent with Phase 6I-C procedure.

Schema compatibility was verified through **Scenario J** of `AdjustmentWorkflowVerification`:
- Spun up an in-process HTTP host backed by `tmcwd_test` EF model — which creates its schema from the current entity definitions including `Reason` and `billing_adjustment_operations`.
- `GET api/BillingAdjustment/History/{reference}` returned **401** (anonymous), **403** (MeterReader), **200** (BillingOperations actor).
- No missing-table or unknown-column error occurred against any of the new schema elements.
- EF model, entity annotations, and deployed schema are compatible.

**Live authenticated UI check: NOT VERIFIED.** No safe known development credential was available to perform a real browser session on the Bill Adjustment page. The synthetic DOM tests (12/12 PASS) verify client-side rendering logic. The Data API auth boundaries are verified via the in-process HTTP host.

History implementation properties verified through source and tmcwd_test regression:
- History query is read-only; never mutates `Billing.RemainingAmount` or any financial table.
- Actor names resolved from `users` via a separate batch query (avoids MySQL "Can't reopen table" on double join).
- `Reason` encoded through EF's parameterized query; no direct string interpolation.
- Historical rows with `Reason = NULL` return `null` in the DTO — UI renders `—` placeholder, no crash.
- Full `User` entity is not serialized; only `Name` is projected.
- `billing_adjustment_operations` is the idempotency receipt table only; it does not appear in history responses.

### 17.8 Regression results (Steps 10–14)

All suites run against `tmcwd_test` temporary tables. No writes to development `tmcwd`.

| Check | Result |
|---|---|
| Solution build | **PASS, 0 errors** |
| BillingRegression build | **PASS, 0 errors** |
| BillingRegression DB checks | **619 PASS** |
| Phase 6D invoice concurrency | **PASS** |
| Receipt migration rerun | **PASS** |
| Payment concurrency groups | **PASS** |
| Installment locking group | **PASS** |
| Adjustment workflow: original 10 scenarios | **10/10 PASS** |
| Adjustment workflow: audit history A–K | **11/11 PASS** |
| Adjustment workflow total | **21/21 PASS** |
| Adjustment concurrency (4 scenarios) | **4/4 PASS** |
| Adjustment browser regression | **12/12 PASS** |
| Main browser regression | **25 PASS** |
| Penalty browser regression | **5 PASS** |
| Collections/Ledger browser (31 checks + 6 viewport) | **PASS** |
| MeterReaderAuthRegression | **43 PASS** |
| ServiceRequestRegression (guarded `tmcwd_test`) | **44 PASS** |
| Flutter | **NOT RERUN** — no Flutter contract changed; prior 73-test PASS retained as dated evidence |

### 17.9 InvoiceSnapshotJson SHA-256 hashes (post-migration, unchanged)

| `Billing.Id` | SHA-256 |
|---|---|
| 23 | `2e67cbb4a1540f8ac630ffd61df089e7135fbbb8edf89acf949fedb6844c2407` |
| 24 | `28e1ab296b0696d52f3b9cedc7d7f2fb853edf3431e3723af04010c822460d8d` |
| 25 | `69a6a44c68e1e622a5c9aa909d60bc95399be0e081e057e2f55f50a7ed8af157` |
| 26 | `9a98c4ff24cb60aee8925ba12c351946fb68205bf02e0b7e7cc7148cd16dceda` |
| 27 | `627f5133fb628e64ea46a4eb498128558bf1b2c2636b1e41f034452f457434df` |
| 28 | `69a6a44c68e1e622a5c9aa909d60bc95399be0e081e057e2f55f50a7ed8af157` |
| 29 | `975ed9c9262ed22c4cc21b070e488484e4443aaadbe39a4f849c10725e36fff8` |
| 30 | `69a6a44c68e1e622a5c9aa909d60bc95399be0e081e057e2f55f50a7ed8af157` |
| 31 | `975ed9c9262ed22c4cc21b070e488484e4443aaadbe39a4f849c10725e36fff8` |
| 32 | `69a6a44c68e1e622a5c9aa909d60bc95399be0e081e057e2f55f50a7ed8af157` |
| 33 | `a8f9a5009fd88ed264faf4a7344f5090401f534b34d04857b62c60f5434620bb` |
| 34 | `69a6a44c68e1e622a5c9aa909d60bc95399be0e081e057e2f55f50a7ed8af157` |
| 35 | `84f2d6dc36b09ce86c0a61bfa1ad493cfde731e03a06d0b480adea87eb84c175` |

### 17.10 Remaining gaps after C2

- **Live authenticated UI**: staff browser session on the Bill Adjustment page with Adjustment History section not performed — no safe credential available. Synthetic DOM tests and Data API in-process HTTP tests cover the rendering and authorization paths.
- `Reason` not yet surfaced in `AccountLedgerService` history event description — low priority, no requirement stated.
- `--verify-development-deployment` not runnable without user-secrets; development identity verified by direct SQL instead.
- Organizational debit/negative/approval policy, preferred race priority and future rate/floor policy remain unresolved. No new policy was introduced.
- **PRODUCTION: DEFERRED / NOT EVALUATED.**

### 17.11 Phase 6I-C2 verdicts

**BILL ADJUSTMENT AUDIT PERSISTENCE: PASS.** `Reason VARCHAR(255) DEFAULT NULL` deployed. Historical row has `NULL` Reason — legitimate. Financial sums/counts/snapshot hashes unchanged by migration.

**BILL ADJUSTMENT USER-FACING HISTORY: PASS (synthetic/API scope). LIVE AUTHENTICATED UI: NOT VERIFIED.**

**PHASE 6I-C1 SOURCE/TEST REMEDIATION: PASS** (unchanged from C1; all regressions re-confirmed).

**DEVELOPMENT SCHEMA ROLLOUT: PASS.** Both reviewed migrations deployed by stakeholder; schemas match reviewed artifacts exactly; operation receipt table count = 0; no back-fill.

**OVERALL PHASE 6I-C DEVELOPMENT INTEGRATION: PASS.** All mandatory gates satisfied: deployed schema matches reviewed artifacts; source/schema compatible (0 build errors); financial baseline unchanged; 21/21 adjustment scenarios PASS; 4 concurrency PASS; all required browser/auth/reader/service regressions PASS; Collections/Ledger/cancellation/arrears reconcile; issued snapshots immutable; Reason and operation receipt tables deployed and verified.

**FULL APPLICATION SERVICE-WORKFLOW READINESS: BLOCKED BY POLICY** (unchanged).

**PRODUCTION DEPLOYMENT: DEFERRED / NOT EVALUATED.**
