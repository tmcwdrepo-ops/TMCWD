// ============================================================
// Bill Adjustment — page script
// ============================================================

(function () {
  "use strict";

  var form       = document.getElementById("billAdjustmentForm");
  var acctInput  = document.getElementById("accountNumber");
  var favBtn     = document.getElementById("baFavoriteBtn");
  var gridRows   = document.querySelectorAll(".ba-grid-row");
  var toast      = document.getElementById("baToast");

  // ── Custom select dropdowns ──
  var customSelects = document.querySelectorAll(".ba-select");

  customSelects.forEach(function (selectEl) {
    var btn      = selectEl.querySelector(".ba-select__btn");
    var text     = selectEl.querySelector(".ba-select__text");
    var list     = selectEl.querySelector(".ba-select__list");
    var options  = Array.from(selectEl.querySelectorAll(".ba-select__option"));
    var hiddenInput = selectEl.parentElement.querySelector('input[type="hidden"]');
    
    if (!btn || !list || !hiddenInput) return;

    // Restore previously selected value
    var savedVal = selectEl.dataset.value || hiddenInput.value || "";
    if (savedVal) {
      var match = options.find(function (opt) { return opt.dataset.value === savedVal; });
      if (match) {
        text.textContent = match.textContent;
        text.classList.remove("ba-select__text--placeholder");
        match.classList.add("ba-select__option--selected");
      }
    } else {
      text.classList.add("ba-select__text--placeholder");
    }

    // Toggle open/close
    btn.addEventListener("click", function (e) {
      e.stopPropagation();
      var isOpen = !list.hasAttribute("hidden");
      if (isOpen) {
        list.setAttribute("hidden", "");
        selectEl.classList.remove("ba-select--open");
        btn.setAttribute("aria-expanded", "false");
      } else {
        list.removeAttribute("hidden");
        selectEl.classList.add("ba-select--open");
        btn.setAttribute("aria-expanded", "true");
      }
    });

    // Select an option
    options.forEach(function (opt) {
      opt.addEventListener("click", function () {
        var val = opt.dataset.value;
        hiddenInput.value = val;
        text.textContent = opt.textContent;
        text.classList.toggle("ba-select__text--placeholder", val === "");
        
        options.forEach(function (o) { o.classList.remove("ba-select__option--selected"); });
        opt.classList.add("ba-select__option--selected");
        
        list.setAttribute("hidden", "");
        selectEl.classList.remove("ba-select--open");
        btn.setAttribute("aria-expanded", "false");
      });
    });

    // Close on outside click
    document.addEventListener("click", function (e) {
      if (!selectEl.contains(e.target) && !list.hasAttribute("hidden")) {
        list.setAttribute("hidden", "");
        selectEl.classList.remove("ba-select--open");
        btn.setAttribute("aria-expanded", "false");
      }
    });

    // Close on Escape
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape" && !list.hasAttribute("hidden")) {
        list.setAttribute("hidden", "");
        selectEl.classList.remove("ba-select--open");
        btn.setAttribute("aria-expanded", "false");
      }
    });
  });

  // ── Favorite toggle ──
  favBtn.addEventListener("click", function () {
    favBtn.classList.toggle("active");
  });

  // ── Adjustment grid: checkbox toggle + auto-calc ──
  gridRows.forEach(function (row) {
    var cb       = row.querySelector(".line-checkbox");
    var asBilled = row.querySelector(".as-billed-input");
    var shouldBe = row.querySelector(".should-be-input");
    var adj      = row.querySelector(".adjustment-input");

    function setEnabled(on) {
      asBilled.disabled = !on;
      shouldBe.disabled = !on;
      if (!on) {
        asBilled.value = "";
        shouldBe.value = "";
        if (adj) adj.value = "";
      }
    }

    function recalc() {
      if (!adj) return;
      var a = parseFloat(asBilled.value);
      var b = parseFloat(shouldBe.value);
      adj.value = (isNaN(a) || isNaN(b)) ? "" : (b - a).toFixed(2);
    }

    cb.addEventListener("change", function () { setEnabled(cb.checked); recalc(); });

    [asBilled, shouldBe].forEach(function (inp) {
      inp.addEventListener("input", recalc);
      inp.addEventListener("blur", function () {
        var v = parseFloat(inp.value);
        if (!isNaN(v)) inp.value = v.toFixed(2);
        recalc();
      });
    });

    // Init on page load
    setEnabled(cb.checked);
    recalc();
  });

  // ── Validation ──
  function setInvalid(el, isInvalid) {
    var field = el.closest(".ba-field");
    if (field) field.classList.toggle("invalid", isInvalid);
  }

  acctInput.addEventListener("input", function () {
    if (acctInput.value.trim()) setInvalid(acctInput, false);
  });

  function validate() {
    var ok = true;
    if (!acctInput.value.trim()) {
      setInvalid(acctInput, true);
      ok = false;
    }
    var anyChecked = Array.from(document.querySelectorAll(".line-checkbox"))
      .some(function (c) { return c.checked; });
    if (!anyChecked) {
      alert("Select at least one line item to adjust.");
      ok = false;
    }
    return ok;
  }

  // ── Submit ──
  form.addEventListener("submit", function (e) {
    if (!validate()) { e.preventDefault(); }
  });

  // ── Toast helper ──
  var toastTimer;
  function showToast(msg) {
    toast.textContent = msg;
    toast.removeAttribute("hidden");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { toast.setAttribute("hidden", ""); }, 2800);
  }

})();
