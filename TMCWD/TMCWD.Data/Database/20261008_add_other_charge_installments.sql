-- Phase 6G: explicit deployment only; no legacy conversion or backfill.
-- Inspected parent/payment IDs: signed BIGINT; PayableIn: nullable VARCHAR(50).
SET @installment_ddl = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='other_charges' AND column_name='InstallmentCount'), 'SELECT 1', 'ALTER TABLE other_charges ADD COLUMN InstallmentCount INT NULL');
PREPARE installment_stmt FROM @installment_ddl;
EXECUTE installment_stmt;
DEALLOCATE PREPARE installment_stmt;

CREATE TABLE IF NOT EXISTS other_charge_installments (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    OtherChargeId BIGINT NOT NULL,
    InstallmentNumber INT NOT NULL,
    DueBillingPeriod DATETIME NOT NULL,
    Amount DECIMAL(6,2) NOT NULL,
    PaymentStatus INT NOT NULL,
    SettledPaymentId BIGINT NULL,
    ActivatedBillingReferenceId VARCHAR(50) NULL,
    ActivatedAt DATETIME NULL,
    CreatedBy BIGINT NOT NULL,
    DateCreated DATETIME NOT NULL,
    UpdatedBy BIGINT NOT NULL,
    DateUpdated DATETIME NOT NULL,
    UNIQUE KEY UQ_other_charge_installment_sequence (OtherChargeId, InstallmentNumber),
    KEY IX_other_charge_installment_due (PaymentStatus, DueBillingPeriod),
    KEY IX_other_charge_installment_payment (SettledPaymentId)
);
-- No foreign keys: existing other_charges/payment links use service-enforced integrity.
