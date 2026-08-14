/* ===========================================================
   Other Charges — behavior
   Wired to real backend endpoints under /Billing/...
   =========================================================== */

(function () {
    "use strict";

    const ENDPOINTS = {
        chargeTypes: "/Billing/GetChargeTypes",
        searchAccounts: (q) => `/Billing/SearchAccounts?q=${encodeURIComponent(q)}`,
        charges: (accountNumber) => `/Billing/GetOtherChargesByAccount?accountNumber=${encodeURIComponent(accountNumber)}`,
        addCharge: () => `/Billing/SaveOtherCharge`,
        deleteCharge: (chargeId) => `/Billing/DeactivateOtherCharge?id=${encodeURIComponent(chargeId)}`,
    };

    const FALLBACK_CHARGE_TYPES = [
        { value: 4, label: "MSR - Recon. Fee" },
        { value: 1, label: "Reconnection Fee" },
        { value: 2, label: "Penalty" },
        { value: 3, label: "Meter Testing Fee" },
        { value: 5, label: "Service Connection Fee" },
        { value: 6, label: "Miscellaneous Charge" },
    ];

    document.addEventListener("DOMContentLoaded", function () {

        const el = {
            accountNumber: document.getElementById("ocAccountNumber"),
            accountNumberError: document.getElementById("ocAccountNumberError"),
            accountName: document.getElementById("ocAccountName"),
            accountSuggestions: document.getElementById("ocAccountSuggestions"),
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

        function escapeHtml(str) {
            const div = document.createElement("div");
            div.textContent = str == null ? "" : String(str);
            return div.innerHTML;
        }

       /** async function safeFetchJson(url, options) {
            try {
                const res = await fetch(url, options);
                if (!res.ok) return null;
                return await res.json();
            } catch (err) {
                return null;
            }
        }**/

        async function safeFetchJson(url, options) {
            try {
                const res = await fetch(url, options);
                const text = await res.text();

                console.log("[OtherCharges] Status:", res.status);
                console.log("[OtherCharges] Response:", text);

                if (!res.ok) {
                    return null;
                }

                if (!text) {
                    return null;
                }

                return JSON.parse(text);
            }
            catch (err) {
                console.error("[OtherCharges] Error:", err);
                return null;
            }
        }

        // ---------- Delete confirmation modal ----------

        const confirmOverlay = document.getElementById("ocConfirmOverlay");
        const confirmBody = document.getElementById("ocConfirmBody");
        const confirmOk = document.getElementById("ocConfirmOk");
        const confirmCancel = document.getElementById("ocConfirmCancel");
        let _confirmResolve = null;

        function showConfirm(message) {
            return new Promise((resolve) => {
                _confirmResolve = resolve;
                confirmBody.textContent = message;
                confirmOverlay.classList.remove("oc-hidden");
            });
        }

        confirmOk?.addEventListener("click", () => {
            confirmOverlay.classList.add("oc-hidden");
            if (_confirmResolve) { _confirmResolve(true); _confirmResolve = null; }
        });

        confirmCancel?.addEventListener("click", () => {
            confirmOverlay.classList.add("oc-hidden");
            if (_confirmResolve) { _confirmResolve(false); _confirmResolve = null; }
        });

        confirmOverlay?.addEventListener("click", (e) => {
            if (e.target === confirmOverlay) {
                confirmOverlay.classList.add("oc-hidden");
                if (_confirmResolve) { _confirmResolve(false); _confirmResolve = null; }
            }
        });

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

        // ---------- Account search / autocomplete ----------

        let debounceTimer = null;
        let activeSuggestionIndex = -1;
        let currentSuggestions = [];

        function hideSuggestions() {
            if (!el.accountSuggestions) return;
            el.accountSuggestions.classList.add("oc-hidden");
            el.accountSuggestions.innerHTML = "";
            currentSuggestions = [];
            activeSuggestionIndex = -1;
        }

        function renderSuggestions(list) {
            if (!el.accountSuggestions) return;
            currentSuggestions = list;
            activeSuggestionIndex = -1;

            if (!list.length) {
                hideSuggestions();
                return;
            }

            el.accountSuggestions.innerHTML = list.map((acc, i) => `
            <div class="oc-autocomplete-item" data-index="${i}">
                <div class="oc-ac-number">${escapeHtml(acc.accountNumber)}</div>
                <div class="oc-ac-name">${escapeHtml(acc.name || "")}</div>
            </div>
        `).join("");

            el.accountSuggestions.classList.remove("oc-hidden");

            el.accountSuggestions.querySelectorAll(".oc-autocomplete-item").forEach((item) => {
                item.addEventListener("click", () => {
                    const idx = parseInt(item.dataset.index, 10);
                    selectAccount(currentSuggestions[idx]);
                });
            });
        }

        async function selectAccount(acc) {
            el.accountNumber.value = acc.accountNumber;
            el.accountName.value = acc.name || "";
            state.accountNumber = acc.accountNumber;
            hideSuggestions();
            setAccountError("");

            const charges = await safeFetchJson(ENDPOINTS.charges(acc.accountNumber));
            state.charges = Array.isArray(charges) ? charges : [];
            state.page = 1;
            renderTable();
        }

        el.accountNumber.addEventListener("input", function () {
            const q = this.value.trim();
            clearTimeout(debounceTimer);

            // Typing invalidates the previously selected account until a new one is chosen.
            state.accountNumber = "";
            el.accountName.value = "";
            state.charges = [];
            renderTable();

            if (!q) {
                hideSuggestions();
                return;
            }

            debounceTimer = setTimeout(async () => {
                const results = await safeFetchJson(ENDPOINTS.searchAccounts(q));
                renderSuggestions(Array.isArray(results) ? results : []);
            }, 250);
        });

        el.accountNumber.addEventListener("keydown", function (e) {
            if (!el.accountSuggestions || el.accountSuggestions.classList.contains("oc-hidden")) return;

            const items = el.accountSuggestions.querySelectorAll(".oc-autocomplete-item");
            if (e.key === "ArrowDown") {
                e.preventDefault();
                activeSuggestionIndex = Math.min(activeSuggestionIndex + 1, items.length - 1);
                items.forEach((it, i) => it.classList.toggle("is-active", i === activeSuggestionIndex));
            } else if (e.key === "ArrowUp") {
                e.preventDefault();
                activeSuggestionIndex = Math.max(activeSuggestionIndex - 1, 0);
                items.forEach((it, i) => it.classList.toggle("is-active", i === activeSuggestionIndex));
            } else if (e.key === "Enter") {
                e.preventDefault();
                if (activeSuggestionIndex >= 0) {
                    selectAccount(currentSuggestions[activeSuggestionIndex]);
                }
            } else if (e.key === "Escape") {
                hideSuggestions();
            }
        });

        document.addEventListener("click", function (e) {
            if (!el.accountNumber.contains(e.target) &&
                el.accountSuggestions && !el.accountSuggestions.contains(e.target)) {
                hideSuggestions();
            }
        });

        // ---------- Add charge ----------

        async function addCharge() {
            setEntryError("");

            if (!state.accountNumber) {
                setEntryError("Select an account first.");
                return;
            }

            const chargeTypeEl = el.chargeType;
            const chargeTypeValue = chargeTypeEl.value;
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
                accountNumber: state.accountNumber,
                chargeType: parseInt(chargeTypeValue, 10),
                payableIn,
                particulars,
                amount: Math.round(amount * 100) / 100,
            };
            console.log("========== SAVE OTHER CHARGE ==========");
            console.log("accountNumber:", state.accountNumber);
            console.log("chargeTypeValue:", chargeTypeValue);
            console.log("chargeType parsed:", parseInt(chargeTypeValue, 10));
            console.log("payableIn:", payableIn);
            console.log("particulars:", particulars);
            console.log("amount:", amount);
            console.log("payload:", payload);
            console.log("JSON:", JSON.stringify(payload));
            console.log("=======================================");

            const saved = await safeFetchJson(ENDPOINTS.addCharge(), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload),
            });

            if (!saved) {
                setEntryError("Could not save the charge. Please try again.");
                el.addBtn.disabled = false;
                return;
            }

            state.charges.push(saved);
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

            const charge = state.charges.find((c) => String(c.id) === String(chargeId));
            const label = charge ? `${charge.description} — \u20B1${formatCurrency(charge.amount)}` : "this charge";

            const confirmed = await showConfirm(`Delete ${label}? This cannot be undone.`);
            if (!confirmed) return;

            const result = await safeFetchJson(ENDPOINTS.deleteCharge(chargeId), { method: "POST" });

            if (result === null) {
                setEntryError("Could not delete the charge. Please try again.");
                return;
            }

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

        // ---------- Event wiring ----------

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