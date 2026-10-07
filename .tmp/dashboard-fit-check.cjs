const fs = require('fs');
const vm = require('vm');
const assert = require('assert/strict');
const source = fs.readFileSync('TMCWD/TMCWD.Application/wwwroot/js/dashboard.js', 'utf8');
const start = source.indexOf('    let fitFrame = 0;');
const end = source.indexOf('    const modalResizeObserver', start);
let frame;
const panel = { offsetHeight: 1000, scrollHeight: 1000, offsetWidth: 800, scrollWidth: 800, style: {} };
const modal = { clientHeight: 624, clientWidth: 1024, querySelector: () => panel };
const context = {
  requestAnimationFrame: fn => { frame = fn; return 1; },
  document: { querySelectorAll: () => [modal] },
  getComputedStyle: () => ({ paddingTop: '12px', paddingBottom: '12px', paddingLeft: '12px', paddingRight: '12px' })
};
vm.createContext(context);
vm.runInContext(source.slice(start, end), context);
function fit() { vm.runInContext('scheduleModalFit()', context); frame(); }
fit();
assert.equal(Number(panel.style.zoom), 0.6);
panel.offsetHeight = panel.scrollHeight = 400;
fit();
assert.equal(Number(panel.style.zoom), 1);
panel.offsetWidth = panel.scrollWidth = 2000;
fit();
assert.equal(Number(panel.style.zoom), 0.5);
console.log('Viewport fitting checks passed: tall content, content shrinking, horizontal overflow.');
