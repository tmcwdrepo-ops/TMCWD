/* ===========================================================
   Other Charges — behavior
   Replace the ENDPOINTS below with your real API routes.
   =========================================================== */

(function () {
    "use strict";

    const ENDPOINTS = {
        accountLookup: (accountNumber) => `/api/accounts/${encodeURIComponent(accountNumber)}`,
        chargeTypes: "/api/charge-types",
        charges: (accountNumber) => `/api/accounts/${encodeURIComponent(accountNumber)}/other-charges`,
        addCharge: (accountNumber) => `/api/accounts/${encodeURIComponent(accountNumber)}/other-charges`,
        deleteCharge: (accountNumber, chargeId) =>
            `/api/accounts/${encodeURIComponent(accountNumber)}/other-charges/${encodeURIComponent(chargeId)}`,
    };

    const FALLBACK_CHARGE_TYPES = [
        { value: "MSR-RECON", label: "MSR - Recon. Fee" },
        { value: "RECONNECTION", label: "Reconnection Fee" },
        { value: "PENALTY", label: "Penalty" },
        { value: "METER-TEST", label: "Meter Testing Fee" },
        { value: "SERVICE-CONN", label: "Service Connection Fee" },
        { value: "MISC", label: "Miscellaneous Charge" },
    ];

    document.addEventListener("DOMContentLoaded", function () {

    const el = {
        accountNumber: document.getElementById("ocAccountNumber"),
        accountNumberError: document.getElementById("ocAccountNumberError"),
        accountName: document.getElementById("ocAccountName"),
        chargeType: document.getElementById("ocChargeType"),
        payableIn: document.getElementById("ocPayableIn"),
        payableInOther: document.getElementById("ocPayableInOther"),
        particulars: document.getElementById("ocParticulars"),
        amount: document.getElementById("ocAmount"),
        addBtn: document.getElementById("ocAddChargeBtn"),
        entryError: document.getElementById("ocEntryError"),
        totalDue: document.getElementById("ocTotalDue"),
        tableBody: document.getElementById("ocTableBody"),
        emptyState: document.getElementById("ocEmptyState"),
        pageSize: document.getElementById("ocPageSize"),
        pageInfo: document.getElementById("ocPageInfo"),
        pageCurrent: document.getElementById("ocPageCurrent"),
        firstPage: document.getElementById("ocFirstPage"),
        prevPage: document.getElementById("ocPrevPage"),
        nextPage: document.getElementById("ocNextPage"),
        lastPage: document.getElementById("ocLastPage"),
        favoriteBtn: null,
    };

    const state = {
        accountNumber: "",
        charges: [], // { id, description, payableIn, particulars, amount }
        sortCol: null, // "description" | "particulars"
        sortDir: "asc", // "asc" | "desc"
        page: 1,
        pageSize: parseInt(el.pageSize.value, 10) || 10,
    };

    // ---------- Helpers ----------

    function formatCurrency(value) {
        const n = Number(value) || 0;
        return n.toLocaleString("en-PH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function clampAmountInput(input) {
        // Allow only digits and a single decimal point, max 2 decimal places.
        let v = input.value.replace(/[^0-9.]/g, "");
        const firstDot = v.indexOf(".");
        if (firstDot !== -1) {
            v = v.slice(0, firstDot + 1) + v.slice(firstDot + 1).replace(/\./g, "");
        }
        const parts = v.split(".");
        if (parts[1] && parts[1].length > 2) {
            parts[1] = parts[1].slice(0, 2);
            v = parts[0] + "." + parts[1];
        }
        input.value = v;
    }

    function setEntryError(message) {
        el.entryError.textContent = message || "";
    }

    function setAccountError(message) {
        el.accountNumberError.textContent = message || "";
        el.accountNumber.classList.toggle("oc-input-error", !!message);
    }

    async function safeFetchJson(url, options) {
        try {
            const res = await fetch(url, options);
            if (!res.ok) return null;
            return await res.json();
        } catch (err) {
            return null;
        }
    }

    // ---------- Charge types ----------

    async function loadChargeTypes() {
        const data = await safeFetchJson(ENDPOINTS.chargeTypes);
        const types = Array.isArray(data) && data.length ? data : FALLBACK_CHARGE_TYPES;

        types.forEach((t) => {
            const opt = document.createElement("option");
            opt.value = t.value;
            opt.textContent = t.label;
            el.chargeType.appendChild(opt);
        });
    }

    // ---------- Account lookup ----------

    async function lookupAccount() {
        const accountNumber = el.accountNumber.value.trim();
        setAccountError("");
        el.accountName.value = "";

        if (!accountNumber) {
            return;
        }

        // Try the real API first; fall back to sample data
        var data = await safeFetchJson(ENDPOINTS.accountLookup(accountNumber));

        if (!data || !data.accountName) {
            // Check sample data
            if (typeof OC_SAMPLE_ACCOUNTS !== "undefined" && OC_SAMPLE_ACCOUNTS[accountNumber]) {
                data = OC_SAMPLE_ACCOUNTS[accountNumber];
            } else {
                setAccountError("Account not found.");
                state.accountNumber = "";
                state.charges = [];
                renderTable();
                return;
            }
        }

        el.accountName.value = data.accountName;
        state.accountNumber = accountNumber;

        // Try API for charges; fall back to sample data
        var charges = await safeFetchJson(ENDPOINTS.charges(accountNumber));
        if (!Array.isArray(charges)) {
            charges = (typeof OC_SAMPLE_CHARGES !== "undefined" && OC_SAMPLE_CHARGES[accountNumber])
                ? OC_SAMPLE_CHARGES[accountNumber].map(function (c) { return Object.assign({}, c); })
                : [];
        }

        state.charges = charges;
        state.page = 1;
        renderTable();
    }

    // ---------- Add charge ----------

    async function addCharge() {
        setEntryError("");

        if (!state.accountNumber) {
            setEntryError("Enter a valid account number first.");
            return;
        }

        const chargeTypeEl = el.chargeType;
        const chargeTypeValue = chargeTypeEl.value;
        const chargeTypeLabel = chargeTypeEl.options[chargeTypeEl.selectedIndex]?.textContent || "";
        const payableIn = el.payableIn.value === "__other__"
            ? el.payableInOther.value.trim()
            : el.payableIn.value;

        if (el.payableIn.value === "__other__" && !payableIn) {
            setEntryError("Please specify the payment method.");
            return;
        }
        const particulars = el.particulars.value.trim();
        const amountRaw = el.amount.value.trim();
        const amount = parseFloat(amountRaw);

        if (!chargeTypeValue) {
            setEntryError("Select a charge type.");
            return;
        }
        if (!amountRaw || isNaN(amount) || amount <= 0) {
            setEntryError("Enter a valid amount greater than zero.");
            return;
        }

        el.addBtn.disabled = true;

        const payload = {
            chargeType: chargeTypeValue,
            description: chargeTypeLabel,
            payableIn,
            particulars,
            amount: Math.round(amount * 100) / 100,
        };

        const saved = await safeFetchJson(ENDPOINTS.addCharge(state.accountNumber), {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
        });

        // Fall back to an optimistic local row if the API call isn't wired up yet.
        const newRow = saved || {
            id: "local-" + Date.now(),
            description: payload.description,
            payableIn: payload.payableIn,
            particulars: payload.particulars,
            amount: payload.amount,
        };

        state.charges.push(newRow);
        state.page = Math.ceil(state.charges.length / state.pageSize);
        renderTable();

        // Reset entry row.
        chargeTypeEl.value = "";
        el.payableIn.value = "";
        el.payableInOther.value = "";
        el.payableInOther.classList.remove("is-visible");
        el.particulars.value = "";
        el.amount.value = "";
        el.addBtn.disabled = false;
    }

    // ---------- Delete charge ----------

    async function deleteCharge(chargeId) {
        if (!state.accountNumber) return;

        await safeFetchJson(ENDPOINTS.deleteCharge(state.accountNumber, chargeId), { method: "DELETE" });

        state.charges = state.charges.filter((c) => String(c.id) !== String(chargeId));

        const totalPages = Math.max(1, Math.ceil(state.charges.length / state.pageSize));
        if (state.page > totalPages) state.page = totalPages;

        renderTable();
    }

    // ---------- Sorting ----------

    function applySort(rows) {
        if (!state.sortCol) return rows;

        const dir = state.sortDir === "asc" ? 1 : -1;
        return [...rows].sort((a, b) => {
            const av = (a[state.sortCol] || "").toString().toLowerCase();
            const bv = (b[state.sortCol] || "").toString().toLowerCase();
            if (av < bv) return -1 * dir;
            if (av > bv) return 1 * dir;
            return 0;
        });
    }

    function updateSortIcons() {
        var neutralIcon =
            '<svg class="sort-icon" viewBox="0 0 10 14" fill="none" aria-hidden="true">' +
              '<path d="M5 1v12M1 4l4-3 4 3M1 10l4 3 4-3"' +
              ' stroke="currentColor" stroke-width="1.3"' +
              ' stroke-linecap="round" stroke-linejoin="round"/>' +
            '</svg>';

        var ascIcon =
            '<svg class="sort-icon sort-icon--active" viewBox="0 0 10 14" fill="none" aria-hidden="true">' +
              '<path d="M5 1v12M1 4l4-3 4 3"' +
              ' stroke="currentColor" stroke-width="1.6"' +
              ' stroke-linecap="round" stroke-linejoin="round"/>' +
            '</svg>';

        var descIcon =
            '<svg class="sort-icon sort-icon--active" viewBox="0 0 10 14" fill="none" aria-hidden="true">' +
              '<path d="M5 1v12M1 10l4 3 4-3"' +
              ' stroke="currentColor" stroke-width="1.6"' +
              ' stroke-linecap="round" stroke-linejoin="round"/>' +
            '</svg>';

        document.querySelectorAll(".th-inner[data-sort]").forEach(function (inner) {
            var field = inner.getAttribute("data-sort");
            var existingIcon = inner.querySelector(".sort-icon");
            if (!existingIcon) return;

            if (field === state.sortCol) {
                existingIcon.outerHTML = state.sortDir === "asc" ? ascIcon : descIcon;
                inner.setAttribute("aria-sort", state.sortDir === "asc" ? "ascending" : "descending");
            } else {
                existingIcon.outerHTML = neutralIcon;
                inner.removeAttribute("aria-sort");
            }
        });
    }

    // ---------- Rendering ----------

    function renderTable() {
        const sorted = applySort(state.charges);
        const totalItems = sorted.length;
        const totalPages = Math.max(1, Math.ceil(totalItems / state.pageSize));
        state.page = Math.min(Math.max(1, state.page), totalPages);

        const startIdx = (state.page - 1) * state.pageSize;
        const pageRows = sorted.slice(startIdx, startIdx + state.pageSize);

        el.tableBody.innerHTML = "";

        pageRows.forEach((charge) => {
            const tr = document.createElement("tr");

            tr.innerHTML = `
                <td>${escapeHtml(charge.description || "")}</td>
                <td>${escapeHtml(charge.payableIn || "")}</td>
                <td>${escapeHtml(charge.particulars || "")}</td>
                <td class="oc-col-amount">${formatCurrency(charge.amount)}</td>
                <td class="oc-col-actions"></td>
            `;

            const actionCell = tr.querySelector(".oc-col-actions");
            const delBtn = document.createElement("button");
            delBtn.type = "button";
            delBtn.className = "oc-row-delete-btn";
            delBtn.setAttribute("aria-label", "Delete charge");
            delBtn.innerHTML =
                '<svg viewBox="0 0 24 24" width="16" height="16"><path d="M4 7h16M9 7V5a1 1 0 011-1h4a1 1 0 011 1v2m2 0v13a1 1 0 01-1 1H8a1 1 0 01-1-1V7h10z" stroke="currentColor" stroke-width="1.6" fill="none" stroke-linecap="round" stroke-linejoin="round"/></svg>';
            delBtn.addEventListener("click", () => deleteCharge(charge.id));
            actionCell.appendChild(delBtn);

            el.tableBody.appendChild(tr);
        });

        el.emptyState.hidden = totalItems !== 0;

        const rangeStart = totalItems === 0 ? 0 : startIdx + 1;
        const rangeEnd = Math.min(startIdx + state.pageSize, totalItems);
        el.pageInfo.textContent = `${rangeStart}\u2013${rangeEnd} of ${totalItems}`;
        el.pageCurrent.textContent = state.page;

        el.firstPage.disabled = state.page <= 1;
        el.prevPage.disabled = state.page <= 1;
        el.nextPage.disabled = state.page >= totalPages;
        el.lastPage.disabled = state.page >= totalPages;

        const total = state.charges.reduce((sum, c) => sum + (Number(c.amount) || 0), 0);
        el.totalDue.textContent = formatCurrency(total);

        updateSortIcons();
    }

    function escapeHtml(str) {
        const div = document.createElement("div");
        div.textContent = str;
        return div.innerHTML;
    }

    // ---------- Event wiring ----------

    el.accountNumber.addEventListener("blur", lookupAccount);
    el.accountNumber.addEventListener("keydown", (e) => {
        if (e.key === "Enter") {
            e.preventDefault();
            lookupAccount();
        }
    });

    el.amount.addEventListener("input", () => clampAmountInput(el.amount));

    el.payableIn.addEventListener("change", function () {
        var isOther = el.payableIn.value === "__other__";
        el.payableInOther.classList.toggle("is-visible", isOther);
        if (isOther) {
            el.payableInOther.focus();
        } else {
            el.payableInOther.value = "";
        }
    });

    el.addBtn.addEventListener("click", addCharge);

    document.querySelectorAll(".th-inner[data-sort]").forEach(function (inner) {
        inner.closest("th").addEventListener("click", function () {
            var col = inner.getAttribute("data-sort");
            if (state.sortCol === col) {
                if (state.sortDir === "asc") {
                    // 2nd click — descending
                    state.sortDir = "desc";
                } else {
                    // 3rd click — reset
                    state.sortCol = null;
                    state.sortDir = "asc";
                }
            } else {
                // New column — start ascending
                state.sortCol = col;
                state.sortDir = "asc";
            }
            renderTable();
        });
    });

    el.pageSize.addEventListener("change", () => {
        state.pageSize = parseInt(el.pageSize.value, 10) || 10;
        state.page = 1;
        renderTable();
    });

    el.firstPage.addEventListener("click", () => {
        state.page = 1;
        renderTable();
    });
    el.prevPage.addEventListener("click", () => {
        state.page -= 1;
        renderTable();
    });
    el.nextPage.addEventListener("click", () => {
        state.page += 1;
        renderTable();
    });
    el.lastPage.addEventListener("click", () => {
        state.page = Number.MAX_SAFE_INTEGER;
        renderTable();
    });

    el.favoriteBtn && el.favoriteBtn.addEventListener("click", () => {
        const pressed = el.favoriteBtn.getAttribute("aria-pressed") === "true";
        el.favoriteBtn.setAttribute("aria-pressed", String(!pressed));
    });

    // ---------- Init ----------

    loadChargeTypes();
    renderTable();

    }); // end DOMContentLoaded
})();
