-- Check the readings status for all reading sheets
SELECT 
    r.Id as ReadingId,
    r.ReadingSheetId,
    rs.Name as SheetName,
    zb.Zone,
    zb.Book,
    r.Status,
    CASE 
        WHEN r.Status = 0 THEN 'Created'
        WHEN r.Status = 1 THEN 'InProgress'
        WHEN r.Status = 2 THEN 'Completed'
        ELSE 'Unknown'
    END as StatusName,
    r.IsCompleted,
    r.DateUpdated
FROM readings r
INNER JOIN reading_sheets rs ON r.ReadingSheetId = rs.Id
LEFT JOIN zones_books zb ON rs.ZoneBookId = zb.Id
WHERE r.ReadingSheetId IN (170, 171, 172, 173, 174, 175, 176, 177, 178, 179)
ORDER BY zb.Zone, r.Id;
