const fs = require('node:fs');
const path = require('node:path');
const app = 'TMCWD/TMCWD.Application';
const read = file => fs.readFileSync(file, 'utf8');
function files(dir, suffix) {
    return fs.readdirSync(dir, { withFileTypes: true }).flatMap(x => x.isDirectory()
        ? files(path.join(dir, x.name), suffix) : x.name.endsWith(suffix) ? [path.join(dir, x.name)] : []);
}
const actions = new Map();
for (const file of files(app + '/Controllers', '.cs')) {
    actions.set(path.basename(file, 'Controller.cs').toLowerCase(), new Set([...read(file).matchAll(/public\s+(?:async\s+)?[\w<>?,.\[\] ]+?\s+(\w+)\s*\(/g)].map(m => m[1].toLowerCase())));
}
const mvc = [], missingMvc = [], assets = [];
for (const file of [...files(app + '/Views', '.cshtml'), ...files(app + '/wwwroot/js', '.js')]) {
    const text = read(file).replace(/@\*[\s\S]*?\*@|\/\*[\s\S]*?\*\/|\/\/[^\n]*/g, '');
    const check = (controller, action, index) => {
        const row = { file, line: text.slice(0, index).split('\n').length, route: `/${controller}/${action}` };
        mvc.push(row);
        if (!actions.get(controller.toLowerCase())?.has(action.toLowerCase())) missingMvc.push(row);
    };
    for (const m of text.matchAll(/["'`]\/([A-Za-z]+)\/([A-Za-z]\w*)/g)) {
        if (actions.has(m[1].toLowerCase())) check(m[1], m[2], m.index);
    }
    for (const m of text.matchAll(/Url.Action\("(\w+)",\s*"(\w+)"/g)) check(m[2], m[1], m.index);
    for (const m of text.matchAll(/(?:src|href)="~\/([^"?]+\.(?:js|css|png|svg|jpg))"/g)) {
        if (!fs.existsSync(app + '/wwwroot/' + m[1])) assets.push({ file, asset: m[1] });
    }
}
const routes = [];
for (const file of files('TMCWD/TMCWD.Data/Controllers', '.cs')) {
    const text = read(file), prefix = text.match(/\[Route\("([^"]+)"\)\]/)?.[1]?.replace('[controller]', path.basename(file, 'Controller.cs'));
    if (!prefix) continue;
    for (const m of text.matchAll(/\[Http(Get|Post|Put|Delete|Patch)(?:\("([^"]+)"\))?\]\s*(?:\[Route\("([^"]+)"\)\])?/g)) {
        if (!m[2] && !m[3]) continue;
        const template = prefix + '/' + (m[2] || m[3]);
        routes.push({ method: m[1], template, regex: new RegExp('^' + template.replace(/\{[^}]+\}/g, '[^/]+') + '/?$', 'i') });
    }
}
const api = [], missingApi = [];
for (const dir of ['Administration', 'Billing', 'CustomerSupport', 'Engineering']) {
    for (const file of fs.readdirSync('TMCWD/TMCWD.' + dir).filter(x => x.endsWith('.cs')).map(x => 'TMCWD/TMCWD.' + dir + '/' + x)) {
        const text = read(file);
        for (const m of text.matchAll(/(?:Client\.|_client\.)(Get|Post|Put|Delete|Patch)(?:AsJson)?Async\(\s*\$?"(api\/[^"\r\n]+)"/g)) {
            const route = m[2].replace(/\{[^}]+\}/g, 'VALUE').split('?')[0];
            const row = { file, line: text.slice(0, m.index).split('\n').length, method: m[1], route };
            api.push(row);
            if (!routes.some(r => r.method === m[1] && r.regex.test(route))) missingApi.push(row);
        }
    }
}
const result = { scope: 'Static literal-route discovery; dynamic URLs and runtime behavior need separate verification.', mvcReferences: mvc.length, missingMvc, apiReferences: api.length, declaredApiRoutes: routes.length, missingApi, missingAssets: assets };
fs.writeFileSync('.tmp/workflow-route-audit.json', JSON.stringify({ ...result, mvc, api }, null, 2));
console.log(JSON.stringify(result, null, 2));
