# Phase 6G canonical Account Ledger — October 8, 2026

This is the initial implementation report. Subsequent authenticated runtime verification exposed an undeployed DCM dependency and duplicate Ledger Coming Soon handler that the initial checks missed. Both are fixed; see the [HTTP 500 fix and authenticated verification report](Phase6G-Account-Ledger-HTTP500-Fix.md), which supersedes this report's DCM history and completion claims. The prior installment migration and financial cleanup remain preserved.

1. **Entry-point audit.** Financial ledgers existed in `Views/Collections/Collections.cshtml`, the duplicate `Views/Billing/Collections.cshtml`, Customer Information's Ledger and Bills actions, and the Dashboard service-request Ledger action. The first three ledger dialogs copied markup and used `js/collections/ledger-modal.js`; Customer Bills and Dashboard also used `/Billing/History` with separate rendering. The old ledger script and Dashboard independently computed debit-minus-credit balances. Their export actions were placeholders. Billing Adjustment's Billing History is an interactive bill selector and remains separate. Collections Other Charges is a payment surface, not an account ledger. Customer Adjustments, receipt search/reprint, and unrelated operational/report histories retain their existing purpose.

2. **Unified entry points.** Both Collections views, Customer Information Ledger and Bills, and Dashboard service-request Ledger now open the same component. Dashboard Charging additionally has an Account Ledger button that selects Other Charges initially. All three tabs remain available from every entry point. All existing financial-ledger containers were modals, so one native dialog suffices; no speculative page/drawer framework was added.

3. **Shared Razor.** `TMCWD/TMCWD.Application/Views/Shared/_AccountLedger.cshtml` is included once by each of the four views. It owns the account header, total balance, Bills / Other Charges / History tabs, and tables.

4. **Shared JavaScript.** `TMCWD/TMCWD.Application/wwwroot/js/account-ledger.js` exposes `openAccountLedger(accountNumber, { initialTab })`. It fetches one backend source on every opening or Refresh. It handles loading, missing accounts, authorization failures, retry, request abortion, and stale-response protection. It renders untrusted data with DOM `textContent`, supports keyboard tab navigation, and uses native dialog focus/modal behavior. Dashboard's Escape handler leaves an open ledger to native dialog handling.

5. **Shared CSS.** `TMCWD/TMCWD.Application/wwwroot/css/account-ledger.css` uses existing theme variables and centralizes typography, header, balance, badges, tables, spacing, and narrow-screen behavior. Wide financial tables scroll horizontally without shrinking their contents.

6. **Canonical backend.** MVC `GET /Billing/AccountLedgerData?accountNumber=...` calls `BillingTransaction.GetAccountLedger`, which calls Data API `GET /api/Billing/AccountLedger/{accountNumber}`. `AccountLedgerService.Get` owns the query and returns `TMCWD.Model.Billing.AccountLedgerDto`. The API requires the existing `BillingOperations` policy, with server-validated identity and finance roles. Invalid input returns 400, missing accounts 404, inconsistent financial records 409, and unauthenticated requests 401. Reads run in a repeatable-read transaction and perform no financial writes.

7. **Header sources.** Account and linked Customer records provide account number, name, service address, meter number, classification and account status. Shared enums provide classification/status names. No authoritative Rate Code field exists in the inspected account contract, so the UI displays an em dash. Page-local account display values never supply financial/header truth.

8. **Total Balance.** Each persisted account billing row is read through existing `BillingService.GetCollectionBill`. The ledger sums its `AmountDue`: eligible live `Billing.RemainingAmount` plus applicable persisted active penalties plus collectible child installments. It never adds parent contractual Amount, future/paid installments, inactive plans, arrears, or snapshot TotalDue. Existing settled/waived exclusions remain authoritative. Calculations use backend decimal values.

9. **Bills.** Persisted Billings supply month, reference, service invoice identity, payment status and receipt reference. Water/maintenance components come from immutable invoice snapshots through CollectionBill; historical missing components remain unavailable. Exact linked Readings and ReadingSheets supply reading/billing/due dates, with no nearest-reading or scheduling reconstruction. Synthetic charge-only anchors do not acquire invented reading/billing dates or water-posting history. Bills are ordered by billing month and ID descending. Maintenance is descriptive within the water row and is not added to the live balance a second time.

10. **Other Charges.** The existing parent-by-reference query supplies ordered child records; its optional inactive-plan inclusion is used only for ledger metadata. Existing callers keep their active-only behavior. The shared installment contract now also includes persisted activation reference/time. Parents show type, particulars, contractual total, installment count and status; inactive plans remain visible with zero collectible balance. Children show sequence, due month, amount, status, receipt and activation reference. Child Draft displays as Future without changing the persisted enum. Per-parent and account Collectible Now come exclusively from CollectionBill's existing `GetCollectibleByReference` results.

11. **History.** Newest-first events use persisted timestamps and stable event keys: Billing.DateCreated for water posting, parent DateCreated for contract creation, child ActivatedAt for activation, Penalty.DateCreated, Payment.DateCreated, InvoiceCancellation.DateCancelled, BillingAdjustment.DateCreated and DebitCreditMemo.DateCreated. Contract creation and type-specific adjustment/memo amounts are informational with no invented debit/credit or balance impact. Memo creation does not imply posting. Referenced payment lines are grouped by persisted receipt reference within the account; unreferenced payments remain separate. Original cancelled payments remain visible; unsuccessful rows are only labelled Cancelled when an actual cancellation record exists. Exact child details are shown while SettledPaymentId still links them. Cancellation clears that relationship, so the ledger does not invent historical child mappings. Separate invoice-issue or parent-post events are omitted where no authoritative issue/post timestamp exists. No running balance is reconstructed.

12. **Collections reconciliation.** Database tests compare Account Ledger TotalBalance directly with CollectionBill.AmountDue, and ledger Other Charges with the collectible-child query. The browser fixture verifies water 397 + collectible installment 250 = total 647; the 1,000 contract and three future installments do not inflate it. Payment reload changes the balance to 397 and cancellation restores 647.

13. **Charging reconciliation.** The actual Dashboard Charging workspace and canonical ledger both display collectible Other Charges 250 for the same synthetic state; this matches Collections. Backend checks compare the underlying child sums used by these surfaces.

14. **Dashboard queue semantics.** The Charging queue card remains an application count. The Charging workspace's monetary total continues to sum server `collectibleAmount`. A queue count is not compared with a monetary ledger balance.

15. **Payment/cancellation refresh.** Every ledger opening fetches fresh server state; Refresh reloads the active account without changing tabs. Actual browser payment and cancellation actions are followed by ledger reopening and reconciliation. Persisted receipt amounts are not recalculated from current state; existing reprint behavior remains in place.

16. **Exports.** Old ledger PDF/Excel actions were unimplemented placeholders. The canonical component hides those unavailable actions. No export subsystem or duplicate export calculation was added.

17. **Removed duplicates.** Removed three copied ledger dialogs, Customer Bills' separate modal/table/pagination/rendering, Dashboard's separate ledger dialog/calculation, the obsolete ledger script, the `.ldg-*` CSS section, Dashboard ledger CSS, unused ledger export selectors and Customer's obsolete ledger toast. The only remaining `/Billing/History` browser caller is the unrelated Billing Adjustment bill selector.

18. **Exact source/document files changed for this task.** Earlier dirty work was preserved. The implementation changes are:

    - `TMCWD/TMCWD.Model/Billing/AccountLedgerDto.cs` — new
    - `TMCWD/TMCWD.Model/Billing/OtherChargeInstallments.cs`
    - `TMCWD/TMCWD.Data/Services/AccountLedgerService.cs` — new
    - `TMCWD/TMCWD.Data/Services/OtherChargeService.cs`
    - `TMCWD/TMCWD.Data/Controllers/BillingController.cs`
    - `TMCWD/TMCWD.Data/Program.cs`
    - `TMCWD/TMCWD.Billing/BillingTransaction.cs`
    - `TMCWD/TMCWD.Application/Controllers/BillingController.cs`
    - `TMCWD/TMCWD.Application/Views/Shared/_AccountLedger.cshtml` — new
    - `TMCWD/TMCWD.Application/Views/Collections/Collections.cshtml`
    - `TMCWD/TMCWD.Application/Views/Billing/Collections.cshtml`
    - `TMCWD/TMCWD.Application/Views/CustomerSupport/CustomerInformation.cshtml`
    - `TMCWD/TMCWD.Application/Views/Admin/Index.cshtml`
    - `TMCWD/TMCWD.Application/wwwroot/js/account-ledger.js` — new
    - `TMCWD/TMCWD.Application/wwwroot/js/dashboard.js`
    - `TMCWD/TMCWD.Application/wwwroot/js/collections/ledger-modal.js` — deleted
    - `TMCWD/TMCWD.Application/wwwroot/css/account-ledger.css` — new
    - `TMCWD/TMCWD.Application/wwwroot/css/collections.css`
    - `TMCWD/TMCWD.Application/wwwroot/css/dashboard.css`
    - `TMCWD/TMCWD.Application/wwwroot/css/document-actions.css`
    - `tests/BillingRegression/AccountLedgerRegression.cs` — new
    - `tests/BillingRegression/Program.cs`
    - `tests/BillingRegression/collections-browser-regression.cjs`
    - `docs/Phase6G-Canonical-Account-Ledger.md` — this report
    - `docs/Phase6G-Financial-Model-Cleanup.md` — completion cross-reference; historical cleanup details preserved

    Isolated build output, diagnostic logs and one-off edit helpers are under `.tmp`; they are not application source. No database scripts, Flutter files, unrelated histories or Penalty Charging implementation were changed by this task.

19. **Build.** `dotnet build TMCWD/TMCWD.slnx --nologo --artifacts-path .tmp/phase6g-ledger-artifacts` passed, zero errors. The final incremental build reports 14 NuGet vulnerability-feed connectivity warnings. An earlier full compile reported the repository's existing nullable/analyzer warnings; these were not suppressed. Windows locked running apphost files during an intermediate rebuild; the verified Development hosts were stopped, and the exact required build subsequently passed.

20. **BillingRegression.** `dotnet run --project tests/BillingRegression --artifacts-path .tmp/phase6g-ledger-artifacts -- .` passed: 98 temporary-table database regression groups, including 19 Account Ledger groups, plus five pure penalty-rounding cases, MySQL query translation and existing real connection/concurrency/migration suites. New coverage includes authoritative headers/bills, ordered children, future/paid/inactive exclusions, no contract/arrears double counting, reconciliation, persisted chronology, repeated read safety, invalid/missing accounts, payment/cancellation reload, immutable snapshots, informational pending memos, missing dates, inconsistent-plan rejection, and distinct unreferenced payments. Tests use the dedicated local synthetic test database and connection-local tables; no live application data is changed.

21. **Browser regressions.** `node tests/BillingRegression/collections-browser-regression.cjs` passed its existing Collections/Other Charges/cancellation/Charging scenarios plus 26 ledger checks across actual Collections and Dashboard entry points. Shared structure, header truth, tabs, status labels, contract/collectible distinction, XSS safety, keyboard behavior, responsive overflow, safe missing-account handling, payment/cancellation reload, reconciliation and native Escape behavior are covered. `node tests/BillingRegression/browser-regression.cjs` passed 25/25, zero skipped. The existing headless Edge harness was extended rather than duplicated into a dedicated suite. Headless Edge required execution outside the sandbox. The separate Penalty Charging browser issue was not changed.

22. **Local hosts.** Both hosts were restarted successfully from the final isolated build using `dotnet run --project TMCWD/TMCWD.Data --launch-profile http --no-build --artifacts-path .tmp/phase6g-ledger-artifacts` and the equivalent `TMCWD/TMCWD.Application` command. They run hidden in Development on Data API port 5178 and MVC port 5054. Port owners were identified by PID, executable name and repository path before stopping only TMCWD processes; unrelated processes were not stopped. Startup logs confirm both listening addresses and Development environment. Final HTTP checks passed: anonymous ledger API 401, forged bearer 401, anonymous MVC ledger 401, shared JavaScript 200 and shared CSS 200. These live checks validate startup/routing/authentication; financial behavior was verified separately with the synthetic backend and browser regressions above.

23. **Remaining scope.** No blocking issue remains in the requested unified Account Ledger scope. Rate Code and unsupported exports remain unavailable. History deliberately omits unsupported timestamps, cancelled child mappings that were cleared by the existing workflow, and reconstructed running balances. These are required truthful-display limits. The separate previously reported Penalty Charging browser issue remains outside this task and was not modified.
