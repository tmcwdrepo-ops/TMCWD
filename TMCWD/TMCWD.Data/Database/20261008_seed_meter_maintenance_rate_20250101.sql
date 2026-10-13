-- CURRENT INITIAL SEED: stakeholder amendment dated 2026-10-08 replaces the 2026-01-01 approval.
-- Use only this 2025-01-01 artifact after separate deployment authorization; never run both initial seeds.
-- SEPARATE INITIAL DATA SEED — NOT A SCHEMA MIGRATION.
-- Do not execute until deployment to the selected database is separately authorized.
-- Requires meter_maintenance_rates to exist with:
--   Id, Amount, EffectiveFrom, EffectiveTo, CreatedBy, DateCreated, UpdatedBy, DateUpdated.
-- Set @authorized_service_user_id to an active, verified SuperAdmin user before running.
-- This script only inserts the exact approved initial row:
--   Amount = 20.00, EffectiveFrom = 2025-01-01, EffectiveTo = NULL.
-- It never updates or deletes configuration or historical financial records.

SET @meter_maintenance_seed_actor_id = @authorized_service_user_id;

DELIMITER //
DROP PROCEDURE IF EXISTS `_seed_meter_maintenance_rate_20250101`//
CREATE PROCEDURE `_seed_meter_maintenance_rate_20250101`()
BEGIN
    DECLARE lock_acquired INT DEFAULT 0;
    DECLARE exact_count INT DEFAULT 0;
    DECLARE conflict_count INT DEFAULT 0;
    DECLARE actor_count INT DEFAULT 0;
    DECLARE seed_actor_id BIGINT;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        IF lock_acquired = 1 THEN
            DO RELEASE_LOCK('tmcwd:meter_maintenance_rates');
        END IF;
        RESIGNAL;
    END;

    SET seed_actor_id = @meter_maintenance_seed_actor_id;
    IF seed_actor_id IS NULL OR seed_actor_id <= 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Set @authorized_service_user_id to an active, verified SuperAdmin before seeding.';
    END IF;

    SELECT COUNT(*) INTO actor_count
    FROM `users`
    WHERE `Id` = seed_actor_id
      AND `Role` = 1
      AND `IsActive` = 1
      AND `IsVerified` = 1;

    IF actor_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'The seed audit actor must be an active, verified SuperAdmin.';
    END IF;

    SELECT GET_LOCK('tmcwd:meter_maintenance_rates', 10) INTO lock_acquired;
    IF lock_acquired IS NULL OR lock_acquired <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Could not acquire the meter-maintenance configuration lock.';
    END IF;

    START TRANSACTION;

    SELECT COUNT(*) INTO exact_count
    FROM `meter_maintenance_rates`
    WHERE `Amount` = 20.00
      AND `EffectiveFrom` = '2025-01-01'
      AND `EffectiveTo` IS NULL;

    IF exact_count > 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Multiple exact initial meter-maintenance rows exist; manual review required.';
    END IF;

    -- The approved seed is open-ended. Any other interval extending to or
    -- beyond 2025-01-01 overlaps it and must be resolved by an administrator.
    SELECT COUNT(*) INTO conflict_count
    FROM `meter_maintenance_rates`
    WHERE NOT (
            `Amount` = 20.00
        AND `EffectiveFrom` = '2025-01-01'
        AND `EffectiveTo` IS NULL
    )
      AND (
            `EffectiveFrom` = '2025-01-01'
         OR `EffectiveTo` IS NULL
         OR `EffectiveTo` >= '2025-01-01'
      );

    IF conflict_count > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Conflicting or overlapping meter-maintenance configuration exists; no row was changed.';
    END IF;

    IF exact_count = 0 THEN
        INSERT INTO `meter_maintenance_rates`
            (`Amount`, `EffectiveFrom`, `EffectiveTo`,
             `CreatedBy`, `DateCreated`, `UpdatedBy`, `DateUpdated`)
        VALUES
            (20.00, '2025-01-01', NULL,
             seed_actor_id, CURRENT_TIMESTAMP, seed_actor_id, CURRENT_TIMESTAMP);
        SELECT 'SEEDED' AS `SeedResult`;
    ELSE
        SELECT 'ALREADY_PRESENT' AS `SeedResult`;
    END IF;

    COMMIT;
    DO RELEASE_LOCK('tmcwd:meter_maintenance_rates');
    SET lock_acquired = 0;
END//

CALL `_seed_meter_maintenance_rate_20250101`()//
DROP PROCEDURE `_seed_meter_maintenance_rate_20250101`//
DELIMITER ;

