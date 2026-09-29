CREATE TABLE IF NOT EXISTS `senior_citizen_discounts` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT,
    `BillingReferenceId` VARCHAR(200) NOT NULL,
    `SeniorCitizenIdNumber` VARCHAR(30) NOT NULL,
    `SeniorCitizenName` VARCHAR(160) NOT NULL,
    `AccountHolderName` VARCHAR(160) NOT NULL,
    `Rate` DECIMAL(5,4) NOT NULL,
    `WaterCharge` DECIMAL(12,2) NOT NULL,
    `DiscountAmount` DECIMAL(12,2) NOT NULL,
    `CreatedBy` BIGINT NOT NULL,
    `DateCreated` DATETIME(6) NOT NULL,
    `IsCancelled` TINYINT(1) NOT NULL DEFAULT 0,
    `CancelledBy` BIGINT NOT NULL DEFAULT 0,
    `DateCancelled` DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_SeniorCitizenDiscount_BillingReference` (`BillingReferenceId`),
    KEY `IX_SeniorCitizenDiscount_IdNumber` (`SeniorCitizenIdNumber`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
