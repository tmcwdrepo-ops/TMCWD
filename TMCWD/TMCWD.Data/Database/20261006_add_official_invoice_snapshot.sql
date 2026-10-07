-- Additive official invoice snapshot. Historical rows remain NULL.
-- Apply explicitly; never an application startup migration.
SET @invoice_snapshot_sql = IF(
  EXISTS(SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'Billings' AND column_name = 'InvoiceSnapshotJson'),
  'SELECT 1',
  'ALTER TABLE `Billings` ADD COLUMN `InvoiceSnapshotJson` LONGTEXT NULL'
);
PREPARE invoice_snapshot_stmt FROM @invoice_snapshot_sql;
EXECUTE invoice_snapshot_stmt;
DEALLOCATE PREPARE invoice_snapshot_stmt;
