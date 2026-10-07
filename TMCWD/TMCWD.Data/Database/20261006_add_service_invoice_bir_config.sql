-- Phase 6D: Add ServiceInvoiceNumber to Billings and create bir_invoice_configs table.
-- Additive only. Never drops or renames existing columns.
-- Safe to rerun on MySQL (all DDL is guarded with IF NOT EXISTS or SELECT 1).

-- ─── 1. ServiceInvoiceNumber column on Billings ──────────────────────────────
-- Format: configured InvoicePrefix + serial padded to SeriesWidth (or the digits in SeriesTo).
-- NULLable so historical rows that pre-date Phase 6D are untouched initially.
-- A UNIQUE index is added after the column exists.

SET @add_si_col = IF(
    EXISTS(SELECT 1 FROM information_schema.columns
           WHERE table_schema = DATABASE()
             AND table_name   = 'Billings'
             AND column_name  = 'ServiceInvoiceNumber'),
    'SELECT 1',
    'ALTER TABLE `Billings`
       ADD COLUMN `ServiceInvoiceNumber` VARCHAR(30) NULL
         COMMENT ''Customer-facing service invoice number. Assigned by PartialPost. NULL for pre-Phase-6D rows.'''
);
PREPARE _stmt FROM @add_si_col;
EXECUTE _stmt;
DEALLOCATE PREPARE _stmt;

-- Unique index (safe to rerun — CREATE UNIQUE INDEX IF NOT EXISTS requires MySQL 8+).
SET @add_si_idx = IF(
    EXISTS(SELECT 1 FROM information_schema.statistics
           WHERE table_schema = DATABASE()
             AND table_name   = 'Billings'
             AND index_name   = 'UQ_Billings_ServiceInvoiceNumber'),
    'SELECT 1',
    'CREATE UNIQUE INDEX `UQ_Billings_ServiceInvoiceNumber`
       ON `Billings` (`ServiceInvoiceNumber`)'
);
PREPARE _stmt FROM @add_si_idx;
EXECUTE _stmt;
DEALLOCATE PREPARE _stmt;


-- ─── 2. bir_invoice_configs table ────────────────────────────────────────────
-- Stores BIR PTU registration data used when printing invoices.
-- Exactly one row should have IsActive = 1 at any time; enforced by application.
CREATE TABLE IF NOT EXISTS `bir_invoice_configs` (
    `Id`              BIGINT         NOT NULL AUTO_INCREMENT,
    `PTUNumber`       VARCHAR(100)   NOT NULL  COMMENT 'BIR PTU Number',
    `PermitDateIssued` DATE          NOT NULL  COMMENT 'BIR permit / date issued',
    `SeriesFrom`      BIGINT         NOT NULL  COMMENT 'Inclusive start of authorised series',
    `SeriesTo`        BIGINT         NOT NULL  COMMENT 'Inclusive end of authorised series',
    `InvoicePrefix`   VARCHAR(10)    NULL,
    `SeriesWidth`     INT            NULL,
    `IsActive`        TINYINT(1)     NOT NULL  DEFAULT 0,
    `EffectiveFrom`   DATE           NULL      COMMENT 'Optional: earliest billing date this config covers',
    `EffectiveTo`     DATE           NULL      COMMENT 'Optional: last billing date this config covers',
    `CreatedBy`       BIGINT         NOT NULL  DEFAULT 0,
    `DateCreated`     DATETIME       NOT NULL  DEFAULT CURRENT_TIMESTAMP,
    `UpdatedBy`       BIGINT         NOT NULL  DEFAULT 0,
    `DateUpdated`     DATETIME       NOT NULL  DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
  COMMENT 'BIR invoice configuration (PTU, series range). One active row at a time.';


-- ─── 3. Sequence helper table for ServiceInvoiceNumber ───────────────────────
-- One counter per BIR configuration, allocated inside the posting transaction.
CREATE TABLE IF NOT EXISTS `service_invoice_seq` (
    `Id`          BIGINT   NOT NULL COMMENT 'BIR configuration ID',
    `LastValue`   BIGINT   NOT NULL DEFAULT 0
        COMMENT 'The last successfully assigned sequence value. Next = LastValue + 1.',
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- First use seeds each config at SeriesFrom - 1. No global seed.


-- ─── 4. Backfill strategy for historical Billings rows ───────────────────────
-- Historical rows (pre-Phase-6D) have ServiceInvoiceNumber = NULL.
-- We intentionally leave them NULL rather than fabricating official invoice
-- numbers. The application treats NULL as "invoice not assigned" and omits
-- the field from SOA responses for such bills.
-- No automatic historical backfill is performed.

SET @add_config_col = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'bir_invoice_configs' AND column_name = 'InvoicePrefix'), 'SELECT 1', 'ALTER TABLE bir_invoice_configs ADD COLUMN InvoicePrefix VARCHAR(10) NULL');
PREPARE _stmt FROM @add_config_col;
EXECUTE _stmt;
DEALLOCATE PREPARE _stmt;

SET @add_config_col = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'bir_invoice_configs' AND column_name = 'SeriesWidth'), 'SELECT 1', 'ALTER TABLE bir_invoice_configs ADD COLUMN SeriesWidth INT NULL');
PREPARE _stmt FROM @add_config_col;
EXECUTE _stmt;
DEALLOCATE PREPARE _stmt;

ALTER TABLE service_invoice_seq MODIFY COLUMN Id BIGINT NOT NULL COMMENT 'BIR configuration ID';
