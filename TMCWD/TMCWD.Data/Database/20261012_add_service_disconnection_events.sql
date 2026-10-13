-- DEVELOPMENT MIGRATION REQUIRED: review/authorize exact target separately.
-- Additive only. Never infer physical events from legacy orders or account status.
CREATE TABLE IF NOT EXISTS `service_disconnection_events` (
  `Id` VARCHAR(36) NOT NULL,
  `AccountId` BIGINT NOT NULL,
  `FirstBillingId` BIGINT NOT NULL,
  `SecondBillingId` BIGINT NOT NULL,
  `FirstBillingReference` VARCHAR(50) NOT NULL,
  `SecondBillingReference` VARCHAR(50) NOT NULL,
  `SubjectDate` DATETIME(6) NOT NULL,
  `ScheduledDisconnectionDate` DATETIME(6) NOT NULL,
  `ActualDisconnectionDateUtc` DATETIME(6) NOT NULL,
  `DisconnectedBy` BIGINT NOT NULL,
  `ReconnectionFeeOtherChargeId` BIGINT NOT NULL,
  `ReconnectionEligibleDateUtc` DATETIME(6) NULL,
  `ActualReconnectionDateUtc` DATETIME(6) NULL,
  `ReconnectedBy` BIGINT NULL,
  `ReconnectionOperationId` VARCHAR(36) NULL,
  `SettlementPaymentIdsJson` LONGTEXT NULL,
  `ActiveAccountId` BIGINT GENERATED ALWAYS AS
    (CASE WHEN `ActualReconnectionDateUtc` IS NULL THEN `AccountId` ELSE NULL END) STORED,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `ux_service_disconnection_active` (`ActiveAccountId`),
  UNIQUE KEY `ux_service_disconnection_source` (`AccountId`, `SecondBillingId`),
  UNIQUE KEY `ux_service_disconnection_fee` (`ReconnectionFeeOtherChargeId`),
  UNIQUE KEY `ux_service_reconnection_operation` (`ReconnectionOperationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
