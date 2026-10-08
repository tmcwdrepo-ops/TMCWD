-- Run explicitly with payment writers stopped; never a startup migration.
-- One receipt covers multiple bill-level rows: reference uniqueness belongs to the header.
SET @receipt_previous_mode = @@SESSION.sql_mode;
SET SESSION sql_mode = CONCAT(@@SESSION.sql_mode, ',STRICT_ALL_TABLES');
CREATE TABLE IF NOT EXISTS payment_receipts (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    PaymentReference VARCHAR(200) NULL,
    CreatedBy BIGINT NOT NULL,
    Method INT NOT NULL,
    DateCreated DATETIME(6) NOT NULL,
    ExternalReference VARCHAR(200) NULL,
    UNIQUE KEY UQ_payment_receipts_reference (PaymentReference)
) ENGINE=InnoDB;
SET @receipt_ddl = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='payments' AND column_name='BalanceBefore'), 'SELECT 1', 'ALTER TABLE payments ADD BalanceBefore DECIMAL(65,30) NULL');
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
SET @receipt_ddl = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='payments' AND column_name='ChargesOnly'), 'SELECT 1', 'ALTER TABLE payments ADD ChargesOnly TINYINT(1) NOT NULL DEFAULT 0');
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
SET @receipt_ddl = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='other_charges' AND column_name='SettledPaymentId'), 'SELECT 1', 'ALTER TABLE other_charges ADD SettledPaymentId BIGINT NULL');
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
SET @receipt_ddl = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='penalties' AND column_name='SettledPaymentId'), 'SELECT 1', 'ALTER TABLE penalties ADD SettledPaymentId BIGINT NULL');
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
-- Strict-mode NOT NULL rejects ambiguous legacy references (mixed cashiers/methods).
-- Investigate those groups rather than silently assigning their receipt to one cashier.
INSERT INTO payment_receipts (PaymentReference,CreatedBy,Method,DateCreated)
SELECT p.PaymentReference,
       CASE WHEN COUNT(DISTINCT p.CreatedBy)=1 AND COUNT(DISTINCT p.Method)=1 THEN MIN(p.CreatedBy) ELSE NULL END,
       MIN(p.Method),MIN(p.DateCreated)
FROM payments p WHERE NOT EXISTS (SELECT 1 FROM payment_receipts r WHERE r.PaymentReference=p.PaymentReference)
GROUP BY p.PaymentReference;
-- Deployment initialization only. Runtime uses MySQL AUTO_INCREMENT, never MAX+1.
SELECT GREATEST(COALESCE(MAX(Id),0),COALESCE(MAX(CASE WHEN PaymentReference REGEXP '^CR[0-9]{1,18}$'
       THEN CAST(SUBSTRING(PaymentReference,3) AS UNSIGNED) ELSE 0 END),0)) + 1 INTO @receipt_next_serial FROM payment_receipts;
SET @receipt_ddl=CONCAT('ALTER TABLE payment_receipts AUTO_INCREMENT = ',@receipt_next_serial);
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
-- Narrow reference locks; prefix indexes also support legacy TEXT columns without truncation.
SET @receipt_index_length = LEAST(191,(SELECT CHARACTER_MAXIMUM_LENGTH FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='Billings' AND column_name='BillingReferenceId'));
SET @receipt_ddl = IF(EXISTS(SELECT 1 FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='Billings' AND index_name='IX_Billings_collection_reference'), 'SELECT 1', CONCAT('CREATE INDEX IX_Billings_collection_reference ON Billings (BillingReferenceId(',@receipt_index_length,'))'));
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
SET @receipt_index_length = LEAST(191,(SELECT CHARACTER_MAXIMUM_LENGTH FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='payments' AND column_name='PaymentReference'));
SET @receipt_ddl = IF(EXISTS(SELECT 1 FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='payments' AND index_name='IX_payments_receipt_reference'), 'SELECT 1', CONCAT('CREATE INDEX IX_payments_receipt_reference ON payments (PaymentReference(',@receipt_index_length,'))'));
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
SET @receipt_index_length = LEAST(191,(SELECT CHARACTER_MAXIMUM_LENGTH FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='penalties' AND column_name='BillingReferenceId'));
SET @receipt_ddl = IF(EXISTS(SELECT 1 FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='penalties' AND index_name='IX_penalties_collection_reference'), 'SELECT 1', CONCAT('CREATE INDEX IX_penalties_collection_reference ON penalties (BillingReferenceId(',@receipt_index_length,'))'));
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
SET @receipt_index_length = LEAST(191,(SELECT CHARACTER_MAXIMUM_LENGTH FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='other_charges' AND column_name='BillingReferenceId'));
SET @receipt_ddl = IF(EXISTS(SELECT 1 FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='other_charges' AND index_name='IX_other_charges_collection_reference'), 'SELECT 1', CONCAT('CREATE INDEX IX_other_charges_collection_reference ON other_charges (BillingReferenceId(',@receipt_index_length,'))'));
PREPARE receipt_statement FROM @receipt_ddl;
EXECUTE receipt_statement;
DEALLOCATE PREPARE receipt_statement;
SET SESSION sql_mode = @receipt_previous_mode;
