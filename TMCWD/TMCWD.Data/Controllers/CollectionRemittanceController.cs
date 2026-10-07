using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Model.Billing.Requests;

namespace TMCWD.Data.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize(Policy = "Collection")]
public sealed class CollectionRemittanceController : ControllerBase
{
    private readonly UserDbContext _context;

    public CollectionRemittanceController(UserDbContext context)
    {
        _context = context;
    }

    [HttpPost("Save")]
    public async Task<IActionResult> Save([FromBody] CollectionRemittanceRequest request)
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var collectorId) || collectorId <= 0)
            return Unauthorized();
        if (request != null) request.CollectorId = collectorId;
        if (request == null || request.CollectorId <= 0)
            return BadRequest("A valid collector is required.");
        if (request.Date == default || request.Date.Date > DateTime.Today)
            return BadRequest("A valid remittance date is required.");
        if (request.Counts == null || request.Counts.Values.Any(value => value < 0))
            return BadRequest("Cash and check counts cannot be negative.");

        try
        {
            await EnsureTableAsync();

            var date = request.Date.Date;
            var nextDate = date.AddDays(1);
            var payments = await _context.Payments
                .AsNoTracking()
                .Where(payment => payment.Success
                    && payment.CreatedBy == request.CollectorId
                    && payment.DateCreated >= date
                    && payment.DateCreated < nextDate)
                .OrderBy(payment => payment.DateCreated)
                .ToListAsync();

            decimal Count(string key) => request.Counts.TryGetValue(key, out var value) ? value : 0m;
            var billsTotal = Count("1000") * 1000m
                + Count("500") * 500m
                + Count("200") * 200m
                + Count("100") * 100m
                + Count("50") * 50m
                + Count("20") * 20m
                + Count("10") * 10m
                + Count("5") * 5m;
            var countedCash = billsTotal + Count("coins");
            var checkAmount = Count("check");
            var expectedCash = payments
                .Where(payment => payment.Method == (int)PaymentMethod.Cash)
                .Sum(payment => payment.PaidAmount);
            var expectedOnline = payments
                .Where(payment => payment.Method == (int)PaymentMethod.EWallet)
                .Sum(payment => payment.PaidAmount);

            // Recalculate all derived/system values instead of trusting browser totals.
            request.OrStart = payments.FirstOrDefault()?.PaymentReference ?? string.Empty;
            request.OrEnd = payments.LastOrDefault()?.PaymentReference ?? string.Empty;
            request.BillsTotal = billsTotal;
            request.OtherCharges = 0m;
            request.CheckAmount = checkAmount;
            request.OnlineAmount = expectedOnline;
            request.CashOnHand = expectedCash;
            request.CountedCash = countedCash;
            request.TotalCollection = countedCash + checkAmount + expectedOnline;
            request.ShortOver = countedCash - expectedCash;

            var remittance = new CollectionRemittance
            {
                Date = request.Date.Date,
                CollectorId = request.CollectorId,
                OrStart = request.OrStart.Trim(),
                OrEnd = request.OrEnd.Trim(),
                IsDeposit = request.IsDeposit,
                CountsJson = JsonSerializer.Serialize(request.Counts),
                OtherCharges = request.OtherCharges,
                BillsTotal = request.BillsTotal,
                TotalCollection = request.TotalCollection,
                CheckAmount = request.CheckAmount,
                OnlineAmount = request.OnlineAmount,
                CashOnHand = request.CashOnHand,
                CountedCash = request.CountedCash,
                ShortOver = request.ShortOver,
                DateCreated = DateTime.Now
            };

            _context.CollectionRemittances.Add(remittance);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = remittance.Id });
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private async Task EnsureTableAsync()
    {
        await _context.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS `collection_remittances` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT,
    `Date` DATETIME NOT NULL,
    `CollectorId` BIGINT NOT NULL,
    `OrStart` VARCHAR(100) NOT NULL,
    `OrEnd` VARCHAR(100) NOT NULL,
    `IsDeposit` BOOLEAN NOT NULL,
    `CountsJson` LONGTEXT NOT NULL,
    `OtherCharges` DECIMAL(12,2) NOT NULL,
    `BillsTotal` DECIMAL(12,2) NOT NULL,
    `TotalCollection` DECIMAL(12,2) NOT NULL,
    `CheckAmount` DECIMAL(12,2) NOT NULL,
    `OnlineAmount` DECIMAL(12,2) NOT NULL,
    `CashOnHand` DECIMAL(12,2) NOT NULL,
    `CountedCash` DECIMAL(12,2) NOT NULL,
    `ShortOver` DECIMAL(12,2) NOT NULL,
    `DateCreated` DATETIME NOT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_collection_remittances_DateCollector` (`Date`, `CollectorId`)
) ENGINE=InnoDB;");
    }
}
