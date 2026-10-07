(function () {
    'use strict';

    /* ============================================================
       STATE
       ============================================================ */
    const state = {
        applications: [],
        selected: null,
        category: '',
        inspection: null,
        connectOrder: null,
        tapping: null,
        workOrder: null,
        workOrderMaterials: [],   // local material list (not yet saved)
        installationRequestSaved: false,
        charges: [],
        chargeTypesLoaded: false
    };

    /* ============================================================
       DOM REFERENCES
       ============================================================ */
    const applicationsModal  = document.getElementById('applicationsModal');
    const chargeModal        = document.getElementById('chargeModal');
    const inspectionModal    = document.getElementById('inspectionModal');
    const connectOrderModal  = document.getElementById('connectOrderModal');
    const houseTappingModal  = document.getElementById('houseTappingModal');
    const workOrderModal     = document.getElementById('workOrderModal');
    const installationRequestModal = document.getElementById('installationRequestModal');
    const requestReviewModal = document.getElementById('requestReviewModal');
    const paymentDetailModal = document.getElementById('paymentDetailModal');
    const serviceRequestModal = document.getElementById('serviceRequestModal');
    const srDetailModal       = document.getElementById('srDetailModal');
    const srAccountLedgerModal = document.getElementById('srAccountLedgerModal');
    const applicationList    = document.getElementById('applicationList');
    const search             = document.getElementById('applicationSearch');
    const token              = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    /* ============================================================
       BOOTSTRAP EVENT LISTENERS
       ============================================================ */
    document.querySelectorAll('[data-service-category]').forEach(card =>
        card.addEventListener('click', () => openApplications(card.dataset.serviceCategory, card.dataset.serviceTitle))
    );
    document.querySelectorAll('[data-close-modal]').forEach(button =>
        button.addEventListener('click', () => closeModal(document.getElementById(button.dataset.closeModal)))
    );

    search?.addEventListener('input', renderApplications);
    document.getElementById('dashboardAddCharge')?.addEventListener('click', saveCharge);
    document.getElementById('dashboardPostCharges')?.addEventListener('click', postCharges);

    // Summary table row clicks — open service request list modal
    document.querySelectorAll('.summary-table__row--clickable').forEach(row => {
        row.addEventListener('click', () => openServiceRequestModal(row.dataset.description));
    });

    document.getElementById('serviceRequestSearch')?.addEventListener('input', resetServiceRequestPage);
    ['srDateFrom', 'srDateTo'].forEach(id => document.getElementById(id)?.addEventListener('change', resetServiceRequestPage));
    document.getElementById('srPrevious')?.addEventListener('click', () => { serviceRequestPage = Math.max(1, serviceRequestPage - 1); renderServiceRequests(); });
    document.getElementById('srNext')?.addEventListener('click', () => { serviceRequestPage++; renderServiceRequests(); });
    document.getElementById('serviceRequestCompleted')?.addEventListener('click', () => openServiceRequestModal('', 'Completed'));

    // Pre-fill amount when a charge type is selected
    document.getElementById('dashboardChargeType')?.addEventListener('change', function () {
        const select = document.getElementById('dashboardChargeType');
        const selected = select.options[select.selectedIndex];
        const defaultAmount = selected ? Number(selected.dataset.defaultAmount || 0) : 0;
        if (defaultAmount > 0) {
            document.getElementById('dashboardAmount').value = defaultAmount.toFixed(2);
        }
    });
    document.getElementById('inspectionSaveButton')?.addEventListener('click', saveInspection);
    document.getElementById('inspectionPdfButton')?.addEventListener('click', createFormalInspectionPdf);
    document.getElementById('inspectionActivateButton')?.addEventListener('click', activateAccountFromInspection);
    document.getElementById('connectOrderSaveButton')?.addEventListener('click', saveConnectOrder);
    document.getElementById('tappingSaveButton')?.addEventListener('click', saveHouseTapping);
    document.getElementById('tappingZone')?.addEventListener('change', () => fillTappingBooks());
    document.getElementById('workOrderSaveButton')?.addEventListener('click', saveWorkOrder);
    document.getElementById('workOrderAddMaterial')?.addEventListener('click', addWorkOrderMaterial);
    document.getElementById('installationRequestForm')?.addEventListener('submit', saveInstallationServiceRequest);
    document.getElementById('installSrReferredTo')?.addEventListener('change', event => {
        document.getElementById('installSrDateReferred').disabled = !event.target.value;
    });

    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        if (installationRequestModal.classList.contains('is-open')) closeModal(installationRequestModal);
        else if (houseTappingModal.classList.contains('is-open'))    closeModal(houseTappingModal);
        else if (workOrderModal.classList.contains('is-open'))       closeModal(workOrderModal);
        else if (srAccountLedgerModal?.classList.contains('is-open')) closeModal(srAccountLedgerModal);
        else if (srDetailModal?.classList.contains('is-open'))       closeModal(srDetailModal);
        else if (serviceRequestModal?.classList.contains('is-open')) closeModal(serviceRequestModal);
        else if (paymentDetailModal.classList.contains('is-open')) closeModal(paymentDetailModal);
        else if (requestReviewModal.classList.contains('is-open')) closeModal(requestReviewModal);
        else if (connectOrderModal.classList.contains('is-open')) closeModal(connectOrderModal);
        else if (inspectionModal.classList.contains('is-open'))   closeModal(inspectionModal);
        else if (chargeModal.classList.contains('is-open'))       closeModal(chargeModal);
        else                                                       closeModal(applicationsModal);
    });

    /* ============================================================
       APPLICATIONS LIST MODAL
       ============================================================ */
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
        } catch (error) {
            applicationList.innerHTML = `<div class="dashboard-modal__state">${html(error.message || 'Unable to load applications.')}</div>`;
        }
    }

    function renderApplications() {
        const query = (search.value || '').trim().toLowerCase();
        const rows = state.applications.filter(x =>
            !query || [x.controlNumber, x.accountNumber, x.accountName, x.serviceName]
                .some(v => (v || '').toLowerCase().includes(query))
        );
        if (!rows.length) {
            applicationList.innerHTML = '<div class="dashboard-modal__state">No matching service applications.</div>';
            return;
        }
        applicationList.innerHTML = rows.map(x => `
            <button type="button" class="application-row" data-application-index="${state.applications.indexOf(x)}">
                <span>
                    <strong class="application-row__control">${html(x.controlNumber || 'NO CONTROL NUMBER')}</strong>
                    <span class="application-row__account">${html(x.accountNumber)}</span>
                </span>
                <span>
                    <strong class="application-row__name">${html(x.accountName)}</strong>
                    <span class="application-row__service">${html(x.serviceName)}</span>
                </span>
                <span class="application-row__meta">
                    <span class="application-row__date">${dateText(x.dateSubmitted)}</span>
                    <span class="application-row__stage">${html(x.stage)}</span>
                </span>
            </button>`).join('');

        applicationList.querySelectorAll('[data-application-index]').forEach(row =>
            row.addEventListener('click', () => {
                const application = state.applications[Number(row.dataset.applicationIndex)];
                if      (state.category === 'inspection')                        openInspectionWorkspace(application);
                else if (state.category === 'connect')                           openConnectOrder(application);
                else if (state.category === 'installation')                     openInstallationServiceRequest(application);
                else if (state.category === 'tapping')                          openHouseTapping(application);
                else if (state.category === 'payment')                           openPaymentWorkspace(application);
                else if (state.category === 'charging')                          openChargeWorkspace(application);
                else if (state.category === 'applications')                      openOnlineApplicationReview(application);
                else if (state.category === 'new')                               openNewApplicationReview(application);
                else if (state.category === 'tickets')                           openTicketWorkspace(application);
                else                                                             showUnsupportedWorkspace(application);
            })
        );
    }

    async function openPaymentWorkspace(application) {
        state.selected = application;
        textValue('paymentTrackingNumber', application.controlNumber);
        textValue('paymentApplicantName', application.accountName);
        openModal(paymentDetailModal);
        try {
            const response = await fetch('/Admin/GetApplicationPayment?applicationId=' + encodeURIComponent(application.requestId));
            if (!response.ok) throw new Error(await response.text());
            const payment = await response.json();
            textValue('paymentTrackingNumber', payment.trackingNumber);
            textValue('paymentApplicantName', payment.fullName);
            textValue('paymentChargeReference', payment.chargeReference || 'Not charged');
            textValue('paymentChargeAmount', payment.chargeAmount == null ? '—' : '₱' + money(payment.chargeAmount));
            textValue('paymentReference', payment.paymentReference || 'Not paid');
            textValue('paymentMethod', payment.paymentMethod || '—');
            textValue('paymentDate', payment.paymentDate ? dateText(payment.paymentDate) : '—');
            textValue('paymentStatus', payment.paymentStatus || 'Pending');
            textValue('paymentRemarks', payment.remarks || 'Payment information is loaded from the application payment record.');
        } catch (error) {
            textValue('paymentRemarks', error.message || 'Unable to load payment details.');
        }
    }

    function openOnlineApplicationReview(application) {
        return openRequestReview(application, 'applications');
    }

    function openNewApplicationReview(application) {
        return openRequestReview(application, 'new');
    }

    function openTicketWorkspace(application) {
        return openRequestReview(application, 'tickets');
    }

    async function openRequestReview(application, category) {
        state.selected = application;
        const labels = {
            applications: ['Online application', 'Application Review', 'Review the submitted application and open the complete record when changes are required.'],
            new: ['New request intake', 'New Application Review', 'Validate the new request before moving it to inspection and job-order processing.'],
            tickets: ['Service ticket', 'Ticket Details', 'Review the ticket information, requested service, and current processing stage.']
        }[category];
        textValue('requestReviewEyebrow', labels[0]);
        textValue('requestReviewTitle', labels[1]);
        textValue('reviewControlNumber', application.controlNumber);
        textValue('reviewAccountName', application.accountName);
        textValue('reviewDateSubmitted', dateText(application.dateSubmitted));
        textValue('reviewServiceName', application.serviceName);
        textValue('reviewStage', application.stage);
        textValue('reviewAddress', '—');
        textValue('reviewContact', '—');
        textValue('reviewEmail', '—');
        textValue('reviewSource', '—');
        textValue('requestReviewNote', labels[2]);

        // Show the Send to Inspection section only for New applications still at 'application' stage
        syncReviewAdvanceSection(category, application.stage);
        setReviewAdvanceMessage('');

        openModal(requestReviewModal);
        try {
            const response = await fetch('/Admin/GetApplicationTicket?applicationId=' + encodeURIComponent(application.requestId));
            if (!response.ok) throw new Error(await response.text());
            const detail = await response.json();
            textValue('reviewControlNumber', detail.trackingNumber);
            textValue('reviewAccountName', detail.fullName);
            textValue('reviewDateSubmitted', dateText(detail.createdAt));
            textValue('reviewServiceName', (detail.connectionType || '—') + ' connection');
            textValue('reviewStage', detail.stage);
            textValue('reviewAddress', [detail.address, detail.barangay].filter(Boolean).join(', ') || '—');
            textValue('reviewContact', detail.contactNumber || '—');
            textValue('reviewEmail', detail.email || '—');
            textValue('reviewSource', String(detail.source || '—').replaceAll('_', ' '));
            // remarks column stores internal JSON — don't display it; use the category hint instead
            textValue('requestReviewNote', labels[2]);
            // Re-sync with the accurate stage from the server
            syncReviewAdvanceSection(category, detail.stage);
        } catch (error) {
            textValue('requestReviewNote', error.message || 'Unable to load the complete application record.');
        }
    }

    /**
     * Show the "Send to inspection" panel only when:
     *  - we are in the 'new' category (the New card), AND
     *  - the application is still at 'application' stage (not yet advanced)
     * Hides the plain Close-only row when the advance panel is visible.
     */
    function syncReviewAdvanceSection(category, stage) {
        const section     = document.getElementById('reviewAdvanceSection');
        const closeOnly   = document.getElementById('reviewCloseActions');
        const shouldShow  = category === 'new' && (stage || '').toLowerCase() === 'application';
        if (section)   section.hidden   = !shouldShow;
        if (closeOnly) closeOnly.hidden =  shouldShow;

        // Reset the button in case a prior call already sent it to inspection
        const btn = document.getElementById('reviewSendToInspectionButton');
        if (btn) { btn.disabled = false; btn.textContent = 'Send to inspection'; }
    }

    // Wire the Send to Inspection button once on page load
    document.getElementById('reviewSendToInspectionButton')
        ?.addEventListener('click', sendToInspection);

    async function sendToInspection() {
        if (!state.selected) return;

        const btn = document.getElementById('reviewSendToInspectionButton');
        btn.disabled = true;
        setReviewAdvanceMessage('Advancing to inspection...');

        try {
            const response = await fetch('/Admin/AdvanceStage', {
                method:  'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body:    JSON.stringify({
                    applicationId:        Number(state.selected.requestId),
                    expectedCurrentStage: 'application'
                })
            });

            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const result = await response.json();

            // Update the stage label in the modal so it reflects the change
            textValue('reviewStage', result.newStage);

            // Hide the advance section — the action is done
            syncReviewAdvanceSection('done', result.newStage);

            setReviewAdvanceMessage(
                'Application sent to inspection successfully. It will now appear in the Inspection queue.',
                true
            );

            // Also update the applications list row so the stage badge refreshes
            // without needing to close and reopen the list
            if (state.selected) state.selected.stage = result.newStage;

        } catch (error) {
            btn.disabled = false;
            setReviewAdvanceMessage(error.message || 'Could not advance the application. Please try again.');
        }
    }

    function setReviewAdvanceMessage(message, success) {
        const box = document.getElementById('reviewAdvanceMessage');
        if (!box) return;
        box.textContent = message;
        box.classList.toggle('is-success', !!success);
    }

    function showUnsupportedWorkspace(application) {
        window.alert('No workspace is configured for ' + (application?.serviceName || 'this service') + '.');
    }

    /* ============================================================
       SERVICE REQUEST MODAL  (from summary table row click)
       ============================================================ */

    let serviceRequests = [];   // current loaded rows
    let serviceRequestViewStatus = 'Pending';
    let serviceRequestPage = 1;
    const serviceRequestPageSize = 10;
    function resetServiceRequestPage() { serviceRequestPage = 1; renderServiceRequests(); }

    async function openServiceRequestModal(description, status = 'Pending') {
        if (!description && status !== 'Completed') return;
        serviceRequestViewStatus = status;
        serviceRequestPage = 1;
        serviceRequests = [];
        serviceRequestModal.classList.toggle('sr-completed-modal', status === 'Completed');
        serviceRequestModal.querySelector('.dashboard-modal__panel').style.zoom = '';
        ['srDateFilters', 'srDateEndFilters'].forEach(id => { document.getElementById(id).hidden = status !== 'Completed'; });
        ['srDateFrom', 'srDateTo'].forEach(id => { document.getElementById(id).value = ''; });
        document.getElementById('srPagination').hidden = true;
        const list = document.getElementById('serviceRequestList');
        const titleEl = document.getElementById('serviceRequestModalTitle');
        const searchEl = document.getElementById('serviceRequestSearch');

        if (titleEl) titleEl.textContent = status === 'Completed' ? 'Completed Service Requests' : description + ' — Pending';
        if (searchEl) searchEl.value = '';
        if (list) list.innerHTML = '<div class="dashboard-modal__state">Loading...</div>';

        openModal(serviceRequestModal);

        try {
            const response = await fetch(
                '/Admin/GetServiceRequestsByDescription?description=' + encodeURIComponent(description) + '&status=' + encodeURIComponent(status)
            );
            if (!response.ok) throw new Error(await response.text());
            serviceRequests = await response.json() || [];
            renderServiceRequests();
        } catch (error) {
            if (list) list.innerHTML =
                `<div class="dashboard-modal__state">${html(error.message || 'Unable to load service requests.')}</div>`;
        }
    }

    let selectedServiceRequest = null;
    let accomplishingServiceRequest = false;
    const editableServiceRequestFields = { source: 'Source', reportedBy: 'ReportedBy', contactNumber: 'Contact', name: 'Name', particulars: 'Particulars' };
    document.getElementById('srDetailSave')?.addEventListener('click', () => saveServiceRequestDetail(false));
    document.getElementById('srDetailAccomplish')?.addEventListener('click', () => saveServiceRequestDetail(true));
    document.getElementById('srDetailPdf')?.addEventListener('click', viewServiceRequestPdf);
    document.getElementById('srDetailLedger')?.addEventListener('click', event => {
        event.preventDefault();
        openServiceRequestLedger();
    });

    let serviceRequestPdfUrl = null;
    function viewServiceRequestPdf(event) {
        const request = selectedServiceRequest;
        const Pdf = window.jspdf?.jsPDF || window.jsPDF;
        if (!request || !Pdf) {
            event.preventDefault();
            textValue('srDetailMessage', 'The PDF component is unavailable. Refresh the page and try again.');
            return;
        }
        try {
            const doc = createServiceRequestJobOrderPdf(Pdf, request);
            doc.setProperties({ title: 'Service Request ' + (request.jobOrderNumber || '') });
            if (serviceRequestPdfUrl) URL.revokeObjectURL(serviceRequestPdfUrl);
            serviceRequestPdfUrl = URL.createObjectURL(doc.output('blob'));
            event.currentTarget.href = serviceRequestPdfUrl;
        } catch {
            event.preventDefault();
            textValue('srDetailMessage', 'Unable to create the PDF. Refresh the page and try again.');
        }
    }
    function createServiceRequestJobOrderPdf(Pdf, request) {
        const doc = new Pdf({ orientation: 'portrait', unit: 'pt', format: 'a4' });
        const left = 14, right = 828, middle = 421, top = 28, bottom = 565;
        const pageWidth = doc.internal.pageSize.getWidth();
        const pageHeight = doc.internal.pageSize.getHeight();
        const margin = 18;
        let formScale = Math.min(1, (pageWidth - margin * 2) / (right - left),
            (pageHeight / 2 - margin * 2) / (bottom + 4 - top));
        const formOffsetX = (pageWidth - (right - left) * formScale) / 2 - left * formScale;
        const formOffsetY = margin - top * formScale;
        let offsetX = formOffsetX, offsetY = formOffsetY;
        const x = value => value * formScale + offsetX;
        const y = value => value * formScale + offsetY;
        const line = (x1, y1, x2, y2) => doc.line(x(x1), y(y1), x(x2), y(y2));
        const overflow = [];
        const stamp = value => {
            if (!value) return '';
            const date = new Date(value);
            return Number.isNaN(date.getTime()) ? '' : date.toLocaleString('en-PH', {
                timeZone: 'Asia/Manila', year: 'numeric', month: '2-digit', day: '2-digit',
                hour: '2-digit', minute: '2-digit', hour12: false
            });
        };
        const text = (content, x, y, size = 10, bold = false, align = 'left') => {
            doc.setFont('helvetica', bold ? 'bold' : 'normal');
            doc.setFontSize(size * formScale);
            doc.text(String(content ?? ''), x * formScale + offsetX, y * formScale + offsetY, { align });
        };
        const field = (label, content, x, y, valueX, width, maxLines = 1) => {
            text(label, x, y);
            text(':', valueX - 7, y);
            doc.setFont('helvetica', 'bold');
            doc.setFontSize(10 * formScale);
            const value = String(content ?? '');
            const lines = doc.splitTextToSize(value, width * formScale);
            doc.text(lines.slice(0, maxLines), valueX * formScale + offsetX, y * formScale + offsetY, { lineHeightFactor: 1.15 });
            if (lines.length > maxLines) overflow.push([label, value]);
        };
        doc.setDrawColor(0);
        doc.setTextColor(0);
        doc.setLineWidth(0.9 * formScale);
        doc.rect(x(left), y(top), (right - left) * formScale, (bottom - top) * formScale);
        line(left, 88, right, 88);
        line(middle, 88, middle, 490);
        line(left, 220, middle, 220);
        line(left, 355, middle, 355);
        line(left, 490, right, 490);
        line(middle, 182, right, 182);
        const logo = document.querySelector('.sidebar__logo-img');
        if (logo?.complete && logo.naturalWidth) doc.addImage(logo, 'PNG', x(19), y(33), 48 * formScale, 48 * formScale);
        text('TRECE MARTIRES CITY WATER DISTRICT', 86, 51, 13, true);
        text('Brgy. San Agustin, Trece Martires City, Cavite', 106, 66, 10);
        text('Job Order', right - 6, 46, 13, true, 'right');
        text('Run Date: ' + stamp(new Date()), right - 6, 62, 9, false, 'right');

        field('No.', request.jobOrderNumber, 21, 107, 100, 128);
        field('Received on', stamp(request.jobOrderDate), 236, 107, 308, 106);
        field('Name', request.accountName || request.reportedBy, 21, 132, 100, 310, 2);
        field('Address', request.address, 21, 162, 100, 310, 3);
        field('Acc No.', request.accountNumber || 'Non-customer', 21, 199, 100, 128);
        field('Meter No.', request.meterNumber, 236, 199, 306, 108);
        field('Contact No.', request.contactNumber, 21, 214, 100, 128);
        field('Last Reading', request.lastReading, 236, 214, 306, 108);

        field('Classification', request.jobOrderType || 'Service Request', 428, 107, 520, 298);
        field('Department', request.referredToDepartment || request.referredToName, 428, 123, 520, 298);
        doc.setLineWidth(0.5 * formScale);
        line(428, 130, right - 7, 130);
        line(428, 133, right - 7, 133);
        field('Description', request.jobDescription, 428, 149, 520, 298, 2);
        field('Particulars', request.particulars, 428, 173, 520, 298);

        text('ACCOMPLISHED BY', (left + middle) / 2, 240, 12, true, 'center');
        line(110, 265, 325, 265);
        line(110, 287, 325, 287);
        line(46, 322, 204, 322);
        line(232, 322, 389, 322);
        text('Vehicle Used', 125, 340, 10, true, 'center');
        text('Date & Time Received', 310, 340, 10, true, 'center');
        text(stamp(request.jobOrderDate), 310, 316, 10, false, 'center');

        text('RESULT / CUSTOMER COMMENT', (left + middle) / 2, 376, 12, true, 'center');
        line(110, 403, 325, 403);
        line(110, 425, 325, 425);
        line(46, 456, 204, 456);
        line(232, 456, 389, 456);
        text('Signature of Customer', 125, 474, 10, true, 'center');
        text('Date & Time Accomplished', 310, 474, 10, true, 'center');

        text('Others', 428, 201, 11, true);
        text('Requested by:', 556, 239, 10, true, 'center');
        field('Reported By', request.reportedBy, 435, 259, 520, 280);
        line(435, 276, 680, 276);
        text('Supervisor', 556, 294, 10, false, 'center');
        field('Source', request.source, 435, 330, 520, 298);
        field('Status', request.status, 435, 350, 520, 298);
        field('Referred To', request.referredToName, 435, 370, 520, 298);
        field('Date Referred', stamp(request.dateReferred), 435, 390, 520, 298);
        field('Name', request.name, 435, 410, 520, 298);
        field('Received By', request.receivedByName, 435, 430, 520, 298);
        text('Prepared by:', 110, 510, 10, true);
        text(request.receivedByName || '', 145, 541, 11, true, 'center');
        line(22, 550, 268, 550);
        line(left, bottom + 4, right, bottom + 4);
        if (overflow.length) {
            text('Full details continued on the following page.', right - 8, 553, 9, false, 'right');
            doc.addPage();
            formScale = 1;
            offsetX = 0;
            offsetY = 0;
            text('Job Order ' + (request.jobOrderNumber || '') + ' — Continued details', 28, 36, 13, true);
            doc.autoTable({
                startY: 50, margin: { left: 28, right: 28 }, theme: 'grid',
                head: [['Field', 'Full details']], body: overflow,
                styles: { fontSize: 10, cellPadding: 7, textColor: 0, lineColor: 0, lineWidth: 0.5, overflow: 'linebreak' },
                headStyles: { fillColor: [255, 255, 255], textColor: 0 },
                columnStyles: { 0: { cellWidth: 130, fontStyle: 'bold' } }
            });
        }
        return doc;
    }
    window.addEventListener('pagehide', () => {
        if (serviceRequestPdfUrl) URL.revokeObjectURL(serviceRequestPdfUrl);
    });

    let ledgerLoad = 0;
    async function openServiceRequestLedger() {
        const request = selectedServiceRequest;
        if (!request || !request.accountNumber) return;
        const load = ++ledgerLoad;
        const rows = document.getElementById('srAccountLedgerRows');
        rows.innerHTML = '';
        textValue('srAccountLedgerIdentity', request.accountNumber + ' · ' + (request.accountName || request.reportedBy || ''));
        textValue('srAccountLedgerMessage', 'Loading billing history...');
        textValue('srAccountLedgerBalance', '—');
        openModal(srAccountLedgerModal);
        try {
            const response = await fetch('/Billing/History?accountNumber=' + encodeURIComponent(request.accountNumber), {
                headers: { Accept: 'application/json' }
            });
            if (!response.ok) throw new Error('Unable to load billing history.');
            const history = await response.json();
            if (!Array.isArray(history)) throw new Error('Invalid billing history.');
            if (load !== ledgerLoad || request !== selectedServiceRequest) return;
            let balance = 0;
            rows.innerHTML = history.map(entry => {
                const debit = Number(entry.debit ?? entry.amount) || 0;
                const credit = Number(entry.credit) || 0;
                const outstanding = Math.max(0, debit - credit);
                balance += outstanding;
                return `<tr><td>${html(entry.referenceNo || '—')}<small>${html(dateText(entry.billingDate))}</small></td>` +
                    `<td>${html(entry.previous ?? '—')}</td><td>${html(entry.present ?? '—')}</td><td>${html(entry.usage ?? '—')}</td>` +
                    `<td>₱${money(debit)}</td><td>₱${money(credit)}</td><td>₱${money(outstanding)}</td><td>${html(entry.processedBy || '—')}</td></tr>`;
            }).join('');
            textValue('srAccountLedgerBalance', '₱' + money(balance));
            textValue('srAccountLedgerMessage', history.length ? 'Water billing and payments for this account.' : 'No billing entries found for this account.');
        } catch {
            if (load === ledgerLoad && request === selectedServiceRequest) {
                textValue('srAccountLedgerMessage', 'Unable to load this account’s billing ledger. Close and reopen it to try again.');
            }
        }
        scheduleModalFit();
    }

    async function saveServiceRequestDetail(accomplish) {
        const request = selectedServiceRequest;
        if (!request || accomplishingServiceRequest) return;
        accomplishingServiceRequest = true;
        document.getElementById('srDetailSave').disabled = true;
        document.getElementById('srDetailAccomplish').disabled = true;
        textValue('srDetailMessage', 'Saving...');
        const payload = { requestId: request.requestId };
        for (const [key, suffix] of Object.entries(editableServiceRequestFields)) payload[key] = document.getElementById('srDetail' + suffix).value.trim();
        try {
            const response = await fetch('/Admin/SaveServiceRequest', {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error(await response.text());
            Object.assign(request, await response.json());
            if (accomplish) {
                const completion = await fetch('/Admin/AccomplishServiceRequest?requestId=' + encodeURIComponent(request.requestId), { method: 'POST', headers: { 'RequestVerificationToken': token } });
                if (!completion.ok) throw new Error(await completion.text());
                request.status = (await completion.json()).status;
                const summaryRow = Array.from(document.querySelectorAll('.summary-table__row--clickable'))
                    .find(row => row.dataset.description === request.jobDescription);
                if (summaryRow) {
                    const countCell = summaryRow.cells[1];
                    const remaining = Math.max(0, Number(countCell.textContent) - 1);
                    if (remaining) countCell.textContent = remaining;
                    else summaryRow.remove();
                    const summaryBody = document.getElementById('serviceRequestSummaryRows');
                    if (summaryBody && !summaryBody.children.length)
                        summaryBody.innerHTML = '<tr><td colspan="2" class="summary-empty">No pending service requests.</td></tr>';
                }
            }
            renderServiceRequests();
            if (selectedServiceRequest === request) {
                textValue('srDetailStatus', request.status);
                textValue('srDetailMessage', accomplish ? 'Service request accomplished.' : 'Changes saved.');
            }
        } catch (error) {
            if (selectedServiceRequest === request) textValue('srDetailMessage', error.message || 'Unable to save request.');
        } finally {
            accomplishingServiceRequest = false;
            if (selectedServiceRequest) updateServiceRequestButtons(selectedServiceRequest);
            fitServiceRequestDetail();
        }
    }

    function updateServiceRequestButtons(request) {
        const finished = request.isReadOnly || (request.status || '').toLowerCase() === 'completed';
        document.getElementById('srDetailSave').disabled = finished || accomplishingServiceRequest;
        document.getElementById('srDetailAccomplish').disabled = finished || accomplishingServiceRequest;
        for (const suffix of Object.values(editableServiceRequestFields)) document.getElementById('srDetail' + suffix).disabled = finished || accomplishingServiceRequest;
    }

    function fitServiceRequestDetail() {
        scheduleModalFit();
    }

    function openSrDetail(x) {
        if (!x) return;
        selectedServiceRequest = x;
        const ledgerLink = document.getElementById('srDetailLedger');
        if (ledgerLink) ledgerLink.hidden = !x.accountNumber;
        textValue('srDetailMessage', '');
        textValue('srDetailReceivedBy', x.receivedByName || '—');
        textValue('srDetailReferredTo', x.referredToName || 'Not yet referred');
        textValue('srDetailDateReferred', x.dateReferred ? dateText(x.dateReferred) : '—');
        for (const [key, suffix] of Object.entries(editableServiceRequestFields)) inputValue('srDetail' + suffix, x[key] || '');
        updateServiceRequestButtons(x);
        // Populate header
        textValue('srDetailEyebrow', x.jobOrderType || 'Service Request');
        textValue('srDetailTitle', 'Service Request - Job Order / Maintenance / Complaints');
        // Populate fields
        textValue('srDetailJobNumber',    x.jobOrderNumber);
        textValue('srDetailJobDate',      x.jobOrderDate ? dateText(x.jobOrderDate) : '—');
        textValue('srDetailJobType',      x.jobOrderType);
        textValue('srDetailStatus',       x.status);
        textValue('srDetailAccountNumber', x.accountNumber || 'Non-customer');
        textValue('srDetailAccountName',   x.accountName  || x.reportedBy || '—');
        textValue('srDetailAddress',       x.address      || '—');
        textValue('srDetailDescription',   x.jobDescription || '—');
        openModal(srDetailModal);
        requestAnimationFrame(fitServiceRequestDetail);
    }

    function renderServiceRequests() {
        const list   = document.getElementById('serviceRequestList');
        const search = document.getElementById('serviceRequestSearch');
        if (!list) return;

        const query = (search?.value || '').trim().toLowerCase();
        const rows  = serviceRequests.filter(x =>
            x.status === serviceRequestViewStatus && (!query || [x.jobOrderNumber, x.accountNumber, x.accountName, x.reportedBy, x.jobDescription]
                .some(v => (v || '').toLowerCase().includes(query))
            )
        );

        if (serviceRequestViewStatus === 'Completed') {
            renderCompletedRequests(list, rows);
            return;
        }
        document.getElementById('srPagination').hidden = true;

        if (!rows.length) {
            list.innerHTML = '<div class="dashboard-modal__state">No matching service requests.</div>';
            return;
        }

        list.innerHTML = rows.map((x, i) => `
            <button type="button" class="application-row" data-sr-index="${i}">
                <span>
                    <strong class="application-row__control">${html(x.jobOrderNumber || '—')}</strong>
                    <span class="application-row__account">${html(x.accountNumber || 'Non-customer')}</span>
                </span>
                <span>
                    <strong class="application-row__name">${html(x.accountName || x.reportedBy || '—')}</strong>
                    <span class="application-row__service">${html(x.address || '—')}</span>
                </span>
                <span class="application-row__meta">
                    <span class="application-row__date">${dateText(x.jobOrderDate)}</span>
                    <span class="application-row__stage">${html(x.status)}</span>
                </span>
            </button>`).join('');

        list.querySelectorAll('[data-sr-index]').forEach(btn => {
            btn.addEventListener('click', () => openSrDetail(rows[Number(btn.dataset.srIndex)]));
        });
    }

    function renderCompletedRequests(list, matchingRows) {
        const from = document.getElementById('srDateFrom').value;
        const to = document.getElementById('srDateTo').value;
        const rows = matchingRows.filter(x => {
            const date = (x.jobOrderDate || '').slice(0, 10);
            return (!from || date >= from) && (!to || date <= to);
        }).sort((a, b) => (b.jobOrderDate || '').localeCompare(a.jobOrderDate || '') || b.requestId - a.requestId);
        const pages = Math.max(1, Math.ceil(rows.length / serviceRequestPageSize));
        serviceRequestPage = Math.min(serviceRequestPage, pages);
        const offset = (serviceRequestPage - 1) * serviceRequestPageSize;
        const visible = rows.slice(offset, offset + serviceRequestPageSize);
        document.getElementById('srPagination').hidden = false;
        document.getElementById('srResultCount').textContent = rows.length ? 'Showing ' + (offset + 1) + '\u2013' + (offset + visible.length) + ' of ' + rows.length + ' requests' : '0 requests';
        document.getElementById('srPageLabel').textContent = 'Page ' + serviceRequestPage + ' of ' + pages;
        document.getElementById('srPrevious').disabled = serviceRequestPage === 1;
        document.getElementById('srNext').disabled = serviceRequestPage === pages;
        list.innerHTML = '<table class="sr-completed-table" aria-label="Completed service requests"><thead><tr><th scope="col">Job Order</th><th scope="col">Account</th><th scope="col">Customer / Address</th><th scope="col">Job-order Date</th><th scope="col">Details</th></tr></thead><tbody>' + (visible.length ? visible.map((x, i) =>
            '<tr><td><strong>' + html(x.jobOrderNumber || '?') + '</strong></td><td>' + html(x.accountNumber || 'Non-customer') + '</td><td><strong>' + html(x.accountName || x.reportedBy || '?') + '</strong><span class="sr-completed-address">' + html(x.address || '?') + '</span></td><td>' + dateText(x.jobOrderDate) + '</td><td><button type="button" class="inspection-secondary-button" data-sr-index="' + i + '" aria-label="View details for ' + html(x.jobOrderNumber || 'request') + '">View Details</button></td></tr>'
        ).join('') : '<tr><td colspan="5" class="dashboard-modal__state">No matching service requests.</td></tr>') + '</tbody></table>';
        list.scrollTop = 0;
        list.querySelectorAll('[data-sr-index]').forEach(button => button.addEventListener('click', () => openSrDetail(visible[Number(button.dataset.srIndex)])));
    }

    function statusColor(status) {
        switch ((status || '').toLowerCase()) {
            case 'closed':     return 'rgba(48,209,88,.12)';
            case 'open':       return 'rgba(61,107,250,.12)';
            case 'in progress':return 'rgba(255,214,10,.12)';
            default:           return 'rgba(143,154,181,.12)';
        }
    }    /* ============================================================
       INSPECTION MODAL
       ============================================================ */
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
        inputValue('inspectionStatus', data.status || 'Approved');
        document.getElementById('inspectionBoring').checked = !!data.needsUndergroundBoring;
        document.getElementById('inspectionChargeNsc').checked = data.chargeNscFee !== false;
        fillOptions('inspectionInspector', data.inspectors, 'Select inspector', data.inspectedByUserId);
        fillOptions('inspectionApprover', data.approvers, 'Select approver', data.approvedByUserId);

        // Zone and Book come from the original application — display as read-only
        const appZone = data.applicationZone || data.zone || null;
        const appBook = data.applicationBook || data.book || null;
        const zoneEl = document.getElementById('inspectionZoneDisplay');
        const bookEl = document.getElementById('inspectionBookDisplay');
        if (zoneEl) zoneEl.textContent = appZone ? `Zone ${appZone}` : '—';
        if (bookEl) bookEl.textContent = appBook ? `Book ${appBook}` : '—';

        // Pre-fill meter size from the application's service size (or already-saved value)
        const meterSizeEl = document.getElementById('inspectionMeterSize');
        if (meterSizeEl && (data.meterSize || 0) > 0)
            meterSizeEl.value = String(data.meterSize);

        // Pre-fill meter number if already saved
        inputValue('inspectionMeterNumber', data.meterNumber);

        syncInspectionActivateSection(data);
    }

    function inspectionPayload() {
        return {
            reportId: Number(state.inspection?.reportId || 0),
            requestId: Number(state.selected?.requestId || 0),
            waterServiceCondition: value('inspectionWaterCondition'),
            classOfPavement: value('inspectionPavement'),
            pipeSize: value('inspectionPipeSize'),
            typeOfConnection: value('inspectionConnectionType'),
            distanceFromMainline: value('inspectionDistance'),
            needsUndergroundBoring: document.getElementById('inspectionBoring').checked,
            // Zone and book are read-only — send back the application's own values
            zone: state.inspection?.applicationZone || state.inspection?.zone || null,
            book: state.inspection?.applicationBook || state.inspection?.book || null,
            applicationZone: state.inspection?.applicationZone || null,
            applicationBook: state.inspection?.applicationBook || null,
            nearestAccountNumber: value('inspectionNearestNumber'),
            nearestAccountName: value('inspectionNearestName'),
            inspectionRemarks: value('inspectionRemarks'),
            inspectedByUserId: nullableNumber('inspectionInspector'),
            approvedByUserId: nullableNumber('inspectionApprover'),
            dateInspected: value('inspectionDate') || null,
            status: value('inspectionStatus'),
            chargeNscFee: document.getElementById('inspectionChargeNsc').checked,
            meterNumber: value('inspectionMeterNumber'),
            meterSize: parseFloat(document.getElementById('inspectionMeterSize')?.value || '0.5') || 0.5
        };
    }

    async function saveInspection() {
        const payload = inspectionPayload();
        if (!payload.inspectedByUserId || !payload.dateInspected)
            return setInspectionMessage('Select the inspector and inspection date.');
        const button = document.getElementById('inspectionSaveButton');
        button.disabled = true;
        setInspectionMessage('Saving inspection...');
        try {
            const response = await fetch('/Admin/SaveServiceInspection', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved   = await response.json();
            const context = state.inspection;
            state.inspection = {
                ...context, ...saved,
                applicationNumber: context.applicationNumber, applicantName: context.applicantName,
                dateApplied: context.dateApplied, address: context.address,
                mobileNumber: context.mobileNumber, classification: context.classification,
                serviceName: context.serviceName, inspectors: context.inspectors,
                approvers: context.approvers, zones: context.zones,
                applicationZone: context.applicationZone, applicationBook: context.applicationBook
            };

            const isDisapproved = (saved.status || '').toLowerCase() === 'disapproved';
            if (isDisapproved) {
                // Hide activation section — application is rejected
                const section = document.getElementById('inspectionActivateSection');
                if (section) section.hidden = true;
                setInspectionMessage(
                    'Application disapproved. This application will not proceed further.',
                    false
                );
            } else {
                const isApproved = (saved.status || '').toLowerCase() === 'approved'
                    && !!saved.approvedByUserId;
                const bypassedCharging = isApproved && saved.chargeNscFee === false;
                const msg = bypassedCharging
                    ? saved.accountNumber
                        ? `Inspection approved — account number ${saved.accountNumber} generated and moved directly to Installation. No service connection fee was charged.`
                        : 'Inspection approved and moved directly to Installation. No service connection fee was charged.'
                    : saved.accountNumber
                        ? `Inspection approved — account number ${saved.accountNumber} generated. The PDF is ready.`
                        : 'Inspection saved successfully. The PDF report is ready.';
                setInspectionMessage(msg, true);
                syncInspectionActivateSection(state.inspection);
            }
        } catch (error) {
            setInspectionMessage(error.message || 'The inspection could not be saved.');
        } finally {
            button.disabled = false;
        }
    }

    /* ============================================================
       CONNECT ORDER MODAL
       ============================================================ */
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
        button.disabled = true;
        setConnectMessage('Saving connect order...');
        try {
            const response = await fetch('/Admin/SaveConnectOrder', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved   = await response.json();
            const context = state.connectOrder;
            state.connectOrder = {
                ...context, ...saved,
                applicationNumber: context.applicationNumber, applicantName: context.applicantName,
                dateApplied: context.dateApplied, address: context.address,
                classification: context.classification, processedByName: context.processedByName,
                inspectedByName: context.inspectedByName,
                inspectionApprovedByName: context.inspectionApprovedByName,
                invoiceNumber: context.invoiceNumber, invoiceDate: context.invoiceDate,
                recommenders: context.recommenders, approvers: context.approvers
            };
            setConnectMessage('Connect order saved successfully.', true);
        } catch (error) {
            setConnectMessage(error.message || 'The connect order could not be saved.');
        } finally {
            button.disabled = false;
        }
    }

    /* ============================================================
       ACTIVATE ACCOUNT (from Inspection modal — on Approval)
       ============================================================ */

    /**
     * Show / hide the inspection activation panel.
     * Visible when: status = Approved AND approver is set AND zone/book filled.
     * Shows the generated account number if already activated.
     */
    function syncInspectionActivateSection(data) {
        const section = document.getElementById('inspectionActivateSection');
        if (!section) return;

        const isApproved = (data.status || '').toLowerCase() === 'approved'
                        && !!(data.approvedByUserId);
        const isDisapproved = (data.status || '').toLowerCase() === 'disapproved';
        const alreadyDone = !!(data.accountNumber);

        // Hide entirely if disapproved or not yet approved
        section.hidden = !isApproved || isDisapproved;

        const resultEl = document.getElementById('inspectionActivateResult');
        const numEl    = document.getElementById('inspectionActivatedAccountNumber');
        const btn      = document.getElementById('inspectionActivateButton');

        if (alreadyDone) {
            if (resultEl) resultEl.hidden = false;
            if (numEl)    numEl.textContent = data.accountNumber;
            if (btn)      { btn.disabled = true; btn.textContent = 'Account generated'; }
            setInspectionActivateMessage('Account number: ' + data.accountNumber, true);
        } else {
            if (resultEl) resultEl.hidden = true;
            if (numEl)    numEl.textContent = '—';
            if (btn)      { btn.disabled = false; btn.textContent = 'Generate account number'; }
            setInspectionActivateMessage('');
        }
    }

    async function activateAccountFromInspection() {
        if (!state.selected) return;
        const btn = document.getElementById('inspectionActivateButton');
        btn.disabled = true;
        setInspectionActivateMessage('Generating account number...');

        // Re-save the inspection with the current meter fields included —
        // the server creates the account and returns the account number in one call.
        const payload = inspectionPayload();
        if (!payload.inspectedByUserId || !payload.dateInspected)
            return setInspectionActivateMessage('Save the inspection with an inspector and date first.');
        if (!payload.zone || !payload.book)
            return setInspectionActivateMessage('Select Zone and Book before generating the account number.');

        try {
            const response = await fetch('/Admin/SaveServiceInspection', {
                method:  'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body:    JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved = await response.json();

            if (!saved.accountNumber)
                throw new Error('Zone and Book must be set before generating the account number.');

            // Merge the new account number into state
            const context = state.inspection;
            state.inspection = {
                ...context, ...saved,
                applicationNumber: context.applicationNumber, applicantName: context.applicantName,
                dateApplied: context.dateApplied, address: context.address,
                mobileNumber: context.mobileNumber, classification: context.classification,
                serviceName: context.serviceName, inspectors: context.inspectors,
                approvers: context.approvers, zones: context.zones
            };

            // Show the badge
            const resultEl = document.getElementById('inspectionActivateResult');
            const numEl    = document.getElementById('inspectionActivatedAccountNumber');
            if (resultEl) resultEl.hidden = false;
            if (numEl)    numEl.textContent = saved.accountNumber;
            btn.textContent = 'Account generated';
            setInspectionActivateMessage('Account number ' + saved.accountNumber + ' created. Print the PDF to get the report.', true);

        } catch (error) {
            btn.disabled = false;
            setInspectionActivateMessage(error.message || 'The account could not be created.');
        }
    }

    function setInspectionActivateMessage(message, success) {
        const box = document.getElementById('inspectionActivateMessage');
        if (!box) return;
        box.textContent = message;
        box.classList.toggle('is-success', !!success);
    }

    /* ============================================================
       INSTALLATION SERVICE REQUEST / JOB ORDER
       ============================================================ */
    async function openInstallationServiceRequest(application) {
        state.selected = application;
        state.installationRequestSaved = false;

        const form = document.getElementById('installationRequestForm');
        form.reset();
        inputValue('installSrApplicationId', application.requestId);
        inputValue('installSrAccountId', application.accountId);
        inputValue('installSrAccountNumber', application.accountNumber);
        inputValue('installSrAccountName', application.accountName);
        inputValue('installSrAddress', application.address);
        inputValue('installSrReportedBy', '');
        inputValue('installSrContact', application.contactNumber);
        inputValue('installSrJobNumber', 'Generated after saving');
        inputValue('installSrJobDate', dateInput(new Date()));
        inputValue('installSrSource', installationSource(application.source));
        inputValue('installSrType', 'Maintenance');
        inputValue('installSrDescription', 'NEW CONNECTION INSTALLATION');
        inputValue('installSrReferredTo', 'Maintenance');
        inputValue('installSrDateReferred', dateInput(new Date()));
        document.getElementById('installSrDateReferred').disabled = false;
        document.getElementById('installSrResult').hidden = true;
        const submit = document.getElementById('installSrSubmit');
        submit.disabled = false;
        submit.textContent = 'Save';
        textValue('installSrReceivedBy', document.querySelector('.topbar__user-name')?.textContent?.trim() || 'Current user');
        setInstallationRequestMessage('');
        openModal(installationRequestModal);
    }

    function installationSource(source) {
        const normalized = String(source || '').toLowerCase().replace(/[_\s-]/g, '');
        if (normalized === 'online') return 'Other';
        if (normalized === 'phone') return 'Phone';
        if (normalized === 'referral') return 'Other';
        return 'Walk-In';
    }

    async function saveInstallationServiceRequest(event) {
        event.preventDefault();
        if (state.installationRequestSaved) return;
        const form = event.currentTarget;
        if (!form.reportValidity()) return;

        const referredToDepartment = value('installSrReferredTo');
        const payload = {
            applicationId: Number(value('installSrApplicationId')),
            isNonCustomer: false,
            accountId: Number(value('installSrAccountId')),
            address: value('installSrAddress'),
            reportedBy: value('installSrReportedBy'),
            contactNumber: value('installSrContact'),
            jobOrderDate: value('installSrJobDate'),
            source: value('installSrSource'),
            jobOrderType: value('installSrType'),
            jobDescription: value('installSrDescription'),
            referredToDepartment,
            dateReferred: referredToDepartment ? value('installSrDateReferred') : null,
            name: value('installSrName'),
            particulars: value('installSrParticulars')
        };
        const submit = document.getElementById('installSrSubmit');
        submit.disabled = true;
        setInstallationRequestMessage('Saving installation job order...');
        try {
            const response = await fetch('/Admin/SaveInstallationServiceRequest', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved = await response.json();
            inputValue('installSrJobNumber', saved.jobOrderNumber);
            document.getElementById('installSrResultNumber').textContent = saved.jobOrderNumber;
            document.getElementById('installSrResultStatus').textContent = `${saved.status} · Received by ${saved.receivedByName}`;
            document.getElementById('installSrResult').hidden = false;
            state.installationRequestSaved = true;
            submit.textContent = 'Saved';
            setInstallationRequestMessage('The installation job order was saved and sent to the Connect queue.', true);
            state.applications = state.applications.filter(x => x.requestId !== payload.applicationId);
            renderApplications();
        } catch (error) {
            submit.disabled = false;
            setInstallationRequestMessage(error.message || 'The installation job order could not be saved.');
        }
    }

    /* ============================================================
       HOUSE TAPPING
       ============================================================ */
    async function openHouseTapping(application) {
        state.selected = application;
        state.tapping = null;
        setTappingMessage('Loading house tapping details...');
        openModal(houseTappingModal);
        try {
            const response = await fetch('/Admin/GetHouseTapping?requestId=' + encodeURIComponent(application.requestId));
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            state.tapping = await response.json();
            renderHouseTapping(state.tapping);
            setTappingMessage('');
        } catch (error) {
            setTappingMessage(error.message || 'Unable to load the house tapping record.');
        }
    }

    function renderHouseTapping(data) {
        inputValue('tappingApplicationNumber', data.applicationNumber);
        inputValue('tappingApplicantName', data.applicantName);
        inputValue('tappingDateApplied', dateInput(data.dateApplied));
        inputValue('tappingAddress', data.address);
        inputValue('tappingClassification', data.classification);
        inputValue('tappingInspectedBy', data.inspectedByName);
        inputValue('tappingDateInspected', dateInput(data.dateInspected));

        const zones = [...new Set((data.zoneBooks || []).map(x => Number(x.zone)))].filter(Boolean).sort((a, b) => a - b);
        document.getElementById('tappingZone').innerHTML = '<option value="">Select zone</option>' +
            zones.map(zone => `<option value="${zone}">Zone ${zone}</option>`).join('');
        inputValue('tappingZone', data.zone);
        fillTappingBooks(data.book);

        document.getElementById('tappingMeterBrand').innerHTML = '<option value="">Select meter brand</option>' +
            (data.meterBrands || []).map(brand => `<option value="${html(brand)}">${html(brand)}</option>`).join('');
        inputValue('tappingMeterBrand', data.meterBrand);
        inputValue('tappingSerialNumber', data.serialNumber);
        inputValue('tappingMeterSize', data.meterSize || 0.5);
        inputValue('tappingInitialReading', Number(data.initialReading || 0));
        fillOptions('tappingAccomplishedBy', data.personnel, 'Select personnel', data.accomplishedByUserId);
        inputValue('tappingDateAccomplished', dateTimeInput(data.dateAccomplished));
        inputValue('tappingRemarks', data.remarks);
    }

    function fillTappingBooks(selectedBook) {
        const zone = Number(value('tappingZone'));
        const books = [...new Set((state.tapping?.zoneBooks || [])
            .filter(x => Number(x.zone) === zone).map(x => Number(x.book)))]
            .filter(Boolean).sort((a, b) => a - b);
        document.getElementById('tappingBook').innerHTML = '<option value="">Select book</option>' +
            books.map(book => `<option value="${book}">Book ${book}</option>`).join('');
        inputValue('tappingBook', selectedBook);
    }

    function houseTappingPayload() {
        return {
            requestId: Number(state.selected?.requestId || 0),
            applicationNumber: value('tappingApplicationNumber'),
            applicantName: value('tappingApplicantName'),
            dateApplied: state.tapping?.dateApplied || null,
            address: value('tappingAddress'),
            classification: value('tappingClassification'),
            inspectedByName: value('tappingInspectedBy'),
            dateInspected: state.tapping?.dateInspected || null,
            zone: nullableNumber('tappingZone'),
            book: nullableNumber('tappingBook'),
            meterBrand: value('tappingMeterBrand'),
            meterSize: Number(value('tappingMeterSize')),
            serialNumber: value('tappingSerialNumber'),
            initialReading: Number(value('tappingInitialReading')),
            accomplishedByUserId: nullableNumber('tappingAccomplishedBy'),
            dateAccomplished: value('tappingDateAccomplished') || null,
            remarks: value('tappingRemarks')
        };
    }

    async function saveHouseTapping() {
        if (!state.tapping) return setTappingMessage('Load the house tapping record first.');
        const payload = houseTappingPayload();
        if (!payload.zone || !payload.book || !payload.meterBrand || !payload.serialNumber ||
            !payload.meterSize || payload.initialReading < 0 || !payload.accomplishedByUserId || !payload.dateAccomplished)
            return setTappingMessage('Complete the zone, book, meter, reading, and accomplishment details.');

        const button = document.getElementById('tappingSaveButton');
        button.disabled = true;
        setTappingMessage('Saving house tapping record...');
        try {
            const response = await fetch('/Admin/SaveHouseTapping', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved = await response.json();
            state.tapping = { ...state.tapping, ...saved };
            setTappingMessage('House tapping saved. The account meter details are now active.', true);
            state.applications = state.applications.filter(x => x.requestId !== payload.requestId);
            renderApplications();
        } catch (error) {
            setTappingMessage(error.message || 'The house tapping record could not be saved.');
        } finally {
            button.disabled = false;
        }
    }

    /* ============================================================
       LEGACY WORK ORDER MODAL
       ============================================================ */
    const WORK_ORDER_LABELS = {
        installation: { eyebrow: 'Installation workflow', title: 'Installation Order' },
        tapping:      { eyebrow: 'Tapping workflow',      title: 'Tapping Order'      }
    };

    async function openWorkOrderWorkspace(application) {
        state.selected = application;
        state.workOrder = null;
        state.workOrderMaterials = [];

        // Update header labels to reflect the category
        const labels = WORK_ORDER_LABELS[state.category] || { eyebrow: 'Work order', title: 'Work Order' };
        textValue('workOrderEyebrow', labels.eyebrow);
        textValue('workOrderTitle',   labels.title);

        // Pre-fill identity fields immediately so the modal feels responsive
        textValue('workOrderApplicationNumber', application.controlNumber);
        textValue('workOrderApplicantName',     application.accountName);
        textValue('workOrderAccountNumber',     application.accountNumber);
        textValue('workOrderAddress',           '');
        textValue('workOrderServiceName',       application.serviceName);

        // Reset job-order badge until data loads
        const badge = document.getElementById('workOrderJobBadge');
        badge.hidden = true;
        document.getElementById('workOrderJobNumber').textContent = '—';

        setWorkOrderMessage('');
        renderWorkOrderMaterials();
        openModal(workOrderModal);

        try {
            const response = await fetch(
                `/Admin/GetWorkOrder?type=${encodeURIComponent(state.category)}&requestId=${encodeURIComponent(application.requestId)}`
            );
            if (!response.ok) throw new Error(await response.text());
            state.workOrder = await response.json();
            state.workOrderMaterials = (state.workOrder.materials || []).map(m => ({ ...m }));
            renderWorkOrder(state.workOrder);
        } catch (error) {
            state.workOrder = null;
            setWorkOrderMessage(error.message || 'Unable to load the work order record.');
        }
    }

    function renderWorkOrder(data) {
        textValue('workOrderApplicationNumber', data.applicationNumber);
        textValue('workOrderApplicantName',     data.applicantName);
        textValue('workOrderAccountNumber',     data.accountNumber);
        textValue('workOrderAddress',           data.address || 'No address recorded');
        textValue('workOrderServiceName',       data.serviceName);

        // Show the job order number badge when present
        const badge = document.getElementById('workOrderJobBadge');
        if (data.jobOrderNumber) {
            document.getElementById('workOrderJobNumber').textContent = data.jobOrderNumber;
            badge.hidden = false;
        } else {
            badge.hidden = true;
        }

        fillOptions('workOrderAssignedTo', data.personnel, 'Select personnel', data.assignedToUserId);
        inputValue('workOrderStatus',        data.status || 'Scheduled');
        inputValue('workOrderScheduledDate', dateInput(data.scheduledDate));
        inputValue('workOrderCompletionDate', dateInput(data.completionDate));
        inputValue('workOrderMeterNumber',   data.meterNumber);
        inputValue('workOrderPipeSize',      data.pipeSize);
        inputValue('workOrderLocation',      data.workLocation);
        inputValue('workOrderNotes',         data.workNotes);

        state.workOrderMaterials = (data.materials || []).map(m => ({ ...m }));
        renderWorkOrderMaterials();
    }

    function addWorkOrderMaterial() {
        const desc = document.getElementById('workOrderMaterialDesc').value.trim();
        const qty  = parseInt(document.getElementById('workOrderMaterialQty').value, 10);
        if (!desc)          return setWorkOrderMessage('Enter a material description.');
        if (!qty || qty < 1) return setWorkOrderMessage('Enter a quantity of at least 1.');
        state.workOrderMaterials.push({ description: desc, quantity: qty });
        document.getElementById('workOrderMaterialDesc').value = '';
        document.getElementById('workOrderMaterialQty').value  = '';
        setWorkOrderMessage('');
        renderWorkOrderMaterials();
        document.getElementById('workOrderMaterialDesc').focus();
    }

    function renderWorkOrderMaterials() {
        const body = document.getElementById('workOrderMaterialRows');
        if (!body) return;
        if (!state.workOrderMaterials.length) {
            body.innerHTML = '<tr><td colspan="2" class="dashboard-modal__state">No materials added yet.</td></tr>';
            return;
        }
        body.innerHTML = state.workOrderMaterials.map((m, i) => `
            <tr>
                <td>${html(m.description)}</td>
                <td class="work-order-material-qty">
                    ${html(String(m.quantity))}
                    <button type="button" class="work-order-material-remove" data-material-index="${i}"
                        aria-label="Remove ${html(m.description)}">&times;</button>
                </td>
            </tr>`).join('');

        body.querySelectorAll('[data-material-index]').forEach(btn =>
            btn.addEventListener('click', () => {
                state.workOrderMaterials.splice(Number(btn.dataset.materialIndex), 1);
                renderWorkOrderMaterials();
            })
        );
    }

    function workOrderPayload() {
        return {
            findingId:         Number(state.workOrder?.findingId  || 0),
            requestId:         Number(state.selected?.requestId   || 0),
            jobOrderId:        Number(state.workOrder?.jobOrderId || 0),
            workOrderType:     state.category,
            assignedToUserId:  nullableNumber('workOrderAssignedTo'),
            status:            value('workOrderStatus') || 'Scheduled',
            scheduledDate:     value('workOrderScheduledDate')   || null,
            completionDate:    value('workOrderCompletionDate')  || null,
            meterNumber:       value('workOrderMeterNumber'),
            pipeSize:          value('workOrderPipeSize'),
            workLocation:      value('workOrderLocation'),
            workNotes:         value('workOrderNotes'),
            materials:         state.workOrderMaterials
        };
    }

    async function saveWorkOrder() {
        const payload = workOrderPayload();
        if (!payload.assignedToUserId || !payload.scheduledDate)
            return setWorkOrderMessage('Select the assigned personnel and a scheduled date.');
        const button = document.getElementById('workOrderSaveButton');
        button.disabled = true;
        setWorkOrderMessage('Saving work order...');
        try {
            const response = await fetch('/Admin/SaveWorkOrder', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved   = await response.json();
            const context = state.workOrder;
            state.workOrder = {
                ...context, ...saved,
                applicationNumber: context?.applicationNumber, applicantName: context?.applicantName,
                accountNumber: context?.accountNumber, address: context?.address,
                serviceName: context?.serviceName, jobOrderNumber: context?.jobOrderNumber,
                personnel: context?.personnel
            };
            // Reflect any server-updated materials
            state.workOrderMaterials = (saved.materials || state.workOrderMaterials).map(m => ({ ...m }));
            renderWorkOrderMaterials();
            setWorkOrderMessage('Work order saved successfully.', true);
        } catch (error) {
            setWorkOrderMessage(error.message || 'The work order could not be saved.');
        } finally {
            button.disabled = false;
        }
    }

    /* ============================================================
       CHARGE MODAL  (Charging, Payment, New, Online Applications, Tickets)
       ============================================================ */
    async function openChargeWorkspace(application) {
        state.selected = application;
        document.getElementById('dashboardAccountNumber').textContent = application.accountNumber || '—';
        document.getElementById('dashboardAccountName').textContent   = application.accountName   || '—';
        document.getElementById('dashboardServiceName').textContent   = application.serviceName   || '—';
        document.getElementById('dashboardParticulars').value = '';   // blank — user fills in
        document.getElementById('dashboardPayableIn').value  = '';   // blank — optional

        // Update the "Full page" link to pre-load this account
        const fullPageLink = document.getElementById('chargeModalFullPageLink');
        if (fullPageLink && application.accountNumber)
            fullPageLink.href = '/Billing/OtherCharges?accountNumber=' + encodeURIComponent(application.accountNumber);
        setMessage('');
        openModal(chargeModal);
        document.getElementById('dashboardChargeRows').innerHTML =
            '<tr><td colspan="4" class="dashboard-modal__state">Loading charges...</td></tr>';
        try {
            const [typesResponse, chargesResponse] = await Promise.all([
                state.chargeTypesLoaded ? Promise.resolve(null) : fetch('/Billing/GetChargeTypes'),
                fetch('/Billing/GetOtherChargesByAccount?accountNumber=' + encodeURIComponent(application.accountNumber))
            ]);
            if (typesResponse) {
                if (!typesResponse.ok) throw new Error('Could not load charge types.');
                const types  = await typesResponse.json() || [];
                const select = document.getElementById('dashboardChargeType');
                select.innerHTML = '<option value="">Select a charge type</option>' +
                    types.map(x => `<option value="${Number(x.value)}" data-default-amount="${Number(x.defaultAmount || 0)}">${html(x.label)}</option>`).join('');
                state.chargeTypesLoaded = true;
                // Make the charge type dropdown searchable
                if (window.makeSearchableSelect) {
                    window.makeSearchableSelect(select, { placeholder: 'Search charge type...' });
                } else {
                    // Already wrapped — force a re-sync of options
                    const evt = new Event('_ss_rebuild');
                    select.dispatchEvent(evt);
                }
            }
            if (!chargesResponse.ok && chargesResponse.status !== 404) throw new Error(await chargesResponse.text());
            state.charges = chargesResponse.ok ? await chargesResponse.json() || [] : [];
            renderCharges();
        } catch (error) {
            setMessage(error.message || 'Unable to load account charges.');
            state.charges = [];
            renderCharges();
        }
    }

    async function saveCharge() {
        if (!state.selected) return;
        const type       = Number(document.getElementById('dashboardChargeType').value);
        const payableIn  = document.getElementById('dashboardPayableIn').value;
        const particulars = document.getElementById('dashboardParticulars').value.trim();
        const amount     = Number(document.getElementById('dashboardAmount').value);
        if (!type || !Number.isFinite(amount) || amount <= 0)
            return setMessage('Select a charge type and enter an amount greater than zero.');
        const button = document.getElementById('dashboardAddCharge');
        button.disabled = true;
        setMessage('');
        try {
            const response = await fetch('/Billing/SaveOtherCharge', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({ accountNumber: state.selected.accountNumber, chargeType: type, payableIn, particulars, amount })
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            state.charges.push(await response.json());
            // Clear entry fields for next charge
            document.getElementById('dashboardChargeType').value = '';
            document.getElementById('dashboardAmount').value     = '';
            document.getElementById('dashboardParticulars').value = '';
            document.getElementById('dashboardPayableIn').value  = '';
            // Reset the searchable select label
            const ssInput = document.querySelector('#dashboardChargeType')?.closest('.ss-wrapper')?.querySelector('.ss-input');
            if (ssInput) ssInput.value = '';
            setMessage('Charge added successfully.', true);
            renderCharges();
        } catch (error) {
            setMessage(error.message || 'The charge could not be saved.');
        } finally {
            button.disabled = false;
        }
    }

    function renderCharges() {
        const body = document.getElementById('dashboardChargeRows');
        body.innerHTML = state.charges.length
            ? state.charges.map((x, i) =>
                `<tr>
                    <td>${html(x.description)}</td>
                    <td>${html(x.payableIn || '—')}</td>
                    <td>${html(x.particulars || '—')}</td>
                    <td>₱${money(x.amount)}</td>
                    <td>
                        <span class="charge-status">${x.isPosted ? 'Posted' : 'Draft'}</span>
                        ${x.isPosted ? '' : `<button type="button" class="charge-delete-btn" data-charge-id="${x.id}" data-charge-index="${i}"
                            aria-label="Delete ${html(x.description)}">
                            <svg viewBox="0 0 20 20" fill="none" aria-hidden="true">
                                <path d="M6 4h8M4 7h12M7 7v9M10 7v9M13 7v9M8 4V3h4v1" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
                            </svg>
                        </button>`}
                    </td>
                </tr>`
              ).join('')
            : '<tr><td colspan="5" class="dashboard-modal__state">No active charges for this account.</td></tr>';

        // Wire trash buttons
        body.querySelectorAll('.charge-delete-btn').forEach(btn => {
            btn.addEventListener('click', () => deleteCharge(Number(btn.dataset.chargeId), Number(btn.dataset.chargeIndex)));
        });

        document.getElementById('dashboardChargeTotal').textContent =
            '₱' + money(state.charges.reduce((sum, x) => sum + Number(x.amount || 0), 0));
        const postButton = document.getElementById('dashboardPostCharges');
        if (postButton) {
            const hasDrafts = state.charges.some(x => !x.isPosted);
            const canReconcile = (state.selected?.stage || '').toLowerCase() === 'charging'
                && state.charges.some(x => x.isPosted
                    && ['unpaid', 'overdue'].includes((x.status || '').toLowerCase()));
            postButton.disabled = !hasDrafts && !canReconcile;
            postButton.textContent = hasDrafts ? 'Post to Collection'
                : canReconcile ? 'Continue to Payment' : 'Post to Collection';
        }
    }

    async function postCharges() {
        const drafts = state.charges.filter(charge => !charge.isPosted);
        const posted = state.charges.filter(charge => charge.isPosted
            && ['unpaid', 'overdue'].includes((charge.status || '').toLowerCase()));
        const chargesToPost = drafts.length ? drafts : posted;
        const button = document.getElementById('dashboardPostCharges');
        if (!state.selected || !chargesToPost.length || button.disabled) return;
        const confirmation = drafts.length
            ? `Post ${drafts.length} charge(s) to Collection? Posted charges can no longer be edited.`
            : 'Continue this application to Payment? The charge is already posted in Collection.';
        if (!confirm(confirmation)) return;

        button.disabled = true;
        setMessage('Posting charges...');
        try {
            const response = await fetch('/Billing/PostOtherCharges', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({ otherChargeIds: chargesToPost.map(charge => Number(charge.id)) })
            });
            const text = await response.text();
            let result = {};
            try { result = text ? JSON.parse(text) : {}; } catch { result = { message: text }; }
            if (!response.ok) throw new Error(result.message || 'The charges could not be posted.');

            const postedIds = new Set(chargesToPost.map(charge => Number(charge.id)));
            state.charges.forEach(charge => {
                if (postedIds.has(Number(charge.id))) {
                    charge.isPosted = true;
                    charge.status = 'Unpaid';
                }
            });
            state.selected.stage = 'payment';
            setMessage(drafts.length
                ? `${result.postedCount || drafts.length} charge(s) posted to Collection. Application moved to Payment.`
                : 'Application moved to Payment. The existing posted charge remains collectible.', true);
            renderCharges();
        } catch (error) {
            setMessage(error.message || 'The charges could not be posted.');
            renderCharges();
        }
    }

    async function deleteCharge(chargeId, chargeIndex) {
        if (!chargeId) return;
        if (!confirm('Remove this charge?')) return;
        setMessage('Removing charge...');
        try {
            const response = await fetch('/Billing/DeactivateOtherCharge?id=' + encodeURIComponent(chargeId), {
                method: 'POST',
                headers: { 'RequestVerificationToken': token }
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            state.charges.splice(chargeIndex, 1);
            setMessage('Charge removed.', true);
            renderCharges();
        } catch (error) {
            setMessage(error.message || 'The charge could not be removed.');
        }
    }

    /* ============================================================
       INSPECTION PDF
       ============================================================ */
    function createInspectionPdf() {
        if (!state.inspection) return setInspectionMessage('Load an inspection record before creating the PDF.');
        const Pdf = window.jspdf?.jsPDF || window.jsPDF;
        if (!Pdf) return setInspectionMessage('The PDF component is unavailable. Refresh the page and try again.');
        const form = inspectionPayload();
        const data = {
            ...state.inspection, ...form,
            inspectedByName: selectedText('inspectionInspector'),
            approvedByName:  selectedText('inspectionApprover')
        };
        const doc = new Pdf({ orientation: 'portrait', unit: 'pt', format: 'letter' });
        const left = 42, right = 570, width = right - left;
        doc.setDrawColor(32); doc.setLineWidth(.8); doc.rect(left, 38, width, 62);
        doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.text('REPUBLIC OF THE PHILIPPINES', left + 12, 54);
        doc.setFont('helvetica', 'bold');  doc.setFontSize(13); doc.text('TRECE MARTIRES CITY WATER DISTRICT', left + 12, 71);
        doc.setFont('helvetica', 'normal'); doc.setFontSize(7); doc.text('Brgy. San Agustin, Trece Martires City, Cavite', left + 12, 84);
        doc.setFont('helvetica', 'bold');  doc.setFontSize(11); doc.text('NEW CONNECTION REPORT', right - 12, 54, { align: 'right' });
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
        doc.autoTable({ startY: 160, margin: { left, right: 42 }, theme: 'grid',
            styles: { fontSize: 7, cellPadding: 5, lineColor: 25, lineWidth: .5 },
            columnStyles: { 0: { fontStyle: 'bold', cellWidth: 115 }, 1: { cellWidth: 145 }, 2: { fontStyle: 'bold', cellWidth: 115 }, 3: { cellWidth: 153 } },
            body: [
                ['Water pressure / condition', data.waterServiceCondition || '—', 'Pipe size', data.pipeSize || '—'],
                ['Underground boring', yesNo(data.needsUndergroundBoring), 'Distance from mainline', data.distanceFromMainline || '—'],
                ['Type of connection', data.typeOfConnection || '—', 'Class of pavement', data.classOfPavement || '—'],
                ['Zone / Book', `${data.zone || '—'} / ${data.book || '—'}`, 'Charge NSC fee', yesNo(data.chargeNscFee)],
                ['Nearest account number', data.nearestAccountNumber || '—', 'Nearest account name', data.nearestAccountName || '—']
            ]
        });
        let y = doc.lastAutoTable.finalY + 12;
        doc.setFont('helvetica', 'bold'); doc.setFontSize(8); doc.text('INSPECTION REMARKS', left + 6, y + 12);
        doc.rect(left, y, width, 78); doc.setFont('helvetica', 'normal'); doc.setFontSize(7);
        doc.text(doc.splitTextToSize(data.inspectionRemarks || 'No remarks recorded.', width - 20), left + 10, y + 27);
        y += 90;
        doc.autoTable({ startY: y, margin: { left, right: 42 }, theme: 'grid',
            styles: { fontSize: 7, cellPadding: 6, lineColor: 25, lineWidth: .5 },
            body: [
                [{ content: 'INSPECTED BY', styles: { fontStyle: 'bold' } }, data.inspectedByName || '—', { content: 'APPROVED BY', styles: { fontStyle: 'bold' } }, data.approvedByName || '—'],
                [{ content: 'DATE INSPECTED', styles: { fontStyle: 'bold' } }, shortDate(data.dateInspected), { content: 'STATUS', styles: { fontStyle: 'bold' } }, data.status || 'Pending']
            ]
        });
        y = doc.lastAutoTable.finalY + 34;
        doc.line(left + 12, y, left + 220, y); doc.line(right - 220, y, right - 12, y);
        doc.setFontSize(7);
        doc.text("Applicant's / Representative's signature over printed name", left + 116, y + 12, { align: 'center' });
        doc.text('Inspection performed by', right - 116, y + 12, { align: 'center' });
        y += 55; doc.line(195, y, 417, y);
        doc.text('Installed by — signature over printed name / date', 306, y + 12, { align: 'center' });
        doc.setFontSize(6); doc.setTextColor(90);
        doc.text('Generated by TMCWD Service Management System', left, 754);
        doc.text('Report ' + (data.applicationNumber || ''), right, 754, { align: 'right' });
        window.open(doc.output('bloburl'), '_blank', 'noopener');
    }

    function createFormalInspectionPdf() {
        if (!state.inspection) return setInspectionMessage('Load an inspection record before creating the PDF.');
        const Pdf = window.jspdf?.jsPDF || window.jsPDF;
        if (!Pdf) return setInspectionMessage('The PDF component is unavailable. Refresh the page and try again.');
        const form = inspectionPayload();
        const data = { ...state.inspection, ...form, inspectedByName: selectedText('inspectionInspector'), approvedByName: selectedText('inspectionApprover') };
        const doc = new Pdf({ orientation: 'portrait', unit: 'pt', format: 'a4' });
        const left = 55, right = 540, width = right - left;
        const mark = active => active ? 'X' : '';
        const matches = (source, choice) => String(source || '').toLowerCase().includes(choice.toLowerCase());
        const span = (content, colSpan, styles = {}) => ({ content, colSpan, styles });
        const base = startY => ({
            startY, margin: { left, right: 55 }, theme: 'grid', tableWidth: width,
            styles: { font: 'helvetica', fontSize: 6.2, cellPadding: 3.2, lineColor: 0, lineWidth: .65, textColor: 0, fillColor: 255, valign: 'middle' },
            headStyles: { fillColor: 255, textColor: 0, fontStyle: 'bold', lineColor: 0, lineWidth: .65 },
            bodyStyles: { fillColor: 255, textColor: 0 },
            alternateRowStyles: { fillColor: 255 }
        });

        doc.setDrawColor(0); doc.setTextColor(0); doc.setFillColor(255);
        doc.setLineWidth(.8); doc.rect(left, 55, width, 69);
        doc.setFont('helvetica', 'normal'); doc.setFontSize(7); doc.text('REPUBLIC OF THE PHILIPPINES', left + 10, 72);
        doc.setFont('helvetica', 'bold'); doc.setFontSize(11.5); doc.text('TRECE MARTIRES CITY WATER DISTRICT', left + 10, 96);
        doc.setFont('helvetica', 'normal'); doc.setFontSize(6.5); doc.text('Brgy. San Agustin, Trece Martires City, Cavite', left + 10, 105);
        doc.setFont('helvetica', 'bold'); doc.setFontSize(10); doc.text('NEW CONNECTION REPORT', 459, 72, { align: 'center' });
        doc.setFontSize(6); doc.rect(389, 82, 140, 16); doc.text('Application Number', 397, 93);
        doc.setFont('helvetica', 'normal'); doc.text(data.applicationNumber || '-', 522, 93, { align: 'right' });
        doc.setFont('helvetica', 'bold'); doc.rect(389, 102, 140, 16); doc.text('Date Applied', 397, 113);
        doc.setFont('helvetica', 'normal'); doc.text(shortDate(data.dateApplied), 522, 113, { align: 'right' });

        doc.setFontSize(6.2); doc.setFont('helvetica', 'bold');
        doc.text('Applicant Name', 90, 145); doc.text(':', 157, 145); doc.text('Classification', 367, 145); doc.text(':', 425, 145);
        doc.text('Nearest Connection', 75, 157); doc.text(':', 157, 157); doc.text('Contact Number', 367, 157); doc.text(':', 425, 157);
        doc.text('Location / Landmark', 73, 169); doc.text(':', 157, 169);
        doc.setFont('helvetica', 'normal');
        doc.text(trimPdf(data.applicantName, 35) || '-', 165, 145); doc.text(data.classification || '-', 433, 145);
        doc.text(trimPdf([data.nearestAccountNumber, data.nearestAccountName].filter(Boolean).join(' - '), 35) || '-', 165, 157);
        doc.text(data.mobileNumber || data.phoneNumber || '-', 433, 157); doc.text(trimPdf(data.address, 72) || '-', 165, 169);
        doc.setLineWidth(.8); doc.line(left, 177, right, 177);
        doc.setFont('helvetica', 'bold'); doc.setFontSize(8); doc.text('INSPECTION REPORT', (left + right) / 2, 190, { align: 'center' });

        doc.autoTable({ ...base(196),
            columnStyles: {
                0: { cellWidth: 72, fontStyle: 'bold' }, 1: { cellWidth: 47 }, 2: { cellWidth: 32, halign: 'center', fontStyle: 'bold' },
                3: { cellWidth: 47 }, 4: { cellWidth: 32, halign: 'center', fontStyle: 'bold' }, 5: { cellWidth: 76, fontStyle: 'bold' },
                6: { cellWidth: 55 }, 7: { cellWidth: 55 }, 8: { cellWidth: 69 }
            },
            body: [
                ['Water Pressure', 'High', mark(matches(data.waterServiceCondition, 'high')), 'Low', mark(matches(data.waterServiceCondition, 'low')), 'Pipe Size', span(data.pipeSize || '-', 3)],
                ['Needs Underground Boring', 'Yes', mark(data.needsUndergroundBoring), 'No', mark(!data.needsUndergroundBoring), 'Distance From Mainline', span(data.distanceFromMainline || '-', 3)],
                ['Needs Excavation', 'Yes', '', 'No', '', span('', 4)],
                ['Needs Excavation Permit', 'DPWH', '', 'City Engineer', '', span('', 4)],
                ['Water Meter Connection', 'Cluster', mark(matches(data.typeOfConnection, 'cluster')), 'Standard', mark(matches(data.typeOfConnection, 'standard')), 'Dual', mark(matches(data.typeOfConnection, 'dual')), 'Crossroad', mark(matches(data.typeOfConnection, 'crossroad'))],
                ['Class of Pavement', 'Unpaved', mark(matches(data.classOfPavement, 'unpaved')), 'Asphalt', mark(matches(data.classOfPavement, 'asphalt')), 'Natural Soil', mark(matches(data.classOfPavement, 'natural')), 'w/ Concrete', mark(matches(data.classOfPavement, 'concrete'))],
                ['', span('Open Surface / Expose', 2), '', span('', 5)]
            ]
        });

        let y = doc.lastAutoTable.finalY;
        doc.autoTable({ ...base(y), styles: { ...base(y).styles, halign: 'center', minCellHeight: 18 },
            head: [['SERIAL NUMBER', 'SIZE', 'BRAND', 'READING']], body: [[data.meterNumber || '', data.pipeSize || '', '', '']],
            columnStyles: { 0: { cellWidth: 195 }, 1: { cellWidth: 80 }, 2: { cellWidth: 105 }, 3: { cellWidth: 105 } }
        });
        y = doc.lastAutoTable.finalY + 6;
        doc.autoTable({ ...base(y), styles: { ...base(y).styles, halign: 'center', minCellHeight: 18 },
            head: [['Guarantee Deposit', 'Registration Fee', 'SACO Materials', 'Paid WBOR No.', 'Dated']],
            body: [['', data.chargeNscFee ? 'For charging' : '', '', '', '']]
        });
        y = doc.lastAutoTable.finalY + 5;
        doc.autoTable({ ...base(y), styles: { ...base(y).styles, minCellHeight: 20 },
            head: [[{ content: 'REMARKS', styles: { halign: 'center' } }]], body: [[data.inspectionRemarks || '']],
            didParseCell: hook => { if (hook.section === 'body') hook.cell.styles.minCellHeight = 48; }
        });
        y = doc.lastAutoTable.finalY + 5;
        doc.autoTable({ ...base(y),
            columnStyles: { 0: { cellWidth: 98, fontStyle: 'bold' }, 1: { cellWidth: 147 }, 2: { cellWidth: 98, fontStyle: 'bold' }, 3: { cellWidth: 142 } },
            body: [['Received By', data.approvedByName || '', 'Requested By', data.applicantName || ''], ['Date', shortDate(data.dateInspected || data.dateApplied), 'Date', shortDate(data.dateApplied)]]
        });

        y = doc.lastAutoTable.finalY + 16; doc.setFont('helvetica', 'bold'); doc.setFontSize(6.2);
        doc.text('Inspection performed in the presence of:', left + 10, y); doc.text('Inspection performed by:', 365, y);
        y += 29; doc.setLineWidth(.7); doc.line(left + 10, y, 318, y); doc.line(365, y, right - 20, y);
        doc.setFontSize(6); doc.text("Applicant's or Representative's Signature over Printed Name", 186, y + 10, { align: 'center' });
        doc.text(data.inspectedByName || 'Maintenance', 442, y + 10, { align: 'center' });
        y += 46; doc.setFont('helvetica', 'bold'); doc.text('Installed By:', 176, y); y += 27; doc.line(174, y, 426, y);
        doc.setFont('helvetica', 'normal'); doc.text('Signature over Printed Name / Date', 300, y + 10, { align: 'center' });
        window.open(doc.output('bloburl'), '_blank', 'noopener');
    }

    /* ============================================================
       MODAL HELPERS
       ============================================================ */
    // Fit the complete workspace, including asynchronously loaded rows, without
    // clipping fields or introducing another scroll area or navigation control.
    let fitFrame = 0;
    function scheduleModalFit() {
        if (fitFrame) return;
        fitFrame = requestAnimationFrame(() => {
            fitFrame = 0;
            document.querySelectorAll('.service-dashboard .dashboard-modal.is-open').forEach(modal => {
                const panel = modal.querySelector('.dashboard-modal__panel');
                if (!panel || modal.classList.contains('sr-completed-modal')) return;
                const spacing = getComputedStyle(modal);
                const availableHeight = modal.clientHeight - parseFloat(spacing.paddingTop) - parseFloat(spacing.paddingBottom);
                const availableWidth = modal.clientWidth - parseFloat(spacing.paddingLeft) - parseFloat(spacing.paddingRight);
                const scale = Math.min(1, availableHeight / Math.max(panel.offsetHeight, panel.scrollHeight),
                    availableWidth / Math.max(panel.offsetWidth, panel.scrollWidth));
                if (scale > 0 && Math.abs(Number(panel.style.zoom || 1) - scale) > 0.001) {
                    panel.style.zoom = String(scale);
                }
            });
        });
    }
    const modalResizeObserver = new ResizeObserver(scheduleModalFit);
    document.querySelectorAll('.service-dashboard .dashboard-modal__panel').forEach(panel => {
        modalResizeObserver.observe(panel);
        new MutationObserver(scheduleModalFit).observe(panel, {
            childList: true, subtree: true, characterData: true, attributes: true,
            attributeFilter: ['class', 'hidden', 'style']
        });
    });
    window.addEventListener('resize', scheduleModalFit);
    window.visualViewport?.addEventListener('resize', scheduleModalFit);

    function openModal(modal)  { modal.classList.add('is-open'); modal.setAttribute('aria-hidden', 'false'); scheduleModalFit(); }
    function closeModal(modal) { if (!modal) return; modal.classList.remove('is-open'); modal.setAttribute('aria-hidden', 'true'); }

    function setMessage(message, success) {
        const box = document.getElementById('dashboardChargeMessage');
        box.textContent = message; box.classList.toggle('is-success', !!success);
    }
    function setInspectionMessage(message, success) {
        const box = document.getElementById('inspectionMessage');
        box.textContent = message; box.classList.toggle('is-success', !!success);
    }
    function setConnectMessage(message, success) {
        const box = document.getElementById('connectOrderMessage');
        box.textContent = message; box.classList.toggle('is-success', !!success);
    }
    function setWorkOrderMessage(message, success) {
        const box = document.getElementById('workOrderMessage');
        box.textContent = message; box.classList.toggle('is-success', !!success);
    }
    function setTappingMessage(message, success) {
        const box = document.getElementById('tappingMessage');
        box.textContent = message; box.classList.toggle('is-success', !!success);
    }
    function setInstallationRequestMessage(message, success) {
        const box = document.getElementById('installSrMessage');
        box.textContent = message;
        box.classList.toggle('is-success', !!success);
    }

    /* ============================================================
       DOM UTILITIES
       ============================================================ */
    function inputValue(id, val) { const el = document.getElementById(id); if (el) el.value = val == null ? '' : val; }
    function textValue(id, val)  { const el = document.getElementById(id); if (el) el.textContent = val || '—'; }
    function value(id)           { return document.getElementById(id)?.value?.trim() || ''; }
    function nullableNumber(id)  { const n = Number(value(id)); return n > 0 ? n : null; }
    function selectedText(id)    { const s = document.getElementById(id); return s?.selectedIndex > 0 ? s.options[s.selectedIndex].text : ''; }
    function fillOptions(id, rows, placeholder, selected) {
        const select = document.getElementById(id);
        select.innerHTML = `<option value="">${placeholder}</option>` +
            (rows || []).map(x => `<option value="${Number(x.value)}">${html(x.label)}</option>`).join('');
        inputValue(id, selected);
    }
    function money(val)         { return Number(val || 0).toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
    function dateText(val)      { const d = new Date(val); return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString('en-PH', { month: 'short', day: 'numeric', year: 'numeric' }); }
    function shortDate(val)     { const d = new Date(val); return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('en-PH', { month: 'short', day: 'numeric', year: 'numeric' }); }
    function dateInput(val)     { const d = new Date(val); return Number.isNaN(d.getTime()) ? '' : `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; }
    function dateTimeInput(val) { const d = new Date(val); return Number.isNaN(d.getTime()) ? '' : `${dateInput(d)}T${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`; }
    function trimPdf(val, len)  { const t = String(val || ''); return t.length > len ? t.slice(0, len - 1) + '…' : t; }
    function html(val)          { const node = document.createElement('div'); node.textContent = val == null ? '' : String(val); return node.innerHTML; }

}());
