const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../..');
const source = fs.readFileSync(path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/penalties.js'), 'utf8');
function page(fetch) {
    const handlers = {};
    const button = { disabled: false, addEventListener: (_, fn) => handlers.waive = fn };
    const reason = { value: 'Customer requested medical consideration.' };
    const alerts = [];
    const context = vm.createContext({
        fetch,
        document: {
            addEventListener: (_, fn) => handlers.ready = fn,
            getElementById: id => id === 'waivePenaltyBtn' ? button : id === 'waiverReason' ? reason : null,
            querySelectorAll: () => [{ dataset: { ref: 'BILL-1' } }],
            querySelector: () => ({ value: 'test-token' }),
        },
    });
    vm.runInContext(source, context);
    vm.runInContext("isLoaded = true; penaltyList = [{billingReferenceId: 'BILL-1'}];", context);
    context.renderTable = () => {};
    context.alertModal = (...args) => alerts.push(args);
    handlers.ready();
    return { button, alerts, click: () => handlers.waive(), count: () => vm.runInContext('penaltyList.length', context) };
}
const settle = () => new Promise(resolve => setImmediate(resolve));
test('Confirmed request uses server count, reason, and antiforgery token', async () => {
    let options;
    const p = page(async (_, opts) => { options = opts; return { ok: true, text: async () => JSON.stringify({ submittedCount: 2 }) }; });
    p.click();
    assert.equal(p.button.disabled, true);
    await settle();
    assert.equal(options.headers.RequestVerificationToken, 'test-token');
    assert.match(options.body, /Customer requested medical consideration/);
    assert.equal(p.count(), 1);
    assert.match(p.alerts[0][1], /2 waiver request/);
    assert.equal(p.button.disabled, false);
});
for (const failure of ['HTTP error', 'network error', 'zero updates', 'invalid JSON']) {
    test(`${failure} preserves rows and never displays success`, async () => {
        const p = page(async () => {
            if (failure === 'network error') throw new Error('offline');
            return { ok: failure !== 'HTTP error', text: async () => failure === 'invalid JSON'
                ? 'not JSON' : JSON.stringify({ submittedCount: 0 }) };
        });
        p.click();
        await settle();
        assert.equal(p.count(), 1);
        assert.equal(p.alerts[0][2], 'error');
        assert.equal(p.button.disabled, false);
    });
}
test('Repeated clicks while pending send only one waiver request', async () => {
    let calls = 0;
    let resolve;
    const p = page(() => { calls++; return new Promise(r => resolve = r); });
    p.click(); p.click();
    assert.equal(calls, 1);
    resolve({ ok: true, text: async () => JSON.stringify({ submittedCount: 1 }) });
    await settle();
    assert.equal(p.button.disabled, false);
});

test('Penalty waiver entries are selected and loaded by due date', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Billing/Penalty.cshtml'), 'utf8');
    const controller = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Controllers/BillingController.cs'), 'utf8');

    assert.match(view, /for="dueDate">Due Date/);
    assert.doesNotMatch(view, /for="billingDate">Billing Date/);
    assert.match(source, /GetPenaltiesByDueDate\?dueDate=/);
    assert.match(controller, /GetPenaltiesByDueDate\(DateTime dueDate\)/);
});

test('Present Reading uses the tapping baseline and excludes pending accounts', () => {
    const controller = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Controllers/BillingController.cs'), 'utf8');
    const accountService = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Services/AccountService.cs'), 'utf8');
    const readingService = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Services/ReadingService.cs'), 'utf8');

    assert.match(controller, /latestReading\?\.CurrentReading \?\? account\.InitialReading/);
    assert.match(controller, /currentReading\?\.CurrentReading \?\? account\.InitialReading/);
    assert.match(controller, /account\.Status == AccountStatus\.Active/);
    assert.match(controller, /Complete the connection and House Tapping workflow/);
    assert.match(accountService, /JsonSerializer\.Deserialize<HouseTappingRecord>/);
    assert.match(readingService, /reading\.PreviousReading = tapping\.InitialReading/);
});

test('Reading Sheet progress counts active accounts in each Zone and Book assignment', () => {
    const controller = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Controllers/ReadingSheetController.cs'), 'utf8');
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Billing/ReadingSheet.cshtml'), 'utf8');
    const readingSheetScript = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/reading-sheet.js'), 'utf8');

    assert.match(controller, /GetByZoneBookAndSequence\(/);
    assert.match(controller, /Where\(x => x\.Status == AccountStatus\.Active\b/);
    assert.match(controller, /var totalAccounts = eligibleAccountIds\.Count/);
    assert.match(readingSheetScript, /var actualDone = Number\(row\.totalCompleted \|\| 0\)/);
    assert.match(readingSheetScript, /var actualTotal = Number\(row\.totalAccounts \|\| 0\)/);
    assert.match(readingSheetScript, /row\.zone \+ '-' \+ row\.book/);
    assert.match(view, /Zone - Book/);
    assert.doesNotMatch(readingSheetScript, /zoneStats\[r\.zone\]/);
});

test('Phase 4 invoice workflows are live and do not use simulated success', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Collections/Collections.cshtml'), 'utf8');
    for (const script of ['invoice-search', 'reprint-invoice', 'cancel-invoice']) {
        assert.match(view, new RegExp(`<script[^>]+${script}`));
    }
    assert.doesNotMatch(view, /id="invoiceSearchBtn"[^>]+aria-disabled="true"/);
    assert.doesNotMatch(view, /id="reprintInvoiceBtn"[^>]+aria-disabled="true"/);
    assert.doesNotMatch(view, /id="cancelInvoiceBtn"[^>]+aria-disabled="true"/);
    assert.match(view, /data-modal="discount" title="Apply Senior Citizen discount"/);
    const invoiceSearch = fs.readFileSync(path.join(root,
        'TMCWD/TMCWD.Application/wwwroot/js/collections/invoice-search.js'), 'utf8');
    const reprint = fs.readFileSync(path.join(root,
        'TMCWD/TMCWD.Application/wwwroot/js/collections/reprint-invoice.js'), 'utf8');
    const cancel = fs.readFileSync(path.join(root,
        'TMCWD/TMCWD.Application/wwwroot/js/collections/cancel-invoice.js'), 'utf8');
    assert.match(invoiceSearch, /\/Collections\/SearchInvoices/);
    assert.match(reprint, /\/Collections\/Invoice\?invoiceNumber=/);
    assert.match(cancel, /\/Collections\/CancelInvoice/);
    assert.match(cancel, /RequestVerificationToken/);
    assert.doesNotMatch(cancel, /new Promise\(function \(resolve\).*setTimeout/);
});

test('Collection payment posts an antiforgery token and displays the included meter fee separately', () => {
    const collectionSource = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/collections.js'), 'utf8');
    const controller = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Controllers/CollectionsController.cs'), 'utf8');
    assert.match(collectionSource, /RequestVerificationToken/);
    assert.match(controller, /GetCollectionBill\(billing\.BillingReferenceId\)/);
    assert.match(controller, /meterMaintenanceFee = summary\.MeterMaintenanceFee/);
    assert.doesNotMatch(controller, /WaterChargeCalculator|billBeforeOtherCharges - meterMaintenanceFee/);
    assert.match(collectionSource, /seniorCitizenDiscount: payment\.seniorCitizenDiscount/);
    assert.match(collectionSource, /totals\.amount \* activeDiscount\.rate/);
    assert.doesNotMatch(collectionSource, /grossTotal \* activeDiscount\.rate/);
});

test('OIC and Cashier logins redirect to their authorized work areas', () => {
    const home = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Controllers/HomeController.cs'), 'utf8');
    assert.match(home, /case \(int\)UserRole\.CustomerServiceOic:[\s\S]*?RedirectToAction\("Penalty", "Billing"\)/);
    assert.match(home, /case \(int\)UserRole\.Cashier:[\s\S]*?RedirectToAction\("Index", "Collections"\)/);
});

test('Logout posts to the server and clears the authenticated session', () => {
    const home = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Controllers/HomeController.cs'), 'utf8');
    const layout = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Shared/_InsideLayout.cshtml'), 'utf8');
    assert.match(home, /\[ValidateAntiForgeryToken\][\s\S]*?IActionResult Logout\(\)[\s\S]*?Session\.Clear\(\)/);
    assert.match(layout, /<form asp-controller="Home" asp-action="Logout" method="post"/);
    assert.match(layout, /<button type="submit" class="nav-link nav-link--logout"/);
});

test('Dashboard service cards open live account and other-charge workspaces', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Admin/Index.cshtml'), 'utf8');
    const dashboard = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/dashboard.js'), 'utf8');

    assert.match(view, /data-service-category=/);
    assert.match(view, /id="applicationsModal"/);
    assert.match(view, /id="chargeModal"/);
    assert.match(view, /@Html\.AntiForgeryToken\(\)/);
    assert.match(view, /<script[^>]+dashboard\.js/);
    assert.match(dashboard, /\/Admin\/GetServiceApplications\?category=/);
    assert.match(dashboard, /\/Billing\/GetChargeTypes/);
    assert.match(dashboard, /\/Billing\/GetOtherChargesByAccount\?accountNumber=/);
    assert.match(dashboard, /\/Billing\/SaveOtherCharge/);
    assert.match(dashboard, /RequestVerificationToken/);
});

test('All Other Charges screens receive a shared charge-type fallback list', () => {
    const billing = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Controllers/BillingController.cs'), 'utf8');
    const collections = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/collections/oc-modal.js'), 'utf8');
    const dashboard = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/dashboard.js'), 'utf8');
    const page = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Billing/OtherCharges.cshtml'), 'utf8');
    const pageScript = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/other-charges.js'), 'utf8');

    assert.match(billing, /GetChargeTypes\(\)[\s\S]*?types\.Count > 0/);
    assert.match(billing, /MSR - Recon\. Fee/);
    assert.match(billing, /Reconnection Fee/);
    assert.match(billing, /Miscellaneous Charge/);
    assert.match(collections, /fetch\('\/Billing\/GetChargeTypes'\)/);
    assert.match(dashboard, /fetch\('\/Billing\/GetChargeTypes'\)/);
    assert.match(page, /id="ocPostChargesBtn"/);
    assert.match(pageScript, /\/Billing\/PostOtherCharges/);
    assert.match(dashboard, /dashboardPostCharges/);
    assert.match(dashboard, /fetch\('\/Billing\/PostOtherCharges'/);
});

test('Inspection applications use the inspection form and printable report workflow', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Admin/Index.cshtml'), 'utf8');
    const dashboard = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/dashboard.js'), 'utf8');
    const controller = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Controllers/DashboardController.cs'), 'utf8');

    assert.match(view, /id="inspectionModal"/);
    assert.match(view, /id="inspectionSaveButton"/);
    assert.match(view, /id="inspectionPdfButton"/);
    assert.match(dashboard, /state\.category === 'inspection'/);
    assert.match(dashboard, /\/Admin\/GetServiceInspection\?requestId=/);
    assert.match(dashboard, /\/Admin\/SaveServiceInspection/);
    assert.match(dashboard, /new Pdf\(\{ orientation: 'portrait'/);
    assert.match(dashboard, /NEW CONNECTION REPORT/);
    assert.match(dashboard, /createFormalInspectionPdf/);
    assert.match(dashboard, /Needs Excavation Permit/);
    assert.match(dashboard, /SERIAL NUMBER/);
    assert.match(dashboard, /Guarantee Deposit/);
    assert.match(dashboard, /Inspection performed in the presence of/);
    assert.match(dashboard, /RequestVerificationToken/);
    assert.match(controller, /HttpGet\("ServiceInspection\/\{requestId:long\}"\)/);
    assert.match(controller, /HttpPost\("SaveServiceInspection\/\{userId:long\}"\)/);
    assert.match(controller, /isApproval \? \(record\.ChargeNscFee \? "charging" : "installation"\)/);
    assert.match(controller, /PaymentStatus\s*= \(int\)PaymentStatus\.Draft/);
    assert.match(dashboard, /moved directly to Installation/);
    assert.match(dashboard, /Continue to Payment/);
});

test('Connect applications use the connect-order approval workflow', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Admin/Index.cshtml'), 'utf8');
    const dashboard = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/dashboard.js'), 'utf8');
    const controller = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Controllers/DashboardController.cs'), 'utf8');

    assert.match(view, /id="connectOrderModal"/);
    assert.match(view, /Customer Service Representative/);
    assert.match(view, /id="connectOrderSaveButton"/);
    assert.match(dashboard, /state\.category === 'connect'/);
    assert.match(dashboard, /\/Admin\/GetConnectOrder\?requestId=/);
    assert.match(dashboard, /\/Admin\/SaveConnectOrder/);
    assert.match(controller, /source\.request\.CreatedBy/);
    assert.match(controller, /HttpPost\("SaveConnectOrder\/\{userId:long\}"\)/);
});

test('Dashboard cards have explicit workflows and only Charging uses Other Charges', () => {
    const dashboard = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/dashboard.js'), 'utf8');
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Admin/Index.cshtml'), 'utf8');
    const collections = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/collections.js'), 'utf8');
    const controller = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Controllers/DashboardController.cs'), 'utf8');

    assert.match(dashboard, /state\.category === 'charging'[^\n]+openChargeWorkspace/);
    assert.match(dashboard, /state\.category === 'payment'[^\n]+openPaymentWorkspace/);
    assert.match(dashboard, /state\.category === 'installation'[^\n]+openInstallationServiceRequest/);
    assert.match(dashboard, /state\.category === 'tapping'[^\n]+openHouseTapping/);
    assert.match(dashboard, /state\.category === 'applications'[^\n]+openOnlineApplicationReview/);
    assert.match(dashboard, /state\.category === 'new'[^\n]+openNewApplicationReview/);
    assert.match(dashboard, /state\.category === 'tickets'[^\n]+openTicketWorkspace/);
    assert.match(dashboard, /\/Admin\/GetApplicationTicket\?applicationId=/);
    assert.doesNotMatch(dashboard, /else\s+openChargeWorkspace\(application\)/);
    assert.match(view, /id="requestReviewModal"/);
    assert.match(view, /id="workOrderModal"/);
    assert.match(view, /id="houseTappingModal"/);
    assert.match(view, /id="tappingMeterBrand"/);
    assert.match(view, /id="tappingAccomplishedBy"/);
    assert.match(dashboard, /\/Admin\/GetHouseTapping\?requestId=/);
    assert.match(dashboard, /\/Admin\/SaveHouseTapping/);
    assert.match(controller, /HttpGet\("HouseTapping\/\{requestId:long\}"\)/);
    assert.match(controller, /application\.DoneData = completionData/);
    assert.match(controller, /application\.Stage = "done"/);
    assert.match(view, /id="installationRequestModal"/);
    assert.match(view, /id="installSrAccountNumber"/);
    assert.match(view, /id="installSrReportedBy" placeholder="Enter the reporter's name" required/);
    assert.match(dashboard, /inputValue\('installSrReportedBy', ''\)/);
    assert.match(view, /NEW CONNECTION INSTALLATION/);
    assert.match(view, /<option>Service Request<\/option><option selected>Maintenance<\/option>/);
    assert.match(view, /<option>Walk-In<\/option><option>Phone<\/option><option>Other<\/option>/);
    assert.match(view, /<option>Billing<\/option><option selected>Maintenance<\/option><option>Customer Service<\/option>/);
    assert.match(dashboard, /\/Admin\/SaveInstallationServiceRequest/);
    assert.match(collections, /URLSearchParams\(window\.location\.search\)\.get\('accountNumber'\)/);
});

test('Collections includes posted Other Charges in bill totals and the charge modal', () => {
    const controller = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Controllers/CollectionsController.cs'), 'utf8');
    const collections = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/collections.js'), 'utf8');
    const chargeModal = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/collections/oc-modal.js'), 'utf8');
    const dashboard = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Controllers/DashboardController.cs'), 'utf8');

    assert.match(controller, /otherCharges = accountOtherCharges/);
    assert.match(controller, /otherCharges = otherChargeAmount/);
    assert.match(controller, /totalOtherCharges/);
    assert.match(collections, /totals\.otherCharges/);
    assert.match(collections, /otherCharges: Array\.isArray\(apiAccount\.otherCharges\)/);
    assert.match(chargeModal, /installmentIds:/);
    assert.match(chargeModal, /refreshCurrentAccountCharges/);
    assert.match(chargeModal, /loadAccountWithCharges/);
    assert.match(chargeModal, /loadOthAccount\(selectedAccountNumber\)/);
    assert.match(chargeModal, /_ocOthCharges = Array\.isArray\(rec\.otherCharges\)/);
    assert.doesNotMatch(chargeModal, /otherChargeIds:/);
    assert.match(chargeModal, /refreshCollectionsAccount\(paidAccountNumber\)/);
    assert.match(collections, /window\.refreshCollectionsAccount = async function/);
    assert.match(controller, /if \(summary == null \|\| summary\.AmountDue <= 0m\) continue/);
    assert.doesNotMatch(dashboard, /ReconcilePaidApplicationPayments/);
    assert.match(dashboard, /BackfillApplicationAccountNumbers/);
    assert.match(dashboard, /applicationRecord\.AccountNumber/);
    assert.match(dashboard, /linkedAccount\?\.AccountNumber/);
    assert.match(chargeModal, /\/Collections\/SearchAccounts\?q=/);
    assert.doesNotMatch(chargeModal, /\/api\/Billing\/AccountLookup/);
});

test('Services exposes an independent New Connection application page', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/CustomerSupport/NewConnection.cshtml'), 'utf8');
    const script = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/new-connection.js'), 'utf8');
    const layout = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Shared/_InsideLayout.cshtml'), 'utf8');
    const data = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Controllers/DashboardController.cs'), 'utf8');

    assert.match(layout, /NewConnection.*CustomerSupport/);
    assert.match(view, /id="newConnectionForm"/);
    assert.match(view, /data-nc-tab="profile"/);
    assert.match(view, /data-nc-tab="meter"/);
    assert.match(view, /data-nc-tab="requirements"/);
    assert.match(view, /id="ncPrintSheet"/);
    assert.match(view, /Applicant's Signature over Printed Name/);
    assert.match(script, /\/CustomerSupport\/SaveNewConnection/);
    assert.match(script, /RequestVerificationToken/);
    assert.match(script, /function preparePrint\(\)/);
    assert.match(script, /beforeprint/);
    assert.match(data, /HttpPost\("NewConnection\/\{userId:long\}"\)/);
    assert.match(data, /APP-\{now:yyyy\}-\{application\.Id:D6\}/);
    for (const barangay of ['Aguado', 'Cabezas', 'Cabuco', 'Conchu', 'De Ocampo', 'Gregorio', 'Inocencio', 'Lallana', 'Lapidario', 'Luciano', 'Osorio', 'Perez', 'San Agustin']) {
        assert.match(view, new RegExp(`<option>${barangay}</option>`));
    }
});

test('Services exposes an independent Disconnection order page', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/CustomerSupport/Disconnection.cshtml'), 'utf8');
    const script = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/disconnection.js'), 'utf8');
    const layout = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Shared/_InsideLayout.cshtml'), 'utf8');
    const data = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Controllers/DashboardController.cs'), 'utf8');

    assert.match(layout, /Disconnection.*CustomerSupport/);
    assert.match(view, /id="dcForm"/);
    assert.match(view, /id="dcShowOrders"/);
    assert.match(view, /data-dc-action="disconnect"/);
    assert.match(view, /data-dc-action="accomplish"/);
    assert.match(view, /data-dc-action="cancel"/);
    assert.match(script, /\/CustomerSupport\/SearchDisconnectionAccounts/);
    assert.match(script, /\/CustomerSupport\/SaveDisconnectionOrder/);
    assert.match(script, /RequestVerificationToken/);
    assert.match(data, /HttpPost\("DisconnectionOrder\/\{userId:long\}"\)/);
    assert.match(data, /DCO-\{now:yyyy\}-\{order\.Id:D6\}/);
});

test('Services exposes an independent Account Transfer page', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/CustomerSupport/AccountTransfer.cshtml'), 'utf8');
    const script = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/account-transfer.js'), 'utf8');
    const layout = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Shared/_InsideLayout.cshtml'), 'utf8');
    const data = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Controllers/DashboardController.cs'), 'utf8');

    assert.match(layout, /AccountTransfer.*CustomerSupport/);
    assert.match(view, /id="atForm"/);
    assert.match(view, /id="atNewAccount"/);
    assert.match(view, /id="atTransferDate"/);
    assert.match(script, /\/CustomerSupport\/SearchAccountTransferAccounts/);
    assert.match(script, /\/CustomerSupport\/SaveAccountTransfer/);
    assert.match(script, /RequestVerificationToken/);
    assert.match(data, /HttpPost\("AccountTransfer\/\{userId:long\}"\)/);
    assert.match(data, /ATO-\{now:yyyy\}-\{order\.Id:D6\}/);
});

test('Services exposes an independent Service Request job-order page', () => {
    const view = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/CustomerSupport/ServiceRequest.cshtml'), 'utf8');
    const script = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/wwwroot/js/service-request.js'), 'utf8');
    const layout = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Application/Views/Shared/_InsideLayout.cshtml'), 'utf8');
    const data = fs.readFileSync(
        path.join(root, 'TMCWD/TMCWD.Data/Controllers/DashboardController.cs'), 'utf8');

    assert.match(layout, /ServiceRequest.*CustomerSupport/);
    assert.match(view, /id="srForm"/);
    assert.match(view, /INTERCONNECTION OF MAINLINE/);
    assert.match(view, /NEW CONNECTION INSTALLATION/);
    assert.match(view, /<option>Service Request<\/option><option>Maintenance<\/option>/);
    assert.match(view, /<option>Walk-In<\/option><option>Phone<\/option><option>Other<\/option>/);
    assert.match(view, /<option>Billing<\/option><option>Maintenance<\/option><option>Customer Service<\/option>/);
    assert.match(view, /id="srNonCustomer"/);
    assert.match(view, /id="srReportedBy"[^>]*\brequired/);
    assert.match(script, /set\('srReportedBy', ''\)/);
    assert.match(view, /Received By/);
    assert.match(script, /\/CustomerSupport\/SearchDisconnectionAccounts/);
    assert.doesNotMatch(script, /\/CustomerSupport\/GetServiceRequestAssignees/);
    assert.match(script, /referredToDepartment: value\('srReferredTo'\)/);
    assert.match(script, /\/CustomerSupport\/SaveServiceRequest/);
    assert.match(script, /RequestVerificationToken/);
    assert.match(data, /HttpPost\("ServiceRequest\/\{userId:long\}"\)/);
    assert.match(data, /JO-\{now:yyyy\}-\{order\.Id:D6\}/);
});

test('Reading Sheet drawer shows posted roster counts and refreshes after Partial Post', async () => {
    const script = fs.readFileSync(path.join(root,
        'TMCWD/TMCWD.Application/wwwroot/js/reading-sheet.js'), 'utf8');
    const posting = { textContent: '' };
    const button = {};
    const backdrop = { style: {}, setAttribute() {} };
    const requests = [];
    let completed = 3;
    const context = vm.createContext({
        document: {
            addEventListener() {},
            getElementById(id) {
                return { rsdPostingStatus: posting, rsdPartialPostBtn: button,
                    rsdDrawerBackdrop: backdrop }[id] || null;
            },
            body: { classList: { add() {} } },
        },
        window: { addEventListener() {} }, console, alert() {},
        async fetch(url, options) {
            requests.push([url, options.method]);
            const data = url.includes('PartialPostReadingSheet') ? { postedCount: 3 }
                : url === '/ReadingSheet/GetAllReadingSheet'
                    ? [{ id: 99, totalCompleted: 9, totalAccounts: 9 },
                        { id: 7, totalCompleted: completed, totalAccounts: 20, forPosting: 2 }]
                    : [];
            return { ok: true, async json() { return data; } };
        },
    });
    vm.runInContext(script, context);
    context.ensureDrawerExists = () => {};
    context.applyAllFilters = context.applyAndRender = context.generateZoneProgressInputs = () => {};
    context.readingSheetState.rows = context.mapReadingSheetRows([
        { id: 7, totalCompleted: 3, totalAccounts: 20, forPosting: 2 }]);
    await context.openViewPanel(7);
    assert.equal(posting.textContent, '3 of 20');
    assert.equal(context.currentDrawerRow.forPosting, 2);

    context.readingSheetState.rows[0].totalCompleted = 0;
    await context.openViewPanel(7);
    assert.equal(posting.textContent, '0 of 20');
    for (completed of [3, 5]) {
        await context.handlePartialPostClick();
        assert.equal(posting.textContent, `${completed} of 20`);
        assert.equal(context.currentDrawerRow.totalCompleted, completed);
        assert.equal(context.currentDrawerRow.id, 7);
        assert.equal(context.currentDrawerRow.forPosting, 2);
        assert.equal(button.disabled, false);
        assert.deepEqual(requests.slice(-3), [
            ['/ReadingSheet/PartialPostReadingSheet?readingSheetId=7', 'POST'],
            ['/ReadingSheet/GetReadingSheetAccounts?readingSheetId=7', 'GET'],
            ['/ReadingSheet/GetAllReadingSheet', 'GET'],
        ]);
    }
});

test('Reading Sheet detail renders 41 roster rows with 37 pending and four saved readings', () => {
    const script = fs.readFileSync(path.join(root,
        'TMCWD/TMCWD.Application/wwwroot/js/reading-sheet.js'), 'utf8');
    const context = vm.createContext({
        document: { addEventListener() {} },
        window: { addEventListener() {} },
        console,
    });
    vm.runInContext(script, context);
    const rows = Array.from({ length: 41 }, (_, index) => ({
        number: `TEST-${index + 1}`, name: 'Synthetic account', code: `TEST-${index + 1}`,
        prev: index < 4 ? index * 10 : 0,
        pres: index < 4 ? index * 10 : null,
        usage: index < 4 ? 0 : null,
        status: index < 4 ? 'Completed' : 'Pending',
    }));
    const html = context.renderAccountRows(rows);
    assert.equal((html.match(/<tr /g) || []).length, 41);
    assert.equal((html.match(/<td class="col-num">Pending<\/td>/g) || []).length, 37);
    assert.equal((html.match(/status-completed/g) || []).length, 4);
    assert.match(html, /<td class="col-num">30<\/td>/);
    assert.match(html, /<td class="col-num">0<\/td>/);
    const hostile = context.renderAccountRows([{ ...rows[0], name: '<script>alert(1)</script>', number: '" onmouseover="x' }]);
    assert.doesNotMatch(hostile, /<script>|data-account-number="" onmouseover/);
    assert.match(hostile, /&lt;script&gt;/);
});
