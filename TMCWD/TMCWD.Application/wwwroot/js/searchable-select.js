/* =============================================================
   searchable-select.js
   Converts a plain <select> into a searchable dropdown.
   Uses position:fixed so the list escapes modal overflow:hidden.

   Usage:
     makeSearchableSelect(selectEl, { placeholder: 'Search...' });

   The underlying <select> stays in sync so all existing
   form/save code continues to work unchanged.
   ============================================================= */
(function (global) {
    'use strict';

    // Single shared list element reused across all instances
    var _sharedList  = null;
    var _activeInst  = null;   // currently open instance

    function getSharedList() {
        if (!_sharedList) {
            _sharedList = document.createElement('ul');
            _sharedList.className = 'ss-list';
            _sharedList.setAttribute('role', 'listbox');
            document.body.appendChild(_sharedList);
        }
        return _sharedList;
    }

    function escapeHtml(str) {
        var d = document.createElement('div');
        d.textContent = str == null ? '' : String(str);
        return d.innerHTML;
    }

    function makeSearchableSelect(selectEl, opts) {
        if (!selectEl || selectEl.dataset.searchable) return;
        selectEl.dataset.searchable = '1';
        opts = opts || {};

        /* ── Wrap & hide the original select ────────────────── */
        var wrapper = document.createElement('div');
        wrapper.className = 'ss-wrapper';
        selectEl.parentNode.insertBefore(wrapper, selectEl);
        wrapper.appendChild(selectEl);
        selectEl.style.display = 'none';

        /* ── Search input ─────────────────────────────────── */
        var input = document.createElement('input');
        input.type = 'text';
        input.className = 'ss-input';
        input.placeholder = opts.placeholder || 'Search...';
        input.autocomplete = 'off';
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-expanded', 'false');
        input.setAttribute('aria-autocomplete', 'list');
        wrapper.appendChild(input);

        /* ── State ─────────────────────────────────────────── */
        var allOptions = [];
        var activeIdx  = -1;
        var isOpen     = false;

        /* ── Option helpers ────────────────────────────────── */
        function buildOptions() {
            allOptions = Array.from(selectEl.options)
                .filter(function (o) { return o.value !== ''; })
                .map(function (o) { return { value: o.value, label: o.text }; });
        }

        function filtered(query) {
            if (!query || !query.trim()) return allOptions;
            var q = query.trim().toLowerCase();
            return allOptions.filter(function (o) {
                return o.label.toLowerCase().includes(q);
            });
        }

        /* ── Position the shared list under the input ──────── */
        function positionList() {
            var list = getSharedList();
            var rect = input.getBoundingClientRect();
            var spaceBelow = window.innerHeight - rect.bottom;
            var spaceAbove = rect.top;
            var listHeight = Math.min(240, list.scrollHeight);

            list.style.width  = rect.width + 'px';
            list.style.left   = rect.left + window.scrollX + 'px';

            if (spaceBelow >= listHeight || spaceBelow >= spaceAbove) {
                // Open downward
                list.style.top    = (rect.bottom + window.scrollY + 2) + 'px';
                list.style.bottom = 'auto';
            } else {
                // Open upward
                list.style.top    = 'auto';
                list.style.bottom = (window.innerHeight - rect.top + window.scrollY + 2) + 'px';
            }
        }

        /* ── Render items ──────────────────────────────────── */
        function renderList(query) {
            var list = getSharedList();
            var rows = filtered(query);
            list.innerHTML = '';
            activeIdx = -1;

            if (!rows.length) {
                var empty = document.createElement('li');
                empty.className = 'ss-empty';
                empty.textContent = 'No matches found.';
                list.appendChild(empty);
                return;
            }

            rows.forEach(function (opt, i) {
                var li = document.createElement('li');
                li.className = 'ss-option';
                li.setAttribute('role', 'option');
                li.dataset.value = opt.value;
                li.dataset.idx   = i;

                if (query && query.trim()) {
                    var q   = query.trim();
                    var idx = opt.label.toLowerCase().indexOf(q.toLowerCase());
                    if (idx >= 0) {
                        li.innerHTML =
                            escapeHtml(opt.label.slice(0, idx)) +
                            '<mark>' + escapeHtml(opt.label.slice(idx, idx + q.length)) + '</mark>' +
                            escapeHtml(opt.label.slice(idx + q.length));
                    } else {
                        li.textContent = opt.label;
                    }
                } else {
                    li.textContent = opt.label;
                }

                li.addEventListener('mousedown', function (e) {
                    e.preventDefault();
                    selectOption(opt.value, opt.label);
                });
                list.appendChild(li);
            });
        }

        /* ── Open / close ──────────────────────────────────── */
        function openList() {
            // Close any other open instance
            if (_activeInst && _activeInst !== inst) {
                _activeInst.close();
            }
            _activeInst = inst;
            isOpen = true;

            buildOptions();
            renderList(input.value);
            positionList();

            var list = getSharedList();
            list.style.display = 'block';
            input.setAttribute('aria-expanded', 'true');
        }

        function closeList() {
            if (!isOpen) return;
            isOpen = false;
            var list = getSharedList();
            list.style.display = 'none';
            input.setAttribute('aria-expanded', 'false');
            activeIdx = -1;
            if (_activeInst === inst) _activeInst = null;
        }

        function selectOption(val, label) {
            selectEl.value = val;
            input.value    = label;
            closeList();
            // Dispatch change so existing listeners on the <select> fire
            selectEl.dispatchEvent(new Event('change', { bubbles: true }));
            if (opts.onChange) opts.onChange(val, label);
        }

        /* ── Keyboard navigation ───────────────────────────── */
        function setActive(idx) {
            var list  = getSharedList();
            var items = list.querySelectorAll('.ss-option');
            items.forEach(function (el, i) {
                el.classList.toggle('is-active', i === idx);
            });
            if (items[idx]) items[idx].scrollIntoView({ block: 'nearest' });
            activeIdx = idx;
        }

        /* ── Sync input text when select value changes ──────── */
        function syncLabel() {
            var match = allOptions.find(function (o) { return o.value === selectEl.value; });
            input.value = match ? match.label : '';
        }

        /* ── Events ────────────────────────────────────────── */
        input.addEventListener('focus', function () {
            buildOptions();
            openList();
        });

        input.addEventListener('input', function () {
            // Reopen and filter
            openList();
            renderList(input.value);
            positionList();
            // Clear the hidden select when user is typing freely
            if (selectEl.value !== '') {
                selectEl.value = '';
                selectEl.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });

        input.addEventListener('keydown', function (e) {
            if (!isOpen && (e.key === 'ArrowDown' || e.key === 'Enter')) {
                openList();
                return;
            }
            var list  = getSharedList();
            var items = list.querySelectorAll('.ss-option');

            switch (e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    setActive(Math.min(activeIdx + 1, items.length - 1));
                    break;
                case 'ArrowUp':
                    e.preventDefault();
                    setActive(Math.max(activeIdx - 1, 0));
                    break;
                case 'Enter':
                    e.preventDefault();
                    if (activeIdx >= 0 && items[activeIdx]) {
                        items[activeIdx].dispatchEvent(new MouseEvent('mousedown'));
                    } else if (items.length === 1) {
                        // Only one result — auto-select it
                        items[0].dispatchEvent(new MouseEvent('mousedown'));
                    }
                    break;
                case 'Escape':
                    closeList();
                    input.blur();
                    break;
                case 'Tab':
                    closeList();
                    break;
            }
        });

        input.addEventListener('blur', function () {
            // Delay so mousedown on list fires first
            setTimeout(function () {
                closeList();
                buildOptions();
                syncLabel();
            }, 160);
        });

        // Reposition on scroll/resize so list tracks the input
        window.addEventListener('scroll', function () { if (isOpen) positionList(); }, true);
        window.addEventListener('resize', function () { if (isOpen) positionList(); });

        // Initial state
        buildOptions();
        syncLabel();

        var inst = {
            close: closeList,
            sync: function () { buildOptions(); syncLabel(); }
        };

        return inst;
    }

    global.makeSearchableSelect = makeSearchableSelect;

}(window));
