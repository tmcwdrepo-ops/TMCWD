using System.Text.Json;
using MySqlConnector;
var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft/UserSecrets/TMCWD.Data-local-development/secrets.json");
using var json = JsonDocument.Parse(File.ReadAllText(file));
var cs = json.RootElement.GetProperty("ConnectionStrings:DefaultConnection").GetString();
await using var connection = new MySqlConnection(cs);
await connection.OpenAsync();
foreach (var sql in new[] { "SELECT VERSION(), DATABASE()", "SHOW CREATE TABLE requests", "SHOW CREATE TABLE request_details", "SHOW CREATE TABLE job_orders", "SELECT COUNT(*), MIN(id), MAX(id) FROM service_request_orders", "SELECT status, COUNT(*) FROM service_request_orders GROUP BY status", "SHOW CREATE TABLE inspection_types" }) {
    await using var command = new MySqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    Console.WriteLine(sql);
    while (await reader.ReadAsync()) Console.WriteLine(string.Join(" | ", Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i).ToString())));
}
