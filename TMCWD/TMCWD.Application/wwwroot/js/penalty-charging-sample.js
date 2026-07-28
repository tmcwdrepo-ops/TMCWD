/**
 * Penalty Charging — Sample Data
 * 
 * Sample data for testing the penalty charging interface when server endpoints
 * are not available. This provides realistic penalty data for UI development.
 */
(function () {
    "use strict";

    // Export sample data to global scope for penalty-charging.js to use
    window.PenaltyChargingSampleData = {
        getSampleEntries: function() {
            return [
                { accountNo: "02-090030", name: "Bayug, Jenifer", usage: 9, billAmount: 170.0, discount: 0, penalty: 17.0 },
                { accountNo: "02-090039", name: "Casing, Sarah Jane", usage: 17, billAmount: 297.75, discount: 0, penalty: 29.78 },
                { accountNo: "02-090040", name: "Sagayadoro, Daisybel", usage: 46, billAmount: 898.0, discount: 0, penalty: 13.8 },
                { accountNo: "02-090042", name: "Ara, Noel", usage: 6, billAmount: 170.0, discount: 0, penalty: 1.8 },
                { accountNo: "02-090043", name: "Ramirez, Marriz", usage: 5, billAmount: 170.0, discount: 0, penalty: 1.5 },
                { accountNo: "02-090056", name: "Gaña, Glendil", usage: 121, billAmount: 2660.5, discount: 0, penalty: 266.05 },
                { accountNo: "02-090062", name: "(Omipon, Jennifer) Omipon, Reggie Boy", usage: 13, billAmount: 224.75, discount: 0, penalty: 22.48 },
                { accountNo: "02-090079", name: "Rey, Jane", usage: 6, billAmount: 170.0, discount: 0, penalty: 1.8 },
                { accountNo: "02-090081", name: "Santos, Maria Elena", usage: 25, billAmount: 456.25, discount: 0, penalty: 45.63 },
                { accountNo: "02-090095", name: "Cruz, Roberto Jr.", usage: 34, billAmount: 678.50, discount: 0, penalty: 67.85 },
                { accountNo: "02-090112", name: "Dela Rosa, Carmen", usage: 18, billAmount: 315.75, discount: 0, penalty: 31.58 },
                { accountNo: "02-090134", name: "Villanueva, Jose Martin", usage: 52, billAmount: 1025.40, discount: 0, penalty: 102.54 },
                { accountNo: "02-090156", name: "Fernandez, Ana Luz", usage: 8, billAmount: 170.0, discount: 0, penalty: 17.0 },
                { accountNo: "02-090178", name: "Mendoza, Patrick", usage: 41, billAmount: 823.75, discount: 0, penalty: 82.38 },
                { accountNo: "02-090201", name: "Garcia, Linda", usage: 29, billAmount: 567.25, discount: 0, penalty: 56.73 }
            ];
        }
    };

})();