SET @has_is_cancelled = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'senior_citizen_discounts'
      AND column_name = 'IsCancelled'
);
SET @add_is_cancelled = IF(@has_is_cancelled = 0,
    'ALTER TABLE `senior_citizen_discounts` ADD COLUMN `IsCancelled` TINYINT(1) NOT NULL DEFAULT 0',
    'SELECT 1');
PREPARE add_is_cancelled_statement FROM @add_is_cancelled;
EXECUTE add_is_cancelled_statement;
DEALLOCATE PREPARE add_is_cancelled_statement;

SET @has_cancelled_by = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'senior_citizen_discounts'
      AND column_name = 'CancelledBy'
);
SET @add_cancelled_by = IF(@has_cancelled_by = 0,
    'ALTER TABLE `senior_citizen_discounts` ADD COLUMN `CancelledBy` BIGINT NOT NULL DEFAULT 0',
    'SELECT 1');
PREPARE add_cancelled_by_statement FROM @add_cancelled_by;
EXECUTE add_cancelled_by_statement;
DEALLOCATE PREPARE add_cancelled_by_statement;

SET @has_date_cancelled = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'senior_citizen_discounts'
      AND column_name = 'DateCancelled'
);
SET @add_date_cancelled = IF(@has_date_cancelled = 0,
    'ALTER TABLE `senior_citizen_discounts` ADD COLUMN `DateCancelled` DATETIME(6) NULL',
    'SELECT 1');
PREPARE add_date_cancelled_statement FROM @add_date_cancelled;
EXECUTE add_date_cancelled_statement;
DEALLOCATE PREPARE add_date_cancelled_statement;

SET @has_unique_discount_index = (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'senior_citizen_discounts'
      AND index_name = 'UX_SeniorCitizenDiscount_BillingReference'
);
SET @drop_unique_discount_index = IF(
    @has_unique_discount_index > 0,
    'ALTER TABLE `senior_citizen_discounts` DROP INDEX `UX_SeniorCitizenDiscount_BillingReference`',
    'SELECT 1'
);
PREPARE discount_index_statement FROM @drop_unique_discount_index;
EXECUTE discount_index_statement;
DEALLOCATE PREPARE discount_index_statement;

SET @has_discount_index = (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'senior_citizen_discounts'
      AND index_name = 'IX_SeniorCitizenDiscount_BillingReference'
);
SET @create_discount_index = IF(
    @has_discount_index = 0,
    'CREATE INDEX `IX_SeniorCitizenDiscount_BillingReference` ON `senior_citizen_discounts` (`BillingReferenceId`)',
    'SELECT 1'
);
PREPARE create_discount_index_statement FROM @create_discount_index;
EXECUTE create_discount_index_statement;
DEALLOCATE PREPARE create_discount_index_statement;

CREATE TABLE IF NOT EXISTS `invoice_cancellations` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT,
    `PaymentReference` VARCHAR(200) NOT NULL,
    `Reason` VARCHAR(200) NOT NULL,
    `CancelledBy` BIGINT NOT NULL,
    `DateCancelled` DATETIME(6) NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UX_InvoiceCancellation_PaymentReference` (`PaymentReference`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
