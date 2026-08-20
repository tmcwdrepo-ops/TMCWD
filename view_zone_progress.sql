-- View zone progress with detailed reading status
SELECT 
    zb.Zone,
    rs.Id AS ReadingSheetId,
    rs.Name AS SheetName,
    COUNT(r.Id) AS TotalReadings,
    SUM(CASE WHEN r.Status = 2 THEN 1 ELSE 0 END) AS CompletedReadings,
    SUM(CASE WHEN r.Status = 1 THEN 1 ELSE 0 END) AS InProgressReadings,
    SUM(CASE WHEN r.Status = 0 THEN 1 ELSE 0 END) AS CreatedReadings,
    CONCAT(
        SUM(CASE WHEN r.Status = 2 THEN 1 ELSE 0 END), 
        ' / ', 
        COUNT(r.Id),
        ' (', 
        ROUND(SUM(CASE WHEN r.Status = 2 THEN 1 ELSE 0 END) / COUNT(r.Id) * 100, 0),
        '%)'
    ) AS Progress
FROM zones_books zb
INNER JOIN reading_sheets rs ON zb.Id = rs.ZoneBookId
LEFT JOIN readings r ON r.ReadingSheetId = rs.Id
GROUP BY zb.Zone, rs.Id, rs.Name
ORDER BY zb.Zone, rs.Id;

-- Summary by zone
SELECT 
    zb.Zone,
    COUNT(DISTINCT rs.Id) AS TotalSheets,
    COUNT(r.Id) AS TotalReadings,
    SUM(CASE WHEN r.Status = 2 THEN 1 ELSE 0 END) AS CompletedReadings,
    CONCAT(
        SUM(CASE WHEN r.Status = 2 THEN 1 ELSE 0 END), 
        ' / ', 
        COUNT(r.Id),
        ' (', 
        ROUND(SUM(CASE WHEN r.Status = 2 THEN 1 ELSE 0 END) / COUNT(r.Id) * 100, 0),
        '%)'
    ) AS ZoneProgress
FROM zones_books zb
INNER JOIN reading_sheets rs ON zb.Id = rs.ZoneBookId
LEFT JOIN readings r ON r.ReadingSheetId = rs.Id
GROUP BY zb.Zone
ORDER BY zb.Zone;

-- View individual readings with status
SELECT 
    r.Id AS ReadingId,
    r.ReadingSheetId,
    zb.Zone,
    rs.Name AS SheetName,
    r.AccountId,
    r.Status,
    CASE 
        WHEN r.Status = 0 THEN 'Created'
        WHEN r.Status = 1 THEN 'InProgress'
        WHEN r.Status = 2 THEN 'Completed'
        ELSE 'Unknown'
    END AS StatusName,
    r.IsCompleted,
    r.DateUpdated
FROM readings r
INNER JOIN reading_sheets rs ON r.ReadingSheetId = rs.Id
LEFT JOIN zones_books zb ON rs.ZoneBookId = zb.Id
ORDER BY zb.Zone, r.ReadingSheetId, r.Id;
