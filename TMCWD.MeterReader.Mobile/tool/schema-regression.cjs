// Runs the exact bundled SQLite schema with synthetic data; Node 22.13+.
const assert = require('node:assert/strict');
const { DatabaseSync } = require('node:sqlite');
const { readFileSync, mkdtempSync, rmSync } = require('node:fs');
const { tmpdir } = require('node:os');
const { join } = require('node:path');
const schema = readFileSync(join(__dirname, '../lib/data/local/schema.sql'), 'utf8');
const directory = mkdtempSync(join(tmpdir(), 'tmcwd-mobile-'));
const path = join(directory, 'local.db');
let db;
try {
  db = new DatabaseSync(path);
  db.exec('PRAGMA foreign_keys=ON');
  for (const statement of schema.split('-- next')) db.exec(statement);
  const insertSheet = db.prepare(`INSERT INTO reading_sheets
    (id,server_id,name,zone_book_id,zone,book,assigned_to,status,downloaded_at)
    VALUES(?,?,?,?,?,?,?,?,?)`);
  insertSheet.run(1,15,'Synthetic',2,'3','2',7,1,'2026-10-05T00:00:00Z');
  insertSheet.run(2,16,'Synthetic second',2,'3','2',7,1,'2026-10-05T00:00:00Z');
  assert.throws(() => insertSheet.run(3,17,'Bad',2,'3','2',7,5,'now'));
  db.prepare(`INSERT INTO accounts
    (id,server_account_id,reading_sheet_id,account_number,customer_name,address,
     meter_number,classification,meter_size,previous_reading,previous_balance,sequence_number)
    VALUES(1,101,1,'TEST-101','Synthetic','Test address','TEST-METER',1,'0.5','542.25','123.45',1)`).run();
  const insertReading = db.prepare(`INSERT INTO readings
    (local_uuid,reading_sheet_id,account_id,previous_reading,current_reading,
     consumption,reading_date,meter_condition,created_at,updated_at)
    VALUES(?,?,?,?,?,?,?,?,?,?)`);
  const uuid = 'a1420e6c-9ef9-4bc4-8df0-191a6659dd01';
  const args = [uuid,1,1,'542.25','570.25','28','2026-10-05','normal','now','now'];
  // Queue and reading must be able to commit atomically.
  db.exec('BEGIN');
  insertReading.run(...args);
  db.prepare('INSERT INTO sync_queue(reading_id,created_at) VALUES(1,?)').run('now');
  db.exec('COMMIT');
  assert.throws(() => insertReading.run(...args), /UNIQUE/);
  assert.throws(() => insertReading.run('a1420e6c-9ef9-4bc4-8df0-191a6659dd02',...args.slice(1)), /UNIQUE/);
  assert.throws(() => insertReading.run('a1420e6c-9ef9-4bc4-8df0-191a6659dd03',2,...args.slice(2)), /FOREIGN KEY/);
  assert.throws(() => db.exec('DELETE FROM readings'), /Unsynchronized/);
  assert.throws(() => db.exec('DELETE FROM reading_sheets WHERE id=1'), /FOREIGN KEY/);
  assert.throws(() => db.exec("UPDATE readings SET sync_status='lost'"), /CHECK/);
  // A failure in a multi-write operation leaves neither partial data nor lost data.
  db.exec('BEGIN');
  db.exec("UPDATE readings SET remarks='temporary'");
  assert.throws(() => db.exec('INSERT INTO sync_queue(reading_id,created_at) VALUES(1,\'now\')'));
  db.exec('ROLLBACK');
  assert.equal(db.prepare('SELECT remarks FROM readings').get().remarks, '');
  db.exec(`INSERT INTO billing_rates VALUES(1,1,1,'0.5','100.10','12.3456',
    '13.4567','14.5678','15.6789','2026-01-01',1)`);
  db.exec(`INSERT INTO bills
    (reading_id,billing_period,billing_date,due_date,water_charge,previous_balance,
     penalties,other_charges,total_amount_due,created_at)
    VALUES(1,'2026-10','2026-10-05','2026-10-20','150.10','123.45','0','0','273.55','now')`);
  assert.throws(() => db.exec("INSERT INTO printer_settings VALUES(1,'TEST','Test','escpos',72)"), /CHECK/);
  db.exec("INSERT INTO printer_settings VALUES(1,'TEST','Test','escpos',58)");
  db.exec("UPDATE printer_settings SET paper_width=80");
  db.close();
  db = new DatabaseSync(path);
  assert.equal(db.prepare('SELECT previous_balance FROM accounts').get().previous_balance, '123.45');
  assert.equal(db.prepare('SELECT rate11_to20 FROM billing_rates').get().rate11_to20, '12.3456');
  assert.equal(db.prepare('SELECT sync_status FROM readings').get().sync_status, 'pending');
  assert.equal(db.prepare('SELECT COUNT(*) AS n FROM sync_queue').get().n, 1);
  assert.equal(db.prepare('SELECT total_amount_due FROM bills').get().total_amount_due, '273.55');
  console.log('PASS: schema, constraints, retention, atomic rollback, decimal precision, persistence.');
} finally {
  db?.close();
  rmSync(directory, { recursive: true, force: true });
}

