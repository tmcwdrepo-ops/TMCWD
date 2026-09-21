using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TMCWD.Application.Controllers
{
    public class DebugController : Controller
    {
        private readonly IConfiguration _configuration;

        public DebugController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Database()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    DATABASE(),
                    @@hostname,
                    @@port,
                    @@datadir;
            ";

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return Ok(new
                {
                    Database = reader.GetValue(0).ToString(),
                    Host = reader.GetValue(1).ToString(),
                    Port = reader.GetValue(2).ToString(),
                    DataDirectory = reader.GetValue(3).ToString(),
                    ConnectionStringUsed = connectionString
                });
            }

            return BadRequest();
        }
    }
}