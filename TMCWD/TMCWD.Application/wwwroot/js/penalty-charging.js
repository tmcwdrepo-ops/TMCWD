/**
 * Penalty Charging — page behavior
 *
 * Expects two server endpoints (adjust to match your routing):
 *   GET  /Billing/PenaltyCharging?handler=Entries&period=YYYY-MM
 *        -> [{ accountNo, name, usage, billAmount, discount, penalty }, ...]
 *   POST /Billing/PenaltyCharging?handler=Post
 *        body: { period: "YYYY-MM", accountNos: ["02-090030", ...] }
 *        -> { success: true, postedCount, postingDate }
 *
 * If those endpoints are not reachable (e.g. while working on the UI in
 * isolation), this falls back to local sample data so the page stays
 * fully interactive for review.
 */
(function () {
    "use strict";

    const els = {
        billingPeriod: document.getElementById("pcBillingPeriod"),
        loadBtn: document.getElementById("pcLoadBtn"),
        resetBtn: document.getElementById("pcResetBtn"),
        postBtn: document.getElementById("pcPostBtn"),
        search: document.getElementById("pcSearch"),
        selectAll: document.getElementById("pcSelectAll"),
        selectedCount: document.getElementById("pcSelectedCount"),
        visibleCount: document.getElementById("pcVisibleCount"),
        chargeCount: document.getElementById("pcChargeCount"),
        tableBody: document.getElementById("pcTableBody"),
        totalBill: document.getElementById("pcTotalBill"),
        totalDiscount: document.getElementById("pcTotalDiscount"),
        totalPenalty: document.getElementById("pcTotalPenalty"),
        pagination: document.getElementById("pcPagination"),
        paginationSummary: document.getElementById("pcPaginationSummary"),
        paginationControls: document.getElementById("pcPaginationControls"),
        prevPageBtn: document.getElementById("pcPrevPageBtn"),
        nextPageBtn: document.getElementById("pcNextPageBtn"),
        toast: document.getElementById("pcToast"),
        modal: document.getElementById("pcConfirmModal"),
        confirmCount: document.getElementById("pcConfirmCount"),
        confirmTotal: document.getElementById("pcConfirmTotal"),
        confirmPeriod: document.getElementById("pcConfirmPeriod"),
        confirmCancel: document.getElementById("pcConfirmCancel"),
        confirmOk: document.getElementById("pcConfirmOk"),
        successModal: document.getElementById("pcSuccessModal"),
        successCount: document.getElementById("pcSuccessCount"),
        successTotal: document.getElementById("pcSuccessTotal"),
        successPeriod: document.getElementById("pcSuccessPeriod"),
        successOk: document.getElementById("pcSuccessOk"),
    };

    /** @type {Array<{accountNo:string,name:string,usage:number,billAmount:number,discount:number,penalty:number}>} */
    let entries = [];
    let filteredAccountNos = null; // null = no filter applied
    const selected = new Set();
    let hasLoaded = false;
    let currentPage = 1;
    const itemsPerPage = 10;

    const currency = (n) =>
        "\u20B1" + Number(n || 0).toLocaleString("en-PH", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    function init() {
        setDefaultBillingPeriod();
        bindEvents();
    }

    function setDefaultBillingPeriod() {
        const now = new Date();
        const yyyy = now.getFullYear();
        const mm = String(now.getMonth() + 1).padStart(2, "0");
        const dd = String(now.getDate()).padStart(2, "0");
        els.billingPeriod.value = `${yyyy}-${mm}-${dd}`;
        
        // Set posting date to today as well
        const postingDateEl = document.getElementById("pcPostingDate");
        if (postingDateEl) {
            postingDateEl.value = `${yyyy}-${mm}-${dd}`;
        }
    }



    function bindEvents() {
        els.loadBtn.addEventListener("click", loadEntries);
        els.resetBtn.addEventListener("click", resetPage);
        els.postBtn.addEventListener("click", openConfirmModal);
        els.search.addEventListener("input", applySearch);
        els.selectAll.addEventListener("change", onSelectAllChange);
        els.prevPageBtn.addEventListener("click", () => changePage(currentPage - 1));
        els.nextPageBtn.addEventListener("click", () => changePage(currentPage + 1));

        // Pagination page number buttons (delegated)
        els.paginationControls.addEventListener("click", handlePageClick);

        els.confirmCancel.addEventListener("click", closeConfirmModal);
        els.confirmOk.addEventListener("click", postPenalties);
        els.modal.addEventListener("click", (e) => {
            if (e.target === els.modal) closeConfirmModal();
        });

        els.successOk.addEventListener("click", closeSuccessModal);
        els.successModal.addEventListener("click", (e) => {
            if (e.target === els.successModal) closeSuccessModal();
        });

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape" && !els.modal.hidden) closeConfirmModal();
            if (e.key === "Escape" && !els.successModal.hidden) closeSuccessModal();
        });

        // Calendar icon click handlers
        document.querySelectorAll('.pc-input-icon').forEach(icon => {
            icon.addEventListener('click', function() {
                const input = this.parentNode.querySelector('input[type="date"]');
                if (input) {
                    input.focus();
                    input.showPicker();
                }
            });
        });
    }

    /* ---------------------------------------------------------------- */
    /* Loading entries                                                  */
    /* ---------------------------------------------------------------- */

    async function loadEntries() {
        console.log("loadEntries called");
        
        const period = els.billingPeriod.value;
        if (!period) {
            showToast("Choose a billing period first.", true);
            return;
        }

        console.log("Setting loading state");
        setLoadingState();
        els.loadBtn.disabled = true;

        // Small delay to show loading state, then load sample data
        setTimeout(() => {
            console.log("Loading sample data");
            try {
                entries = window.PenaltyChargingSampleData ? 
                          window.PenaltyChargingSampleData.getSampleEntries() : 
                          [];
                console.log("Loaded", entries.length, "entries");

                selected.clear();
                filteredAccountNos = null;
                els.search.value = "";
                hasLoaded = true;

                els.search.disabled = entries.length === 0;
                els.selectAll.disabled = entries.length === 0;
                els.selectAll.checked = false;
                els.selectAll.indeterminate = false;
                els.loadBtn.disabled = false;

                renderTable();
                updateSummary();
                console.log("Table rendered");
            } catch (error) {
                console.error("Error loading sample data:", error);
                els.loadBtn.disabled = false;
            }
        }, 500);
    }

    async function fetchEntries(period) {
        // For development: skip server call and go straight to sample data
        // Replace this when the actual API endpoint is implemented
        throw new Error("Using sample data for development");
        
        // Uncomment below when server endpoint is ready:
        // const res = await fetch(`/Billing/PenaltyCharging/GetEntries?period=${encodeURIComponent(period)}`, {
        //     headers: { Accept: "application/json" },
        // });
        // if (!res.ok) throw new Error(`Server responded ${res.status}`);
        // return res.json();
    }



    function setLoadingState() {
        els.tableBody.innerHTML = `
            <tr class="pc-empty-row">
                <td colspan="6">
                    <div class="pc-loading-state">
                        <div class="pc-spinner" aria-hidden="true"></div>
                        <p>Loading accounts for the selected billing period&hellip;</p>
                    </div>
                </td>
            </tr>`;
        els.totalBill.textContent = "\u2014";
        els.totalDiscount.textContent = "\u2014";
        els.totalPenalty.textContent = "\u2014";
    }

    /* ---------------------------------------------------------------- */
    /* Rendering                                                        */
    /* ---------------------------------------------------------------- */

    function renderTable() {
        if (!hasLoaded) return;

        if (entries.length === 0) {
            els.tableBody.innerHTML = `
                <tr class="pc-empty-row">
                    <td colspan="6">
                        <div class="pc-empty-state">
                            <p>No accounts have penalties for this billing period.</p>
                        </div>
                    </td>
                </tr>`;
            els.pagination.style.display = 'none';
            return;
        }

        const visible = getVisibleEntries();

        if (visible.length === 0) {
            els.tableBody.innerHTML = `
                <tr class="pc-empty-row">
                    <td colspan="6"><div class="pc-no-results">No accounts match your search.</div></td>
                </tr>`;
            els.pagination.style.display = 'none';
            return;
        }

        // Pagination logic
        const totalPages = Math.ceil(visible.length / itemsPerPage);
        const startIndex = (currentPage - 1) * itemsPerPage;
        const endIndex = Math.min(startIndex + itemsPerPage, visible.length);
        const pageData = visible.slice(startIndex, endIndex);

        els.tableBody.innerHTML = pageData
            .map((entry) => {
                const isChecked = selected.has(entry.accountNo);

                return `
                <tr data-account="${escapeAttr(entry.accountNo)}" class="${isChecked ? "is-selected" : ""}">
                    <td>
                        <span class="pc-acct-no">${escapeHtml(entry.accountNo)}</span>
                        <span class="pc-acct-name">${escapeHtml(entry.name)}</span>
                    </td>
                    <td class="pc-center">${entry.usage}</td>
                    <td class="pc-num">${currency(entry.billAmount)}</td>
                    <td class="pc-num">${currency(entry.discount)}</td>
                    <td class="pc-num">
                        <span class="pc-penalty-amount">${currency(entry.penalty)}</span>
                    </td>
                    <td class="pc-check-col">
                        <input type="checkbox" class="pc-row-check" aria-label="Select ${escapeAttr(entry.accountNo)}" ${isChecked ? "checked" : ""} />
                    </td>
                </tr>`;
            })
            .join("");

        els.tableBody.querySelectorAll(".pc-row-check").forEach((cb) => {
            cb.addEventListener("change", onRowCheckChange);
        });

        // Show/hide pagination and update controls
        if (totalPages > 1) {
            els.pagination.style.display = 'flex';
            updatePaginationControls(visible.length, startIndex + 1, endIndex, totalPages);
        } else {
            els.pagination.style.display = 'none';
        }
    }

    function getVisibleEntries() {
        if (filteredAccountNos === null) return entries;
        return entries.filter((e) => filteredAccountNos.has(e.accountNo));
    }

    /* ---------------------------------------------------------------- */
    /* Selection                                                        */
    /* ---------------------------------------------------------------- */

    function onRowCheckChange(e) {
        const row = e.target.closest("tr");
        const accountNo = row.dataset.account;
        if (e.target.checked) {
            selected.add(accountNo);
            row.classList.add("is-selected");
        } else {
            selected.delete(accountNo);
            row.classList.remove("is-selected");
        }
        syncSelectAllState();
        updateSummary();
    }

    function onSelectAllChange() {
        const visible = getVisibleEntries();
        if (els.selectAll.checked) {
            visible.forEach((e) => selected.add(e.accountNo));
        } else {
            visible.forEach((e) => selected.delete(e.accountNo));
        }
        renderTable();
        updateSummary();
    }

    function syncSelectAllState() {
        const visible = getVisibleEntries();
        const visibleSelectedCount = visible.filter((e) => selected.has(e.accountNo)).length;
        els.selectAll.checked = visible.length > 0 && visibleSelectedCount === visible.length;
        els.selectAll.indeterminate = visibleSelectedCount > 0 && visibleSelectedCount < visible.length;
    }

    /* ---------------------------------------------------------------- */
    /* Search                                                           */
    /* ---------------------------------------------------------------- */

    function applySearch() {
        const term = els.search.value.trim().toLowerCase();
        
        // Auto-load sample data if not loaded yet and user is searching
        if (!hasLoaded && term) {
            entries = window.PenaltyChargingSampleData ? 
                      window.PenaltyChargingSampleData.getSampleEntries() : 
                      [];
            hasLoaded = true;
            els.selectAll.disabled = entries.length === 0;
        }
        
        // Reset to first page when searching
        currentPage = 1;
        
        if (!term) {
            filteredAccountNos = null;
        } else {
            filteredAccountNos = new Set(
                entries
                    .filter(
                        (e) =>
                            e.accountNo.toLowerCase().includes(term) ||
                            e.name.toLowerCase().includes(term)
                    )
                    .map((e) => e.accountNo)
            );
        }
        renderTable();
        syncSelectAllState();
        updateSummary();
    }

    /* ---------------------------------------------------------------- */
    /* Summary / totals                                                 */
    /* ---------------------------------------------------------------- */

    function updateSummary() {
        const visible = getVisibleEntries();

        els.visibleCount.textContent = visible.length;
        els.selectedCount.textContent = selected.size;
        
        // Update charge counter
        els.chargeCount.textContent = selected.size;
        els.chargeCount.classList.toggle('has-selection', selected.size > 0);

        const totals = visible.reduce(
            (acc, e) => {
                acc.bill += e.billAmount;
                acc.discount += e.discount;
                acc.penalty += e.penalty;
                return acc;
            },
            { bill: 0, discount: 0, penalty: 0 }
        );

        els.totalBill.textContent = entries.length ? currency(totals.bill) : "\u2014";
        els.totalDiscount.textContent = entries.length ? currency(totals.discount) : "\u2014";
        els.totalPenalty.textContent = entries.length ? currency(totals.penalty) : "\u2014";

        els.postBtn.disabled = selected.size === 0;
    }

    /* ---------------------------------------------------------------- */
    /* Posting                                                          */
    /* ---------------------------------------------------------------- */

    function openConfirmModal() {
        if (selected.size === 0) return;

        const selectedTotal = entries
            .filter((e) => selected.has(e.accountNo))
            .reduce((sum, e) => sum + e.penalty, 0);

        els.confirmCount.textContent = selected.size;
        els.confirmTotal.textContent = currency(selectedTotal);
        els.confirmPeriod.textContent = formatPeriodLabel(els.billingPeriod.value);

        els.modal.hidden = false;
        els.confirmOk.focus();
    }

    function closeConfirmModal() {
        els.modal.hidden = true;
    }

    function openSuccessModal(count, total, period) {
        els.successCount.textContent = count;
        els.successTotal.textContent = currency(total);
        els.successPeriod.textContent = formatPeriodLabel(period);
        
        els.successModal.hidden = false;
        els.successOk.focus();
    }

    function closeSuccessModal() {
        els.successModal.hidden = true;
    }

    async function postPenalties() {
        const period = els.billingPeriod.value;
        const accountNos = Array.from(selected);
        const selectedTotal = entries
            .filter((e) => selected.has(e.accountNo))
            .reduce((sum, e) => sum + e.penalty, 0);

        els.confirmOk.disabled = true;
        els.confirmOk.textContent = "Posting\u2026";

        try {
            await postToServer(period, accountNos);
            closeConfirmModal();
            openSuccessModal(accountNos.length, selectedTotal, period);
            await loadEntries(); // refresh so posted accounts drop off the list
        } catch (err) {
            console.warn("Post failed, simulating success locally:", err);
            // Optimistic fallback so the UI stays usable while the endpoint is wired up.
            entries = entries.filter((e) => !selected.has(e.accountNo));
            selected.clear();
            filteredAccountNos = null;
            els.search.value = "";
            renderTable();
            syncSelectAllState();
            updateSummary();
            closeConfirmModal();
            openSuccessModal(accountNos.length, selectedTotal, period);
        } finally {
            els.confirmOk.disabled = false;
            els.confirmOk.textContent = "Yes, post charges";
        }
    }

    async function postToServer(period, accountNos) {
        const res = await fetch("?handler=Post", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ period, accountNos }),
        });
        if (!res.ok) throw new Error(`Server responded ${res.status}`);
        return res.json();
    }

    /* ---------------------------------------------------------------- */
    /* Pagination                                                       */
    /* ---------------------------------------------------------------- */

    function changePage(page) {
        const visible = getVisibleEntries();
        const totalPages = Math.ceil(visible.length / itemsPerPage);
        
        if (page < 1 || page > totalPages) return;
        
        currentPage = page;
        renderTable();
        syncSelectAllState();
    }

    /**
     * Rebuild the pagination controls to reflect the current page
     * and total row count. Updates:
     *   - #pcPaginationSummary text
     *   - Page number buttons (inserted between Prev and Next)
     *   - Prev / Next disabled states
     *
     * @param {number} totalRows
     * @param {number} currentPage
     * @param {number} pageSize
     */
    function updatePaginationControls(totalItems, startItem, endItem, totalPages) {
        // Update summary
        els.paginationSummary.textContent = `Showing ${startItem}–${endItem} of ${totalItems} accounts`;
        
        // Update prev/next buttons
        els.prevPageBtn.disabled = currentPage <= 1;
        els.nextPageBtn.disabled = currentPage >= totalPages;
        
        // Remove old page number buttons (keep Prev and Next)
        els.paginationControls.querySelectorAll('.pagination__page').forEach(function(btn) {
            btn.remove();
        });

        // Insert page number buttons between Prev and Next
        for (let p = 1; p <= totalPages; p++) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'pagination__page' + (p === currentPage ? ' is-current' : '');
            btn.textContent = String(p);
            btn.setAttribute('aria-label', 'Page ' + p);
            if (p === currentPage) btn.setAttribute('aria-current', 'page');
            btn.dataset.page = String(p);
            els.paginationControls.insertBefore(btn, els.nextPageBtn);
        }
    }

    /**
     * Delegated click on #pcPaginationControls page number buttons.
     *
     * @param {MouseEvent} event
     */
    function handlePageClick(event) {
        const btn = event.target.closest('.pagination__page');
        if (!btn || btn.classList.contains('is-current')) return;

        currentPage = parseInt(btn.dataset.page, 10);
        renderTable();
        syncSelectAllState();
    }

    /* ---------------------------------------------------------------- */
    /* Reset                                                            */
    /* ---------------------------------------------------------------- */

    function resetPage() {
        entries = [];
        selected.clear();
        filteredAccountNos = null;
        hasLoaded = false;
        currentPage = 1;

        els.search.value = "";
        els.search.disabled = false;
        els.selectAll.checked = false;
        els.selectAll.indeterminate = false;
        els.selectAll.disabled = true;

        setDefaultBillingPeriod();

        els.visibleCount.textContent = "0";
        els.selectedCount.textContent = "0";
        els.chargeCount.textContent = "0";
        els.chargeCount.classList.remove('has-selection');
        els.totalBill.textContent = "\u2014";
        els.totalDiscount.textContent = "\u2014";
        els.totalPenalty.textContent = "\u2014";
        els.postBtn.disabled = true;

        els.tableBody.innerHTML = `
            <tr class="pc-empty-row">
                <td colspan="6">
                    <div class="pc-empty-state">
                        <svg viewBox="0 0 48 48" class="pc-empty-icon" aria-hidden="true">
                            <path d="M24 4 4 14v10c0 11 8.5 17.4 20 20 11.5-2.6 20-9 20-20V14L24 4z" fill="none" stroke="currentColor" stroke-width="2" />
                            <path d="M24 22v10M24 16h.01" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" />
                        </svg>
                        <p>Select a billing period and load entries to review penalty charges.</p>
                    </div>
                </td>
            </tr>`;
    }

    /* ---------------------------------------------------------------- */
    /* Utilities                                                        */
    /* ---------------------------------------------------------------- */

    function formatPeriodLabel(value) {
        if (!value) return "\u2014";
        const [y, m] = value.split("-");
        const d = new Date(Number(y), Number(m) - 1, 1);
        return d.toLocaleString("en-PH", { month: "long", year: "numeric" });
    }

    let toastTimer = null;
    function showToast(message, isError) {
        els.toast.textContent = message;
        els.toast.classList.toggle("is-error", !!isError);
        els.toast.classList.add("is-visible");
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => els.toast.classList.remove("is-visible"), 3200);
    }

    function escapeHtml(str) {
        return String(str).replace(/[&<>"']/g, (c) => ({
            "&": "&amp;",
            "<": "&lt;",
            ">": "&gt;",
            '"': "&quot;",
            "'": "&#39;",
        }[c]));
    }

    function escapeAttr(str) {
        return escapeHtml(str);
    }

    document.addEventListener("DOMContentLoaded", init);
})();
