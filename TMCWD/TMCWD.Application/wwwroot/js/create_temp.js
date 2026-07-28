// ============================================================
// create_temp.js — Reading Sheet Templates modal
// Wired to #crTemplatesMenuBtn (the green Templates button)
// Now backed by real API data instead of an in-memory array.
// ============================================================

(function () {
    'use strict';

    // ---------- State ----------
    var templates = [];   // populated from server via loadTemplates()
    var readers = [];   // [{id, name}]
    var zones = [];   // [{value, label}]
    var editingId = null;

    // ---------- Element refs ----------
    var openBtn = document.getElementById('crTemplatesMenuBtn');
    var overlay = document.getElementById('templateModalOverlay');
    var closeBtn = document.getElementById('closeTemplateModal');

    // Confirm dialog
    var confirmOverlay = document.getElementById('ctConfirmOverlay');
    var confirmBody = document.getElementById('ctConfirmBody');
    var confirmOk = document.getElementById('ctConfirmOk');
    var confirmCancel = document.getElementById('ctConfirmCancel');
    var _confirmResolve = null;

    function showConfirm(message) {
        return new Promise(function (resolve) {
            _confirmResolve = resolve;
            confirmBody.textContent = message;
            confirmOverlay.classList.remove('ct-hidden');
        });
    }

    if (confirmOk) confirmOk.addEventListener('click', function () {
        confirmOverlay.classList.add('ct-hidden');
        if (_confirmResolve) { _confirmResolve(true); _confirmResolve = null; }
    });

    if (confirmCancel) confirmCancel.addEventListener('click', function () {
        confirmOverlay.classList.add('ct-hidden');
        if (_confirmResolve) { _confirmResolve(false); _confirmResolve = null; }
    });

    if (confirmOverlay) confirmOverlay.addEventListener('click', function (e) {
        if (e.target === confirmOverlay) {
            confirmOverlay.classList.add('ct-hidden');
            if (_confirmResolve) { _confirmResolve(false); _confirmResolve = null; }
        }
    });

    var form = document.getElementById('templateForm');
    var idInput = document.getElementById('templateId');
    var nameInput = document.getElementById('templateName');
    var mrSelect = document.getElementById('ctMeterReader');
    var zoneSelect = document.getElementById('ctZone');
    var bookSelect = document.getElementById('ctBook');
    var scopeSelect = document.getElementById('ctScope');
    var rangeFields = document.getElementById('ctRangeFields');
    var fromInput = document.getElementById('ctFromSeq');
    var toInput = document.getElementById('ctToSeq');
    var rangeError = document.getElementById('ctRangeError');
    var resetBtn = document.getElementById('ctResetForm');
    var submitLabel = document.getElementById('ctSubmitLabel');
    var tbody = document.getElementById('ctTemplateTableBody');
    var countEl = document.getElementById('ctTemplateCount');
    var emptyState = document.getElementById('ctEmptyState');

    if (!overlay) return; // modal not present on this page

    // ---------- API ----------
    // NOTE: Scope / From / To are collected in the form for future use,
    // but the backend (ReadingSheetTemplate) does not have columns for
    // them yet, so they are NOT sent to the server or persisted.

    function apiGet(url) {
        return fetch(url, { method: 'GET', headers: { 'Accept': 'application/json' } })
            .then(function (res) {
                if (res.status === 404) return [];
                if (!res.ok) throw new Error('Request failed: ' + url);
                return res.json();
            });
    }

    function loadReaders() {
        return apiGet('/ReadingSheet/GetReaders').then(function (data) {
            readers = data || [];
            mrSelect.innerHTML = '<option value="">Select meter reader...</option>';
            readers.forEach(function (r) {
                var opt = document.createElement('option');
                opt.value = r.id;
                opt.textContent = r.name;
                mrSelect.appendChild(opt);
            });
        });
    }

    function loadZones() {
        return apiGet('/ReadingSheet/GetZones').then(function (data) {
            zones = data || [];
            zoneSelect.innerHTML = '<option value="">Select zone...</option>';
            zones.forEach(function (z) {
                var opt = document.createElement('option');
                opt.value = z.value;
                opt.textContent = z.label;
                zoneSelect.appendChild(opt);
            });
        });
    }

    function loadBooksForZone(zoneValue) {
        bookSelect.innerHTML = '<option value="">Select book...</option>';
        if (!zoneValue) return Promise.resolve();
        return apiGet('/ReadingSheet/GetBooksByZone?zone=' + encodeURIComponent(zoneValue))
            .then(function (data) {
                (data || []).forEach(function (b) {
                    var opt = document.createElement('option');
                    opt.value = b.value;
                    opt.textContent = b.label;
                    bookSelect.appendChild(opt);
                });
            });
    }

    function loadTemplates() {
        return apiGet('/ReadingSheet/GetReadingSheetTemplates').then(function (data) {
            templates = (data || []).map(function (t) {
                return {
                    id: String(t.id),
                    name: t.name,
                    readerId: t.readerId,
                    readerName: t.readerName,
                    zone: t.zone,
                    book: t.book
                };
            });
            renderTable();
        });
    }

    zoneSelect.addEventListener('change', function () {
        loadBooksForZone(zoneSelect.value);
    });

    // ---------- Open / close ----------
    function openModal() {
        overlay.classList.remove('ct-hidden');
        document.body.style.overflow = 'hidden';
        loadReaders();
        loadZones();
        loadTemplates();
    }

    function closeModal() {
        overlay.classList.add('ct-hidden');
        document.body.style.overflow = '';
    }

    if (openBtn) openBtn.addEventListener('click', function (e) {
        e.stopPropagation(); // prevent the close-dropdown listener from interfering
        var crDropdown = document.getElementById('crTemplatesDropdown');
        if (crDropdown) crDropdown.hidden = true;
        openModal();
    });

    closeBtn.addEventListener('click', closeModal);

    overlay.addEventListener('click', function (e) {
        if (e.target === overlay) closeModal();
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !overlay.classList.contains('ct-hidden')) closeModal();
    });

    // ---------- Scope: show/hide range fields ----------
    function syncScope() {
        var ranged = scopeSelect.value === 'ranged';
        rangeFields.hidden = !ranged;
        if (!ranged) {
            clearRangeErr();
            fromInput.value = '';
            toInput.value = '';
        }
    }
    scopeSelect.addEventListener('change', syncScope);

    // ---------- Validation ----------
    function setInvalid(fieldEl, bad) {
        fieldEl.classList.toggle('ct-invalid', bad);
    }

    function clearRangeErr() {
        rangeError.classList.remove('ct-visible');
    }

    function validate() {
        var ok = true;

        var nameField = nameInput.closest('.ct-field');
        var nameOk = nameInput.value.trim().length > 0;
        setInvalid(nameField, !nameOk);
        if (!nameOk) ok = false;

        if (!mrSelect.value || !zoneSelect.value || !bookSelect.value) {
            ok = false; // reader/zone/book are now required real selections
        }

        clearRangeErr();
        if (scopeSelect.value === 'ranged') {
            var f = parseFloat(fromInput.value);
            var t = parseFloat(toInput.value);
            var rangeOk = fromInput.value !== '' && toInput.value !== '' && !isNaN(f) && !isNaN(t) && t > f;
            if (!rangeOk) { rangeError.classList.add('ct-visible'); ok = false; }
        }

        return ok;
    }

    nameInput.addEventListener('input', function () {
        if (nameInput.value.trim()) setInvalid(nameInput.closest('.ct-field'), false);
    });
    [fromInput, toInput].forEach(function (el) { el.addEventListener('input', clearRangeErr); });

    // ---------- Reset ----------
    function resetForm() {
        form.reset();
        idInput.value = '';
        editingId = null;
        submitLabel.textContent = 'Save Template';
        setInvalid(nameInput.closest('.ct-field'), false);
        clearRangeErr();
        syncScope();
        loadBooksForZone(null);
    }
    resetBtn.addEventListener('click', resetForm);

    // ---------- Helpers ----------
    function esc(str) {
        var d = document.createElement('div');
        d.textContent = String(str == null ? '' : str);
        return d.innerHTML;
    }

    // ---------- Render table ----------
    function renderTable() {
        tbody.innerHTML = '';
        templates.forEach(function (tpl) {
            var tr = document.createElement('tr');
            tr.dataset.id = tpl.id;
            if (tpl.id === editingId) tr.classList.add('ct-editing');
            tr.innerHTML =
                '<td>' + esc(tpl.name) + '</td>' +
                '<td>' + esc(tpl.readerName) + '</td>' +
                '<td>Zone ' + esc(tpl.zone) + '</td>' +
                '<td>Book ' + esc(tpl.book) + '</td>' +
                '<td>—</td>' + // Scope not persisted yet
                '<td class="ct-actions">' +
                '<button type="button" class="ct-row-btn ct-edit" title="Edit">' +
                '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>' +
                '</button>' +
                '<button type="button" class="ct-row-btn ct-delete" title="Delete">' +
                '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>' +
                '</button>' +
                '</td>';
            tbody.appendChild(tr);
        });

        countEl.textContent = templates.length + (templates.length === 1 ? ' template' : ' templates');
        emptyState.classList.toggle('ct-empty-visible', templates.length === 0);

        // Let the header dropdown (create_reading.js) know templates changed
        document.dispatchEvent(new CustomEvent('templates:updated', { detail: { templates: templates } }));
    }

    // ---------- Row actions ----------
    tbody.addEventListener('click', function (e) {
        var editBtn = e.target.closest('.ct-row-btn.ct-edit');
        var deleteBtn = e.target.closest('.ct-row-btn.ct-delete');

        if (editBtn) {
            var id = editBtn.closest('tr').dataset.id;
            var tpl = templates.find(function (t) { return t.id === id; });
            if (!tpl) return;

            idInput.value = tpl.id;
            nameInput.value = tpl.name;
            mrSelect.value = tpl.readerId;
            zoneSelect.value = tpl.zone;

            loadBooksForZone(tpl.zone).then(function () {
                bookSelect.value = tpl.book;
            });

            editingId = tpl.id;
            submitLabel.textContent = 'Update Template';
            renderTable();
            nameInput.focus();
            return;
        }

        if (deleteBtn) {
            var delId = deleteBtn.closest('tr').dataset.id;
            var delTpl = templates.find(function (t) { return t.id === delId; });
            showConfirm('Delete "' + (delTpl ? delTpl.name : '') + '"? This cannot be undone.').then(function (confirmed) {
                if (!confirmed) return;
                fetch('/ReadingSheet/DeactivateReadingSheetTemplate?id=' + encodeURIComponent(delId), { method: 'POST' })
                    .then(function (res) {
                        if (!res.ok) throw new Error('Delete failed');
                        return loadTemplates();
                    })
                    .then(function () {
                        if (editingId === delId) resetForm();
                    })
                    .catch(function (err) {
                        alert('Could not delete template: ' + err.message);
                    });
            });
        }
    });

    // ---------- Submit ----------
    form.addEventListener('submit', function (e) {
        e.preventDefault();
        if (!validate()) return;

        var payload = {
            id: editingId ? parseInt(editingId, 10) : 0,
            name: nameInput.value.trim(),
            readerId: parseInt(mrSelect.value, 10),
            zone: parseInt(zoneSelect.value, 10),
            book: parseInt(bookSelect.value, 10)
        };

        fetch('/ReadingSheet/SaveReadingSheetTemplate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
            .then(function (res) {
                if (!res.ok) throw new Error('Save failed');
                return res.json();
            })
            .then(function () {
                return loadTemplates();
            })
            .then(function () {
                resetForm();
            })
            .catch(function (err) {
                alert('Could not save template: ' + err.message);
            });

    });

    // ---------- Init ----------
    syncScope();

})();