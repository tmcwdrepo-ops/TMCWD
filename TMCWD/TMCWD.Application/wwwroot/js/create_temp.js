// ============================================================
// create_temp.js — Reading Sheet Templates modal
// Wired to #crTemplatesMenuBtn (the green Templates button)
// ============================================================

(function () {
  'use strict';

  // ---------- State ----------
  var templates  = [];
  var editingId  = null;

  // ---------- Element refs ----------
  var openBtn      = document.getElementById('crTemplatesMenuBtn');
  var overlay      = document.getElementById('templateModalOverlay');
  var closeBtn     = document.getElementById('closeTemplateModal');

  // Confirm dialog
  var confirmOverlay = document.getElementById('ctConfirmOverlay');
  var confirmBody    = document.getElementById('ctConfirmBody');
  var confirmOk      = document.getElementById('ctConfirmOk');
  var confirmCancel  = document.getElementById('ctConfirmCancel');
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

  var form         = document.getElementById('templateForm');
  var idInput      = document.getElementById('templateId');
  var nameInput    = document.getElementById('templateName');
  var mrSelect     = document.getElementById('ctMeterReader');
  var zoneSelect   = document.getElementById('ctZone');
  var bookSelect   = document.getElementById('ctBook');
  var scopeSelect  = document.getElementById('ctScope');
  var rangeFields  = document.getElementById('ctRangeFields');
  var fromInput    = document.getElementById('ctFromSeq');
  var toInput      = document.getElementById('ctToSeq');
  var rangeError   = document.getElementById('ctRangeError');
  var resetBtn     = document.getElementById('ctResetForm');
  var submitLabel  = document.getElementById('ctSubmitLabel');
  var tbody        = document.getElementById('ctTemplateTableBody');
  var countEl      = document.getElementById('ctTemplateCount');
  var emptyState   = document.getElementById('ctEmptyState');

  if (!overlay) return; // modal not present on this page

  // ---------- Open / close ----------
  function openModal() {
    overlay.classList.remove('ct-hidden');
    document.body.style.overflow = 'hidden';
  }

  function closeModal() {
    overlay.classList.add('ct-hidden');
    document.body.style.overflow = '';
  }

  if (openBtn) openBtn.addEventListener('click', function (e) {
    e.stopPropagation(); // prevent the close-dropdown listener from interfering
    // also close the small cr-templates dropdown if it was open
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
      toInput.value   = '';
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
    editingId     = null;
    submitLabel.textContent = 'Save Template';
    setInvalid(nameInput.closest('.ct-field'), false);
    clearRangeErr();
    syncScope();
  }
  resetBtn.addEventListener('click', resetForm);

  // ---------- Helpers ----------
  function labelOf(sel) {
    var opt = sel.options[sel.selectedIndex];
    return opt ? opt.textContent.trim() : '—';
  }

  function esc(str) {
    var d = document.createElement('div');
    d.textContent = String(str == null ? '' : str);
    return d.innerHTML;
  }

  function scopePillHTML(tpl) {
    var cls  = { all: 'ct-scope-all', unbilled: 'ct-scope-unbilled', ranged: 'ct-scope-ranged' }[tpl.scope] || 'ct-scope-all';
    var lbl  = { all: 'All', unbilled: 'Unbilled', ranged: 'Ranged' }[tpl.scope] || tpl.scope;
    var html = '<span class="ct-scope-pill ' + cls + '">' + lbl + '</span>';
    if (tpl.scope === 'ranged') {
      html += ' <span style="font-size:12px;opacity:0.6;">' + esc(tpl.from) + '–' + esc(tpl.to) + '</span>';
    }
    return html;
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
        '<td>' + esc(tpl.mrLabel) + '</td>' +
        '<td>' + esc(tpl.zoneLabel) + '</td>' +
        '<td>' + esc(tpl.bookLabel) + '</td>' +
        '<td>' + scopePillHTML(tpl) + '</td>' +
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
  }

  // ---------- Row actions ----------
  tbody.addEventListener('click', function (e) {
    var editBtn   = e.target.closest('.ct-row-btn.ct-edit');
    var deleteBtn = e.target.closest('.ct-row-btn.ct-delete');

    if (editBtn) {
      var id  = editBtn.closest('tr').dataset.id;
      var tpl = templates.find(function (t) { return t.id === id; });
      if (!tpl) return;
      idInput.value          = tpl.id;
      nameInput.value        = tpl.name;
      mrSelect.value         = tpl.mrValue;
      zoneSelect.value       = tpl.zoneValue;
      bookSelect.value       = tpl.bookValue;
      scopeSelect.value      = tpl.scope;
      syncScope();
      if (tpl.scope === 'ranged') { fromInput.value = tpl.from; toInput.value = tpl.to; }
      editingId              = tpl.id;
      submitLabel.textContent = 'Update Template';
      renderTable();
      nameInput.focus();
      return;
    }

    if (deleteBtn) {
      var delId  = deleteBtn.closest('tr').dataset.id;
      var delTpl = templates.find(function (t) { return t.id === delId; });
      showConfirm('Delete "' + (delTpl ? delTpl.name : '') + '"? This cannot be undone.').then(function (confirmed) {
        if (!confirmed) return;
        templates = templates.filter(function (t) { return t.id !== delId; });
        if (editingId === delId) resetForm();
        renderTable();
      });
    }
  });

  // ---------- Submit ----------
  form.addEventListener('submit', function (e) {
    e.preventDefault();
    if (!validate()) return;

    var tpl = {
      id:        editingId || ('tpl-' + Date.now()),
      name:      nameInput.value.trim(),
      mrValue:   mrSelect.value,
      mrLabel:   labelOf(mrSelect) || '—',
      zoneValue: zoneSelect.value,
      zoneLabel: labelOf(zoneSelect) || '—',
      bookValue: bookSelect.value,
      bookLabel: labelOf(bookSelect) || '—',
      scope:     scopeSelect.value,
      from:      scopeSelect.value === 'ranged' ? fromInput.value : '',
      to:        scopeSelect.value === 'ranged' ? toInput.value   : ''
    };

    if (editingId) {
      templates = templates.map(function (t) { return t.id === editingId ? tpl : t; });
    } else {
      templates.push(tpl);
    }

    renderTable();
    resetForm();
  });

  // ---------- Init ----------
  syncScope();
  renderTable();

})();
