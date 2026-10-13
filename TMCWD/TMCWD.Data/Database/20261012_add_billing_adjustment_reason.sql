-- Additive audit-reason column on billing_adjustments.
-- Allows a free-text reason to be captured at adjustment submission time.
-- Historical rows remain valid with NULL; no backfill of fake data.
-- Apply to tmcwd_test for C1 verification only.
-- Development tmcwd and production require separate rollout authorization.
SET @reason_sql = IF(
    EXISTS(SELECT 1 FROM information_schema.columns
           WHERE table_schema = DATABASE()
             AND table_name   = 'billing_adjustments'
             AND column_name  = 'Reason'),
    'SELECT 1 /* Reason column already present */',
    'ALTER TABLE `billing_adjustments` ADD COLUMN `Reason` VARCHAR(255) NULL AFTER `Amount`'
);
PREPARE reason_stmt FROM @reason_sql;
EXECUTE reason_stmt;
DEALLOCATE PREPARE reason_stmt;
