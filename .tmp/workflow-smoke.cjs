const fs = require('node:fs');
const base = process.env.TMCWD_AUDIT_BASE || 'http://localhost:5054';
const routes = [
    '/Home/Index', '/Admin/Index', '/Admin/Users', '/Admin/AddEditUser', '/Admin/InspectionTypes', '/Admin/AddEditInspectionType', '/Admin/OtherFeeTypes',
    '/CustomerSupport/Index', '/CustomerSupport/CustomerInformation', '/CustomerSupport/AddEditCustomer', '/CustomerSupport/AddEditAccount', '/CustomerSupport/Requests', '/CustomerSupport/AddEditRequest',
    '/CustomerSupport/NewConnection', '/CustomerSupport/Disconnection', '/CustomerSupport/AccountTransfer', '/CustomerSupport/ServiceRequest',
    '/Billing/ReadingSheet', '/Billing/ReadingSheetDetails', '/Billing/Reading', '/Billing/BillAdjustment', '/Billing/OtherCharges', '/Billing/DebitCreditMemo', '/Billing/Penalty', '/Billing/PenaltyCharging', '/Billing/StatementOfAccount', '/Billing/Collections',
    '/Collections/Index', '/ReadingSheet/Index', '/Account/Index', '/JobOrder/Index', '/Engineering/Index', '/Engineering/Inventory', '/Engineering/AddEditInventory'
];
(async () => {
    let cookie = '';
    if (process.env.TMCWD_AUDIT_EMAIL && process.env.TMCWD_AUDIT_PASSWORD) {
        const login = await fetch(base + '/Home/Login', { method: 'POST', redirect: 'manual',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: new URLSearchParams({ email: process.env.TMCWD_AUDIT_EMAIL, password: process.env.TMCWD_AUDIT_PASSWORD }) });
        cookie = login.headers.getSetCookie().map(x => x.split(';')[0]).join('; ');
        if (login.status !== 302 || !cookie) throw new Error('Login did not create an authenticated session.');
        console.log('Authenticated login:', login.status, login.headers.get('location'));
    }
    const headers = { Cookie: cookie, Accept: 'text/html' };
    const result = [];
    for (const route of routes) {
        try {
            const response = await fetch(base + route, { headers, redirect: 'manual', signal: AbortSignal.timeout(15000) });
            const html = await response.text();
            const row = { route, status: response.status, location: response.headers.get('location'), assets: [] };
            if (response.ok) {
                for (const m of html.matchAll(/(?:src|href)="(\/(?:js|css|lib|images)\/[^"?]+(?:\?[^" ]*)?)"/g)) {
                    const asset = await fetch(base + m[1], { signal: AbortSignal.timeout(10000) });
                    if (!asset.ok) row.assets.push({ url: m[1], status: asset.status });
                    await asset.arrayBuffer();
                }
                row.rendered = true;
                if (html.includes('Live service totals are temporarily unavailable')) row.warning = 'Dashboard totals unavailable';
            } else if (response.status >= 500) {
                row.error = html.match(/(?:InvalidOperationException|NullReferenceException|HttpRequestException|Argument\w*Exception):[^\r\n<]+/)?.[0] || 'Server error';
            }
            result.push(row);
            console.log(JSON.stringify(row));
        } catch (e) { result.push({ route, error: e.message }); console.log(route, e.message); }
    }
    for (const category of ['applications', 'new', 'inspection', 'charging', 'payment', 'installation', 'connect', 'tapping', 'tickets']) {
        const response = await fetch(base + '/Admin/GetServiceApplications?category=' + category, { headers });
        const row = { category, status: response.status };
        if (response.ok) {
            const records = await response.json(); row.records = records.length; row.details = [];
            const detail = { inspection: 'GetServiceInspection?requestId=', connect: 'GetConnectOrder?requestId=',
                payment: 'GetApplicationPayment?applicationId=', applications: 'GetApplicationTicket?applicationId=',
                new: 'GetApplicationTicket?applicationId=', tickets: 'GetApplicationTicket?applicationId=', tapping: 'GetHouseTapping?requestId=' }[category];
            if (detail) for (const record of records) {
                const res = await fetch(base + '/Admin/' + detail + record.requestId, { headers });
                row.details.push({ status: res.status }); await res.text();
            }
        }
        result.push(row); console.log(JSON.stringify(row));
    }
    fs.writeFileSync('.tmp/workflow-smoke.json', JSON.stringify(result, null, 2));
})().catch(e => { console.error(e.message); process.exitCode = 1; });
