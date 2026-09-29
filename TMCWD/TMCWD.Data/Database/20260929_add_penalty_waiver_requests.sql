CREATE TABLE IF NOT EXISTS `penalty_waiver_requests` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT,
  `BillingReferenceId` VARCHAR(200) NOT NULL,
  `Reason` VARCHAR(500) NOT NULL,
  `Status` INT NOT NULL,
  `RequestedBy` BIGINT NOT NULL,
  `DateRequested` DATETIME(6) NOT NULL,
  `ReviewedBy` BIGINT NULL,
  `DateReviewed` DATETIME(6) NULL,
  `ReviewRemarks` VARCHAR(500) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_penalty_waiver_requests_Status` (`Status`),
  INDEX `IX_penalty_waiver_requests_BillingReferenceId` (`BillingReferenceId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
