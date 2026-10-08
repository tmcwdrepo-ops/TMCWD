-- Additive arrears field for the authoritative prior unpaid/overdue water bill total.
-- Historical rows remain NULL unless the prior snapshots are valid and authoritatively eligible.
SET @bill_arrears_sql = IF(
  EXISTS(SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'Billings' AND column_name = 'BillArrearsAmount'),
  'SELECT 1',
  'ALTER TABLE `Billings` ADD COLUMN `BillArrearsAmount` DECIMAL(18,2) NULL'
);
PREPARE bill_arrears_stmt FROM @bill_arrears_sql;
EXECUTE bill_arrears_stmt;
DEALLOCATE PREPARE bill_arrears_stmt;
