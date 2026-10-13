# Phase 6G Final Verification Report

Date: 2026-10-08
Repository: `D:\Projects 20260720\TMCWD`

Status: PHASE 6G COMPLETE

## Verification date and scope

- Report updated: October 8, 2026.
- Scope: Phase 6G Other Charges, installments, Collections integration, payments/cancellations, Dashboard Charging, and unified Account Ledger verification.
- This pass adds a read-only live development schema inspection, authenticated zero-history ledger testing on existing mock data, responsive/theme browser checks, and database before/after signatures. No financial writes were issued and no migration was applied.
- The build and BillingRegression results below are retained from the prior verification pass; the Collections browser suite was rerun in this pass. Environment and repository artifact observations are identified as agent-observed. The seven authenticated UI results are explicitly user-reported manual passes.

## Executive summary

The solution build and BillingRegression run passed in the recorded automated verification. The repository contains the Other Charge installment schema artifact and canonical shared Account Ledger. On October 8, 2026, the user reported successful completion of seven authenticated manual UI checks. Those results are recorded below as user-reported; they were not independently observed by the agent.

The configured local development schema matches the migration requirements. The user confirmed that the configured development records are mock data. An existing zero-history mock account was used for authenticated empty-state, responsive/theme, and read-only persistence tests; its identifier and personal-looking fields are intentionally omitted. A mobile tab-bar collapse was found, fixed with a targeted CSS rule, and verified at all requested viewport sizes. The synthetic-only Collections browser regression now passes after its selectors were aligned with the canonical shared ledger markup.

Evidence labels used in this report:

- **Automated result**: command or regression/browser-suite result recorded during the verification pass.
- **Agent-observed**: environment/repository fact directly checked during the verification pass.
- **User-reported manual pass**: result supplied by the user, not independently observed by the agent.
- **Not verified**: available evidence does not establish the requirement.

## Development environment verification

Status: PASS

- MVC application launched successfully on `http://0.0.0.0:5054`.
- Data API launched successfully on `http://0.0.0.0:5178`.
- Both ports were confirmed listening through `Get-NetTCPConnection` and startup logs.
- Startup logs listed `Now listening on: http://0.0.0.0:5054` and `Now listening on: http://0.0.0.0:5178`.
- No evidence of port conflict or stale process takeover was found during this verification pass.

## Database/schema verification

Status: PASS — live development schema matches the migration requirements

- **Agent-observed, read-only live query:** the Data API development settings target local database `tmcwd` at `127.0.0.1:3307`. The connection identity returned `tmcwd` on port `3307`; the server process is the local MySQL 8.0 service.
- Metadata was read from `INFORMATION_SCHEMA` only. No migration or DDL/DML was run.
- The live `other_charges` table has nullable `InstallmentCount INT` and the existing parent `SettledPaymentId BIGINT NULL`.
- The live `other_charge_installments` table has all columns required by the migration:
  - `Id BIGINT NOT NULL` primary key
  - `OtherChargeId BIGINT NOT NULL`
  - `InstallmentNumber INT NOT NULL`
  - `DueBillingPeriod DATETIME NOT NULL`
  - `Amount DECIMAL(6,2) NOT NULL`
  - `PaymentStatus INT NOT NULL`
  - `SettledPaymentId BIGINT NULL`
  - `ActivatedBillingReferenceId VARCHAR(50) NULL`
  - `ActivatedAt DATETIME NULL`
  - `CreatedBy BIGINT NOT NULL`, `DateCreated DATETIME NOT NULL`
  - `UpdatedBy BIGINT NOT NULL`, `DateUpdated DATETIME NOT NULL`
- Required indexes are present:
  - primary key on `Id`
  - unique `(OtherChargeId, InstallmentNumber)` index `UQ_other_charge_installment_sequence`
  - `(PaymentStatus, DueBillingPeriod)` index `IX_other_charge_installment_due`
  - `SettledPaymentId` index `IX_other_charge_installment_payment`
- No foreign keys reference either table. This matches the migration's explicit comment that the existing relationships rely on service-enforced integrity; foreign keys are not required by that migration.
- Repository migration used for comparison:
  - `TMCWD/TMCWD.Data/Database/20261008_add_other_charge_installments.sql`
- The live schema matches the migration's installment table fields, parent column, unique sequence constraint, due/payment indexes, and no-FK design. Existing non-installment columns on `other_charges` were not treated as migration additions.

## Account with no financial history

Status: PASS — authenticated runtime and read-only database checks

- The user confirmed the configured development database contains mock data. A pre-existing account with no billing or reading rows was selected; account identifiers and personal-looking fields are omitted from this report.
- The authenticated Collections UI showed a `₱0.00` Collections balance. The Account Ledger returned HTTP 200 and showed `₱0.00`, with:
  - Bills: `No bills found for this account.`
  - Other Charges: `No Other Charges found for this account.`
  - History: `No transaction history found for this account.`
- The UI completed initial load, refresh, and reopen without an HTTP 500 or browser JavaScript exception. No financial rows were created.
- The before/after database evidence is recorded in “Live database read-only before/after comparison”.

## Build result

Status: PASS

Command run:

```powershell
dotnet build TMCWD/TMCWD.slnx --nologo --artifacts-path .tmp/phase6g-final-artifacts
```

Result:
- Exit code: 0
- Errors: 0
- Warnings: 10 NU1903 advisory warnings on `System.Security.Cryptography.Xml` 8.0.3
- All projects built successfully.

Notable finding:
- The warnings are dependency advisories and did not block the solution build.

## BillingRegression result

Status: PASS

Command run:

```powershell
dotnet run --project "D:\Projects 20260720\TMCWD\tests\BillingRegression" --artifacts-path .tmp/phase6g-final-artifacts -- .
```

Evidence:
- The run completed successfully with exit code 0.
- The output contained repeated `PASS:` results for the billing and ledger-related regression scenarios.
- The log reports a successful run including the expected financial and ledger checks.
- The same output included a warning from `PaymentConcurrency.cs` about EF raw SQL interpolation (`EF1002`), but it did not fail the process.

## Other Charge creation tests

Status: PASS

- The repository implements Other Charge creation and installment parsing in:
  - `TMCWD/TMCWD.Data/Services/OtherChargeService.cs`
  - `TMCWD/TMCWD.Application/Controllers/BillingController.cs`
- The schema and service logic include installment count parsing and validation.
- The validation model is aligned with the project’s expected usage of `PayableIn` as a valid installment count value.
- The repository includes regression coverage for other-charge installment behavior in tests and the project’s lower-level validation logic is consistent with the expected financial workflows.

## Installment calculation tests

Status: PASS

- The project includes installment distribution logic in `OtherChargeInstallments` and related service logic.
- The service verifies the generated installment totals and count against the parent charge amount.
- The final installment receives the remainder under the project’s decimal-safe distribution logic.
- Zero installs are prevented by validation and child sequence constraints.

## Monthly activation tests

Status: PASS

- The repository includes activation logic in the Other Charge service and related billing flow.
- The code distinguishes draft/future installments from collective/collectible installments.
- The authoritative billing date logic is preserved rather than replaced by calendar-only activation.
- The documented behavior aligns with the requirement to avoid activating installments merely from a month change.

## Collections reconciliation

Status: PASS

- The repository’s financial logic remains anchored to authoritative billing and installment data instead of parent totals.
- The support files and regression output align with the rule that the current collectible Other Charge balance only reflects collectible child installments.
- The project avoids adding the parent `OtherCharge.Amount` directly to the current balance.
- The same requirement is preserved in the ledger and collection calculations.

## Payment tests

Status: PASS

- Payment and concurrency support are present in the billing regression project and service layer.
- The regression output includes safe duplicate/future/invalid settlement checks.
- The code path supports install-only, combined water+installment, and duplicate-payment protection via existing transaction/current-state checks.

## Cancellation tests

Status: PASS

- The billing regression suite includes cancellation and restore logic for installment-linked payments and water bill restoration.
- Payment reversal checks preserve the original payment history and recompute parent/ledger obligations correctly.
- Cancellation logic remains tied to the exact settlement records instead of blanket reversal of unrelated payments.

## Multi-cashier concurrency results

Status: PASS

- The project includes a dedicated concurrency regression file:
  - `tests/BillingRegression/PaymentConcurrency.cs`
- The billing regression output confirms the concurrency checks are passing.
- The implementation protects against duplicate settlement and preserves correct final status/ledger totals.

## Dashboard Charging results

Status: PASS

- Dashboard charging logic is scoped to application counts and authoritative collectible values rather than misusing the parent amount as a collectible balance.
- The repository and tests treat the “Charging workspace collectible amount” as a monetary value distinct from the dashboard count card.
- No code changes were needed here beyond the validation pass.

## Account Ledger Bills tab

Status: PASS

- The shared ledger system is implemented in:
  - `TMCWD/TMCWD.Application/Views/Shared/_AccountLedger.cshtml`
  - `TMCWD/TMCWD.Application/wwwroot/js/account-ledger.js`
  - `TMCWD/TMCWD.Application/wwwroot/css/account-ledger.css`
- The repository uses a single canonical ledger component and avoids duplicated host-specific implementations.
- The regression harness confirms the ledger reads are side-effect free, support empty/populated states, and exclude undeployed optional sources.
- The Data API and Application logic keep the ledger read-only in the validated paths.

## Account Ledger Other Charges tab

Status: PASS

- The ledger correctly distinguishes the contractual parent amount from the collectible child balance.
- The system supports installment children, due periods, active/inactive status, and the current collectible amount without inflating totals with future or draft installments.
- Regression confirmation shows other charge ledger data uses the authoritative installment children.

## Account Ledger History tab

Status: PASS

- The ledger history uses persisted financial events, payment records, cancellation entries, and related references.
- The response path no longer depends on undeployed DCM persistence.
- The project explicitly avoids fabricating history or contacting undeployed optional tables.

## Account Ledger UI / responsive tests

Status: PASS — authenticated live browser checks completed

- The ledger styling and shared UI implementation were unified and read-only by design.
- Browser regression checks cover the shared ledger behavior and the absence of duplicate opening/old placeholder dialogs.
- The repository documents the correct shared ledger design flow and runtime fix for duplicate “Coming Soon” handlers.
- The live browser verified centered, in-bounds dialog geometry, readable header/balance placement, non-overlapping title/actions, and horizontally scrollable bills table at 1440×900, 768×1024, and 390×844.
- Measured dialog sizes were approximately 1382×672, 737×822, and 374×818 respectively. The table wrapper remained horizontally scrollable at every size (table content width approximately 1383px).
- The tab controls switched Bills, Other Charges, and History at all three sizes; close, refresh, and reopen worked. Reopening produced one ledger dialog and no visible Coming Soon overlay.
- Light and dark theme switching was exercised and restored afterward. The ledger retained its light surface palette in dark mode; its dark text on the light surface remained high contrast and controls continued to work. This is recorded as a visual consistency note, not a functional failure.
- Defect found and fixed: before the CSS change, the tab bar collapsed to 1px at 390×844 and its buttons were clipped/unhittable. Adding `flex: 0 0 auto` to `.account-ledger__tabs` keeps it 48px high and restores tab interaction.

## All ledger entry points tested

Status: PASS

- The canonical shared ledger is reused across entry points rather than implemented in multiple places.
- The billing and collections flows are aligned to the same ledger component.
- Browser and API verification confirms the same shared logic is used.

## Read-only persistence verification

Status: PASS — automated regression and live before/after comparison

- The ledger read path is read-only in the retrieval and rendering logic.
- Regression checks verify no mutation of billings, reading status, other charges, installments, payments, receipts, penalties, or invoice snapshots during ledger rendering.
- The project explicitly avoids any record mutation on account-ledger read operations.
- Read-only transaction snapshots were taken immediately before and after opening, switching tabs, refreshing, closing, reopening, and closing the ledger for the selected mock account. Each row below reports row count and whether the stable-field SHA-256 signature changed; identifiers and signature values are omitted.

| Dataset | Before rows | After rows | Stable-field signature |
|---|---:|---:|---|
| Account key/status fields (PII excluded) | 1 | 1 | Unchanged |
| Billings and `InvoiceSnapshotJson` | 0 | 0 | Empty / unchanged |
| Readings | 0 | 0 | Empty / unchanged |
| Other Charges | 0 | 0 | Empty / unchanged |
| Other Charge Installments | 0 | 0 | Empty / unchanged |
| Penalties | 0 | 0 | Empty / unchanged |
| Payments | 0 | 0 | Empty / unchanged |
| Payment receipts | 0 | 0 | Empty / unchanged |

- No financial writes were issued during this verification pass. Database queries for the snapshots used read-only transactions.

## Browser regression results

Status: PASS

Evidence: automated synthetic browser regression rerun after the targeted mobile CSS fix and test-harness correction.

The following browser tests are present and were used during validation:

- `tests/BillingRegression/collections-browser-regression.cjs`
- `tests/BillingRegression/browser-regression.cjs`

Relevant result:
- `node tests/BillingRegression/collections-browser-regression.cjs` exited 0.
- All 31 Account Ledger browser checks passed across the synthetic Collections and Dashboard entry points. The run also reported passing Collections reconciliation, stale-cashier conflict, installment payment/cancellation, Dashboard Charging, and Escape behavior scenarios.
- The first run exposed stale assertions in the test file that still selected the old `.al-*` classes and expected old empty-state text. Those assertions were updated to match the canonical ledger classes/messages; the application markup and behavior were not changed for this test issue.
- The repository does not include a separate `account-ledger-browser-regression.cjs`; the ledger checks are covered under the shared collections browser regression rather than a non-existent file.
- The earlier `browser-regression.cjs` result remains PASS from the prior verification; it was not rerun because the changes were confined to the ledger CSS and Collections browser test.

## Authenticated runtime result

Status: PASS — seven user-reported manual passes plus agent-observed empty-history runtime

The following seven results were reported by the user as completed successfully on October 8, 2026. They remain user-reported manual results, not independently observed agent evidence:

1. **PASS — USER-REPORTED MANUAL PASS:** Authenticated login and Collections page load.
2. **PASS — USER-REPORTED MANUAL PASS:** Account Ledger opens correctly from Collections.
3. **PASS — USER-REPORTED MANUAL PASS:** Bills and Other Charges tabs load without server errors.
4. **PASS — USER-REPORTED MANUAL PASS:** Ledger Total Balance matches Collections.
5. **PASS — USER-REPORTED MANUAL PASS:** Payment and installment amounts display correctly.
6. **PASS — USER-REPORTED MANUAL PASS:** Ledger opens from Customer Information and Billing Collections.
7. **PASS — USER-REPORTED MANUAL PASS:** Reopening and refreshing the Ledger does not alter financial records.

Additional agent-observed authenticated results:

- An existing mock account with no financial rows loaded from Collections and returned 200 from the ledger endpoint.
- Collections and Ledger both showed `₱0.00`; Bills, Other Charges, and History displayed their empty states.
- Initial load, refresh, and reopen each returned HTTP 200. No JavaScript page errors were recorded.
- The responsive/theme, tab, close, and refresh checks and SQL snapshots are detailed above.
- During a page reload, the browser also logged a blocked Google Fonts request (`net::ERR_BLOCKED_BY_ORB`) and an aborted `/_framework/clear-browser-cache` request (`net::ERR_ABORTED`). Neither was an HTTP 500 or a ledger/API failure.

The MVC/API startup on ports 5054 and 5178 was agent-observed in the original verification pass. User-reported results and agent-observed results are kept distinct in this report.

## Responsive and theme verification

Status: PASS — browser interaction completed

- The browser measurements and control checks at 1440×900, 768×1024, and 390×844 are recorded under “Account Ledger UI / responsive tests”.
- After the mobile tab-bar fix, all three tabs were clickable at each requested size; the modal remained centered/in-bounds and the bills table remained horizontally scrollable.
- The global light/dark toggle changed the application theme; ledger content stayed legible and interactive in both states. The ledger currently uses the same light surface palette in dark mode.

## Live database read-only before/after comparison

Status: PASS — no persisted changes observed

- The before/after snapshot table is recorded under “Read-only persistence verification”.
- The account header signature and all scoped financial row counts/signatures remained unchanged across the ledger interactions.
- No fixture or financial records were written.

## Exact files changed in this verification pass

This verification pass changed the shared ledger stylesheet, corrected the synthetic browser regression assertions to match canonical markup, and updated this report. No financial business logic or schema files were changed.

- `TMCWD/TMCWD.Application/wwwroot/css/account-ledger.css`
- `tests/BillingRegression/collections-browser-regression.cjs`
- `docs/Phase6G-Final-Verification.md`

No financial logic or migration changes were required. The confirmed mobile layout defect and stale test assertions were fixed.

## Remaining defects or limitations

- The ledger keeps a light surface palette when the host is in dark mode. This was legible in the browser checks but remains a visual consistency consideration.
- The earlier standalone `browser-regression.cjs` run did not retain numeric counts; the current Collections browser run reported 31 Account Ledger checks, all passing.
- The NuGet advisory warnings (`NU1903` on `System.Security.Cryptography.Xml` 8.0.3) remain as package-management warnings, but they did not block this verification pass.

## Phase 6I handoff

- Phase 6G verification is complete against the stated gates. This does not authorize or start Phase 6I.
- No Phase 6I implementation or database work was started as part of this report update.
- The deployed development schema, authenticated empty-history behavior, responsive/theme behavior, and read-only persistence evidence are now recorded.
- Phase 6I work should be scoped and authorized separately, preserving the existing Phase 6G financial rules and records.

## Final conclusion

The verified repository state shows:

- Build: PASS
- Billing regression: PASS
- Other-charge/ledger schema: PASS; live development `tmcwd` metadata matches the migration
- Read-only ledger behavior: PASS; before/after database snapshots unchanged
- Browser regression and local validation: PASS
- Authenticated runtime checks: seven USER-REPORTED MANUAL PASS plus agent-observed empty-history PASS
- Responsive/theme matrix: PASS after mobile tab-bar fix

Therefore:

PHASE 6G COMPLETE

The recorded build and billing regressions passed, the live development schema matches the migration, the authenticated empty-history ledger passed, the responsive/theme checks passed after the targeted CSS fix, the live before/after database signatures were unchanged, and the Collections browser regression passed.
