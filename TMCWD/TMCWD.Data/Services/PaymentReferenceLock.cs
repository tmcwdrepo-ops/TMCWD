using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TMCWD.Data.Context;

namespace TMCWD.Data.Services;

// MySQL connection-scoped lock serializes the same payment reference across bills and charge payments.
internal sealed class PaymentReferenceLock(UserDbContext context, string key) : IAsyncDisposable
{
    public static async Task<PaymentReferenceLock> Acquire(UserDbContext context, string reference)
    {
        var key = "tmcwd_pay_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.Database.GetDbConnection().Database + ":" + reference))).Substring(0, 48);
        await context.Database.OpenConnectionAsync();
        try {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT GET_LOCK(@key, 10)";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@key"; parameter.Value = key;
            command.Parameters.Add(parameter);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
                throw new InvalidOperationException("This payment reference is being processed. Reload before retrying.");
            return new PaymentReferenceLock(context, key);
        }
        catch { await context.Database.CloseConnectionAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        try {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT RELEASE_LOCK(@key)";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@key"; parameter.Value = key;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync();
        }
        finally { await context.Database.CloseConnectionAsync(); }
    }
}
