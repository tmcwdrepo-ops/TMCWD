-- Additive, preserves historical rows. Run against the selected TMCWD database
-- before deploying the Phase 4B API. Safe to rerun on MySQL.
SET @reading_remarks_sql = IF(
  EXISTS(SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'readings' AND column_name = 'Remarks'),
  'SELECT 1',
  'ALTER TABLE readings ADD COLUMN Remarks VARCHAR(2000) NULL');
PREPARE reading_remarks_stmt FROM @reading_remarks_sql;
EXECUTE reading_remarks_stmt;
DEALLOCATE PREPARE reading_remarks_stmt;
