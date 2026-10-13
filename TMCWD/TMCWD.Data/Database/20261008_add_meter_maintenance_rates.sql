-- Additive schema only. Keep the approved initial rate in the separate guarded seed script.
-- Do not run this migration against development or production without deployment authorization.
CREATE TABLE IF NOT EXISTS `meter_maintenance_rates` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT,
    `Amount` DECIMAL(12,2) NOT NULL,
    `EffectiveFrom` DATE NOT NULL,
    `EffectiveTo` DATE NULL,
    `CreatedBy` BIGINT NOT NULL,
    `DateCreated` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `UpdatedBy` BIGINT NOT NULL,
    `DateUpdated` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_meter_maintenance_rates_effective_from` (`EffectiveFrom`),
    KEY `IX_meter_maintenance_rates_effective_interval` (`EffectiveFrom`, `EffectiveTo`),
    CONSTRAINT `CK_meter_maintenance_rates_amount` CHECK (`Amount` >= 0),
    CONSTRAINT `CK_meter_maintenance_rates_interval` CHECK (`EffectiveTo` IS NULL OR `EffectiveTo` >= `EffectiveFrom`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
  COMMENT='Effective-dated monthly Meter Maintenance billing configuration';
