const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');
const base = process.env.TMCWD_AUDIT_BASE || 'http://localhost:5054';
const routes = JSON.parse(fs.readFileSync('.tmp/workflow-smoke.json', 'utf8')).filter(x => x.route && x.status === 200).map(x => x.route);
const delay = ms => new Promise(r => setTimeout(r, ms));
(async () => {
    const login = await fetch(base + '/Home/Login', { method: 'POST', redirect: 'manual', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ email: process.env.TMCWD_AUDIT_EMAIL, password: process.env.TMCWD_AUDIT_PASSWORD }) });
    const session = login.headers.getSetCookie().find(x => x.startsWith('TMCWD.Session='))?.split(';')[0];
    if (!session || login.status !== 302) throw new Error('Authenticated login failed.');
    const edge = spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', ['--headless=new', '--disable-gpu', '--no-first-run', '--disable-extensions', '--remote-debugging-port=9334',
        '--user-data-dir=' + path.resolve('.tmp/workflow-edge-profile'), 'about:blank'], { windowsHide: true, stdio: 'ignore' });
    let socket;
    try {
        let tabs;
        for (let i = 0; i < 30; i++) { try { tabs = await (await fetch('http://localhost:9334/json')).json(); break; } catch { await delay(200); } }
        if (!tabs) throw new Error('Headless Edge did not start.');
        socket = new WebSocket(tabs.find(x => x.type === 'page').webSocketDebuggerUrl);
        await new Promise((r, reject) => { socket.onopen = r; socket.onerror = reject; });
        let id = 0, errors = [], failed = [], pending = new Map();
        socket.onmessage = e => {
            const m = JSON.parse(e.data);
            if (m.id) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
            if (m.method === 'Runtime.exceptionThrown') {
                const d = m.params.exceptionDetails; errors.push(d.exception?.description?.split('\n')[0] || d.text);
            }
            if (m.method === 'Network.responseReceived' && m.params.response.status >= 400) {
                const r = m.params.response; const u = new URL(r.url);
                if (u.origin === base) failed.push({ path: u.pathname, status: r.status });
            }
        };
        function send(method, params = {}) {
            return new Promise((resolve, reject) => { pending.set(++id, { resolve, reject }); socket.send(JSON.stringify({ id, method, params })); });
        }
        await send('Runtime.enable'); await send('Page.enable'); await send('Network.enable');
        await send('Network.setCookie', { name: 'TMCWD.Session', value: session.slice('TMCWD.Session='.length), url: base, httpOnly: true });
        const results = [];
        for (const route of routes) {
            errors = []; failed = [];
            await send('Page.navigate', { url: base + route });
            await delay(1300);
            const state = await send('Runtime.evaluate', { expression: 'JSON.stringify({ready:document.readyState, forms:document.forms.length, cards:document.querySelectorAll("[data-service-category]").length})', returnByValue: true });
            const result = { route, ...JSON.parse(state.result.value), errors: [...errors], failed: [...failed] };
            results.push(result); console.log(JSON.stringify(result));
        }
        await send('Page.navigate', { url: base + '/Admin/Index' }); await delay(800);
        for (const category of ['applications', 'new', 'inspection', 'charging', 'payment', 'installation', 'connect', 'tapping', 'tickets']) {
            errors = []; failed = [];
            await send('Runtime.evaluate', { expression: `document.querySelector('[data-service-category="${category}"]').click()` });
            await delay(500);
            const count = await send('Runtime.evaluate', { expression: 'document.querySelectorAll("[data-application-index]").length', returnByValue: true });
            if (count.result.value) {
                await send('Runtime.evaluate', { expression: 'document.querySelector("[data-application-index]").click()' }); await delay(600);
            }
            const result = { category, displayedRecords: count.result.value, errors: [...errors], failed: [...failed] };
            results.push(result); console.log(JSON.stringify(result));
            await send('Runtime.evaluate', { expression: 'document.querySelectorAll(".dashboard-modal.is-open").forEach(x=>{x.classList.remove("is-open");x.setAttribute("aria-hidden","true")})' });
        }
        fs.writeFileSync('.tmp/workflow-browser.json', JSON.stringify(results, null, 2));
        await send('Browser.close');
    } finally { socket?.close(); edge.kill(); }
})().catch(e => { console.error(e.message); process.exitCode = 1; });
