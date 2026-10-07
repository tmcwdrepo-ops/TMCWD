CREATE TABLE reading_sheets (
  id INTEGER PRIMARY KEY,
  server_id INTEGER NOT NULL UNIQUE,
  name TEXT NOT NULL,
  zone_book_id INTEGER NOT NULL,
  zone TEXT NOT NULL,
  book TEXT NOT NULL,
  assigned_to INTEGER NOT NULL,
  billing_date TEXT,
  due_date TEXT,
  billing_period_start TEXT,
  seq_from INTEGER,
  seq_to INTEGER,
  disconnection_date TEXT,
  date_created TEXT,
  date_upload TEXT,
  status INTEGER NOT NULL DEFAULT 1 CHECK(status IN (1,2,3,4)),
  downloaded_at TEXT NOT NULL
);
-- next
CREATE TABLE accounts (
  id INTEGER PRIMARY KEY,
  server_account_id INTEGER NOT NULL,
  reading_sheet_id INTEGER NOT NULL REFERENCES reading_sheets(id) ON DELETE RESTRICT,
  account_number TEXT NOT NULL,
  customer_name TEXT NOT NULL,
  address TEXT NOT NULL,
  meter_number TEXT NOT NULL,
  classification INTEGER NOT NULL,
  meter_size TEXT NOT NULL,
  previous_reading TEXT NOT NULL,
  previous_reading_date TEXT,
  in_roster INTEGER NOT NULL DEFAULT 1 CHECK(in_roster IN (0,1)),
  previous_balance TEXT NOT NULL,
  sequence_number INTEGER NOT NULL,
  UNIQUE(reading_sheet_id, server_account_id),
  UNIQUE(id, reading_sheet_id)
);
-- next
CREATE INDEX accounts_sequence ON accounts(reading_sheet_id, sequence_number);
-- next
CREATE TABLE readings (
  id INTEGER PRIMARY KEY,
  local_uuid TEXT NOT NULL UNIQUE CHECK(length(local_uuid) = 36),
  server_reading_id INTEGER UNIQUE,
  reading_sheet_id INTEGER NOT NULL REFERENCES reading_sheets(id) ON DELETE RESTRICT,
  account_id INTEGER NOT NULL,
  previous_reading TEXT NOT NULL,
  current_reading TEXT,
  consumption TEXT,
  reading_date TEXT NOT NULL,
  reading_status INTEGER NOT NULL DEFAULT 2 CHECK(reading_status IN (1,2,3,4)),
  meter_condition TEXT NOT NULL,
  remarks TEXT NOT NULL DEFAULT '',
  sync_status TEXT NOT NULL DEFAULT 'pending'
    CHECK(sync_status IN ('pending','syncing','synced','failed')),
  synced_at TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  FOREIGN KEY(account_id, reading_sheet_id) REFERENCES accounts(id, reading_sheet_id) ON DELETE RESTRICT,
  UNIQUE(reading_sheet_id, account_id)
);
-- next
CREATE INDEX readings_sync_status ON readings(sync_status);
-- next
CREATE TABLE billing_rates (
  id INTEGER PRIMARY KEY,
  server_id INTEGER NOT NULL UNIQUE,
  classification INTEGER NOT NULL,
  meter_size TEXT NOT NULL,
  minimum_charge TEXT NOT NULL,
  rate11_to20 TEXT NOT NULL,
  rate21_to30 TEXT NOT NULL,
  rate31_to40 TEXT NOT NULL,
  rate41_up TEXT NOT NULL,
  effective_date TEXT NOT NULL,
  is_active INTEGER NOT NULL CHECK(is_active IN (0,1))
);
-- next
CREATE TABLE bills (
  id INTEGER PRIMARY KEY,
  server_bill_id INTEGER UNIQUE,
  reading_id INTEGER NOT NULL UNIQUE REFERENCES readings(id) ON DELETE RESTRICT,
  billing_period TEXT NOT NULL,
  billing_date TEXT NOT NULL,
  due_date TEXT NOT NULL,
  water_charge TEXT NOT NULL,
  previous_balance TEXT NOT NULL,
  penalties TEXT NOT NULL,
  other_charges TEXT NOT NULL,
  total_amount_due TEXT NOT NULL,
  server_total_amount_due TEXT,
  review_required INTEGER NOT NULL DEFAULT 0 CHECK(review_required IN (0,1)),
  print_count INTEGER NOT NULL DEFAULT 0 CHECK(print_count >= 0),
  created_at TEXT NOT NULL
);
-- next
CREATE TABLE sync_queue (
  id INTEGER PRIMARY KEY,
  reading_id INTEGER NOT NULL UNIQUE REFERENCES readings(id) ON DELETE RESTRICT,
  retry_count INTEGER NOT NULL DEFAULT 0 CHECK(retry_count >= 0),
  last_attempt_at TEXT,
  last_error TEXT,
  created_at TEXT NOT NULL
);
-- next
CREATE TABLE printer_settings (
  id INTEGER PRIMARY KEY CHECK(id = 1),
  device_address TEXT NOT NULL,
  device_name TEXT NOT NULL,
  printer_type TEXT NOT NULL DEFAULT 'escpos',
  paper_width INTEGER NOT NULL DEFAULT 58 CHECK(paper_width IN (58,80))
);
-- next
CREATE TRIGGER protect_unsynced_reading
BEFORE DELETE ON readings
WHEN OLD.sync_status != 'synced'
BEGIN
  SELECT RAISE(ABORT, 'Unsynchronized readings must be retained');
END;
