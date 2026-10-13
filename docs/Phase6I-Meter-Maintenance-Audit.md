# Phase 6I — Meter Maintenance Fee Audit

**Audit date:** 2026-10-08  
**Scope:** Read-only source and regression-test inspection of server billing/posting, invoice snapshots, collections, arrears, and Flutter SOA presentation.  
**Implementation readiness:** **NOT READY** — a confirmed arrears calculation defect needs a narrowly scoped fix, and the district policy authority for the fee and its applicability is not present in the repository.

## Executive summary

- The runtime fee is a shared hardcoded **₱20.00** constant. No database field, runtime setting, effective-date history, or mobile-side fee constant/calculation was found.
- The server adds the fee once when it posts a bill. It persists water charges and maintenance as distinct immutable invoice snapshot components. Collections and the official Flutter service-invoice preview read those persisted components rather than recalculating the fee.
- `Billing.RemainingAmount` is the collectible balance for the current bill, inclusive of maintenance at posting, and excludes prior arrears, penalties, and Other Charge installments. Collections adds those separately. This is consistent with the documented current-bill-only meaning.
- Current collection payment requires exact full settlement of each selected bill. A partial water-bill payment is rejected. Cancellation restores the recorded pre-payment balance and reopens linked penalties/installments.
- **Confirmed defect:** the next bill's arrears calculation uses the full original snapshot water amount and maintenance, plus active penalty, but ignores `Billing.RemainingAmount` and `BillingAdjustment`. An adjusted but still-unpaid prior bill can therefore contribute more to the next invoice's arrears than its remaining collectible bill balance.
- **Policy gap:** the repository does not establish who authorized ₱20.00, any effective period, or whether every account/status is in scope. The JSON rate artifact says “every bill,” but it is not used by runtime code and is not sufficient evidence of approved district policy.
- No application code, migrations, or databases were changed. Regression tests are cited as existing coverage; they were not run as part of this read-only audit.

## Source of truth and applicability

| Finding | Evidence | Assessment |
|---|---|---|
| Runtime amount | [`BillingCharges.cs`](../TMCWD/TMCWD.Model/Billing/BillingCharges.cs), `BillingCharges.MeterMaintenanceFee` = `20.00m` | The runtime source is a compile-time shared constant, not a database setting. |
| Charge calculation | [`WaterChargeCalculator.cs`](../TMCWD/TMCWD.Billing/WaterChargeCalculator.cs), `ComputeFromRate` | Adds the shared constant to the water-rate calculation. The browser reading UI notes the fee is added after the reading is saved. |
| Official posting | [`ReadingSheetService.cs`](../TMCWD/TMCWD.Data/Services/ReadingSheetService.cs), `PartialPost` / `ComputeWaterCharge` | After a matching water rate is found, adds the shared fee once. The water tariff is selected by classification and meter size; the maintenance amount itself has no classification-, size-, or usage-specific branch. |
| Duplicate policy artifact | [`TMCWD_Water_Rates_Config.json`](../TMCWD_Water_Rates_Config.json) | Contains `waterMeterMaintenanceFee: 20.00` and says it applies regardless of classification, size, or usage. Repository search found no runtime consumer of this file; it can drift from the compiled constant and does not define effective dates. |
| Database-backed fee configuration | Repository source and schema references searched for maintenance fields/settings | No authoritative database fee field or dated maintenance-rate history was found. |
| Account status/effective date | Posting and charge calculation flow | No maintenance-specific status check or fee effective-date selection was found. Posting still requires a reading, a matching water rate, and the normal posting path. Whether inactive accounts can reach that path is governed elsewhere and is not evidence of an approved fee exemption policy. |
| District policy authority | Repository documentation/configuration reviewed | No approved district policy document, resolution, or rate schedule establishing the amount, scope, start date, or exemptions was identified. Do not infer policy from the code constant or unused JSON artifact. |

**Confirmed runtime rule:** every successfully posted bill that reaches the maintenance calculation gets the flat constant once, independent of the selected water-rate tier. **Unresolved policy questions:** whether ₱20.00 remains authorized; whether it applies to all account classifications, meter sizes, account statuses, and billing dates; and how future changes are to treat already-posted bills.

## Billing and official invoice

1. `WaterChargeCalculator` and the posting path use the same shared fee constant.
2. `ReadingSheetService.PartialPost` derives tier lines, sums them as `BillAmount`, adds `MeterMaintenance`, and verifies the combined amount against its calculation.
3. It writes an [`OfficialInvoiceSnapshot`](../TMCWD/TMCWD.Model/Billing/OfficialInvoiceSnapshot.cs) with separate water-only `BillAmount`, `MeterMaintenance`, charge lines, and other frozen components. `Billing.TotalBillAmount` and the initial `Billing.RemainingAmount` contain water plus maintenance.
4. `BillingService.SaveUpdate` rejects changes to an existing `InvoiceSnapshotJson`. The mobile SOA endpoint reads the persisted snapshot; payment and subsequent SOA reads do not recalculate or rewrite the fee.
5. The Flutter official service-invoice preview presents “Bill Amount” and “Meter Maintenance” as separate lines. The web Collections interfaces also receive and display a distinct `meterMaintenanceFee` component.

Existing regression cases in [`Program.cs`](../tests/BillingRegression/Program.cs) assert the ₱20.00 component, one-time inclusion in `TotalDue`/`TotalBillAmount`, separate water and maintenance fields, and preservation of snapshot fields after rate changes, payment, and repeated SOA reads. These are test-source findings, not a new execution result.

**Assessment:** current posting and invoice-snapshot implementation counts the fee exactly once and preserves the issued amount. No confirmed double charge was found in this path.

## Collections, arrears, payments, and penalties

### Current balance and collection

[`BillingService.GetCollectionBill`](../TMCWD/TMCWD.Data/Services/BillingService.cs) reads water and maintenance from the persisted snapshot. It obtains the live current-bill balance from `Billing.RemainingAmount`, then adds unpaid penalties and currently collectible Other Charge installments to `AmountDue`. It does not add snapshot arrears again to that amount.

[`CollectionsController.SearchAccounts`](../TMCWD/TMCWD.Application/Controllers/CollectionsController.cs) exposes the component totals separately. This avoids counting maintenance as an Other Charge or adding it a second time to the payable balance.

`Billing.RemainingAmount` therefore has this observed meaning:

- At posting: current water charge plus the frozen maintenance fee.
- After billing adjustments: current bill balance after those adjustments, subject to the maintenance floor described below.
- After full payment: zero.
- Prior-bill arrears, active penalties, and Other Charge installments are handled separately, not folded into this field.

### Full/partial payment and cancellation

[`PaymentService.ProcessCollectionPayment`](../TMCWD/TMCWD.Data/Services/PaymentService.cs) calculates the due amount from the current remaining balance, active penalties, collectible installments, and any validated discount; the submitted `PaidAmount` must equal that amount. Its regression coverage explicitly rejects partial and excess payments without changing the balance. Thus, **partial water-bill collection is not supported by the current flow**; no partial maintenance allocation exists in that flow.

On full payment, the bill is marked paid and its remaining balance is set to zero; applicable penalties and the exact installments are settled and linked to the payment. `CancelInvoice` restores the payment's recorded `BalanceBefore` and reopens the penalties/installments linked to that payment. Since the fee is included in the pre-payment bill balance, a full-payment cancellation restores that balance, including maintenance. Existing cancellation tests verify restoration and linked-charge behavior, but do not isolate an assertion specifically named for the maintenance component.

Other Charge installments remain a separate collectible model; they are not part of the frozen maintenance fee or the water-only penalty base.

### Arrears defect

`ReadingSheetService.ComputeBillArrearsAmount` currently includes, for each earlier Unpaid/Overdue bill:

`snapshot.BillAmount + snapshot.MeterMaintenance + active prior penalty`

It excludes the prior snapshot's own arrears (avoiding recursive historical arrears), but does not reduce the original snapshot components by `BillingAdjustment` or use the bill's current `RemainingAmount`.

This is a confirmed mismatch when an unpaid bill has an adjustment. The existing collection regression fixture demonstrates a ₱100 water bill plus ₱20 maintenance with a ₱30 adjustment: the current payable `RemainingAmount` is ₱90, while the arrears helper would include ₱120 before penalty. Therefore the subsequent invoice's arrears component can overstate the unpaid prior bill by the adjustment amount. This does not change the frozen prior snapshot, but it can make the next official invoice's displayed arrears exceed the prior bill's collectible balance.

### Penalty and financial invariants

- Collection's informational after-due penalty is calculated from snapshot water-only `BillAmount`, not water plus maintenance.
- Persisted penalties are separate from maintenance and arrears.
- The source and tests preserve the 10% water-only penalty rule; maintenance, Other Charges, and arrears do not enter its base.
- Historical snapshots are not recalculated from current rates, and SOA GET is read-only with respect to financial records.
- The arrears calculation does not recursively add a prior snapshot's `ArrearsAmount`.

### Adjustment-floor risk

[`BillingAdjustmentService.cs`](../TMCWD/TMCWD.Data/Services/BillingAdjustmentService.cs), `SaveUpdate` and `SaveBatch`, set `RemainingAmount` to at least the current compiled `BillingCharges.MeterMaintenanceFee`. This preserves a ₱20 floor under today's constant, but the floor is not read from that bill's immutable snapshot. If the runtime constant changes, adjustment of an old bill can use the new fee as its minimum even though the issued invoice records the old amount. A historical bill without a valid snapshot also has no verified fee amount to justify this floor.

This is a **policy-dependent historical-balance risk**, not evidence that current ₱20 bills are presently miscalculated. Confirm adjustment and legacy-bill policy before changing the floor; do not rewrite historical snapshots.

## Flutter Meter Reader

- No provisional/local maintenance calculation was found in Flutter. The local reading model stores reading work, not a calculated bill or maintenance amount; the reading-entry path does not construct a provisional SOA.
- `MobileReadingSheetController.Soa` returns the persisted official snapshot components. `OfficialSoa` parses the returned maintenance amount, and `ServiceInvoicePreview` renders it separately.
- The primary Statement of Account screen presents the server-provided official balance/status; it does not independently calculate or add the maintenance fee.
- `ReadingSheetService` caches the exact official SOA payload in `official_soa_cache` and can display that persisted response offline. The current mobile schema is version 3. The cache does not reconstruct the fee from local usage or current policy.
- Existing Flutter tests cover parsing/rendering the official maintenance line and preserving its exact value in the offline official-SOA cache. The principal SOA display tests use total/remaining amounts and do not establish a locally calculated or provisional fee.

**Assessment:** no server/mobile fee-calculation divergence was found. The mobile display is server-issued/cached, not a provisional billing authority.

## Regression coverage and gaps

### Existing test-source coverage found

| Area | Existing coverage |
|---|---|
| Fee amount and one-time posting | [`Program.cs`](../tests/BillingRegression/Program.cs): snapshot and `TotalDue` assertions for ₱20.00; fee separated from water-only `BillAmount`. |
| Collection component and penalty separation | `Program.cs`: collection reads snapshot water/maintenance and applies water-only penalty; later rate changes do not change the collection response. |
| Snapshot immutability | `Program.cs`: rate, payment, repeated posting, and SOA GET preserve issued snapshot fields. |
| Adjustment display | `Program.cs`: an adjustment changes payable remaining balance without inventing water or maintenance components. |
| Partial payment rejection and cancellation | `Program.cs`: partial/excess submissions rejected; cancellation restores linked bill/penalty/installment state. |
| Mobile official invoice | [`service_invoice_preview_test.dart`](../TMCWD.MeterReader.Mobile/test/service_invoice_preview_test.dart): invoice component ordering and distinct maintenance line. |
| Mobile offline SOA | [`soa_cache_test.dart`](../TMCWD.MeterReader.Mobile/test/soa_cache_test.dart): exact official snapshot, including maintenance, survives offline cache/retrieval. |

These tests were inspected but not executed for this audit.

### Recommended additions

1. **Arrears after adjustment:** post a synthetic bill, apply a known current-bill adjustment, leave it Unpaid, post the next period, and assert arrears reflect the actual unpaid water-plus-maintenance balance plus active penalty—not the original snapshot total. Include multiple prior bills to protect non-recursive behavior.
2. **Adjustment and historical fee:** prove an adjustment to an old invoice uses its frozen maintenance amount, and define safe behavior for a historical bill with no maintenance snapshot.
3. **Payment/cancellation with maintenance:** assert a full payment settles the precise water-plus-maintenance balance and cancellation restores exactly that amount without changing the snapshot.
4. **Fee applicability policy:** once district policy is supplied, add representative account classification, meter size, status, and effective-date boundary cases. Until then, do not encode guessed exceptions.
5. **Mobile contract:** include `billAmount` and `meterMaintenance` in the official SOA widget fixture and verify the preview displays the server value unchanged; retain the offline exact-payload assertion. Do not add a local/provisional calculator absent an explicit phase and policy decision.
6. **Partial collection contract:** retain a direct negative test that partial water-bill amounts are rejected. If district policy requires partial payments, treat that as a separate authorized financial-workflow change with explicit fee/payment allocation and cancellation tests.

## Defects, risks, and minimum recommended changes

| Classification | Finding / financial impact | Minimum action |
|---|---|---|
| **Confirmed defect** | Arrears use original frozen bill components instead of the current unpaid bill balance; adjustments can be counted again in a later invoice's arrears. | Correct the arrears basis to the authoritative outstanding current-bill principal plus the intended active prior penalties, while continuing to exclude prior arrears and preserving immutable invoice snapshots. Add the regression cases above. |
| **Policy gap** | ₱20.00 amount, universal applicability, exemptions, and effective dates have no authoritative policy record in the repository. The JSON artifact is unused at runtime. | Obtain approved district policy before changing the amount, eligibility, or effective-date behavior. Then maintain one runtime source and prevent the unused artifact from presenting itself as authoritative. |
| **Conditional historical-balance risk** | Adjustment recalculation floors balances at the current constant instead of the invoice's frozen maintenance amount; legacy invoices may not have that component. | Confirm policy; if adjustment behavior is changed, base any retained fee component on that invoice's authoritative snapshot and define handling for missing snapshots. Do not recalculate or mutate old invoice snapshots. |
| **Workflow contract** | Current collection code rejects partial water-bill payments. | No change unless partial collection is explicitly approved; if approved, specify allocation and cancellation semantics first. |

## Phase 6G contract preservation

This audit found no reason to alter the verified Phase 6G behavior. Preserve the immutable issued invoice snapshot, current-bill-only `Billing.RemainingAmount`, snapshot-based Collections amounts, 10% water-only penalty base, and installment-only Other Charges collection model. The recommended arrears fix must not fold prior arrears into `RemainingAmount`, mutate historical invoice snapshots, or count maintenance as an Other Charge.

## Final verdict

**Phase 6I audit: COMPLETE. Implementation readiness: NOT READY.**

The posting, snapshot, Collections, and Flutter official-SOA paths consistently use the server-issued maintenance amount and currently include it once. However, the confirmed adjusted-arrears mismatch requires correction and regression protection. The district's authoritative amount/applicability policy and the historical adjustment-floor policy are also unresolved. No code or database changes are authorized or performed by this audit; stop here pending policy/implementation approval.
