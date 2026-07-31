/**
 * present-reading-sample.js — Sample Data for Concessionaires & Reading Records
 */

window.CONCESSIONAIRES_SAMPLE = {
  // Zone 1, Book 1 (14 accounts to showcase 3 pages of pagination)
  '01-0001': { name: 'Juan De La Cruz', meterNo: 'M-10294', address: 'Blk 4 Lot 12, Brgy. San Agustin', rateClass: 'Residential', prevReading: 120, zone: '1', book: '1' },
  '01-0004': { name: 'Lucia Santos', meterNo: 'M-10296', address: '456 Rizal Ave, Brgy. San Agustin', rateClass: 'Residential', prevReading: 95, zone: '1', book: '1' },
  '01-0005': { name: 'Pedro Penduko', meterNo: 'M-10297', address: '789 Bonifacio St, Brgy. San Agustin', rateClass: 'Residential', prevReading: 110, zone: '1', book: '1' },
  '01-0006': { name: 'Gabriela Silang', meterNo: 'M-10298', address: '12 Mabini Lane, Brgy. San Agustin', rateClass: 'Residential', prevReading: 80, zone: '1', book: '1' },
  '01-0007': { name: 'Gregorio Del Pilar', meterNo: 'M-10299', address: 'Zone 1 Crossing, Brgy. San Agustin', rateClass: 'Residential', prevReading: 150, zone: '1', book: '1' },
  '01-0008': { name: 'Marcelo H. Del Pilar', meterNo: 'M-10300', address: '101 Jacinto St, Brgy. San Agustin', rateClass: 'Commercial', prevReading: 220, zone: '1', book: '1' },
  '01-0009': { name: 'Melchora Aquino II', meterNo: 'M-10301', address: 'Plaza Libertad, Brgy. San Agustin', rateClass: 'Residential', prevReading: 75, zone: '1', book: '1' },
  '01-0010': { name: 'Juan Luna', meterNo: 'M-10302', address: 'Art District, Brgy. San Agustin', rateClass: 'Commercial', prevReading: 310, zone: '1', book: '1' },
  '01-0012': { name: 'Emilio Jacinto', meterNo: 'M-10304', address: 'Light St, Brgy. San Agustin', rateClass: 'Residential', prevReading: 60, zone: '1', book: '1' },
  '01-0013': { name: 'Apolinario Mabini', meterNo: 'M-10305', address: 'Sublime Ave, Brgy. San Agustin', rateClass: 'Residential', prevReading: 90, zone: '1', book: '1' },
  '01-0014': { name: 'Marcelo Del Pilar Jr', meterNo: 'M-10306', address: '102 Jacinto St, Brgy. San Agustin', rateClass: 'Residential', prevReading: 105, zone: '1', book: '1' },
  '01-0015': { name: 'Mariano Ponce', meterNo: 'M-10307', address: 'Ponce Blvd, Brgy. San Agustin', rateClass: 'Residential', prevReading: 140, zone: '1', book: '1' },
  '01-0016': { name: 'Galicano Apacible', meterNo: 'M-10308', address: 'Apacible St, Brgy. San Agustin', rateClass: 'Residential', prevReading: 115, zone: '1', book: '1' },
  '01-0017': { name: 'Artemio Ricarte', meterNo: 'M-10309', address: 'Ricarte Way, Brgy. San Agustin', rateClass: 'Commercial', prevReading: 240, zone: '1', book: '1' },

  // Zone 1, Book 2
  '01-0002': { name: 'Maria Clara Santos', meterNo: 'M-10295', address: '125 Perez Street, Brgy. Luciano', rateClass: 'Residential', prevReading: 85, zone: '1', book: '2' },
  '01-0011': { name: 'Felipe Agoncillo', meterNo: 'M-10303', address: '202 Del Pilar St, Brgy. Luciano', rateClass: 'Residential', prevReading: 130, zone: '1', book: '2' },

  // Zone 1, Book 3
  '01-0003': { name: 'Jose Rizal Enterprise', meterNo: 'M-20110', address: 'Governors Drive, Brgy. Cabuco', rateClass: 'Commercial', prevReading: 340, zone: '1', book: '3' },

  // Zone 2, Book 1
  '02-0010': { name: 'Antonio Luna Tech', meterNo: 'M-20450', address: 'Zone 2 Industrial Park, Brgy. Osorio', rateClass: 'Commercial', prevReading: 512, zone: '2', book: '1' },

  // Zone 2, Book 2
  '02-0025': { name: 'Emilio Aguinaldo', meterNo: 'M-10882', address: '45 Aguinaldo Highway, Brgy. Lapidario', rateClass: 'Residential', prevReading: 198, zone: '2', book: '2' },

  // Zone 3, Book 1
  '03-0005': { name: 'Melchora Aquino', meterNo: 'M-10901', address: 'Blk 10 Lot 5, Brgy. Inocencio', rateClass: 'Residential', prevReading: 64, zone: '3', book: '1' },

  // Zone 4, Book 1
  '04-0012': { name: 'Andres Bonifacio Store', meterNo: 'M-30012', address: 'Public Market, Brgy. Hugo Perez', rateClass: 'Semi-Commercial', prevReading: 230, zone: '4', book: '1' }
};

window.READINGS_SAMPLE_LIST = [
  // Zone 1, Book 1 (14 accounts to showcase 3 pages of pagination)
  { id: 1, accountNo: '01-0001', name: 'Juan De La Cruz', rateClass: 'Residential', present: 135, previous: 120, usage: 15, amount: '475.00', zone: '1', book: '1' },
  { id: 4, accountNo: '01-0004', name: 'Lucia Santos', rateClass: 'Residential', present: 110, previous: 95, usage: 15, amount: '345.00', zone: '1', book: '1' },
  { id: 5, accountNo: '01-0005', name: 'Pedro Penduko', rateClass: 'Residential', present: 128, previous: 110, usage: 18, amount: '420.00', zone: '1', book: '1' },
  { id: 6, accountNo: '01-0006', name: 'Gabriela Silang', rateClass: 'Residential', present: 95, previous: 80, usage: 15, amount: '345.00', zone: '1', book: '1' },
  { id: 7, accountNo: '01-0007', name: 'Gregorio Del Pilar', rateClass: 'Residential', present: 165, previous: 150, usage: 15, amount: '345.00', zone: '1', book: '1' },
  { id: 8, accountNo: '01-0008', name: 'Marcelo H. Del Pilar', rateClass: 'Commercial', present: 250, previous: 220, usage: 30, amount: '1,350.00', zone: '1', book: '1' },
  { id: 9, accountNo: '01-0009', name: 'Melchora Aquino II', rateClass: 'Residential', present: 85, previous: 75, usage: 10, amount: '220.00', zone: '1', book: '1' },
  { id: 10, accountNo: '01-0010', name: 'Juan Luna', rateClass: 'Commercial', present: 335, previous: 310, usage: 25, amount: '1,125.00', zone: '1', book: '1' },
  { id: 15, accountNo: '01-0012', name: 'Emilio Jacinto', rateClass: 'Residential', present: 72, previous: 60, usage: 12, amount: '270.00', zone: '1', book: '1' },
  { id: 16, accountNo: '01-0013', name: 'Apolinario Mabini', rateClass: 'Residential', present: 108, previous: 90, usage: 18, amount: '420.00', zone: '1', book: '1' },
  { id: 17, accountNo: '01-0014', name: 'Marcelo Del Pilar Jr', rateClass: 'Residential', present: 125, previous: 105, usage: 20, amount: '470.00', zone: '1', book: '1' },
  { id: 18, accountNo: '01-0015', name: 'Mariano Ponce', rateClass: 'Residential', present: 155, previous: 140, usage: 15, amount: '345.00', zone: '1', book: '1' },
  { id: 19, accountNo: '01-0016', name: 'Galicano Apacible', rateClass: 'Residential', present: 132, previous: 115, usage: 17, amount: '395.00', zone: '1', book: '1' },
  { id: 20, accountNo: '01-0017', name: 'Artemio Ricarte', rateClass: 'Commercial', present: 285, previous: 240, usage: 45, amount: '2,025.00', zone: '1', book: '1' },

  // Zone 1, Book 2
  { id: 2, accountNo: '01-0002', name: 'Maria Clara Santos', rateClass: 'Residential', present: 98, previous: 85, usage: 13, amount: '415.00', zone: '1', book: '2' },
  { id: 11, accountNo: '01-0011', name: 'Felipe Agoncillo', rateClass: 'Residential', present: 145, previous: 130, usage: 15, amount: '345.00', zone: '1', book: '2' },

  // Zone 1, Book 3
  { id: 3, accountNo: '01-0003', name: 'Jose Rizal Enterprise', rateClass: 'Commercial', present: 380, previous: 340, usage: 40, amount: '1,800.00', zone: '1', book: '3' },

  // Zone 2, Book 1
  { id: 12, accountNo: '02-0010', name: 'Antonio Luna Tech', rateClass: 'Commercial', present: 545, previous: 512, usage: 33, amount: '1,485.00', zone: '2', book: '1' },

  // Zone 2, Book 2
  { id: 13, accountNo: '02-0025', name: 'Emilio Aguinaldo', rateClass: 'Residential', present: 215, previous: 198, usage: 17, amount: '535.00', zone: '2', book: '2' },

  // Zone 3, Book 1
  { id: 14, accountNo: '03-0005', name: 'Melchora Aquino', rateClass: 'Residential', present: 74, previous: 64, usage: 10, amount: '220.00', zone: '3', book: '1' }
];
