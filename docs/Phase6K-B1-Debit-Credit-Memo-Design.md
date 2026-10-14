# Phase 6K-B1 — Debit/Credit Memo Domain & Persistence Design

**Date:** 2026-10-14 (Asia/Manila)
**Revised:** 2026-10-14 — Policy update: added memo business definitions (Section 2.2-A), replaced U3 with approved Advance Credit Balance policy, confirmed U13 (Option A), added `MemoAmount` boundary rules (Section 6.6), and added future-version dependencies (Section 11.1).
**Status:** DESIGN PHASE — no source code, schema, database, or financial behavior changed.
**References:**
- [Phase 6K-A — Legacy Finance Discovery](Phase6K-A-Legacy-Finance-Discovery.md)
- [Phase 6K-B0 — Organizational Role Model](Phase6K-B0-Organizational-Role-Model.md)

---

## 0. Executive summary

This document is the repository-grounded design for implementing the Debit/Credit Memo (DCM) domain and persistence. It is produced from a read-only audit of the working tree (`branch dev`, 17 modified / 4 untracked Phase 6K-B0 files intact) and the development database at `127.0.0.1:3307/tmcwd`. No tables were created, no routes were enabled, no financial entries were posted, and no production system was evaluated.

**Safe to implement now: 0 routes.** The design below maps the correct sequence once the unresolved policy decisions in Section 11 are resolved by stakeholders.

---

## 1. Existing DCM implementation inventory

### 1.1 Source components — confirmed current state

| Component | Path | State | Problem |
|---|---|---|---|
| Entity | `TMCWD/TMCWD.Data/Entities/DebitCreditMemo.cs` | Mapped; table absent | No approval/posting/reversal/number columns; `Amount` untyped (no precision/scale) |
| DbSet | `TMCWD/TMCWD.Data/Context/UserDbContext.cs` | Registered | Will fail EF queries until table exists |
| Service interface | `TMCWD/TMCWD.Data/Services/IDebitCreditMemoService.cs` | Get/GetAll/Save only | No approval, post, reversal, or scoped read operations |
| Service implementation | `TMCWD/TMCWD.Data/Services/DebitCreditMemoService.cs` | Insert Pending only | Actor from `userId` argument (not claim); no transaction; no bill linkage; no financial effect |
| API controller | `TMCWD/TMCWD.Data/Controllers/DebitCreditMemoController.cs` | All 3 routes quarantined | `Save/{userId}` accepts actor from route — security violation; entity-bound command |
| Shared DTO | `TMCWD/TMCWD.Model/Billing/DebitCreditMemo.cs` | Present | `int Id/CreatedBy/UpdatedBy` vs entity `long`; accepts all persistence fields from client |
| HTTP transaction | `TMCWD/TMCWD.Billing/DebitCreditMemoTransaction.cs` | Registered | `GetByAccountNumber` fetches all memos then filters client-side |
| MVC bridge | `TMCWD/TMCWD.Application/Controllers/BillingController.cs` | Active, downstream denied | Passes session actor to downstream route; API denial surfaces as 502 |
| Entry page | `TMCWD/TMCWD.Application/Views/Billing/DebitCreditMemo.cshtml` | Usable shell; no workflow | No bill selector, status, history, approval, or reversal controls |
| Browser script | `TMCWD/TMCWD.Application/wwwroot/js/debit-credit-memo.js` | Functional fetch | No idempotency key; UTC default date drifts in Manila before 08:00; stale account not invalidated on re-type |
| Customer Information history tab | `TMCWD/TMCWD.Application/Views/CustomerSupport/CustomerInformation.cshtml` | Empty result hides denial | Will return empty silently when quarantine is in effect |
| Legacy sample data | `TMCWD/TMCWD.Application/wwwroot/js/dcm-sample-data.js` | Unused | Fake approved memos; must not be promoted to semantics |
| Legacy SQL | `TMCWD/database/debit_credit_memos.sql` | Outside `TMCWD.Data/Database/` convention; never applied | No FKs, no approval/posting columns, no business uniqueness; `Amount DECIMAL(18,2)` |

### 1.2 Development database state — confirmed read-only

Table `debit_credit_memos`: **absent**. No ApprovedBy, PostedBy, MemoNumber, or OperationId memo columns exist anywhere in the deployed schema. No memo-related stored procedures or triggers were returned under the inspected account's metadata visibility.

Confirmed deployed tables relevant to the DCM domain:

| Table | Key columns confirmed | Remarks |
|---|---|---|
| `billings` | `Id bigint AI`, `BillingReferenceId varchar(50) MUL`, `RemainingAmount decimal(6,2)`, `ServiceInvoiceNumber varchar(30) UNI`, `InvoiceSnapshotJson longtext`, `BillArrearsAmount decimal(18,2)` | No enforced FK; no memo column |
| `billing_adjustments` | `Id bigint AI`, `BillingReferenceId varchar(50)`, `Amount decimal(6,2)`, `Type int`, `Reason varchar(255)` | No enforced FK; no MemoId link |
| `billing_adjustment_operations` | `OperationId varchar(36) PK`, `PayloadHash char(64)`, `ResultJson longtext` | Durable idempotency receipt; closest pattern for memo operations |
| `payments` | `Id bigint AI`, `BalanceBefore decimal(65,30)`, `Success bit` | No memo link |
| `penalties` | `Id bigint AI`, `BillingReferenceId varchar(50) MUL`, `Amount decimal(6,2)` | Water-only basis; no memo base |

### 1.3 Defects requiring remediation before any route can open

| Defect | Classification | Required fix |
|---|---|---|
| `Save/{userId}` route: actor from route parameter | **SECURITY** | Derive `CreatedBy` from `NameIdentifier` claim, overriding any client value |
| Entity overbinding: client supplies `Id`, `Status`, actor, dates | **SECURITY** | Narrow validated command DTO; server controls all audit fields |
| `GetAll` with client-side account filter | **PERFORMANCE / SCOPE** | Scoped server query by AccountId or BillingReferenceId |
| No idempotency key on Save | **INTEGRITY** | Add `Idempotency-Key` header; operation receipt table |
| No transaction on any write | **INTEGRITY** | Explicit transaction for every future financial write |
| `int Id/actors` in DTO vs `long` in entity | **TYPE MISMATCH** | Align DTO to `long` |
| Missing `ApprovedBy`, `PostedBy`, `MemoNumber`, status state machine | **MISSING DOMAIN** | New entity columns; status lifecycle |
| No bill linkage: `AccountNumber` only | **MISSING DOMAIN** | Stable `BillingReferenceId` + `AccountId` linkage policy required |
| UTC default date in browser | **UX / DATA QUALITY** | Use Manila local date for default |

---

## 2. Proposed domain model and lifecycle

> **CONFIRMED** requirements are drawn directly from the Phase 6K-A/B0 reports and the approved stakeholder contract from Phase 6K-B0.
> **RECOMMENDED** items are the implementor's proposals subject to stakeholder confirmation.
> **UNRESOLVED** items require explicit policy decisions before implementation can proceed.

### 2.1 Memo status lifecycle

```
Draft ──► Pending ──► Approved ──► Posted
  │          │           │
  └──► Cancelled  Rejected    ──► Reversed (separate linked event)
```

| Status | Meaning | Who may set it | Guard |
|---|---|---|---|
| `Draft` | RECOMMENDED: saved before submission; not yet in approval queue | Creator | No financial effect; UNRESOLVED: whether draft saves are needed |
| `Pending` | Submitted for review | Billing (7), BillingHead (8), SuperAdmin (1) | CreatedBy from claim; no balance effect |
| `Approved` | Approved by authorized reviewer | BillingHead (8) if not creator; SuperAdmin (1) always | ApprovedBy ≠ CreatedBy (unless SuperAdmin); no balance effect yet |
| `Rejected` | Declined; terminal | BillingHead (8), SuperAdmin (1) | No balance effect |
| `Posted` | Financially posted; affects RemainingAmount | BillingHead (8) if not creator; SuperAdmin (1) always | ApprovedBy and PostedBy from claim; balance effect permanent |
| `Cancelled` | Withdrawn before posting | Creator or BillingHead/SuperAdmin | No balance effect; UNRESOLVED: cancellation of Approved before posting |
| `Reversed` | CONFIRMED: post-posting reversal only; new linked audit event | BillingHead (8), SuperAdmin (1) | Separate `debit_credit_memo_reversals` record; no in-place edit/delete |

**CONFIRMED rules:**
- Posted memos must not be edited or deleted. Reversal is the only correction path and must be a separate, auditable, linked record.
- CreatedBy, ApprovedBy, and PostedBy must come from validated server-side `NameIdentifier` claims, never from the request body or route.

### 2.2 Memo kind and sign convention

| MemoType | Financial direction | Effect on RemainingAmount | CONFIRMED? |
|---|---|---|---|
| `Debit` | Increases collectible obligation | Increases RemainingAmount | CONFIRMED (rule 10) |
| `Credit` | Decreases collectible obligation | Decreases RemainingAmount | CONFIRMED (rule 10) |

**CONFIRMED:** Creation must not change the financial balance. Only valid posting may affect `Billing.RemainingAmount` (rule 11).

### 2.2-A Business definitions (APPROVED)

These definitions record the approved billing workflow semantics. Formal accounting journal entries and account classifications must follow the organization's approved accounting policies separately.

**Debit Memo**

- Increases the customer's amount owed when an additional charge or an undercharge correction is authorized.
- A posted debit memo increases the applicable billing balance by its posted amount.
- Any rules for excess charges, applicable limits, settled bills, and additional penalties must follow separately approved policies (see U2, U5, U11).

**Credit Memo**

- Reduces the customer's amount owed when an overcharge correction or other authorized credit is approved.
- A posted credit memo first applies to its referenced bill, up to that bill's eligible outstanding balance.
- A bill's balance must never become negative because of a credit memo posting.
- Any unused credit (credit amount exceeds the bill's eligible outstanding balance) becomes an account-level Advance Credit Balance under the approved future-version policy (see U3 in Section 8 and Section 11.1).

### 2.2-B Posting formula (RECOMMENDED — pending U4 resolution)

```
-- Debit:  billings.MemoAmount += Amount
--         RemainingAmount = MAX(floor, TotalBillAmount - BillingAdjustment + MemoAmount)

-- Credit: applied_to_bill = MIN(Amount, eligible_outstanding_balance)
--         excess_credit    = Amount - applied_to_bill
--         billings.MemoAmount -= applied_to_bill
--         RemainingAmount = MAX(floor, TotalBillAmount - BillingAdjustment + MemoAmount)
--         IF excess_credit > 0: carry to account-level Advance Credit Balance (future version)
```

**UNRESOLVED (U4):** Whether the existing PHP 20.00 legacy adjustment floor governs credit memo allocation, or whether a zero floor or a separate minimum applies. Do not assume the ₱20 floor from the BillingAdjustmentService automatically carries over. This must be explicitly confirmed before implementing credit posting.

**UNRESOLVED (U3 → approved future-version policy):** See Section 8 U3 and Section 11.1 for the full Advance Credit Balance policy. Implementation of the advance-credit ledger is deferred to a future version.

### 2.3 Memo numbering

**Proposed format:** `YYYY` (4-digit year of posting) + 6-digit zero-padded annual sequence, e.g. `2026000001`. Debit and Credit memos share a single annual sequence; `MemoType` distinguishes them.

**Confirmed design constraints (from the existing `ServiceInvoiceSeq` pattern):**

| Property | Mechanism |
|---|---|
| Server-generated | Number is assigned inside the posting transaction, never by the client |
| Immutable | Once assigned, `MemoNumber` is treated identically to `ServiceInvoiceNumber`: BillingService.SaveUpdate's guard is the reference pattern |
| Unique | `UNIQUE` index on `MemoNumber` column; secondary application check mirrors the invoice duplicate guard |
| Concurrency-safe | A `memo_seq` table (one row per year) is locked with `SELECT … FOR UPDATE` inside the posting transaction, following the exact `service_invoice_seq` pattern |
| Annual reset | A new row per year; `INSERT IGNORE` seeds `LastValue = 0` on first use within a calendar year |
| No gaps promised | Gaps are acceptable; a failed transaction does not reserve a number because the sequence row is rolled back with the transaction |

**Proposed `memo_seq` table:**

```sql
-- One counter row per calendar year (keyed by year integer).
-- All writes must be inside a transaction holding FOR UPDATE.
CREATE TABLE IF NOT EXISTS `memo_seq` (
    `Year`      SMALLINT UNSIGNED NOT NULL COMMENT 'Calendar year (e.g. 2026)',
    `LastValue` INT UNSIGNED      NOT NULL DEFAULT 0
        COMMENT 'Last allocated sequence within this year. Next = LastValue + 1.',
    PRIMARY KEY (`Year`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
  COMMENT 'Annual DCM memo number counter. One row per calendar year.';
```

**Assignment logic (pseudocode, to be implemented inside posting transaction):**

```
year = YEAR(UTC posting timestamp)
INSERT IGNORE INTO memo_seq (Year, LastValue) VALUES (year, 0)
SELECT * FROM memo_seq WHERE Year = year FOR UPDATE
next = LastValue + 1
IF next > 999999 THEN RAISE sequence exhausted
UPDATE memo_seq SET LastValue = next WHERE Year = year
memoNumber = CONCAT(year, LPAD(next, 6, '0'))  -- e.g. 2026000001
IF EXISTS (SELECT 1 FROM debit_credit_memos WHERE MemoNumber = memoNumber) THEN RAISE duplicate (safety check)
```

This uses the same `FOR UPDATE` serialization as the invoice sequence. The `UNIQUE` index on `MemoNumber` is the hard database guard; the application check provides an early diagnostic.

---

## 3. Proposed persistence model

### 3.1 Proposed entity columns

The existing entity and legacy DDL are insufficient. The following defines the **replacement schema** to be applied via a new dated script in `TMCWD/TMCWD.Data/Database/` when approved.

**Table: `debit_credit_memos`**

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `BIGINT NOT NULL AUTO_INCREMENT` | NO | PK |
| `MemoNumber` | `VARCHAR(20) NOT NULL` | NO | Server-assigned; unique; format `YYYY######`; immutable after assignment |
| `AccountId` | `BIGINT NOT NULL` | NO | FK reference to `accounts.Id` (application-level; no enforced FK per project convention) |
| `AccountNumber` | `VARCHAR(50) NOT NULL` | NO | Denormalized for display; source of truth is `AccountId` |
| `BillingReferenceId` | `VARCHAR(50)` | YES | Null until a specific bill is targeted; **UNRESOLVED** whether mandatory |
| `MemoType` | `VARCHAR(10) NOT NULL` | NO | `'debit'` or `'credit'`; `CHECK (MemoType IN ('debit','credit'))` |
| `DcmType` | `VARCHAR(150) NOT NULL` | NO | Category label from approved allowlist; **UNRESOLVED** whether validated against enum or free text |
| `Amount` | `DECIMAL(12,2) NOT NULL` | NO | Always positive; `CHECK (Amount > 0)` |
| `DcmDate` | `DATE NOT NULL` | NO | Memo effective date; server-normalized to Manila date |
| `BillMonthBasis` | `DATE` | YES | Optional billing-month context |
| `Explanation` | `VARCHAR(1000) NOT NULL` | NO | Mandatory audit narrative |
| `Remarks` | `VARCHAR(1000)` | YES | Optional additional notes |
| `Status` | `VARCHAR(20) NOT NULL` | NO | `'Pending'`, `'Approved'`, `'Rejected'`, `'Posted'`, `'Cancelled'`; `CHECK` constraint |
| `CreatedBy` | `BIGINT NOT NULL` | NO | User ID from claim; immutable after creation |
| `DateCreated` | `DATETIME(6) NOT NULL` | NO | UTC; immutable after creation |
| `ApprovedBy` | `BIGINT` | YES | Null until approved; from claim |
| `DateApproved` | `DATETIME(6)` | YES | Null until approved |
| `PostedBy` | `BIGINT` | YES | Null until posted; from claim |
| `DatePosted` | `DATETIME(6)` | YES | Null until posted |
| `UpdatedBy` | `BIGINT NOT NULL` | NO | Last actor; from claim |
| `DateUpdated` | `DATETIME(6) NOT NULL` | NO | UTC |
| `OperationId` | `VARCHAR(36)` | YES | RECOMMENDED: idempotency key for posting operation |
| `ReversedByMemoId` | `BIGINT` | YES | Set when this memo is reversed; references `debit_credit_memo_reversals.Id` |

**Proposed indexes:**

```sql
UNIQUE KEY `UQ_debit_credit_memos_number` (`MemoNumber`),
INDEX `IX_dcm_account` (`AccountId`),
INDEX `IX_dcm_billing_ref` (`BillingReferenceId`),
INDEX `IX_dcm_status_date` (`Status`, `DcmDate`),
INDEX `IX_dcm_created_date` (`DateCreated`)
```

**RECOMMENDED precision:** `DECIMAL(12,2)` is consistent with `BillArrearsAmount decimal(18,2)` on `billings` (large values allowed) while being larger than the current `decimal(6,2)` limit on `billing_adjustments`. **UNRESOLVED:** stakeholder approval of the maximum memo amount and whether a per-transaction cap applies.

### 3.2 Proposed reversal table

Reversals are separate records, not in-place edits, matching rule 15.

**Table: `debit_credit_memo_reversals`**

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `Id` | `BIGINT NOT NULL AUTO_INCREMENT` | NO | PK |
| `OriginalMemoId` | `BIGINT NOT NULL` | NO | References `debit_credit_memos.Id`; unique (one reversal per memo) |
| `ReversalOperationId` | `VARCHAR(36) NOT NULL` | NO | Idempotency key for the reversal posting |
| `Reason` | `VARCHAR(1000) NOT NULL` | NO | Mandatory reversal narrative |
| `PostedBy` | `BIGINT NOT NULL` | NO | From claim |
| `DatePosted` | `DATETIME(6) NOT NULL` | NO | UTC |
| `BillingReferenceId` | `VARCHAR(50)` | YES | Bill affected; copied from original memo |

```sql
UNIQUE KEY `UQ_dcm_reversal_memo` (`OriginalMemoId`),
UNIQUE KEY `UQ_dcm_reversal_operation` (`ReversalOperationId`),
INDEX `IX_dcm_reversal_billing` (`BillingReferenceId`)
```

### 3.3 Proposed memo sequence table

See `memo_seq` DDL in Section 2.3.

### 3.4 Existing entity and DTO modernization required

| File | Change required | Reason |
|---|---|---|
| `TMCWD.Data/Entities/DebitCreditMemo.cs` | Add all new columns; add `[Column]` precision attributes; set `[Table("debit_credit_memos")]` | Entity must match proposed schema |
| `TMCWD.Model/Billing/DebitCreditMemo.cs` | Align `long Id`; add `MemoNumber`, `Status`, `ApprovedBy`, `DateApproved`, `PostedBy`, `DatePosted`; remove server-controlled fields from client-writable DTO | Type mismatch; missing domain fields |
| `IDebitCreditMemoService` | Add `Approve`, `Reject`, `Post`, `Cancel`, `Reverse`, scoped `GetByAccount`, `GetByBillingReference` | Missing operations |
| `DebitCreditMemoTransaction.cs` | Replace `GetAll`+client-filter with scoped calls | Performance and scope |

---

## 4. Transaction and idempotency design

### 4.1 Create / submit (Pending)

- No bill lock required at creation (no financial effect).
- Idempotency: **RECOMMENDED** client-supplied `Idempotency-Key` (UUID) stored in a `debit_credit_memo_operations` receipt table, following the `billing_adjustment_operations` pattern.
- Race: two simultaneous creates for the same account/amount/date would create two Pending records. This is acceptable at creation because no balance is affected. The unique operation key prevents exact duplicates from the same submission.

### 4.2 Approve / reject (status transition only)

- Lock the memo row (either `SELECT FOR UPDATE` on the memo, or an application-level compare-and-set on `Status`).
- Verify `Status = 'Pending'` before transitioning.
- Verify maker/checker: `ApprovedBy ≠ CreatedBy` unless the approver is SuperAdmin.
- Write `ApprovedBy`, `DateApproved`, new `Status`.
- No balance effect; no bill lock required.
- Idempotency: re-approval of an already-Approved memo by the same actor returns the existing record without error.

### 4.3 Post (financial effect)

This is the most critical transaction boundary. It must follow the `BillingAdjustmentService.Save` pattern exactly.

```
BEGIN TRANSACTION
  SELECT * FROM billings WHERE BillingReferenceId = ? FOR UPDATE   -- bill lock
  SELECT * FROM debit_credit_memos WHERE Id = ? FOR UPDATE          -- memo lock
  Verify memo.Status = 'Approved'
  Verify bill.Status not Paid/Waived (UNRESOLVED: whether posted memos may target settled bills)
  SELECT * FROM memo_seq WHERE Year = year FOR UPDATE               -- sequence lock
  Assign MemoNumber
  Compute new RemainingAmount (see Section 2.2)
  Update billings.RemainingAmount
  Update memo.Status = 'Posted', PostedBy, DatePosted, MemoNumber
  INSERT INTO debit_credit_memo_operations (receipt)
COMMIT
```

**Lock ordering:** bill → memo → sequence. This ordering must be consistent with `BillingAdjustmentService.Save` (which locks only the bill) to avoid deadlocks. The memo lock must never be acquired before the bill lock.

**Idempotency:** The `debit_credit_memo_operations` table's `OperationId` PK (varchar(36)) catches duplicate posting requests. The unique `MemoNumber` index is the secondary guard.

**MySQL error 1062** on `OperationId` insert must be caught and surfaced as a `409 Conflict`, matching the adjustment service pattern.

### 4.4 Reverse (post-posting, creates new audit record)

```
BEGIN TRANSACTION
  SELECT * FROM billings WHERE BillingReferenceId = ? FOR UPDATE   -- bill lock
  SELECT * FROM debit_credit_memos WHERE Id = ? FOR UPDATE          -- memo lock
  Verify memo.Status = 'Posted'
  Verify no existing reversal record for this MemoId
  Invert RemainingAmount effect (debit reversal decreases; credit reversal increases)
  Apply floor consistent with posting formula
  Update billings.RemainingAmount
  UPDATE memo SET Status = 'Reversed', ReversedByMemoId = reversal.Id, UpdatedBy, DateUpdated
  INSERT INTO debit_credit_memo_reversals (OriginalMemoId, ReversalOperationId, Reason, PostedBy, DatePosted, BillingReferenceId)
COMMIT
```

**UNRESOLVED:** reversal policy when the bill has been settled (paid or waived) after the original posting. Payment/cancellation do not currently account for memo effects; enabling reversal after settlement requires a separate policy decision.

---

## 5. Role and approval matrix

> Based on the confirmed stakeholder contract from Phase 6K-B0 Section 5 and the requirements in this task.

| Operation | Billing (7) | BillingHead (8) | SuperAdmin (1) | GeneralServices (9) | Accounting (10) | Identity source |
|---|---|---|---|---|---|---|
| Create / Submit (→ Pending) | ✓ | ✓ | ✓ | ✗ | ✗ | `NameIdentifier` claim → `CreatedBy` |
| View own Pending memos | ✓ | ✓ | ✓ | ✗ | ✗ | Claim identity filter |
| View all memos for an account | ✗ | ✓ | ✓ | ✗ | Phase 6N read-only | Claim identity |
| Approve / Reject (Pending → Approved/Rejected) | ✗ | ✓ (not own) | ✓ (always) | ✗ | ✗ | `NameIdentifier` claim → `ApprovedBy`; server enforces maker/checker |
| Post (Approved → Posted) | ✗ | ✓ (not own creator) | ✓ (always) | ✗ | ✗ | `NameIdentifier` claim → `PostedBy`; server enforces maker/checker |
| Cancel (Pending → Cancelled) | ✓ (own) | ✓ | ✓ | ✗ | ✗ | UNRESOLVED: cancellation of Approved before posting |
| Reverse (Posted → Reversed) | ✗ | ✓ | ✓ | ✗ | ✗ | `NameIdentifier` claim → reversal `PostedBy` |
| Export/report | ✗ | Phase 6K-F | Phase 6K-F | ✗ | Phase 6N | Read-only; no financial write |

**Maker/checker server enforcement (CONFIRMED, rule 7):**
- The service must compare `ApprovedBy` (or `PostedBy`) claim identity to the stored `CreatedBy`.
- If they are equal and the role is not `SuperAdmin`, the operation must be rejected with `403 Forbidden`.
- SuperAdmin's exemption is the sole confirmed exception (rule 8).
- The check must occur server-side on the claim, not on any client-supplied value.

**Proposed new policy name:** `DebitCreditMemo` — membership `{SuperAdmin(1), BillingHead(8)}` for approve/post/reverse operations. Create/submit may reuse `BillingOperations` (already includes Billing and BillingHead) for the outer policy, with the service enforcing the narrower role rules.

> **UNRESOLVED:** Whether a separate `DebitCreditMemoCreate` policy is needed, or whether `BillingOperations` outer + service role check is sufficient.

---

## 6. Balance and invoice-snapshot integrity rules

### 6.1 What posting may change

| Field | Memo posts to a bill | Guard |
|---|---|---|
| `billings.RemainingAmount` | **CHANGED** by posting and reversal only | Computed inside the posting transaction with `FOR UPDATE` lock |
| `billings.TotalBillAmount` | **NOT CHANGED** | Issued water charge is immutable |
| `billings.InvoiceSnapshotJson` | **NOT CHANGED** | `BillingService.SaveUpdate` already enforces immutability |
| `billings.ServiceInvoiceNumber` | **NOT CHANGED** | Immutable after issuance |
| `billings.BillingAdjustment` | **NOT CHANGED by memo** | Memo effect is separate from BillingAdjustment aggregate |
| `penalties` rows | **NOT CHANGED** | Penalty basis remains water-only snapshot `BillAmount` (rule 14) |
| `BillArrearsAmount` (snapshot of prior balances) | **NOT CHANGED retroactively** | Historical arrears snapshots are immutable; future arrears calculations will see the updated `RemainingAmount` |

### 6.2 RemainingAmount integration

The proposed posting formula must avoid introducing a second signed-aggregate field that could conflict with `BillingAdjustment`. Two clean options exist:

**Option A — Separate memo aggregate column on `billings` (APPROVED — U13):**

```
billings.MemoAmount (new decimal column, signed, default 0)
Posted debit:           MemoAmount += Amount
Posted credit:          MemoAmount -= applied_to_bill (never below 0 on the bill; excess goes to advance-credit ledger)
On reversal:            MemoAmount reverts by original applied amount

RemainingAmount = MAX(floor, TotalBillAmount - BillingAdjustment + MemoAmount)
```

This keeps the memo effect isolated and visible, does not repurpose `BillingAdjustment`, and preserves the existing adjustment formula.

**FLAG — nullable vs. non-nullable:** The proposed migration script in Section 10 uses `nullable DECIMAL(12,2) DEFAULT 0`. Whether this column should be `NOT NULL DEFAULT 0` or nullable must be confirmed before applying the migration. Both are safe for existing rows when a DEFAULT is supplied, but the nullability choice affects entity mapping and formula guards. **Resolve before implementing the schema change.**

**Option B — Add a signed BillingAdjustment line (REJECTED):**

Using `billing_adjustments` with a special `Type` for memos would force the existing BillingAdjustment aggregate to include memo effects, breaking the current semantics of that field and muddying the audit trail. This option is rejected.

### 6.3 Collections projection integration

`BillingService.GetCollectionBill` is the authoritative source for collectible amounts. When memos are posted, the formula must incorporate `billings.MemoAmount` once — it must not also appear as a separate `AmountDue` component or be summed again from the memo table. The integration point is the `GetCollectionBill` projection, not the browser.

### 6.4 Arrears non-double-counting (rule 13)

`ReadingSheetService.ComputeBillArrearsAmount` uses prior bills' `RemainingAmount`. Since `RemainingAmount` will include posted memo effects (via the MemoAmount formula), arrears will naturally reflect the corrected obligation without any change to the arrears helper. No second lookup from `debit_credit_memos` is needed.

### 6.5 Penalty basis protection (rule 14)

`PenaltyService` uses `LatePaymentPenalty.Calculate(snapshot.BillAmount)`, which is the water-only issued amount from the frozen snapshot. Posted memo amounts do not appear in `snapshot.BillAmount`. Penalty rules are therefore unaffected by memo posting or reversal without any code change.

### 6.6 MemoAmount boundaries and advance-credit separation (APPROVED — U13)

These rules define the intended boundary between `billings.MemoAmount` and the future account-level Advance Credit Balance ledger. They are documented here for design clarity; neither field is implemented in this phase.

1. `MemoAmount` tracks only the net effect of posted debit and credit memos on the **referenced bill**. It does not track account-level credit or advance balances.
2. A posted debit memo increases `MemoAmount` (and therefore `RemainingAmount`) by its full posted amount.
3. A posted credit memo decreases `MemoAmount` only by the amount actually applied to the bill — that is, up to the bill's eligible outstanding balance. `MemoAmount` must never be driven so negative that `RemainingAmount` would fall below the approved floor.
4. Any excess credit (the portion of a credit memo not absorbed by the referenced bill's eligible balance) must be tracked separately in the account-level Advance Credit Balance ledger. It must not also reduce `MemoAmount` on the same bill.
5. Do not count the same credit in both `MemoAmount` and the advance-credit ledger. The split between applied-to-bill and excess-credit must be computed atomically at posting time.
6. The advance-credit ledger tracks creation, allocation to subsequent bills, reversals, and remaining balance. Its implementation is deferred to the future version (see Section 11.1).
7. Issued invoice snapshots (`InvoiceSnapshotJson`) and `ServiceInvoiceNumber` remain immutable. `MemoAmount` does not appear in issued snapshots.
8. `RemainingAmount` remains the authoritative current-bill obligation. `GetCollectionBill` is the single source for collectible amounts. Neither the browser nor mobile clients reconstruct this value locally.
9. Arrears (`ComputeBillArrearsAmount`) and penalty calculations (`LatePaymentPenalty.Calculate`) are not affected by `MemoAmount` or advance-credit allocations; their existing sources remain authoritative.
10. Do not implement `MemoAmount`, the posting formula, or any advance-credit table as part of this documentation task.

---

## 7. Reversal design

### 7.1 What a reversal is

A reversal is a first-class auditable financial event that undoes the balance effect of a posted memo. It is not an edit, delete, status rollback, or administrative annotation.

### 7.2 Reversal record fields

See `debit_credit_memo_reversals` table in Section 3.2.

### 7.3 Effect on RemainingAmount

| Original memo type | Reversal effect |
|---|---|
| Debit (increased obligation) | Decrease `billings.MemoAmount` by original `Amount`; `RemainingAmount` decreases |
| Credit (decreased obligation) | Increase `billings.MemoAmount` by original `Amount`; `RemainingAmount` increases |

The floor (`PHP 20.00`) applies to both directions: the same `MAX(floor, TotalBillAmount - BillingAdjustment + MemoAmount)` formula applies after reversal.

### 7.4 State machine effect

After a successful reversal:
- `debit_credit_memos.Status` = `'Reversed'`
- `debit_credit_memos.ReversedByMemoId` = `debit_credit_memo_reversals.Id`
- `debit_credit_memo_reversals` record is immutable
- No further status transition is allowed on the original memo

### 7.5 Unresolved reversal policy

- **UNRESOLVED:** May a bill be reversed after it has been paid/waived? The `RemainingAmount` was zero for a settled bill; reversing a credit memo would attempt to increase it from zero, which requires a policy decision on whether to restore the bill's Unpaid status.
- **UNRESOLVED:** Does reversal require a second maker/checker (reversal creator ≠ original memo creator)?
- **UNRESOLVED:** Time limit, amount threshold, or supervisor approval tier for reversals.

---

## 8. Explicit unresolved business decisions

These must be resolved by stakeholders before any implementation begins. Ordered by blocking impact.

| # | Decision | Why it blocks implementation | Related rule |
|---|---|---|---|
| U1 | **Bill targeting scope.** Must a Debit/Credit Memo target a specific `BillingReferenceId`, or may it be account-level (no specific bill)? | Determines whether `BillingReferenceId` is mandatory; drives the posting formula and collections projection | 6K-A §23 item 2 |
| U2 | **Amount precision and maximum.** Is `DECIMAL(12,2)` acceptable? Is there a per-memo cap? | Schema precision cannot be reduced after data exists | 6K-A §23 item 3 |
| U3 | **Excess credit handling — APPROVED FOR FUTURE VERSION.** The approved policy is: (1) each credit memo must reference a specific billing record (subject to U1); (2) apply the posted credit to its referenced bill up to the eligible outstanding amount; (3) the referenced bill's balance must never become negative; (4) carry any unused credit into an account-level Advance Credit Balance; (5) automatically apply available advance credit to the oldest eligible unpaid bill first, then subsequent eligible unpaid bills in chronological order; (6) if no eligible unpaid balance remains, retain the unused credit for automatic application to future bills; (7) track credit creation, allocations, reversals, and remaining advance balances through an auditable account-level credit ledger; (8) protect every credit allocation against duplicate application using appropriate transaction safety, idempotency, and source-to-target references; (9) ensure credit is not deducted twice from billing balances, arrears, collections, or future invoices; (10) define a separate auditable correction workflow for reversals after credit has already been allocated or a bill has been settled — do not silently reopen settled bills. **IMPLEMENTATION DEFERRED** — advance-credit ledger, allocation logic, idempotency, reversal/reallocation, and reconciliation are future-version dependencies (see Section 11.1). | Advance-credit ledger is a separate future-version feature; its design prerequisites include U1, U4, U5, U6, and authorization controls | 6K-A §23 item 3 |
| U4 | **Floor on credit posting.** Does the PHP 20.00 legacy adjustment floor govern credit memo allocation, or does a zero floor or a separate minimum apply? This floor must not be assumed to carry over automatically from `BillingAdjustmentService`. | Affects the RemainingAmount formula and the split between applied-to-bill and excess credit | 6K-A §23 item 3 |
| U5 | **Settled bill targeting.** May a Pending memo be posted against a Paid or Waived bill? | Determines `PaymentStatus` guard in the posting transaction | 6K-A §23 item 5 |
| U6 | **Reversal after settlement.** May a Posted memo be reversed after the bill is settled? Does reversal restore `Unpaid` status? | Reversal of a credit on a paid bill requires payment/cancellation interaction | 6K-A §23 item 5; rule 15 |
| U7 | **Cancellation window.** May an `Approved` memo be cancelled before posting? By whom? | Status machine completeness | 6K-A §23 item 5 |
| U8 | **DcmType enforcement.** Is the category (`DcmType`) validated against an approved closed allowlist (enum) or accepted as free text? | Determines whether a `dcm_types` reference table or a C# enum is required | 6K-A §23 item 1 |
| U9 | **Draft status.** Is a pre-submission `Draft` state needed, or does every create immediately become `Pending`? | Determines whether the status machine includes Draft; affects the required UI controls | 6K-A §23 item 5 |
| U10 | **Reversal maker/checker.** Must the reversal actor differ from the original memo creator? | Authorization rule for reversal endpoint | Rule 7; 6K-A §23 item 4 |
| U11 | **Penalty treatment for memo liabilities.** Do posted debit memos accrue their own late fee, or does the water-only penalty rule remain the sole late-fee mechanism? | Penalty service must remain unchanged unless explicitly approved | Rule 14 |
| U12 | **Arrears: prior posted memos.** When computing bill arrears for a new bill, should prior bills' `MemoAmount` (already in `RemainingAmount`) appear separately in the arrears breakdown, or is the `RemainingAmount` roll-up sufficient? | Presentation only if Option A is adopted; no formula change needed | 6K-A §12 |
| U13 | **`BillingAdjustment` semantic separation — APPROVED (Option A).** A dedicated `MemoAmount` column on `billings` tracks the net posted memo effect separately from `BillingAdjustment`. Option B (reusing `billing_adjustments`) is rejected. See Section 6.2 and 6.6. **FLAG:** Whether `MemoAmount` is `NOT NULL DEFAULT 0` or nullable must be confirmed before the migration script is applied. | Schema migration scope confirmed; nullable/non-nullable detail requires resolution before implementation | Section 6.2 above |
| U14 | **Accounting (role 10) read scope.** What specific DCM queries/filters does Accounting need? | Required before defining Phase 6N read routes | Phase 6K-B0 §6 |

---

## 9. Regression and concurrency test matrix

> Tests listed here are **proposed**. None are implemented in this phase.

### 9.1 Authorization boundary tests (stateless/synthetic)

| Test | Roles to verify denied | Expected result |
|---|---|---|
| Create memo — anonymous | — | 401 |
| Create memo — GeneralServices (9), Accounting (10) | 9, 10 | 403 |
| Create memo — Billing (7) | — | 200 (once enabled) |
| Approve memo created by same BillingHead | 8 (self) | 403 |
| Approve memo created by different BillingHead | 8 (different actor) | 200 |
| Approve memo — Billing (7) | 7 | 403 |
| Post memo — Billing (7) | 7 | 403 |
| Post memo — BillingHead who created it | 8 (self) | 403 |
| SuperAdmin creates and approves own memo | 1 | 200 (sole exemption) |
| Forged `CreatedBy` in request body | any valid role | CreatedBy from claim, not body |
| Forged `ApprovedBy` in request body | any valid role | ApprovedBy from claim, not body |

### 9.2 Financial integrity tests (synthetic schema, `tmcwd_test`)

| Test | Expected invariant |
|---|---|
| Debit post: `RemainingAmount` increases by `Amount` | `billings.RemainingAmount` (before) + Amount = new value (subject to floor per U4) |
| Credit post: `RemainingAmount` decreases by applied amount only | `billings.RemainingAmount` never negative; excess credit goes to advance-credit ledger, not to `MemoAmount` |
| Credit post with excess: bill balance at floor; excess tracked separately | `MemoAmount` reflects only applied portion; advance-credit ledger carries excess; no double-count |
| `TotalBillAmount` unchanged after any memo operation | `billings.TotalBillAmount` = original value |
| `InvoiceSnapshotJson` unchanged after any memo operation | Snapshot byte-for-byte identical |
| `ServiceInvoiceNumber` unchanged | Same invoice number |
| `BillingAdjustment` unchanged by memo post | Not modified by DCM code path |
| Penalty re-post after credit memo: basis still `snapshot.BillAmount` | Penalty amount unchanged |
| Arrears for next bill includes corrected `RemainingAmount` | No second sum from memo table |
| Payment `AmountDue` reflects posted memo (via `GetCollectionBill`) | Single source of collectible amount |

### 9.3 Concurrency and idempotency tests

| Test | Scenario | Expected result |
|---|---|---|
| Duplicate posting request (same `OperationId`) | Two simultaneous POST requests with identical `Idempotency-Key` | One succeeds (200), duplicate returns original result (200 replay); no double-post |
| Concurrent debit + credit post on same bill | Two simultaneous transactions; one acquires `FOR UPDATE` first | Both serialize; `RemainingAmount` reflects each once; no lost update |
| Memo post concurrent with `BillingAdjustmentService.Save` on same bill | Both acquire bill `FOR UPDATE` | One waits; no deadlock; each completes; final `RemainingAmount` reflects both effects |
| Memo post concurrent with payment on same bill | Both acquire bill `FOR UPDATE` | One serializes; payment sees correct balance |
| Reversal replay (same `ReversalOperationId`) | Two requests | One succeeds; second returns existing reversal |
| Reversal of already-reversed memo | Status = Reversed | 409 Conflict |
| Post when status is already Posted | Status = Posted | 409 Conflict |
| Approve after memo is Rejected | Status = Rejected | 409 Conflict |
| `MemoNumber` sequence under parallel posts (same year) | Two simultaneous posts | Two distinct consecutive numbers; no duplicate |
| Sequence rollback on failed post | Transaction fails after sequence increment | `memo_seq.LastValue` rolls back; number is not assigned; gap acceptable |

### 9.4 Read invariance tests

| Test | Expected result |
|---|---|
| `AccountLedgerRegression`: no `debit_credit_memos` query until Phase 6K-F | Regression still rejects SQL referencing the table directly |
| `GetCollectionBill` with posted memo present | Returns updated `RemainingAmount`; no double count |
| `GetCollectionBill` with no memo | Unchanged from current baseline |

---

## 10. Migration and rollout plan (development-only authorization required)

> **No schema changes are applied in this phase.** The scripts below are designed for review, not execution. Each requires explicit authorization before applying to any environment.

### Script sequence

All scripts follow the `TMCWD/TMCWD.Data/Database/YYYYMMDD_*.sql` naming convention and the `CREATE TABLE IF NOT EXISTS` / `IF NOT EXISTS` column-add pattern established in prior phases.

| Script (proposed name) | Purpose | Depends on |
|---|---|---|
| `20261014_add_debit_credit_memos.sql` | Create `debit_credit_memos` table with all new columns, CHECK constraints, UNIQUE indexes | U1–U4 policy decisions resolved |
| `20261014_add_memo_seq.sql` | Create `memo_seq` annual counter table | None (standalone) |
| `20261014_add_debit_credit_memo_reversals.sql` | Create `debit_credit_memo_reversals` table | `debit_credit_memos` exists |
| `20261014_add_billings_memo_amount.sql` | Add nullable `MemoAmount DECIMAL(12,2) DEFAULT 0` column to `billings`; update `GetCollectionBill` projection | U13 (Option A confirmed); `debit_credit_memos` exists |
| `20261014_add_dcm_operations.sql` | Create `debit_credit_memo_operations` idempotency receipt table (parallel to `billing_adjustment_operations`) | None (standalone) |

### Rollout ordering

1. Apply `memo_seq` and `dcm_operations` scripts first (no dependency on bill schema).
2. Apply `debit_credit_memos` and `debit_credit_memo_reversals` (new standalone tables).
3. Apply `billings_memo_amount` last (additive column; nullable with DEFAULT 0 is safe for existing rows).
4. Verify with `SHOW COLUMNS FROM ...` and `SHOW INDEX FROM ...` before enabling any route.

### Safety properties of each proposed script

- All DDL is `CREATE TABLE IF NOT EXISTS` or `ALTER TABLE … ADD COLUMN IF NOT EXISTS` (MySQL 8.0 syntax).
- No existing table is dropped, renamed, or altered destructively.
- No existing column is modified in type or nullability.
- No data migration is required. Historical `billings` rows receive `MemoAmount = 0` by default.
- Scripts are labeled: apply to `tmcwd_test` first; development `tmcwd` and production require separate authorization.
- Scripts are idempotent on rerun (IF NOT EXISTS guards).

---

## 11. Phased implementation checklist

Ordered to maintain safety at each exit gate. Each phase must not begin until the prior phase's exit condition is met.

### 11.1 Future-version backlog — Advance Credit Balance (APPROVED FOR FUTURE VERSION, IMPLEMENTATION DEFERRED)

The following capabilities belong to a future version of the DCM feature. They must not be implemented in any phase up to and including 6K-H without separate authorization. None of these items affect the core debit/credit posting design; they extend it.

| Dependency | Description | Prerequisite decisions |
|---|---|---|
| Account-level Advance Credit Balance ledger | Table tracking credit creation, allocations, reversals, and remaining balance per account | U1 (bill targeting), U4 (floor), U5 (settled-bill policy) resolved |
| Credit allocation to referenced bill | Atomic split: applied-to-bill amount vs. excess at posting time; `MemoAmount` updated only by applied portion | U4 (floor) resolved |
| Allocation to oldest eligible unpaid bills | Automatic application of advance balance to subsequent unpaid bills in chronological order | Eligible bill status definition required |
| Retention of unused credit | Retain advance balance when no eligible unpaid bill exists; apply to future bills | Account-level ledger implemented |
| Transaction-safe and idempotent credit application | Each allocation guarded by idempotency key; atomically linked to source memo and target bill | Ledger table and locking design confirmed |
| Auditable credit reversal and reallocation | Separate reversal workflow when credit has already been allocated; no silent reopening of settled bills | U6 (reversal-after-settlement) policy confirmed |
| Eligible bill rules | Define which bill statuses qualify (e.g., Unpaid, Overdue); minimum floor for allocation; whether partial allocation is allowed | U4, U5 resolved |
| Reconciliation | Posted memo effects, advance credits, `RemainingAmount`, arrears, collections, and future billing must agree; no double-count | Full integration design required |
| Authorization and maker/checker | Allocation and reversal operations require the same claim-derived identity and maker/checker controls as posting | Role/approval matrix extended |

### Phase 6K-B1 (this phase) — Design ✓
- [x] Working tree audit (git status)
- [x] Full source inventory of existing DCM shell
- [x] Billing persistence and transaction pattern analysis
- [x] Role and authorization infrastructure confirmed
- [x] Schema and sequence pattern analysis
- [x] Design document produced

### Phase 6K-C — Authorization, validated command boundary, audit

**Prerequisites:** U1 (bill targeting), U8 (DcmType enforcement), U9 (Draft status) resolved by stakeholders.

- [ ] Replace `int` DTO IDs/actors with `long`; narrow to a `CreateMemoCommand` request model
- [ ] Controller overrides actor from `NameIdentifier` claim (matches `BillingAdjustmentController` pattern)
- [ ] Add `DebitCreditMemo` authorization policy (and/or reuse `BillingOperations` with service-level role check)
- [ ] Server-side validation: account exists and is Active; `DcmType` from allowlist; `Amount > 0` and within precision; `MemoType` exact; `DcmDate` not in blocked future/historical window (UNRESOLVED policy)
- [ ] Replace route-controlled `{userId}` with claim-derived actor on all DCM routes
- [ ] Keep all three API routes on `PendingAuthorizationDecision` until this phase is complete and reviewed

**Exit condition:** All-role denial tests pass; forged actor tests pass; claim-actor tests pass; no route opened.

### Phase 6K-D — Persistence: tables and memo sequence

**Prerequisites:** Phase 6K-C exit; U2, U4, U5 resolved. U3 (Advance Credit Balance) is a future-version dependency and does not block this phase; credit posting in this phase applies only the amount up to the bill's eligible balance, with excess deferred.

- [ ] Apply `20261014_add_debit_credit_memos.sql` to `tmcwd_test` first; verify columns and indexes
- [ ] Apply `20261014_add_memo_seq.sql` to `tmcwd_test`
- [ ] Apply `20261014_add_dcm_operations.sql` to `tmcwd_test`
- [ ] Update entity `DebitCreditMemo.cs` to match new schema (add columns, precision attributes)
- [ ] Implement `MemoNumberService` (or inline) using `memo_seq` with `FOR UPDATE` inside posting transaction
- [ ] Implement Create/Submit → Pending with operation receipt
- [ ] Add `GetByAccount` and `GetByBillingReference` scoped queries to service; remove `GetAll`+client-filter
- [ ] Regression: confirm no live development data loss; confirm `tmcwd_test` isolation

**Exit condition:** Table deployed to `tmcwd_test`; memo creation and uniqueness regression passes; no route opened to production.

### Phase 6K-E — Approval and maker/checker

**Prerequisites:** Phase 6K-D exit; U7, U10 resolved.

- [ ] Implement `Approve` operation: `FOR UPDATE` memo lock; status guard; maker/checker claim comparison; write `ApprovedBy`, `DateApproved`
- [ ] Implement `Reject` operation: status guard; write `UpdatedBy`, `DateUpdated`, new `Status`
- [ ] Add approval endpoint under `PendingAuthorizationDecision` (still quarantined for external access)
- [ ] Regression: self-approval denial for BillingHead; SuperAdmin exemption; double-approval returns existing

**Exit condition:** Maker/checker tests pass; status machine integrity tests pass; no financial effect yet.

### Phase 6K-F — Posting and balance integration

**Prerequisites:** Phase 6K-E exit; U5, U6, U13 resolved; `billings.MemoAmount` script reviewed.

- [ ] Apply `20261014_add_billings_memo_amount.sql` to `tmcwd_test`
- [ ] Implement Post operation: full transaction boundary per Section 4.3; `FOR UPDATE` on bill and memo and sequence
- [ ] Update `BillingService.GetCollectionBill` to include `MemoAmount` in `RemainingAmount` projection
- [ ] Validate collections integration: `AmountDue` is correct; no double-count
- [ ] Validate arrears: `ComputeBillArrearsAmount` sees corrected `RemainingAmount` without change to arrears helper
- [ ] Validate penalty: water-only basis unchanged

**Exit condition:** Financial integration tests pass; snapshot immutability regression passes; penalty basis regression passes.

### Phase 6K-G — Reversal

**Prerequisites:** Phase 6K-F exit; U6, U10 resolved.

- [ ] Apply `20261014_add_debit_credit_memo_reversals.sql` to `tmcwd_test`
- [ ] Implement Reverse operation: transaction boundary per Section 4.4; idempotency via `ReversalOperationId`
- [ ] Update `GetCollectionBill` if `MemoAmount` column carries the reversal effect (should be automatic if formula is correct)
- [ ] Ledger/history integration: reversal event appears in account history

**Exit condition:** Reversal regression passes; double-reversal returns 409; reversal-after-settlement policy applied per U6 decision.

### Phase 6K-H — Route enablement, UI modernization, Customer Information

**Prerequisites:** Phase 6K-G exit; all unresolved decisions resolved; security review complete.

- [ ] Replace `PendingAuthorizationDecision` with approved policies on specific routes
- [ ] Modernize `DebitCreditMemo.cshtml`: bill selector, status/history, approval/rejection display
- [ ] Fix browser `debit-credit-memo.js`: Manila local date default; account re-validation on retype; idempotency key generation
- [ ] Fix `CustomerInformation.cshtml` history tab: correct empty-vs-denied differentiation
- [ ] Remove `dcm-sample-data.js` (legacy unused)
- [ ] Remove legacy `TMCWD/database/debit_credit_memos.sql` (superseded by dated scripts)
- [ ] Full regression suite including concurrency tests

**Exit condition:** Browser regression passes; all existing regression suites pass; no speculative PCA tests added.

### Phase 6N — Accounting read access (deferred)

- [ ] Define scoped read routes for Accounting (role 10)
- [ ] No financial write authority granted to Accounting under any circumstance

---

## 12. Files changed in this phase

| File | Action | Reason |
|---|---|---|
| `docs/Phase6K-B1-Debit-Credit-Memo-Design.md` | **Created (new, untracked)** — initial design | Phase 6K-B1 discovery document |
| `docs/Phase6K-B1-Debit-Credit-Memo-Design.md` | **Updated** — policy revision | Added memo business definitions (§2.2-A); replaced U3 with approved Advance Credit Balance policy; confirmed U13 (Option A) and flagged nullable detail; added `MemoAmount` boundary rules (§6.6); updated posting formula (§2.2-B); added future-version backlog (§11.1); updated financial integrity tests (§9.2) |

No application source, migration script, database schema, or financial behavior was changed.

---

## 13. Phase 6K-B1 verdict

| Item | Result |
|---|---|
| Working tree audit | PASS — 17 modified / 4 untracked Phase 6K-B0 files preserved; no unrelated changes introduced |
| DCM shell inventory | COMPLETE |
| Billing persistence analysis | COMPLETE |
| Role and authorization analysis | COMPLETE |
| Memo numbering design | COMPLETE — `memo_seq` FOR UPDATE pattern; matches `service_invoice_seq` convention |
| Memo business definitions (Debit/Credit) | **DOCUMENTED** — Section 2.2-A |
| Advance Credit Balance policy (U3) | **APPROVED FOR FUTURE VERSION** — Section 8 U3 and Section 11.1; implementation deferred |
| U13 — Option A (`MemoAmount` column) | **APPROVED** — Section 6.2; nullable/non-nullable flag recorded for resolution before migration |
| MemoAmount boundary rules | **DOCUMENTED** — Section 6.6 |
| Unresolved policy decisions | **12 remaining** (U1, U2, U4–U12, U14); U3 approved/deferred; U13 approved with open flag |
| Application source modified | **NONE** |
| Schema or migration applied | **NONE** |
| Database records changed | **NONE** |
| Financial behavior changed | **NONE** |
| DCM routes enabled | **NONE** — all remain on `PendingAuthorizationDecision` |
| PCA implemented | **NONE** — out of scope |
| PHASE 6K-B1 | **PASS — design complete** |
| PHASE 6K DEVELOPMENT INTEGRATION | **NOT YET EVALUATED** |
| PRODUCTION | **DEFERRED / NOT EVALUATED** |
