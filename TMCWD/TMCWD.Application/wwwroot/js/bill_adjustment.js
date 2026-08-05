(function () {
    "use strict";

    var form = document.getElementById("billAdjustmentForm");
    var acctInput = document.getElementById("accountNumber") || document.getElementById("AccountNumber");
    var favBtn = document.getElementById("baFavoriteBtn");
    var toast = document.getElementById("baToast");
    var acctList = document.getElementById("accountList");
    var billingDateInput = document.getElementById("BillingDate");

    const modal = document.getElementById("billingHistoryModal");
    const openBtn = document.getElementById("btnBillingHistory");
    const closeBtn = document.getElementById("closeBillingHistory");

    // ── Favorite toggle (safe no-op if button not present) ──
    if (favBtn) {
        favBtn.addEventListener("click", function () { favBtn.classList.toggle("active"); });
    }

    // ── Grid: checkbox toggle + auto-calc, wired to the REAL markup ──
    document.querySelectorAll("table.data-table tbody tr").forEach(function (row) {
        var keyInput = row.querySelector('input[name$=".Key"]');
        var checkbox = row.querySelector('input[name$=".IsChecked"]');
        var asBilled = row.querySelector('input[name$=".AsBilled"]');
        var shouldBe = row.querySelector('input[name$=".ShouldBe"]');
        var adjustment = row.querySelector('input[name$=".Adjustment"]');

        if (keyInput) row.dataset.key = keyInput.value;
        if (!checkbox || !asBilled || !shouldBe) return;


        function setEnabled(on) {
            asBilled.disabled = !on;
            shouldBe.disabled = !on;
        }

        function recalc() {
            if (!adjustment || adjustment.type === "hidden") return;
            var a = parseFloat(asBilled.value);
            var b = parseFloat(shouldBe.value);
            adjustment.value = (isNaN(a) || isNaN(b)) ? "" : (b - a).toFixed(2);
        }

        checkbox.addEventListener("change", function () { setEnabled(checkbox.checked); recalc(); });
        [asBilled, shouldBe].forEach(function (inp) {
            inp.addEventListener("input", recalc);
            inp.addEventListener("blur", function () {
                var v = parseFloat(inp.value);
                if (!isNaN(v)) inp.value = v.toFixed(2);
                recalc();
            });
        });

        setEnabled(checkbox.checked);
        recalc();
    });

    // ── Usage auto-calc from Present/Previous ──
    function syncUsageFromReadings() {
        var presentRow = document.querySelector('tr[data-key="present"]');
        var previousRow = document.querySelector('tr[data-key="previous"]');
        var usageRow = document.querySelector('tr[data-key="usage"]');
        if (!presentRow || !previousRow || !usageRow) return;

        var presentShouldBe = parseFloat(presentRow.querySelector('input[name$=".ShouldBe"]').value);
        var previousShouldBe = parseFloat(previousRow.querySelector('input[name$=".ShouldBe"]').value);

        if (isNaN(presentShouldBe) || isNaN(previousShouldBe)) return;

        var usageChk = usageRow.querySelector('input[name$=".IsChecked"]');
        var usageShouldBe = usageRow.querySelector('input[name$=".ShouldBe"]');

        usageChk.checked = true;
        usageChk.dispatchEvent(new Event("change"));
        usageShouldBe.value = (presentShouldBe - previousShouldBe).toFixed(2);
        usageShouldBe.dispatchEvent(new Event("input"));
    }

    ["present", "previous"].forEach(function (key) {
        var row = document.querySelector('tr[data-key="' + key + '"]');
        if (!row) return;
        var shouldBeInput = row.querySelector('input[name$=".ShouldBe"]');
        if (shouldBeInput) shouldBeInput.addEventListener("input", syncUsageFromReadings);
    });

    // ── Validation ──
    function setInvalid(el, isInvalid) {
        var field = el.closest(".cr-field") || el.closest(".ba-field");
        if (field) field.classList.toggle("invalid", isInvalid);
    }

    if (acctInput) {
        acctInput.addEventListener("input", function () {
            if (acctInput.value.trim()) setInvalid(acctInput, false);
        });
    }

    function validate() {
        var ok = true;
        if (acctInput && !acctInput.value.trim()) {
            setInvalid(acctInput, true);
            ok = false;
        }

        var meterReaderSelect = document.getElementById("MeterReader");
        var remarksSelect = document.getElementById("Remarks");

        if (meterReaderSelect && !meterReaderSelect.value) {
            setInvalid(meterReaderSelect, true);
            ok = false;
        }
        if (remarksSelect && !remarksSelect.value) {
            setInvalid(remarksSelect, true);
            ok = false;
        }

        var anyChecked = Array.from(document.querySelectorAll('input[name$=".IsChecked"]'))
            .some(function (c) { return c.checked; });
        if (!anyChecked) {
            alert("Select at least one line item to adjust.");
            ok = false;
        }
        return ok;
    }

    if (form) {
        form.addEventListener("submit", function (e) {
            if (!validate()) { e.preventDefault(); }
        });
    }

    // ── Account Number live search ──
    if (acctInput && acctList) {
        var debounceTimer;

        function renderAccountOptions(matches) {
            acctList.innerHTML = "";
            if (!matches || matches.length === 0) {
                acctList.setAttribute("hidden", "");
                return;
            }
            matches.forEach(function (m) {
                var li = document.createElement("li");
                li.className = "cr-combobox__option";
                li.textContent = m.accountNumber;
                li.setAttribute("role", "option");
                li.addEventListener("mousedown", function (e) {
                    e.preventDefault();
                    acctInput.value = m.accountNumber;
                    acctList.setAttribute("hidden", "");
                    setInvalid(acctInput, false);
                    tryAutoFillAsBilled();
                });
                acctList.appendChild(li);
            });
            acctList.removeAttribute("hidden");
        }

        acctInput.addEventListener("input", function () {
            var q = acctInput.value.trim();
            clearTimeout(debounceTimer);
            if (!q) { acctList.setAttribute("hidden", ""); return; }
            debounceTimer = setTimeout(function () {
                fetch("/Billing/SearchAccounts?query=" + encodeURIComponent(q))
                    .then(function (res) { return res.ok ? res.json() : []; })
                    .then(renderAccountOptions);
            }, 200);
        });

        acctInput.addEventListener("blur", function () {
            setTimeout(function () { acctList.setAttribute("hidden", ""); }, 150);
        });

        document.addEventListener("click", function (e) {
            if (!acctInput.contains(e.target) && !acctList.contains(e.target)) {
                acctList.setAttribute("hidden", "");
            }
        });
    }

    if (billingDateInput) {
        billingDateInput.addEventListener("change", tryAutoFillAsBilled);
    }

    function tryAutoFillAsBilled() {
        if (!acctInput || !billingDateInput) return;
        var acct = acctInput.value.trim();
        var date = billingDateInput.value;
        if (!acct || !date) return;

        fetch("/Billing/GetAsBilledValues?accountNumber=" + encodeURIComponent(acct) + "&billingDate=" + encodeURIComponent(date))
            .then(function (res) { return res.ok ? res.json() : null; })
            .then(function (data) {
                if (!data) return;
                fillLine("currentBill", data.currentBill);
                fillLine("penalty", data.penalty);
            });
    }

    function fillLine(key, value) {
        var row = document.querySelector('tr[data-key="' + key + '"]');
        if (!row) return;
        var checkbox = row.querySelector('input[name$=".IsChecked"]');
        var asBilled = row.querySelector('input[name$=".AsBilled"]');
        if (!checkbox || !asBilled) return;

        checkbox.checked = true;
        checkbox.dispatchEvent(new Event("change"));
        asBilled.value = Number(value).toFixed(2);
        asBilled.dispatchEvent(new Event("input"));
    }

    // ── Toast helper ──
    var toastTimer;
    function showToast(msg) {
        if (!toast) return;
        toast.textContent = msg;
        toast.removeAttribute("hidden");
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { toast.setAttribute("hidden", ""); }, 2800);
    }

    if (openBtn && modal) {
        openBtn.addEventListener("click", function () {

            modal.classList.add("show");

            loadBillingHistory();

        });
    }

    if (closeBtn && modal) {
        closeBtn.addEventListener("click", function () {
            modal.classList.remove("show");
        });
    }
    modal.addEventListener("click", function (e) {

        if (e.target === modal) {
            modal.classList.remove("show");
        }

    });
    async function loadBillingHistory() {

        const accountNumber = acctInput.value.trim();

        if (!accountNumber) {
            alert("Please select an account first.");
            return;
        }

        const response = await fetch(
            "/Billing/History?accountNumber=" +
            encodeURIComponent(accountNumber)
        );

        if (!response.ok) {
            alert("Unable to load billing history.");
            return;
        }

        const data = await response.json();

        const tbody = document.getElementById("billingHistoryTable");
        tbody.innerHTML = "";

        data.forEach(function (item) {

            tbody.insertAdjacentHTML("beforeend", `
            <tr>
                <td>${item.referenceNo}</td>
                <td>${item.billingDate.substring(0, 10)}</td>
                <td>${item.previous}</td>
                <td>${item.present}</td>
                <td>${item.usage}</td>
                <td>${item.amount.toFixed(2)}</td>
                <td>
                    <button
                        type="button"
                        class="btn btn--green btnSelectBilling"
                        data-reference="${item.referenceNo}"
                        data-date="${item.billingDate.substring(0, 10)}"
                        data-previous="${item.previous}"
                        data-present="${item.present}"
                        data-usage="${item.usage}"
                        data-amount="${item.amount}">
                        Select
                    </button>
                </td>
            </tr>
            `);

        });

        document.querySelectorAll(".btnSelectBilling").forEach(function (btn) {

            btn.addEventListener("click", function () {

                document.getElementById("ReferenceNo").value =
                    this.dataset.reference;

                document.getElementById("BillingDate").value =
                    this.dataset.date;

                fillLine("usage", this.dataset.usage);
                fillLine("present", this.dataset.present);
                fillLine("previous", this.dataset.previous);

                modal.classList.remove("show");

                tryAutoFillAsBilled();
            });

        });

    }
    
})();

