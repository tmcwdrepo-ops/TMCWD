Run from the workspace root with the Data API stopped:

```powershell
dotnet run --project tools/ServiceRequestMigration -- inspect .
dotnet run --project tools/ServiceRequestMigration -- apply .
dotnet run --project tools/ServiceRequestMigration -- verify .
```

The tool reads the Data project's local connection settings or `SERVICE_REQUEST_CONNECTION`. It only accepts a local MySQL server. Apply the migration before starting the updated application.

The migration adds service metadata to `requests`, descriptions/types to `request_details`, and job dates/referrals to `job_orders`. Existing customer requests remain unmarked. Non-customer requests use NULL customer/account links. Job order numbers, reporter details, referrals, and timestamps are retained. Closed/completed/accomplished/done map to Completed; cancelled/rejected map to Rejected; other rows map to Inspection and InProgress. The page continues to display Open, Done, and Cancelled.

`LegacyServiceRequestId` prevents duplicate backfills. Installation snapshot IDs are updated using job order numbers. Schema additions can resume after interruption; all data changes run in one transaction. The original `service_request_orders` table remains available as an archive and is no longer read or written by the application. If any archived rows are missing their three-table links, verification fails.
