-- Run with tools/ServiceRequestMigration; it skips schema changes already applied.
-- Keep service_request_orders as an archive. New application code does not use it.
-- SCHEMA
ALTER TABLE requests ADD COLUMN IsServiceRequest TINYINT(1) NOT NULL DEFAULT 0;
ALTER TABLE requests ADD COLUMN IsNonCustomer TINYINT(1) NOT NULL DEFAULT 0;
ALTER TABLE requests ADD COLUMN LegacyServiceRequestId BIGINT NULL;
ALTER TABLE requests ADD COLUMN AccountNumber VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE requests ADD COLUMN AccountName VARCHAR(200) NOT NULL DEFAULT '';
ALTER TABLE requests ADD COLUMN Address VARCHAR(500) NOT NULL DEFAULT '';
ALTER TABLE requests ADD COLUMN ReportedBy VARCHAR(200) NOT NULL DEFAULT '';
ALTER TABLE requests ADD COLUMN ContactNumber VARCHAR(50) NOT NULL DEFAULT '';
ALTER TABLE requests ADD COLUMN Source VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE requests ADD COLUMN Name VARCHAR(200) NOT NULL DEFAULT '';
ALTER TABLE requests ADD COLUMN Particulars TEXT NULL;
ALTER TABLE requests ADD UNIQUE INDEX IX_requests_LegacyServiceRequestId (LegacyServiceRequestId);
ALTER TABLE request_details ADD COLUMN JobDescription VARCHAR(255) NOT NULL DEFAULT '';
ALTER TABLE request_details ADD COLUMN JobOrderType VARCHAR(100) NOT NULL DEFAULT '';
ALTER TABLE job_orders ADD COLUMN JobOrderDate DATETIME NULL;
ALTER TABLE job_orders ADD COLUMN ReferredToUserId BIGINT NULL;
ALTER TABLE job_orders ADD COLUMN DateReferred DATETIME NULL;
ALTER TABLE job_orders MODIFY COLUMN JobOrderNumber VARCHAR(50) NOT NULL;

-- DATA
INSERT INTO inspection_types (Name, WithDetail, IsRequiredAccount, IsNew, IsActive, DateCreated, CreatedBy, DateUpdated, UpdatedBy)
SELECT 'Service Request', 0, 0, 0, 1, NOW(), 0, NOW(), 0
WHERE NOT EXISTS (SELECT 1 FROM inspection_types WHERE Name = 'Service Request');

INSERT INTO requests
    (ControlNumber, CustomerId, AccountId, Status, CreatedBy, DateCreated, UpdatedBy, DateUpdated,
     IsServiceRequest, IsNonCustomer, LegacyServiceRequestId, AccountNumber, AccountName,
     Address, ReportedBy, ContactNumber, Source, Name, Particulars)
SELECT CONCAT('SR-LEGACY-', s.id),
    CASE WHEN s.is_non_customer = 0 THEN a.CustomerId ELSE NULL END,
    CASE WHEN s.is_non_customer = 0 THEN s.account_id ELSE NULL END,
    CASE WHEN LOWER(TRIM(s.status)) IN ('done','completed','accomplished','closed') THEN 3
         WHEN LOWER(TRIM(s.status)) IN ('cancelled','rejected') THEN 4 ELSE 2 END,
    s.received_by_user_id, s.created_at, s.received_by_user_id, s.updated_at,
    1, s.is_non_customer, s.id, s.account_number, s.account_name,
    s.address, s.reported_by, s.contact_number, s.source, s.name, COALESCE(s.particulars, '')
FROM service_request_orders s
LEFT JOIN accounts a ON a.Id = s.account_id
WHERE NOT EXISTS (SELECT 1 FROM requests r WHERE r.LegacyServiceRequestId = s.id);

INSERT INTO request_details
    (RequestId, RequestTypeId, InspectionTypeDetail, DateCreated, DateUpdated, JobDescription, JobOrderType)
SELECT r.Id,
    (SELECT t.Id FROM inspection_types t WHERE t.Name IN (s.job_description, 'Service Request')
     ORDER BY (t.Name = s.job_description) DESC, t.Id LIMIT 1),
    '', s.created_at, s.updated_at, s.job_description, s.job_order_type
FROM service_request_orders s
JOIN requests r ON r.LegacyServiceRequestId = s.id
WHERE NOT EXISTS (SELECT 1 FROM request_details d WHERE d.RequestId = r.Id);

INSERT INTO job_orders
    (RequestId, RequestDetailId, JobOrderNumber, HasCharges, Status, CreatedBy, DateCreated,
     UpdatedBy, DateUpdated, JobOrderDate, ReferredToUserId, DateReferred)
SELECT r.Id, d.Id, s.job_order_number, 0,
    CASE WHEN LOWER(TRIM(s.status)) IN ('done','completed','accomplished','closed') THEN 8
         WHEN LOWER(TRIM(s.status)) IN ('cancelled','rejected') THEN 9 ELSE 1 END,
    s.received_by_user_id, s.created_at, s.received_by_user_id, s.updated_at,
    s.job_order_date, s.referred_to_user_id, s.date_referred
FROM service_request_orders s
JOIN requests r ON r.LegacyServiceRequestId = s.id
JOIN request_details d ON d.RequestId = r.Id
WHERE NOT EXISTS (SELECT 1 FROM job_orders j WHERE j.RequestDetailId = d.Id);

-- Installation snapshots contain the old service_request_orders ID. Match by
-- the unchanged job order number so an overlapping requests ID cannot mislink it.
UPDATE water_connection_applications a
JOIN job_orders j ON j.JobOrderNumber = CONVERT(COALESCE(
    JSON_UNQUOTE(JSON_EXTRACT(IF(JSON_VALID(a.installation_data), a.installation_data, '{}'), '$.JobOrderNumber')),
    JSON_UNQUOTE(JSON_EXTRACT(IF(JSON_VALID(a.installation_data), a.installation_data, '{}'), '$.jobOrderNumber'))
    ) USING utf8mb4) COLLATE utf8mb4_0900_ai_ci
JOIN requests r ON r.Id = j.RequestId AND r.LegacyServiceRequestId IS NOT NULL
SET a.installation_data = JSON_SET(a.installation_data,
    CASE WHEN JSON_CONTAINS_PATH(a.installation_data, 'one', '$.RequestId') THEN '$.RequestId' ELSE '$.requestId' END, r.Id)
WHERE JSON_VALID(a.installation_data);
