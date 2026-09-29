(function () {
    'use strict';
    const state = { applications: [], selected: null, category: '', inspection: null, connectOrder: null, charges: [], chargeTypesLoaded: false };
    const applicationsModal = document.getElementById('applicationsModal');
    const chargeModal = document.getElementById('chargeModal');
    const inspectionModal = document.getElementById('inspectionModal');
    const connectOrderModal = document.getElementById('connectOrderModal');
    const applicationList = document.getElementById('applicationList');
    const search = document.getElementById('applicationSearch');
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    document.querySelectorAll('[data-service-category]').forEach(card => card.addEventListener('click', () => openApplications(card.dataset.serviceCategory, card.dataset.serviceTitle)));
    document.querySelectorAll('[data-close-modal]').forEach(button => button.addEventListener('click', () => closeModal(document.getElementById(button.dataset.closeModal))));
    search?.addEventListener('input', renderApplications);
    document.getElementById('dashboardAddCharge')?.addEventListener('click', saveCharge);
    document.getElementById('inspectionSaveButton')?.addEventListener('click', saveInspection);
    document.getElementById('inspectionPdfButton')?.addEventListener('click', createInspectionPdf);
    document.getElementById('inspectionZone')?.addEventListener('change', populateInspectionBooks);
    document.getElementById('connectOrderSaveButton')?.addEventListener('click', saveConnectOrder);
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        closeModal(connectOrderModal.classList.contains('is-open') ? connectOrderModal : inspectionModal.classList.contains('is-open') ? inspectionModal : chargeModal.classList.contains('is-open') ? chargeModal : applicationsModal);
    });

    async function openApplications(category, title) {
        state.category = category;
        document.getElementById('applicationsModalTitle').textContent = title + ' Applications';
        search.value = '';
        applicationList.innerHTML = '<div class="dashboard-modal__state">Loading applications...</div>';
        openModal(applicationsModal);
        try {
            const response = await fetch('/Admin/GetServiceApplications?category=' + encodeURIComponent(category));
            if (!response.ok) throw new Error(await response.text());
            state.applications = await response.json() || [];
            renderApplications();
            search.focus();
        } catch (error) { applicationList.innerHTML = `<div class="dashboard-modal__state">${html(error.message || 'Unable to load applications.')}</div>`; }
    }

    function renderApplications() {
        const query = (search.value || '').trim().toLowerCase();
        const rows = state.applications.filter(x => !query || [x.controlNumber, x.accountNumber, x.accountName, x.serviceName].some(v => (v || '').toLowerCase().includes(query)));
        if (!rows.length) { applicationList.innerHTML = '<div class="dashboard-modal__state">No matching service applications.</div>'; return; }
        applicationList.innerHTML = rows.map((x, index) => `<button type="button" class="application-row" data-application-index="${state.applications.indexOf(x)}">
            <span><strong class="application-row__control">${html(x.controlNumber || 'NO CONTROL NUMBER')}</strong><span class="application-row__account">${html(x.accountNumber)}</span></span>
            <span><strong class="application-row__name">${html(x.accountName)}</strong><span class="application-row__service">${html(x.serviceName)}</span></span>
            <span class="application-row__meta"><span class="application-row__date">${dateText(x.dateSubmitted)}</span><span class="application-row__stage">${html(x.stage)}</span></span>
        </button>`).join('');
        applicationList.querySelectorAll('[data-application-index]').forEach(row => row.addEventListener('click', () => {
            const application = state.applications[Number(row.dataset.applicationIndex)];
            if (state.category === 'inspection') openInspectionWorkspace(application);
            else if (state.category === 'connect') openConnectOrder(application);
            else openChargeWorkspace(application);
        }));
    }

    async function openInspectionWorkspace(application) {
        state.selected = application;
        setInspectionMessage('');
        openModal(inspectionModal);
        document.getElementById('inspectionApplicationNumber').textContent = application.controlNumber || '—';
        document.getElementById('inspectionApplicantName').textContent = application.accountName || '—';
        try {
            const response = await fetch('/Admin/GetServiceInspection?requestId=' + encodeURIComponent(application.requestId));
            if (!response.ok) throw new Error(await response.text());
            state.inspection = await response.json();
            renderInspection(state.inspection);
        } catch (error) {
            state.inspection = null;
            setInspectionMessage(error.message || 'Unable to load the inspection record.');
        }
    }

    function renderInspection(data) {
        textValue('inspectionApplicationNumber', data.applicationNumber);
        textValue('inspectionApplicantName', data.applicantName);
        textValue('inspectionDateApplied', dateText(data.dateApplied));
        textValue('inspectionAddress', data.address || 'No address recorded');
        textValue('inspectionClassification', data.classification);
        textValue('inspectionMobile', data.mobileNumber || 'Not provided');
        inputValue('inspectionWaterCondition', data.waterServiceCondition);
        inputValue('inspectionPavement', data.classOfPavement);
        inputValue('inspectionPipeSize', data.pipeSize);
        inputValue('inspectionConnectionType', data.typeOfConnection);
        inputValue('inspectionDistance', data.distanceFromMainline);
        inputValue('inspectionNearestNumber', data.nearestAccountNumber);
        inputValue('inspectionNearestName', data.nearestAccountName);
        inputValue('inspectionRemarks', data.inspectionRemarks);
        inputValue('inspectionDate', dateInput(data.dateInspected));
        inputValue('inspectionStatus', data.status || 'Pending');
        document.getElementById('inspectionBoring').checked = !!data.needsUndergroundBoring;
        document.getElementById('inspectionChargeNsc').checked = data.chargeNscFee !== false;
        fillOptions('inspectionInspector', data.inspectors, 'Select inspector', data.inspectedByUserId);
        fillOptions('inspectionApprover', data.approvers, 'Select approver', data.approvedByUserId);
        const zones = [...new Set((data.zones || []).map(x => Number(x.zone)))];
        document.getElementById('inspectionZone').innerHTML = '<option value="">Select zone</option>' + zones.map(x => `<option value="${x}">Zone ${x}</option>`).join('');
        inputValue('inspectionZone', data.zone);
        populateInspectionBooks();
        inputValue('inspectionBook', data.book);
    }

    function populateInspectionBooks() {
        const zone = Number(document.getElementById('inspectionZone').value);
        const current = document.getElementById('inspectionBook').value;
        const books = [...new Set((state.inspection?.zones || []).filter(x => Number(x.zone) === zone).map(x => Number(x.book)))];
        document.getElementById('inspectionBook').innerHTML = '<option value="">Select book</option>' + books.map(x => `<option value="${x}">Book ${x}</option>`).join('');
        if (books.includes(Number(current))) document.getElementById('inspectionBook').value = current;
    }

    function inspectionPayload() {
        return {
            reportId: Number(state.inspection?.reportId || 0), requestId: Number(state.selected?.requestId || 0),
            waterServiceCondition: value('inspectionWaterCondition'), classOfPavement: value('inspectionPavement'),
            pipeSize: value('inspectionPipeSize'), typeOfConnection: value('inspectionConnectionType'),
            distanceFromMainline: value('inspectionDistance'), needsUndergroundBoring: document.getElementById('inspectionBoring').checked,
            zone: nullableNumber('inspectionZone'), book: nullableNumber('inspectionBook'),
            nearestAccountNumber: value('inspectionNearestNumber'), nearestAccountName: value('inspectionNearestName'),
            inspectionRemarks: value('inspectionRemarks'), inspectedByUserId: nullableNumber('inspectionInspector'),
            approvedByUserId: nullableNumber('inspectionApprover'), dateInspected: value('inspectionDate') || null,
            status: value('inspectionStatus'), chargeNscFee: document.getElementById('inspectionChargeNsc').checked
        };
    }

    async function saveInspection() {
        const payload = inspectionPayload();
        if (!payload.inspectedByUserId || !payload.dateInspected) return setInspectionMessage('Select the inspector and inspection date.');
        const button = document.getElementById('inspectionSaveButton');
        button.disabled = true; setInspectionMessage('Saving inspection...');
        try {
            const response = await fetch('/Admin/SaveServiceInspection', {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved = await response.json();
            const context = state.inspection;
            state.inspection = {
                ...context, ...saved,
                applicationNumber: context.applicationNumber, applicantName: context.applicantName,
                dateApplied: context.dateApplied, address: context.address, mobileNumber: context.mobileNumber,
                classification: context.classification, serviceName: context.serviceName,
                inspectors: context.inspectors, approvers: context.approvers, zones: context.zones
            };
            setInspectionMessage('Inspection saved successfully. The PDF report is ready.', true);
        } catch (error) { setInspectionMessage(error.message || 'The inspection could not be saved.'); }
        finally { button.disabled = false; }
    }

    async function openConnectOrder(application) {
        state.selected = application;
        setConnectMessage('');
        openModal(connectOrderModal);
        textValue('connectApplicationNumber', application.controlNumber);
        textValue('connectApplicantName', application.accountName);
        try {
            const response = await fetch('/Admin/GetConnectOrder?requestId=' + encodeURIComponent(application.requestId));
            if (!response.ok) throw new Error(await response.text());
            state.connectOrder = await response.json();
            renderConnectOrder(state.connectOrder);
        } catch (error) {
            state.connectOrder = null;
            setConnectMessage(error.message || 'Unable to load the connect order.');
        }
    }

    function renderConnectOrder(data) {
        textValue('connectApplicationNumber', data.applicationNumber);
        textValue('connectApplicantName', data.applicantName);
        textValue('connectDateApplied', dateText(data.dateApplied));
        textValue('connectAddress', data.address || 'No address recorded');
        textValue('connectClassification', data.classification);
        textValue('connectProcessedBy', data.processedByName || 'Not recorded');
        textValue('connectInspectedBy', data.inspectedByName || 'Pending inspection');
        textValue('connectInspectionApprovedBy', data.inspectionApprovedByName || 'Pending approval');
        textValue('connectInvoiceNumber', data.invoiceNumber || 'No paid invoice');
        textValue('connectInvoiceDate', data.invoiceDate ? dateText(data.invoiceDate) : '—');
        fillOptions('connectRecommendedBy', data.recommenders, 'Select recommender', data.recommendedApprovalByUserId);
        fillOptions('connectApprovedBy', data.approvers, 'Select approver', data.approvedByUserId);
        inputValue('connectDateRecommended', dateInput(data.dateRecommended));
        inputValue('connectDateApproved', dateInput(data.dateApproved));
    }

    function connectOrderPayload() {
        return {
            recommendationId: Number(state.connectOrder?.recommendationId || 0),
            requestId: Number(state.selected?.requestId || 0),
            recommendedApprovalByUserId: nullableNumber('connectRecommendedBy'),
            dateRecommended: value('connectDateRecommended') || null,
            approvedByUserId: nullableNumber('connectApprovedBy'),
            dateApproved: value('connectDateApproved') || null
        };
    }

    async function saveConnectOrder() {
        const payload = connectOrderPayload();
        if (!payload.recommendedApprovalByUserId || !payload.dateRecommended)
            return setConnectMessage('Select who recommended the connection and the recommendation date.');
        if (!!payload.approvedByUserId !== !!payload.dateApproved)
            return setConnectMessage('Select both the approver and approval date, or leave both blank.');
        const button = document.getElementById('connectOrderSaveButton');
        button.disabled = true; setConnectMessage('Saving connect order...');
        try {
            const response = await fetch('/Admin/SaveConnectOrder', {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved = await response.json();
            const context = state.connectOrder;
            state.connectOrder = {
                ...context, ...saved,
                applicationNumber: context.applicationNumber, applicantName: context.applicantName,
                dateApplied: context.dateApplied, address: context.address, classification: context.classification,
                processedByName: context.processedByName, inspectedByName: context.inspectedByName,
                inspectionApprovedByName: context.inspectionApprovedByName, invoiceNumber: context.invoiceNumber,
                invoiceDate: context.invoiceDate, recommenders: context.recommenders, approvers: context.approvers
            };
            setConnectMessage('Connect order saved successfully.', true);
        } catch (error) { setConnectMessage(error.message || 'The connect order could not be saved.'); }
        finally { button.disabled = false; }
    }

    async function openChargeWorkspace(application) {
        state.selected = application;
        document.getElementById('dashboardAccountNumber').textContent = application.accountNumber || '—';
        document.getElementById('dashboardAccountName').textContent = application.accountName || '—';
        document.getElementById('dashboardServiceName').textContent = application.serviceName || '—';
        document.getElementById('dashboardParticulars').value = application.serviceName || '';
        setMessage('');
        openModal(chargeModal);
        document.getElementById('dashboardChargeRows').innerHTML = '<tr><td colspan="4" class="dashboard-modal__state">Loading charges...</td></tr>';
        try {
            const [typesResponse, chargesResponse] = await Promise.all([
                state.chargeTypesLoaded ? Promise.resolve(null) : fetch('/Billing/GetChargeTypes'),
                fetch('/Billing/GetOtherChargesByAccount?accountNumber=' + encodeURIComponent(application.accountNumber))
            ]);
            if (typesResponse) {
                if (!typesResponse.ok) throw new Error('Could not load charge types.');
                const types = await typesResponse.json() || [];
                const select = document.getElementById('dashboardChargeType');
                select.innerHTML = '<option value="">Select a charge type</option>' + types.map(x => `<option value="${Number(x.value)}">${html(x.label)}</option>`).join('');
                state.chargeTypesLoaded = true;
            }
            if (!chargesResponse.ok && chargesResponse.status !== 404) throw new Error(await chargesResponse.text());
            state.charges = chargesResponse.ok ? await chargesResponse.json() || [] : [];
            renderCharges();
        } catch (error) { setMessage(error.message || 'Unable to load account charges.'); state.charges = []; renderCharges(); }
    }

    async function saveCharge() {
        if (!state.selected) return;
        const type = Number(document.getElementById('dashboardChargeType').value);
        const payableIn = document.getElementById('dashboardPayableIn').value;
        const particulars = document.getElementById('dashboardParticulars').value.trim();
        const amount = Number(document.getElementById('dashboardAmount').value);
        if (!type || !Number.isFinite(amount) || amount <= 0) return setMessage('Select a charge type and enter an amount greater than zero.');
        const button = document.getElementById('dashboardAddCharge'); button.disabled = true; setMessage('');
        try {
            const response = await fetch('/Billing/SaveOtherCharge', {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({ accountNumber: state.selected.accountNumber, chargeType: type, payableIn, particulars, amount })
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            state.charges.push(await response.json());
            document.getElementById('dashboardAmount').value = '';
            setMessage('Charge added successfully.', true);
            renderCharges();
        } catch (error) { setMessage(error.message || 'The charge could not be saved.'); }
        finally { button.disabled = false; }
    }

    function renderCharges() {
        const body = document.getElementById('dashboardChargeRows');
        body.innerHTML = state.charges.length ? state.charges.map(x => `<tr><td>${html(x.description)}</td><td>${html(x.payableIn)}</td><td>${html(x.particulars)}</td><td>₱${money(x.amount)}</td></tr>`).join('') : '<tr><td colspan="4" class="dashboard-modal__state">No active charges for this account.</td></tr>';
        document.getElementById('dashboardChargeTotal').textContent = '₱' + money(state.charges.reduce((sum, x) => sum + Number(x.amount || 0), 0));
    }

    function createInspectionPdf() {
        if (!state.inspection) return setInspectionMessage('Load an inspection record before creating the PDF.');
        const Pdf = window.jspdf?.jsPDF || window.jsPDF;
        if (!Pdf) return setInspectionMessage('The PDF component is unavailable. Refresh the page and try again.');
        const form = inspectionPayload();
        const data = { ...state.inspection, ...form, inspectedByName: selectedText('inspectionInspector'), approvedByName: selectedText('inspectionApprover') };
        const doc = new Pdf({ orientation: 'portrait', unit: 'pt', format: 'letter' });
        const left = 42, right = 570, width = right - left;
        doc.setDrawColor(32); doc.setLineWidth(.8); doc.rect(left, 38, width, 62);
        doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text('REPUBLIC OF THE PHILIPPINES', left + 12, 54);
        doc.setFont('helvetica', 'bold'); doc.setFontSize(13); doc.text('TRECE MARTIRES CITY WATER DISTRICT', left + 12, 71);
        doc.setFont('helvetica', 'normal'); doc.setFontSize(7); doc.text('Brgy. San Agustin, Trece Martires City, Cavite', left + 12, 84);
        doc.setFont('helvetica', 'bold'); doc.setFontSize(11); doc.text('NEW CONNECTION REPORT', right - 12, 54, { align: 'right' });
        doc.setFontSize(7); doc.text('APPLICATION NO.', 405, 71); doc.setFont('helvetica', 'normal'); doc.text(data.applicationNumber || '', right - 12, 71, { align: 'right' });
        doc.setFont('helvetica', 'bold'); doc.text('DATE APPLIED', 405, 86); doc.setFont('helvetica', 'normal'); doc.text(shortDate(data.dateApplied), right - 12, 86, { align: 'right' });
        doc.setFontSize(7); doc.setFont('helvetica', 'bold');
        doc.text('APPLICANT NAME', left + 6, 116); doc.text('CLASSIFICATION', 332, 116);
        doc.text('SERVICE ADDRESS', left + 6, 130); doc.text('CONTACT NUMBER', 332, 130);
        doc.setFont('helvetica', 'normal');
        doc.text(data.applicantName || '', 122, 116); doc.text(data.classification || '', 414, 116);
        doc.text(trimPdf(data.address, 54), 122, 130); doc.text(data.mobileNumber || '', 414, 130);
        doc.line(left, 140, right, 140);
        doc.setFont('helvetica', 'bold'); doc.setFontSize(9); doc.text('INSPECTION REPORT', 306, 153, { align: 'center' });
        const yesNo = item => item ? 'YES' : 'NO';
        doc.autoTable({ startY: 160, margin: { left, right: 42 }, theme: 'grid', styles: { fontSize: 7, cellPadding: 5, lineColor: 25, lineWidth: .5 },
            columnStyles: { 0: { fontStyle: 'bold', cellWidth: 115 }, 1: { cellWidth: 145 }, 2: { fontStyle: 'bold', cellWidth: 115 }, 3: { cellWidth: 153 } },
            body: [
                ['Water pressure / condition', data.waterServiceCondition || '—', 'Pipe size', data.pipeSize || '—'],
                ['Underground boring', yesNo(data.needsUndergroundBoring), 'Distance from mainline', data.distanceFromMainline || '—'],
                ['Type of connection', data.typeOfConnection || '—', 'Class of pavement', data.classOfPavement || '—'],
                ['Zone / Book', `${data.zone || '—'} / ${data.book || '—'}`, 'Charge NSC fee', yesNo(data.chargeNscFee)],
                ['Nearest account number', data.nearestAccountNumber || '—', 'Nearest account name', data.nearestAccountName || '—']
            ] });
        let y = doc.lastAutoTable.finalY + 12;
        doc.setFont('helvetica', 'bold'); doc.setFontSize(8); doc.text('INSPECTION REMARKS', left + 6, y + 12);
        doc.rect(left, y, width, 78); doc.setFont('helvetica', 'normal'); doc.setFontSize(7);
        doc.text(doc.splitTextToSize(data.inspectionRemarks || 'No remarks recorded.', width - 20), left + 10, y + 27);
        y += 90;
        doc.autoTable({ startY: y, margin: { left, right: 42 }, theme: 'grid', styles: { fontSize: 7, cellPadding: 6, lineColor: 25, lineWidth: .5 },
            body: [
                [{ content: 'INSPECTED BY', styles: { fontStyle: 'bold' } }, data.inspectedByName || '—', { content: 'APPROVED BY', styles: { fontStyle: 'bold' } }, data.approvedByName || '—'],
                [{ content: 'DATE INSPECTED', styles: { fontStyle: 'bold' } }, shortDate(data.dateInspected), { content: 'STATUS', styles: { fontStyle: 'bold' } }, data.status || 'Pending']
            ] });
        y = doc.lastAutoTable.finalY + 34;
        doc.line(left + 12, y, left + 220, y); doc.line(right - 220, y, right - 12, y);
        doc.setFontSize(7); doc.text("Applicant's / Representative's signature over printed name", left + 116, y + 12, { align: 'center' });
        doc.text('Inspection performed by', right - 116, y + 12, { align: 'center' });
        y += 55; doc.line(195, y, 417, y); doc.text('Installed by — signature over printed name / date', 306, y + 12, { align: 'center' });
        doc.setFontSize(6); doc.setTextColor(90); doc.text('Generated by TMCWD Service Management System', left, 754);
        doc.text('Report ' + (data.applicationNumber || ''), right, 754, { align: 'right' });
        window.open(doc.output('bloburl'), '_blank', 'noopener');
    }

    function openModal(modal) { modal.classList.add('is-open'); modal.setAttribute('aria-hidden', 'false'); }
    function closeModal(modal) { if (!modal) return; modal.classList.remove('is-open'); modal.setAttribute('aria-hidden', 'true'); }
    function setMessage(message, success) { const box = document.getElementById('dashboardChargeMessage'); box.textContent = message; box.classList.toggle('is-success', !!success); }
    function setInspectionMessage(message, success) { const box = document.getElementById('inspectionMessage'); box.textContent = message; box.classList.toggle('is-success', !!success); }
    function setConnectMessage(message, success) { const box = document.getElementById('connectOrderMessage'); box.textContent = message; box.classList.toggle('is-success', !!success); }
    function inputValue(id, value) { const element = document.getElementById(id); if (element) element.value = value == null ? '' : value; }
    function textValue(id, value) { const element = document.getElementById(id); if (element) element.textContent = value || '—'; }
    function value(id) { return document.getElementById(id)?.value?.trim() || ''; }
    function nullableNumber(id) { const result = Number(value(id)); return result > 0 ? result : null; }
    function selectedText(id) { const select = document.getElementById(id); return select?.selectedIndex > 0 ? select.options[select.selectedIndex].text : ''; }
    function fillOptions(id, rows, placeholder, selected) { const select = document.getElementById(id); select.innerHTML = `<option value="">${placeholder}</option>` + (rows || []).map(x => `<option value="${Number(x.value)}">${html(x.label)}</option>`).join(''); inputValue(id, selected); }
    function money(value) { return Number(value || 0).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
    function dateText(value) { const date = new Date(value); return Number.isNaN(date.getTime()) ? '' : date.toLocaleDateString('en-PH', { month: 'short', day: 'numeric', year: 'numeric' }); }
    function shortDate(value) { const date = new Date(value); return Number.isNaN(date.getTime()) ? '—' : date.toLocaleDateString('en-PH', { month: 'short', day: 'numeric', year: 'numeric' }); }
    function dateInput(value) { const date = new Date(value); return Number.isNaN(date.getTime()) ? '' : `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`; }
    function trimPdf(value, length) { const text = String(value || ''); return text.length > length ? text.slice(0, length - 1) + '…' : text; }
    function html(value) { const node = document.createElement('div'); node.textContent = value == null ? '' : String(value); return node.innerHTML; }
}());
