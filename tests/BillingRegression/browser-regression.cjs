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
    assert.match(controller, /waterBillAmount = Math\.Max\(0m, collectibleBillAmount - meterMaintenanceFee\)/);
    assert.match(controller, /meterMaintenanceFee,/);
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
    assert.match(dashboard, /RequestVerificationToken/);
    assert.match(controller, /HttpGet\("ServiceInspection\/\{requestId:long\}"\)/);
    assert.match(controller, /HttpPost\("SaveServiceInspection\/\{userId:long\}"\)/);
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
