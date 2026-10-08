using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services;

internal static class PaymentReferenceAllocator
{
    // MySQL AUTO_INCREMENT allocates one shared serial; gaps on rollback are intentional.
    public static async Task<string> Allocate(UserDbContext db, int cashier, PaymentMethod method, DateTime now, string? externalReference)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Receipt allocation requires the payment transaction.");
        if (externalReference?.Length > 200) throw new InvalidOperationException("External payment reference is too long.");
        var receipt = new PaymentReceipt { CreatedBy = cashier, Method = (int)method, DateCreated = now,
            ExternalReference = method == PaymentMethod.Cash ? null : externalReference?.Trim() };
        db.PaymentReceipts.Add(receipt);
        await db.SaveChangesAsync();
        var reference = "CR" + receipt.Id.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        if (await db.Payments.AnyAsync(p => p.PaymentReference == reference))
            throw new InvalidOperationException("Receipt sequence conflicts with historical data. Apply the payment receipt migration before collecting.");
        receipt.PaymentReference = reference;
        await db.SaveChangesAsync();
        return reference;
    }
}
