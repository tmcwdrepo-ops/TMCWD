-- Batch-payment upload log
-- Each row tracks one CSV file uploaded through the Batch Payments modal.
CREATE TABLE IF NOT EXISTS `batch_payment_uploads` (
  `Id`           BIGINT          NOT NULL AUTO_INCREMENT,
  `Filename`     VARCHAR(260)    NOT NULL,
  `Channel`      VARCHAR(100)    NOT NULL DEFAULT '',
  `Status`       VARCHAR(50)     NOT NULL DEFAULT 'Processed',
  `RowCount`     INT             NOT NULL DEFAULT 0,
  `CreatedBy`    BIGINT          NOT NULL,
  `DateCreated`  DATETIME        NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
