CREATE TABLE IF NOT EXISTS `account_transfer_orders` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `account_id` BIGINT NOT NULL,
  `job_order_number` VARCHAR(50) NOT NULL,
  `job_order_date` DATETIME NOT NULL,
  `old_account_number` VARCHAR(50) NOT NULL,
  `new_account_number` VARCHAR(50) NOT NULL,
  `zone` INT NOT NULL,
  `book` INT NOT NULL,
  `transfer_date` DATETIME NOT NULL,
  `status` VARCHAR(30) NOT NULL DEFAULT 'Completed',
  `processed_by` BIGINT NOT NULL,
  `created_at` DATETIME NOT NULL,
  `updated_at` DATETIME NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `ux_account_transfer_job_order_number` (`job_order_number`),
  KEY `ix_account_transfer_account_id` (`account_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
