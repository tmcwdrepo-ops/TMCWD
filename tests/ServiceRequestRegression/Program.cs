using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using TMCWD.Data.Context;
using TMCWD.Data.Controllers;
using TMCWD.Data.Entities;
using TMCWD.Model.Dashboard;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var secretFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft/UserSecrets/TMCWD.Data-local-development/secrets.json");
using var secrets = JsonDocument.Parse(File.ReadAllText(secretFile));
var settings = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SERVICE_REQUEST_CONNECTION")
    ?? secrets.RootElement.GetProperty("ConnectionStrings:DefaultConnection").GetString());
if (!new[] { "localhost", "127.0.0.1", "::1" }.Contains(settings.Server)) throw new Exception("Tests require local MySQL.");
settings.AllowUserVariables = true;
settings.UseAffectedRows = false;
await using var connection = new MySqlConnection(settings.ConnectionString);
await connection.OpenAsync();
async Task Execute(string sql) { await using var command = new MySqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
async Task<long> Count(string sql) { await using var command = new MySqlCommand(sql, connection); return Convert.ToInt64(await command.ExecuteScalarAsync()); }
void Check(bool value, string message) { if (!value) throw new Exception(message); }

// Every write uses a connection-local temporary table. Persistent records are
// never modified, including when exercising the production migration SQL.
foreach (var table in new[] { "requests", "request_details", "job_orders", "service_request_orders", "inspection_types", "water_connection_applications", "accounts", "customers", "readings" })
{
    string create;
    await using (var command = new MySqlCommand($"SHOW CREATE TABLE `{table}`", connection))
    {
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync(); create = reader.GetString(1);
    }
    create = Regex.Replace(create, @"(?m)^\s*CONSTRAINT .*\n?", "");
    create = Regex.Replace(create, @",\s*\)", "\n)");
    await Execute(create.Replace("CREATE TABLE", "CREATE TEMPORARY TABLE", StringComparison.Ordinal));
}
var migration = await File.ReadAllTextAsync(Path.Combine(root, "TMCWD/TMCWD.Data/Database/20261002_migrate_service_requests.sql"));
var sections = migration.Split("-- DATA", 2);
string[] Statements(string sql) => Regex.Replace(sql, @"(?m)^--.*$", "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
foreach (var statement in Statements(sections[0]))
{
    var column = Regex.Match(statement, @"ALTER TABLE (\w+) ADD COLUMN (\w+)");
    if (column.Success)
    {
        await using var command = new MySqlCommand($"SHOW COLUMNS FROM `{column.Groups[1].Value}` LIKE '{column.Groups[2].Value}'", connection);
        if (await command.ExecuteScalarAsync() != null) continue;
    }
    var index = Regex.Match(statement, @"ALTER TABLE (\w+) ADD UNIQUE INDEX (\w+)");
    if (index.Success)
    {
        await using var command = new MySqlCommand($"SHOW INDEX FROM `{index.Groups[1].Value}` WHERE Key_name = '{index.Groups[2].Value}'", connection);
        if (await command.ExecuteScalarAsync() != null) continue;
    }
    await Execute(statement);
}
await Execute(@"INSERT INTO requests (Id,ControlNumber,CustomerId,AccountId,Status,CreatedBy,DateCreated,UpdatedBy,DateUpdated)
    VALUES (1,'EXISTING',NULL,NULL,2,1,NOW(),1,NOW());");
foreach (var (id, status) in new[] { (1, "Open"), (2, "Closed"), (3, "Done"), (4, "Cancelled") })
{
    await Execute($@"INSERT INTO service_request_orders
        (id,is_non_customer,account_number,account_name,address,reported_by,contact_number,job_order_number,
         job_order_date,source,job_order_type,job_description,referred_to_user_id,date_referred,name,particulars,received_by_user_id,status,created_at,updated_at)
        VALUES ({id},1,'','','Test address','Reporter','09000000000','SRO-TEST-{id}',NOW(),'Phone','Maintenance','LEAK',-2,NOW(),'Crew','Test particulars',1,'{status}',NOW(),NOW());");
}
await Execute(@"INSERT INTO water_connection_applications
    (tracking_number,full_name,address,barangay,contact_number,email,connection_type,source,stage,installation_data,created_at,updated_at)
    VALUES ('MIGRATION-TEST','Test','Address','Barangay','09000000000','','residential','walk_in','installation',
        '{""RequestId"":1,""JobOrderNumber"":""SRO-TEST-1""}',NOW(),NOW());");

async Task Backfill()
{
    await using var transaction = await connection.BeginTransactionAsync();
    foreach (var statement in Statements(sections[1]))
    {
        // MySQL cannot read an INSERT target temporary table again in its
        // NOT EXISTS check. Read an identical snapshot for that check only.
        var target = Regex.Match(statement, @"^INSERT INTO (\w+)");
        var testStatement = statement;
        if (target.Success)
        {
            var table = target.Groups[1].Value;
            foreach (var setup in new[] {
                "DROP TEMPORARY TABLE IF EXISTS migration_existing_rows",
                $"CREATE TEMPORARY TABLE migration_existing_rows LIKE `{table}`",
                $"INSERT INTO migration_existing_rows SELECT * FROM `{table}`" })
            {
                await using var setupCommand = new MySqlCommand(setup, connection, transaction);
                await setupCommand.ExecuteNonQueryAsync();
            }
            testStatement = statement.Replace($"FROM {table}", "FROM migration_existing_rows", StringComparison.Ordinal);
        }
        await using var command = new MySqlCommand(testStatement, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
}
await Backfill();
await Backfill();
Check(await Count("SELECT COUNT(*) FROM requests WHERE IsServiceRequest = 1") == 4, "Backfill duplicates requests.");
Check(await Count("SELECT COUNT(*) FROM request_details") == 4 && await Count("SELECT COUNT(*) FROM job_orders") == 4, "Backfill duplicates details/jobs.");
Check(await Count("SELECT COUNT(*) FROM requests WHERE IsServiceRequest AND CustomerId IS NULL AND AccountId IS NULL") == 4, "Non-customer links must remain NULL.");
Check(await Count("SELECT COUNT(*) FROM job_orders WHERE Status = 8") == 2, "Closed and done must map to completed.");
Check(await Count("SELECT COUNT(*) FROM job_orders WHERE Status = 9") == 1, "Cancelled must map to rejected.");
Check(await Count("SELECT COUNT(*) FROM water_connection_applications a JOIN requests r ON r.Id = JSON_EXTRACT(a.installation_data,'$.RequestId') WHERE r.LegacyServiceRequestId = 1") == 1, "Installation ID was not remapped.");
Check(await Count("SELECT COUNT(*) FROM requests WHERE Id = 1 AND IsServiceRequest = 0") == 1, "Existing request changed.");
Console.WriteLine("PASS: migration preserves links, job numbers and non-customer details; status mapping, snapshot remapping and reruns work.");

var failure = new FailAfterSave();
var options = new DbContextOptionsBuilder<UserDbContext>().UseMySql(connection, ServerVersion.AutoDetect(connection)).AddInterceptors(failure).Options;
await using var db = new UserDbContext(options);
// User lookup is read-only; MySQL temporary tables cannot be read through two
// separate aliases in the dashboard's receiver/referral projection.
var receiverId = await db.Users.AsNoTracking().Where(x => x.IsActive == true).Select(x => x.Id).FirstAsync();
var controller = new DashboardController(db);
ServiceRequestOrderRecord NewRequest() => new() {
    IsNonCustomer = true, Address = "Test address", ReportedBy = "Test reporter", ContactNumber = "09000000000",
    JobOrderDate = DateTime.Today, Source = "Walk-In", JobOrderType = "Maintenance", JobDescription = "LEAK",
    ReferredToDepartment = "Maintenance", Particulars = "Repair service line" };
var createdResult = await controller.ServiceRequest(receiverId, NewRequest());
var created = (createdResult.Result as OkObjectResult)?.Value as ServiceRequestOrderRecord;
Check(created != null && created.RequestId > 0 && created.JobOrderNumber.StartsWith("JO-"), "Creation failed.");
Check(created!.Status == "Pending", "New service requests must be pending.");
var request = await db.Requests.SingleAsync(x => x.Id == created!.RequestId);
var detail = await db.RequestDetails.SingleAsync(x => x.RequestId == request.Id);
var job = await db.JobOrders.SingleAsync(x => x.RequestDetailId == detail.Id);
Check(request.IsServiceRequest && request.AccountId == null && request.CustomerId == null && job.RequestId == request.Id
    && job.ReferredToUserId == -2 && detail.JobDescription == "LEAK", "Creation did not persist the three-table structure.");
Check(await Count("SELECT COUNT(*) FROM service_request_orders") == 4, "Creation wrote to the archive.");

var rows = await controller.ServiceRequestsByDescription("LEAK");
Check((rows.Result as OkObjectResult)?.Value is System.Collections.IEnumerable, "Summary detail query failed.");
var factory = typeof(DashboardController).GetMethod("ServiceRequestRows", BindingFlags.NonPublic | BindingFlags.Instance)!;
var query = (IQueryable<ServiceRequestOrderRecord>)factory.Invoke(controller, null)!;
Check(await query.CountAsync() == 5, "Summary includes unrelated requests or misses jobs.");
Check(await query.Where(x => x.RequestId == created!.RequestId).Select(x => x.Status).SingleAsync() == "Pending",
    "Dashboard must show new requests as pending.");
Check(await query.Where(x => x.Status != "Pending" && x.Status != "Completed").CountAsync() == 0,
    "Dashboard must use only pending and completed, including legacy requests.");
var cancelledRequestId = await db.Requests.Where(r => r.LegacyServiceRequestId == 4).Select(r => r.Id).SingleAsync();
Check(await query.Where(x => x.RequestId == cancelledRequestId).Select(x => x.IsReadOnly).SingleAsync(),
    "Legacy cancelled requests must remain locked.");
var grouped = await query.GroupBy(x => x.JobDescription).Select(x => new { Description = x.Key, Count = x.Count() }).ToListAsync();
Check(grouped.Single().Count == 5, "Summary grouping failed.");
created!.Source = "Phone"; created.Particulars = "Updated details";
var savedResult = await controller.SaveServiceRequest(receiverId, created) as OkObjectResult;
Check(savedResult != null && JsonSerializer.SerializeToElement(savedResult.Value).GetProperty("status").GetString() == "Pending", "Editing must keep the request pending.");
var completedResult = await controller.AccomplishServiceRequest(created.RequestId, receiverId) as OkObjectResult;
Check(completedResult != null && JsonSerializer.SerializeToElement(completedResult.Value).GetProperty("status").GetString() == "Completed", "Accomplishing must return completed.");
Check(await query.Where(x => x.RequestId == created.RequestId).Select(x => x.Status).SingleAsync() == "Completed",
    "Dashboard must show accomplished requests as completed.");
var pendingRows = await controller.ServiceRequestsByDescription("LEAK", "Pending");
var pendingJson = JsonSerializer.SerializeToElement(((OkObjectResult)pendingRows.Result!).Value);
Check(pendingJson.GetArrayLength() == 1 && pendingJson.EnumerateArray().All(x => x.GetProperty("status").GetString() == "Pending" && !x.GetProperty("isReadOnly").GetBoolean()),
    "Pending view must exclude completed and cancelled requests.");
var completedRows = await controller.ServiceRequestsByDescription(null, "Completed");
var completedJson = JsonSerializer.SerializeToElement(((OkObjectResult)completedRows.Result!).Value);
Check(completedJson.GetArrayLength() == 3 && completedJson.EnumerateArray().All(x => x.GetProperty("status").GetString() == "Completed"),
    "Completed history must include finished requests without requiring a description.");
Check(job.Status == (int)JobOrderStatus.Completed && request.Status == (int)RequestStatus.Completed, "Completion must update both statuses.");
Check(await controller.SaveServiceRequest(receiverId, created) is ConflictObjectResult, "Finished request remains editable.");
var cancelled = await db.Requests.SingleAsync(x => x.LegacyServiceRequestId == 4);
Check(await controller.AccomplishServiceRequest(cancelled.Id, receiverId) is ConflictObjectResult, "Cancelled request was accomplished.");
Console.WriteLine("PASS: create, edit, complete, summary grouping/detail and finished/cancelled guards use the three tables.");

var now = DateTime.Now;
db.Customers.Add(new Customer { Id = 1, Firstname = "Test", Lastname = "Customer", PhoneNumber = "09000000000", DateCreated = now, DateUpdated = now });
db.Accounts.Add(new Account { Id = 1, CustomerId = 1, AccountNumber = "TEST-ACCOUNT", HouseNumber = "12", Street = "Test Street", Barangay = "Test Barangay", DateCreated = now, DateUpdated = now });
await db.SaveChangesAsync();
var customerInput = NewRequest();
customerInput.IsNonCustomer = false; customerInput.AccountId = 1;
customerInput.AccountName = "Forged name"; customerInput.Address = "Forged address";
var customerResult = await controller.ServiceRequest(receiverId, customerInput);
var customerCreated = (customerResult.Result as OkObjectResult)?.Value as ServiceRequestOrderRecord;
Check(customerCreated?.AccountNumber == "TEST-ACCOUNT" && customerCreated.AccountName == "Test Customer"
    && customerCreated.Address.Contains("Test Street"), "Customer details must come from the selected account.");
var customerHeader = await db.Requests.SingleAsync(x => x.Id == customerCreated!.RequestId);
Check(customerHeader.CustomerId == 1 && customerHeader.AccountId == 1, "Customer/account links were lost.");

var application = new WaterConnectionApplication { TrackingNumber = "INSTALLATION-TEST", FullName = "Applicant",
    ConnectionType = "residential", Source = "walk_in", Stage = "installation", AccountId = 1, CustomerId = 1, CreatedAt = now, UpdatedAt = now };
db.WaterConnectionApplications.Add(application);
await db.SaveChangesAsync();
var installationInput = NewRequest();
installationInput.ApplicationId = application.Id;
installationInput.JobDescription = "NEW CONNECTION INSTALLATION";
var installationResult = await controller.ServiceRequest(receiverId, installationInput);
var installationCreated = (installationResult.Result as OkObjectResult)?.Value as ServiceRequestOrderRecord;
Check(installationCreated != null && application.Stage == "connect" && !installationCreated.IsNonCustomer
    && installationCreated.AccountId == 1, "Installation must create a linked customer request and advance to Connect.");
var installationSnapshot = JsonSerializer.Deserialize<ServiceRequestOrderRecord>(application.InstallationData!);
Check(installationSnapshot?.RequestId == installationCreated!.RequestId
    && installationSnapshot.JobOrderNumber == installationCreated.JobOrderNumber, "Installation snapshot uses incorrect IDs.");
Check((await controller.ServiceRequest(receiverId, installationInput)).Result is ConflictObjectResult, "Installation can be submitted twice.");
application.Stage = "done";
var migratedApplication = await db.WaterConnectionApplications.SingleAsync(x => x.TrackingNumber == "MIGRATION-TEST");
migratedApplication.Stage = "done";
await db.SaveChangesAsync();
await controller.ServiceRequestsByDescription("NEW CONNECTION INSTALLATION");
Check((await db.Requests.SingleAsync(x => x.Id == installationCreated.RequestId)).Status == (int)RequestStatus.Completed,
    "Installation completion did not update its request.");
Check((await db.Requests.SingleAsync(x => x.LegacyServiceRequestId == 1)).Status == (int)RequestStatus.Completed
    && (await db.Requests.SingleAsync(x => x.Id == 1)).Status == (int)RequestStatus.InProgress,
    "Migrated installation completion changed an overlapping unrelated ID.");
Console.WriteLine("PASS: customer details, installation creation/snapshots, duplicate submission and completion reconciliation.");

db.ChangeTracker.Clear();
var customerJob = await db.JobOrders.AsNoTracking().SingleAsync(x => x.RequestId == customerCreated!.RequestId);
var legacyJobInput = new JobOrder { Id = customerJob.Id, RequestId = customerJob.RequestId, RequestDetailId = customerJob.RequestDetailId,
    JobOrderNumber = customerJob.JobOrderNumber, CreatedBy = customerJob.CreatedBy, DateCreated = customerJob.DateCreated,
    Status = (int)JobOrderStatus.Completed };
await new TMCWD.Data.Services.JobOrderService(db).SaveUpdate(checked((int)receiverId), legacyJobInput);
Check(legacyJobInput.JobOrderDate == customerJob.JobOrderDate && legacyJobInput.ReferredToUserId == -2
    && (await db.Requests.SingleAsync(x => x.Id == customerCreated!.RequestId)).Status == (int)RequestStatus.Completed,
    "Existing job order workflow discarded metadata or failed to synchronize the request status.");
Console.WriteLine("PASS: existing job order updates preserve service metadata and synchronize request completion.");

var before = await db.Requests.CountAsync();
var beforeDetails = await db.RequestDetails.CountAsync();
var beforeJobs = await db.JobOrders.CountAsync();
failure.RemainingSaves = 2;
try { await controller.ServiceRequest(receiverId, NewRequest()); throw new Exception("Failure injection did not run."); }
catch (InjectedFailure) { }
db.ChangeTracker.Clear();
Check(await db.Requests.CountAsync() == before && await db.RequestDetails.CountAsync() == beforeDetails && await db.JobOrders.CountAsync() == beforeJobs,
    "Failed creation left a partial request/detail/job.");
Console.WriteLine("PASS: failure during creation rolls back all three tables.");

sealed class InjectedFailure : Exception { }
sealed class FailAfterSave : SaveChangesInterceptor
{
    public int RemainingSaves { get; set; }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (RemainingSaves > 0 && --RemainingSaves == 0) throw new InjectedFailure();
        return ValueTask.FromResult(result);
    }
}
