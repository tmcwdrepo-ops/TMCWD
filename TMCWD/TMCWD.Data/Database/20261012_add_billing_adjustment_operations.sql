-- Additive idempotency receipts. Apply to tmcwd_test for C1 verification only.
-- Development tmcwd and production require separate rollout authorization.
-- Rerun does not repair a differently defined preexisting table.
CREATE TABLE IF NOT EXISTS `billing_adjustment_operations` (
    `OperationId` VARCHAR(36) NOT NULL,
    `BillingReferenceId` VARCHAR(50) NOT NULL,
    `ActorId` BIGINT NOT NULL,
    `PayloadHash` CHAR(64) NOT NULL,
    `ResultJson` LONGTEXT NOT NULL,
    `DateCreated` DATETIME(6) NOT NULL,
    PRIMARY KEY (`OperationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
