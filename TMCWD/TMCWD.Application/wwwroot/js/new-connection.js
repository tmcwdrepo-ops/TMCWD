document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('newConnectionForm');
    if (!form) return;
    const byId = id => document.getElementById(id);
    const value = id => byId(id)?.value?.trim() || '';
    const checked = id => !!byId(id)?.checked;
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const message = byId('ncMessage');
    let submitted = false;

    document.querySelectorAll('[data-nc-tab]').forEach(button => button.addEventListener('click', () => {
        document.querySelectorAll('[data-nc-tab]').forEach(x => x.classList.toggle('is-active', x === button));
        document.querySelectorAll('[data-nc-panel]').forEach(panel => {
            const active = panel.dataset.ncPanel === button.dataset.ncTab;
            panel.classList.toggle('is-active', active);
            panel.hidden = !active;
        });
    }));

    async function loadZones() {
        try {
            const response = await fetch('/ZoneBook/GetZone');
            if (!response.ok) return;
            const rows = await response.json() || [];
            const zones = [...new Set(rows.map(x => Number(x.zone)).filter(Number.isFinite))];
            byId('ncZone').innerHTML = '<option value="">Select zone</option>' + zones.map(x => `<option value="${x}">Zone ${x}</option>`).join('');
        } catch { }
    }

    byId('ncZone').addEventListener('change', async event => {
        const select = byId('ncBook');
        select.innerHTML = '<option value="">Select book</option>';
        select.disabled = true;
        if (!event.target.value) return;
        try {
            const response = await fetch('/ZoneBook/GetBooksByZone?zone=' + encodeURIComponent(event.target.value));
            if (!response.ok) return;
            const rows = await response.json() || [];
            select.innerHTML += rows.map(x => `<option value="${Number(x.book)}">Book ${Number(x.book)}</option>`).join('');
            select.disabled = false;
        } catch { }
    });

    byId('ncBirthdate').addEventListener('change', event => {
        const birth = new Date(event.target.value + 'T00:00:00');
        if (Number.isNaN(birth.getTime())) return byId('ncAge').value = '';
        const today = new Date();
        let age = today.getFullYear() - birth.getFullYear();
        if (today < new Date(today.getFullYear(), birth.getMonth(), birth.getDate())) age--;
        byId('ncAge').value = Math.max(0, age);
    });

    byId('ncSameApplicant').addEventListener('change', event => {
        if (!event.target.checked) return;
        byId('ncOwnerName').value = value('ncApplicantName');
        // Build combined address from the split fields
        const combinedAddress = [
            value('ncUnitNumber'), value('ncBuilding'),
            value('ncHouseNumber'), value('ncStreet'),
            selectedText('ncBarangay')
        ].filter(Boolean).join(', ');
        byId('ncOwnerAddress').value = combinedAddress;
    });

    const photoInput = byId('ncPhoto');
    byId('ncUploadPhoto').addEventListener('click', () => photoInput.click());
    byId('ncCapturePhoto').addEventListener('click', () => {
        photoInput.setAttribute('capture', 'environment');
        photoInput.click();
    });
    photoInput.addEventListener('change', () => {
        const file = photoInput.files?.[0];
        if (!file) return;
        const url = URL.createObjectURL(file);
        byId('ncPhotoPreview').innerHTML = `<img src="${url}" alt="Applicant preview" />`;
    });

    function selectedText(id) {
        const control = byId(id);
        return control?.selectedOptions?.[0]?.textContent?.trim() || value(id);
    }

    function printValue(target, source, transform) {
        const raw = transform ? transform() : value(source);
        byId(target).textContent = raw || '\u2014';
    }

    function preparePrint() {
        printValue('printTrackingNumber', 'ncTrackingNumber');
        printValue('printDateApplied', 'ncDateApplied', () => value('ncDateApplied') ? new Date(value('ncDateApplied') + 'T00:00:00').toLocaleDateString('en-PH') : '');
        printValue('printApplicantName', 'ncApplicantName', () => [value('ncApplicantName'), value('ncSuffix')].filter(Boolean).join(' '));
        printValue('printConsumerType', 'ncConsumerType', () => selectedText('ncConsumerType'));
        printValue('printServiceSize', 'ncServiceSize', () => selectedText('ncServiceSize'));
        printValue('printServiceAddress', null, () => [
            value('ncUnitNumber'), value('ncBuilding'),
            value('ncHouseNumber'), value('ncStreet')
        ].filter(Boolean).join(', '));
        printValue('printBarangay', 'ncBarangay', () => selectedText('ncBarangay'));
        printValue('printMobile', 'ncMobile'); printValue('printPhone', 'ncPhone'); printValue('printEmail', 'ncEmail');
        printValue('printZoneBook', 'ncZone', () => [selectedText('ncZone'), selectedText('ncBook')].filter(x => x && !x.startsWith('Select')).join(' / '));
        printValue('printNearestAccount', 'ncNearestAccount'); printValue('printNearestName', 'ncNearestName');
        printValue('printSpouse', 'ncSpouse'); printValue('printHousehold', 'ncHousehold'); printValue('printEstablishment', 'ncEstablishment');
        printValue('printPlaceOfBirth', 'ncPlaceOfBirth');
        printValue('printBirthAge', 'ncBirthdate', () => [value('ncBirthdate') ? new Date(value('ncBirthdate') + 'T00:00:00').toLocaleDateString('en-PH') : '', value('ncAge') ? `Age ${value('ncAge')}` : ''].filter(Boolean).join(' / '));
        printValue('printGenderStatus', 'ncGender', () => [selectedText('ncGender'), selectedText('ncCivilStatus')].filter(x => x && !x.startsWith('Select')).join(' / '));
        printValue('printBusiness', 'ncBusiness'); printValue('printOwnership', 'ncOwnership', () => selectedText('ncOwnership'));
        printValue('printSameApplicant', 'ncSameApplicant', () => checked('ncSameApplicant') ? 'Yes' : 'No');
        printValue('printOwnerName', 'ncOwnerName'); printValue('printOwnerAddress', 'ncOwnerAddress'); printValue('printRemarks', 'ncRemarks');
        printValue('printMeterLocation', 'ncMeterLocation'); printValue('printConnectionNotes', 'ncConnectionNotes');
        printValue('printRequirements', 'ncValidId', () => [
            checked('ncValidId') ? 'Valid ID' : '', checked('ncOwnershipProof') ? 'Proof of ownership/authorization' : '',
            checked('ncClearance') ? 'Barangay clearance' : '', checked('ncSketch') ? 'Location sketch' : ''
        ].filter(Boolean).join('; ') || 'None indicated');
    }

    byId('ncPrint').addEventListener('click', () => { preparePrint(); window.print(); });
    window.addEventListener('beforeprint', preparePrint);

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (submitted) return;
        if (!form.reportValidity()) return;
        const submit = byId('ncSubmit');
        submit.disabled = true;
        message.className = '';
        message.textContent = 'Saving application...';
        const payload = {
            dateApplied: value('ncDateApplied'), applicantName: value('ncApplicantName'), suffix: value('ncSuffix'),
            barangay: value('ncBarangay'),
            // Split address fields — map directly to accounts table columns
            unitNumber: value('ncUnitNumber'),
            building:   value('ncBuilding'),
            houseNumber: value('ncHouseNumber'),
            street:     value('ncStreet'),
            // Combined address string for backward-compatible storage in Remarks
            address: [value('ncUnitNumber'), value('ncBuilding'), value('ncHouseNumber'), value('ncStreet')].filter(Boolean).join(', '),
            mobileNumber: value('ncMobile'),
            phoneNumber: value('ncPhone'), emailAddress: value('ncEmail'), consumerType: value('ncConsumerType'),
            serviceSize: value('ncServiceSize'), zone: Number(value('ncZone')) || null, book: Number(value('ncBook')) || null,
            nearestAccountNumber: value('ncNearestAccount'), nearestAccountName: value('ncNearestName'), spouseName: value('ncSpouse'),
            peopleInHousehold: Number(value('ncHousehold')) || null, establishment: value('ncEstablishment'), placeOfBirth: value('ncPlaceOfBirth'),
            birthdate: value('ncBirthdate') || null, gender: value('ncGender'), civilStatus: value('ncCivilStatus'), natureOfBusiness: value('ncBusiness'),
            remarks: value('ncRemarks'), ownershipStatus: value('ncOwnership'), sameAsApplicant: checked('ncSameApplicant'),
            ownerName: value('ncOwnerName'), ownerAddress: value('ncOwnerAddress'), meterLocation: value('ncMeterLocation'),
            connectionNotes: value('ncConnectionNotes'), hasValidId: checked('ncValidId'), hasProofOfOwnership: checked('ncOwnershipProof'),
            hasBarangayClearance: checked('ncClearance'), hasLocationSketch: checked('ncSketch'), photoFileName: photoInput.files?.[0]?.name || ''
        };
        try {
            const response = await fetch('/CustomerSupport/SaveNewConnection', {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(payload)
            });
            if (!response.ok) throw new Error((await response.text()).replace(/^"|"$/g, ''));
            const saved = await response.json();
            byId('ncTrackingNumber').value = saved.trackingNumber;
            message.className = 'is-success';
            message.textContent = `Application ${saved.trackingNumber} was submitted successfully.`;
            submitted = true;
            submit.textContent = 'Application Submitted';
            window.scrollTo({ top: 0, behavior: 'smooth' });
        } catch (error) {
            message.textContent = error.message || 'The application could not be saved.';
        } finally {
            submit.disabled = submitted;
        }
    });

    loadZones();
});
