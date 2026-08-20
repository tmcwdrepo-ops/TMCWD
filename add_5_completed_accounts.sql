-- Add 5 completed accounts (readings) to Zone 1
-- This will create 5 new reading records with Status = 2 (Completed)

-- First, let's find a reading sheet in Zone 1
SET @readingSheetId = (
    SELECT rs.Id 
    FROM reading_sheets rs
    INNER JOIN zones_books zb ON rs.ZoneBookId = zb.Id
    WHERE zb.Zone = 1
    LIMIT 1
);

-- Insert 5 completed readings
-- Note: You'll need to provide real AccountIds from your accounts table
INSERT INTO readings (AccountId, ReadingSheetId, Reading, PreviousReading, IsCompleted, Status, CreatedBy, DateCreated, DateUpdated)
VALUES
    (1, @readingSheetId, 100.00, 90.00, 1, 2, 1, NOW(), NOW()),
    (2, @readingSheetId, 150.00, 140.00, 1, 2, 1, NOW(), NOW()),
    (3, @readingSheetId, 200.00, 180.00, 1, 2, 1, NOW(), NOW()),
    (4, @readingSheetId, 120.00, 110.00, 1, 2, 1, NOW(), NOW()),
    (5, @readingSheetId, 180.00, 170.00, 1, 2, 1, NOW(), NOW());

-- Verify the additions
SELECT 
    zb.Zone,
    rs.Id AS ReadingSheetId,
    COUNT(r.Id) AS TotalReadings,
    SUM(CASE WHEN r.Status = 2 THEN 1 ELSE 0 END) AS CompletedReadings,
    CONCAT(
        SUM(CASE WHEN r.Status = 2 THEN 1 ELSE 0 END), 
        ' / ', 
        COUNT(r.Id)
    ) AS Progress
FROM reading_sheets rs
INNER JOIN zones_books zb ON rs.ZoneBookId = zb.Id
LEFT JOIN readings r ON r.ReadingSheetId = rs.Id
WHERE zb.Zone = 1
GROUP BY zb.Zone, rs.Id
ORDER BY rs.Id;
