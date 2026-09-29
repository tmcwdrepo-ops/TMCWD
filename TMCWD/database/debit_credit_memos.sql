CREATE TABLE IF NOT EXISTS debit_credit_memos (
    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    AccountNumber VARCHAR(100) NOT NULL,
    DcmType VARCHAR(150) NOT NULL,
    MemoType VARCHAR(10) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    DcmDate DATE NOT NULL,
    BillMonthBasis DATE NULL,
    Explanation VARCHAR(1000) NOT NULL,
    Remarks VARCHAR(1000) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    CreatedBy BIGINT NOT NULL,
    DateCreated DATETIME NOT NULL,
    UpdatedBy BIGINT NOT NULL,
    DateUpdated DATETIME NOT NULL,
    INDEX IX_debit_credit_memos_account (AccountNumber),
    INDEX IX_debit_credit_memos_date (DcmDate)
);
