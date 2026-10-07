using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

if (args.Length != 2 || args[0] is not ("inspect" or "apply" or "verify"))
    throw new ArgumentException("Usage: ServiceRequestMigration <inspect|apply|verify> <workspace-root>");
var root = Path.GetFullPath(args[1]);
var connectionString = Environment.GetEnvironmentVariable("SERVICE_REQUEST_CONNECTION");
foreach (var file in new[] {
    Path.Combine(root, "TMCWD/TMCWD.Data/appsettings.json"),
    Path.Combine(root, "TMCWD/TMCWD.Data/appsettings.Development.json"),
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft/UserSecrets/TMCWD.Data-local-development/secrets.json") })
{
    if (connectionString != null || !File.Exists(file)) continue;
    using var json = JsonDocument.Parse(File.ReadAllText(file));
    if (json.RootElement.TryGetProperty("ConnectionStrings", out var strings)
        && strings.TryGetProperty("DefaultConnection", out var value)) connectionString = value.GetString();
    if (json.RootElement.TryGetProperty("ConnectionStrings:DefaultConnection", out var flat)) connectionString = flat.GetString();
}
if (string.IsNullOrWhiteSpace(connectionString)) throw new Exception("No database connection configured.");
var settings = new MySqlConnectionStringBuilder(connectionString);
if (!new[] { "localhost", "127.0.0.1", "::1" }.Contains(settings.Server))
    throw new Exception("This migration tool requires a local database.");
await using var connection = new MySqlConnection(settings.ConnectionString);
await connection.OpenAsync();
Console.WriteLine($"Database: {connection.Database}; mode: {args[0]}");
MySqlTransaction? currentTransaction = null;

async Task<long> Scalar(string sql, params (string, object)[] values)
{
    await using var command = new MySqlCommand(sql, connection, currentTransaction);
    foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}
var hasArchive = await Scalar("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'service_request_orders'") > 0;
async Task<long> Verify()
{
    var migrated = await Scalar("SELECT COUNT(*) FROM requests WHERE LegacyServiceRequestId IS NOT NULL");
    if (hasArchive)
    {
        var archived = await Scalar("SELECT COUNT(*) FROM service_request_orders");
        var missing = await Scalar(@"SELECT COUNT(*) FROM service_request_orders s
            LEFT JOIN requests r ON r.LegacyServiceRequestId = s.id
            LEFT JOIN request_details d ON d.RequestId = r.Id
            LEFT JOIN job_orders j ON j.RequestId = r.Id AND j.RequestDetailId = d.Id
            WHERE r.Id IS NULL OR d.Id IS NULL OR j.Id IS NULL
                OR NOT (BINARY j.JobOrderNumber <=> BINARY s.job_order_number)");
        if (migrated != archived || missing != 0) throw new Exception($"Migration mismatch: archive={archived}, migrated={migrated}, missing links={missing}.");
    }
    return migrated;
}
if (args[0] == "inspect")
{
    foreach (var table in new[] { "requests", "request_details", "job_orders", "service_request_orders" })
    {
        if (table == "service_request_orders" && !hasArchive) continue;
        Console.WriteLine($"{table}: {await Scalar($"SELECT COUNT(*) FROM `{table}`")} rows");
    }
    return;
}

long verifiedCount = -1;
if (args[0] == "apply")
{
    // DDL commits implicitly in MySQL. Each addition is checked so an interrupted
    // run can resume. Backfill and installation ID changes share one transaction.
    var sql = await File.ReadAllTextAsync(Path.Combine(root, "TMCWD/TMCWD.Data/Database/20261002_migrate_service_requests.sql"));
    var sections = sql.Split("-- DATA", 2);
    string[] Statements(string section) => Regex.Replace(section, @"(?m)^--.*$", "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var statement in Statements(sections[0]))
    {
        var column = Regex.Match(statement, @"ALTER TABLE (\w+) ADD COLUMN (\w+)");
        var index = Regex.Match(statement, @"ALTER TABLE (\w+) ADD UNIQUE INDEX (\w+)");
        if (column.Success && await Scalar("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = @table AND column_name = @column",
            ("@table", column.Groups[1].Value), ("@column", column.Groups[2].Value)) > 0) continue;
        if (index.Success && await Scalar("SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = @table AND index_name = @index",
            ("@table", index.Groups[1].Value), ("@index", index.Groups[2].Value)) > 0) continue;
        await using var command = new MySqlCommand(statement, connection);
        await command.ExecuteNonQueryAsync();
    }
    await using var transaction = await connection.BeginTransactionAsync();
    currentTransaction = transaction;
    try
    {
        foreach (var statement in Statements(sections[1]))
        {
            if (!hasArchive && statement.Contains("service_request_orders", StringComparison.Ordinal)) continue;
            if (!hasArchive && statement.StartsWith("UPDATE water_connection_applications", StringComparison.Ordinal)) continue;
            await using var command = new MySqlCommand(statement, connection, transaction);
            await command.ExecuteNonQueryAsync();
        }
        verifiedCount = await Verify();
        await transaction.CommitAsync();
    }
    catch { await transaction.RollbackAsync(); throw; }
    finally { currentTransaction = null; }
}

if (verifiedCount < 0) verifiedCount = await Verify();
Console.WriteLine($"PASS: {verifiedCount} archived service requests have linked request details and job orders.");
