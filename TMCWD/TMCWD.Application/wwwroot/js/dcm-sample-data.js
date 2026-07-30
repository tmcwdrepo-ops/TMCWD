/**
 * Sample Data for Debit/Credit Memo Demo
 * Contains 5 sample accounts for testing the DCM functionality
 */

// Sample DCM Types
const dcmTypes = [
    "BOUNCED CHECK",
    "CANCELLED OFFICIAL RECEIPT",
    "DEFECTIVE SC AND WATER METER",
    "DEFECTIVE SERVICE CONNECTION", 
    "DEFECTIVE WATER METER",
    "DEPOSIT APPLICATION",
    "DISCOUNT",
    "DOUBLE ENTRY OF ACCOUNT",
    "ERRONEOUS READING",
    "ERROR ON PREVIOUS BALANCE",
    "INVALID ACCOUNT PAYMENT",
    "INVALID CHARGES",
    "LATE PENALTY",
    "OTHERS",
    "OVER PAYMENT REFUND",
    "RECLASSIFICATION OF CATEGORY",
    "SPECIAL PRIVILEGE ACCOUNT",
    "TRANSFER OF PAYMENT",
    "WAIVE PENALTY",
    "WITHOLDING TAX"
];

/**
 * Search DCM types function
 * @param {string} searchTerm - The search term
 * @returns {array} - Array of matching DCM types
 */
function searchDCMTypes(searchTerm) {
    if (!searchTerm || searchTerm.length < 1) {
        return dcmTypes; // Return all types if no search term
    }
    
    const term = searchTerm.toLowerCase().trim();
    
    return dcmTypes.filter(type => {
        return type.toLowerCase().includes(term) ||
               type.toLowerCase().startsWith(term);
    });
}
const sampleAccounts = [
    {
        accountNumber: "001-2024-0001",
        accountName: "JUAN DELA CRUZ",
        address: "123 Main Street, Barangay San Jose",
        meterNumber: "M-001-2024",
        connectionType: "Residential"
    },
    {
        accountNumber: "001-2024-0002", 
        accountName: "MARIA SANTOS",
        address: "456 Oak Avenue, Barangay Santa Maria",
        meterNumber: "M-002-2024",
        connectionType: "Residential"
    },
    {
        accountNumber: "002-2024-0001",
        accountName: "PEDRO REYES CONSTRUCTION",
        address: "789 Industrial Road, Barangay San Pedro",
        meterNumber: "M-003-2024",
        connectionType: "Commercial"
    },
    {
        accountNumber: "001-2024-0003",
        accountName: "ANA GARCIA",
        address: "321 Pine Street, Barangay Santa Ana",
        meterNumber: "M-004-2024",
        connectionType: "Residential"
    },
    {
        accountNumber: "003-2024-0001",
        accountName: "MANILA WATER WORKS INC",
        address: "555 Business District, Barangay Centro",
        meterNumber: "M-005-2024",
        connectionType: "Industrial"
    }
];

// Sample DCM records for demonstration
const sampleDCMRecords = [
    {
        id: "DCM-2024-001",
        accountNumber: "001-2024-0001",
        accountName: "JUAN DELA CRUZ",
        dcmType: "OVER PAYMENT REFUND",
        memoType: "credit",
        amount: 150.00,
        dcmDate: "2024-07-15",
        billMonthBasis: "2024-06-01",
        explanation: "OVERPAYMENT ADJUSTMENT",
        remarks: "AMOUNTING TO DUE TO OVERPAYMENT ADJUSTMENT",
        status: "pending",
        createdDate: "2024-07-15T10:30:00"
    },
    {
        id: "DCM-2024-002", 
        accountNumber: "002-2024-0001",
        accountName: "PEDRO REYES CONSTRUCTION",
        dcmType: "DEFECTIVE SERVICE CONNECTION",
        memoType: "debit",
        amount: 500.00,
        dcmDate: "2024-07-20",
        billMonthBasis: "2024-07-01",
        explanation: "RECONNECTION SERVICE",
        remarks: "AMOUNTING TO DUE TO RECONNECTION SERVICE",
        status: "approved",
        createdDate: "2024-07-20T14:15:00"
    },
    {
        id: "DCM-2024-003",
        accountNumber: "001-2024-0002",
        accountName: "MARIA SANTOS", 
        dcmType: "ERRONEOUS READING",
        memoType: "credit",
        amount: 75.50,
        dcmDate: "2024-07-22",
        billMonthBasis: "2024-06-01",
        explanation: "METER READING ERROR",
        remarks: "AMOUNTING TO DUE TO METER READING ERROR",
        status: "pending",
        createdDate: "2024-07-22T09:45:00"
    }
];

/**
 * Account lookup function (single result)
 * @param {string} accountNumber - The account number to search for
 * @returns {object|null} - Account object if found, null if not found
 */
function lookupAccount(accountNumber) {
    return sampleAccounts.find(account => 
        account.accountNumber.toLowerCase().includes(accountNumber.toLowerCase()) ||
        accountNumber.toLowerCase().includes(account.accountNumber.toLowerCase())
    ) || null;
}

/**
 * Search accounts function (multiple results)
 * @param {string} searchTerm - The search term (account number or name)
 * @returns {array} - Array of matching account objects
 */
function searchAccounts(searchTerm) {
    if (!searchTerm || searchTerm.length < 1) {
        return [];
    }
    
    const term = searchTerm.toLowerCase().trim();
    
    return sampleAccounts.filter(account => {
        // Match account number
        const accountMatches = account.accountNumber.toLowerCase().includes(term);
        
        // Match account name (first letters or full name)
        const nameMatches = account.accountName.toLowerCase().includes(term) ||
                           account.accountName.toLowerCase().startsWith(term);
        
        // Match by first letters of each word in name
        const nameWords = account.accountName.toLowerCase().split(' ');
        const firstLetters = nameWords.map(word => word.charAt(0)).join('');
        const firstLetterMatches = firstLetters.includes(term);
        
        return accountMatches || nameMatches || firstLetterMatches;
    });
}

/**
 * Get account display text for dropdown
 * @param {object} account - Account object
 * @returns {string} - Formatted display text
 */
function getAccountDisplayText(account) {
    return `${account.accountNumber} - ${account.accountName}`;
}

/**
 * Get all sample DCM records
 * @returns {array} - Array of DCM records
 */
function getSampleDCMRecords() {
    return [...sampleDCMRecords];
}

/**
 * Add new DCM record (simulation)
 * @param {object} dcmData - DCM data to add
 * @returns {object} - Added DCM record with generated ID
 */
function addDCMRecord(dcmData) {
    const newRecord = {
        id: `DCM-2024-${String(sampleDCMRecords.length + 1).padStart(3, '0')}`,
        ...dcmData,
        status: "pending",
        createdDate: new Date().toISOString()
    };
    
    sampleDCMRecords.push(newRecord);
    return newRecord;
}

/**
 * Format currency for display
 * @param {number} amount - Amount to format
 * @returns {string} - Formatted currency string
 */
function formatCurrency(amount) {
    return new Intl.NumberFormat('en-PH', {
        style: 'currency',
        currency: 'PHP',
        minimumFractionDigits: 2
    }).format(amount);
}

/**
 * Format date for display
 * @param {string} dateString - ISO date string
 * @returns {string} - Formatted date string
 */
function formatDate(dateString) {
    return new Date(dateString).toLocaleDateString('en-PH', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    });
}

/**
 * Get DCM type display name
 * @param {string} dcmType - DCM type code
 * @returns {string} - Display name
 */
function getDCMTypeDisplayName(dcmType) {
    const typeMap = {
        'billing_correction': 'Billing Correction',
        'meter_discrepancy': 'Meter Discrepancy', 
        'penalty_waiver': 'Penalty Waiver',
        'reconnection_fee': 'Reconnection Fee',
        'others': 'Others'
    };
    return typeMap[dcmType] || dcmType;
}

/**
 * Get status badge class
 * @param {string} status - Status string
 * @returns {string} - CSS class for status badge
 */
function getStatusBadgeClass(status) {
    const statusMap = {
        'pending': 'badge-warning',
        'approved': 'badge-success',
        'rejected': 'badge-danger',
        'processing': 'badge-info'
    };
    return statusMap[status] || 'badge-secondary';
}

// Export functions for use in other scripts (if using modules)
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        sampleAccounts,
        dcmTypes,
        sampleDCMRecords,
        lookupAccount,
        searchAccounts,
        searchDCMTypes,
        getAccountDisplayText,
        getSampleDCMRecords,
        addDCMRecord,
        formatCurrency,
        formatDate,
        getDCMTypeDisplayName,
        getStatusBadgeClass
    };
}

/**
 * Toast Notification System
 */
class ToastNotification {
    constructor() {
        this.container = document.getElementById('toastContainer');
        this.toasts = new Map();
        this.autoId = 0;
    }

    /**
     * Show a toast notification
     * @param {object} options - Toast options
     */
    show(options = {}) {
        const {
            title = 'Notification',
            message = '',
            type = 'info', // success, error, warning, info
            duration = 0, // 0 means manual dismiss for popup card
            details = null,
            dismissible = true,
            showActions = true
        } = options;

        const toastId = `toast-${++this.autoId}`;
        const toast = this.createToastElement(toastId, title, message, type, details, showActions);
        
        // Clear existing toasts for modal behavior
        this.clearAll();
        
        this.container.appendChild(toast);
        this.container.classList.add('active');
        this.toasts.set(toastId, toast);

        // Trigger show animation
        requestAnimationFrame(() => {
            toast.classList.add('show');
        });

        // Auto dismiss only if duration > 0
        if (duration > 0) {
            setTimeout(() => {
                this.hide(toastId);
            }, duration);
        }

        return toastId;
    }

    /**
     * Create toast element
     */
    createToastElement(id, title, message, type, details, showActions) {
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.id = id;

        const icons = {
            success: `<svg viewBox="0 0 24 24" fill="currentColor">
                        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                      </svg>`,
            error: `<svg viewBox="0 0 24 24" fill="currentColor">
                     <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>
                   </svg>`,
            warning: `<svg viewBox="0 0 24 24" fill="currentColor">
                       <path d="M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z"/>
                     </svg>`,
            info: `<svg viewBox="0 0 24 24" fill="currentColor">
                    <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z"/>
                  </svg>`
        };

        const actionButtons = showActions ? `
            <div class="toast-actions">
                <button class="toast-btn toast-btn-secondary" onclick="toastSystem.hide('${id}')">
                    Close
                </button>
                ${type === 'success' ? `
                    <button class="toast-btn toast-btn-primary" onclick="toastSystem.hide('${id}')">
                        OK
                    </button>
                ` : ''}
            </div>
        ` : '';

        toast.innerHTML = `
            <div class="toast-header">
                <h4 class="toast-title">
                    <span class="toast-icon">${icons[type] || icons.info}</span>
                    ${title}
                </h4>
            </div>
            <div class="toast-body">
                ${message}
                ${details ? `<div class="toast-details">${details}</div>` : ''}
            </div>
            ${actionButtons}
        `;

        return toast;
    }

    /**
     * Hide a toast notification
     */
    hide(toastId) {
        const toast = this.toasts.get(toastId);
        if (toast) {
            toast.classList.add('hide');
            setTimeout(() => {
                if (toast.parentNode) {
                    toast.parentNode.removeChild(toast);
                }
                this.toasts.delete(toastId);
                
                // Hide container if no more toasts
                if (this.toasts.size === 0) {
                    this.container.classList.remove('active');
                }
            }, 400);
        }
    }

    /**
     * Show success notification
     */
    success(message, details = null) {
        return this.show({
            title: 'Success',
            message,
            type: 'success',
            details,
            duration: 0, // Manual dismiss for popup card
            showActions: true
        });
    }

    /**
     * Show error notification
     */
    error(message, details = null) {
        return this.show({
            title: 'Error',
            message,
            type: 'error',
            details,
            duration: 0, // Manual dismiss for popup card
            showActions: true
        });
    }

    /**
     * Show warning notification
     */
    warning(message, details = null) {
        return this.show({
            title: 'Warning',
            message,
            type: 'warning',
            details,
            duration: 0, // Manual dismiss for popup card
            showActions: true
        });
    }

    /**
     * Show info notification
     */
    info(message, details = null) {
        return this.show({
            title: 'Information',
            message,
            type: 'info',
            details,
            duration: 0, // Manual dismiss for popup card
            showActions: true
        });
    }

    /**
     * Clear all notifications
     */
    clearAll() {
        this.toasts.forEach((toast, id) => {
            this.hide(id);
        });
        this.container.classList.remove('active');
    }
}

// Initialize global toast system
const toastSystem = new ToastNotification();