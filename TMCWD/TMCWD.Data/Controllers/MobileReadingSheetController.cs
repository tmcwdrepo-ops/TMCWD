using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Services;
using TMCWD.Model.Billing;
using TMCWD.Model.Billing.Responses;
using TMCWD.Model.Billing.Requests;

namespace TMCWD.Data.Controllers;

[ApiController]
[Route("api/reading-sheet/mobile")]
[Authorize(Policy = nameof(UserRole.MeterReader))]
public class MobileReadingSheetController(
    IReadingSheetService sheets,
    IUserService users,
    IAccountService accounts,
    IReadingService readings,
    IZoneBookService zoneBooks,
    ICustomerService customers,
    IBillingService billings,
    IBirInvoiceConfigService? birConfig = null,
    IPenaltyService? penalties = null) : ControllerBase
{
    [HttpGet("{id:int}/accounts/{accountId:int}/soa")]
    public async Task<ActionResult<MobileSoaDto>> Soa(int id, int accountId)
    {
        var (readerId, rejection) = await ReaderAccess();
        if (rejection != null) return rejection;
        if (id <= 0 || accountId <= 0) return BadRequest();
        var sheet = await sheets.Get(id);
        if (sheet == null) return NotFound();
        if (sheet.AssignedTo != readerId) return Forbid();
        if (sheet.Status == ReadingStatus.Deleted) return NotFound();
        var account = (await accounts.GetReadingSheetRoster(sheet)).SingleOrDefault(a => a.Id == accountId);
        if (account == null) return NotFound();
        var saved = (await readings.GetByReadingSheetId(id))
            .Where(r => r.ReadingSheetId == id && r.AccountId == accountId && r.Status != ReadingStatus.Deleted).ToList();
        if (saved.Count > 1) return Conflict(new { message = "The reading sheet has duplicate account readings." });
        var reading = saved.SingleOrDefault();
        if (reading == null)
            return Conflict(new { code = "not_posted", message = "SOA unavailable: awaiting billing post." });
        var posted = (await billings.GetByAccountId(accountId))
            .Where(b => b.AccountId == accountId && b.ReadingId == reading.Id).ToList();
        if (posted.Count > 1) return Conflict(new { message = "The reading has duplicate billing records." });
        var billing = posted.SingleOrDefault();
        if (billing == null || reading.Status != ReadingStatus.Completed || !reading.IsCompleted)
            return Conflict(new { code = "not_posted", message = "SOA unavailable: awaiting billing post." });

        var customer = await customers.Get(checked((int)account.CustomerId));
        var name = customer == null ? account.AccountNumber : string.Join(" ",
            new[] { customer.Firstname, customer.Middlename, customer.Lastname }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var address = string.Join(" ", new[] { account.UnitNumber, account.Building,
            account.HouseNumber, account.Street, account.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x)));

        // ── Phase 6D: service invoice, QR, BIR config, reader name, remarks ─────
        var serviceInvoiceNumber = billing.ServiceInvoiceNumber;

        // QR payload encodes only the lookup key — no personal or financial data.
        var qrPayload = serviceInvoiceNumber != null
            ? $"TMCWD|SI|{serviceInvoiceNumber}"
            : null;

        // Retrieve the active BIR config; null is a valid/safe result.
        BirInvoiceConfigDto? birConfigDto = null;
        if (birConfig != null)
        {
            var activeConfig = await birConfig.GetActive();
            if (activeConfig != null)
                birConfigDto = new BirInvoiceConfigDto(
                    activeConfig.Id, activeConfig.PTUNumber, activeConfig.PermitDateIssued,
                    activeConfig.SeriesFrom, activeConfig.SeriesTo,
                    activeConfig.EffectiveFrom, activeConfig.EffectiveTo, activeConfig.InvoicePrefix, activeConfig.SeriesWidth);
        }

        // Meter reader name (informational — the reader who uploaded this reading).
        var reader = await users.Get(checked((int)reading.CreatedBy));
        var readerName = reader?.Name;

        // Reading remarks (informational — entered by the meter reader).
        var readingRemarks = string.IsNullOrWhiteSpace(reading.Remarks) ? null : reading.Remarks;

        // ── Snapshot ────────────────────────────────────────────────────────────
        var invoice = billing.InvoiceSnapshotJson == null ? null
            : System.Text.Json.JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(billing.InvoiceSnapshotJson);

        // ── TotalDue from snapshot — not from live billing totals ────────────
        // The snapshot records the exact water+maintenance amount at post time.
        // OtherCharges and adjustments posted later are not in the snapshot.
        var totalDue = invoice?.TotalDue;

        // ── Penalty fields ───────────────────────────────────────────────────
        // CurrentPenaltyAmount: sum of active Penalty rows (0 before DueDate).
        // PenaltyAfterDueAmount: informational 10% of snapshot BillAmount (server-derived,
        //   same rounding as the authoritative penalty process). Never creates a
        //   Penalty record. Never shown as a current liability.
        // TotalAfterDue: TotalDue + PenaltyAfterDueAmount (informational only).
        decimal? currentPenaltyAmount = null;
        decimal? penaltyAfterDueAmount = null;
        decimal? totalAfterDue = null;

        if (penalties != null)
        {
            var penaltyRows = await penalties.GetByReference(billing.BillingReferenceId);
            var active = penaltyRows
                .Where(p => p.PaymentStatus == (int)PaymentStatus.Unpaid
                         || p.PaymentStatus == (int)PaymentStatus.Overdue)
                .ToList();
            // Only surface a non-null CurrentPenaltyAmount when rows exist so
            // Flutter can distinguish "no penalty rows" from "zero penalty".
            if (penaltyRows.Any())
                currentPenaltyAmount = active.Sum(p => p.Amount);
        }

        if (totalDue.HasValue && invoice != null)
        {
            // Use the canonical calculation so rounding matches the posted penalty.
            penaltyAfterDueAmount = TMCWD.Model.Billing.LatePaymentPenalty.Calculate(invoice.BillAmount);
            totalAfterDue = totalDue.Value + penaltyAfterDueAmount.Value;
        }

        return Ok(new MobileSoaDto(
            sheet.Id, sheet.Name, reading.Id, billing.Id, billing.BillingReferenceId,
            account.Id, account.AccountNumber, name, address, account.MeterNumber, account.Classification,
            account.MeterSize, reading.PreviousReading, reading.CurrentReading,
            reading.CurrentReading - reading.PreviousReading, reading.DateCreated, billing.BillingPeriod,
            sheet.BillingDate, sheet.DueDate, sheet.DisconnectionDate, invoice?.BillingAdjustment ?? billing.BillingAdjustment,
            invoice?.OtherCharges ?? billing.OtherCharges, billing.TotalBillAmount, billing.RemainingAmount, (PaymentStatus)billing.PaymentStatus,
            // Phase 6D additions
            serviceInvoiceNumber, qrPayload, birConfigDto, readerName, readingRemarks,
            invoice?.PeriodFrom, invoice?.PeriodTo, invoice?.BillAmount, invoice?.MeterMaintenance,
            invoice?.PowerCostAdjustment, invoice?.ArrearsAmount, totalDue,
            currentPenaltyAmount, penaltyAfterDueAmount, totalAfterDue, invoice?.ChargeLines));
    }

    [HttpPost("{id:int}/readings")]
    public async Task<ActionResult> Upload(int id, MobileReadingUpload upload)
    {
        var (readerId, rejection) = await ReaderAccess();
        if (rejection != null) return rejection;
        if (id <= 0 || upload.ReadingDate == default) return BadRequest();
        try
        {
            var saved = await readings.Sync(readerId, id, upload);
            return Ok(new { readingId = saved.Id, readingSheetId = saved.ReadingSheetId,
                accountId = saved.AccountId, currentReading = saved.CurrentReading.ToString(CultureInfo.InvariantCulture),
                remarks = saved.Remarks ?? "" });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
    }

    [HttpGet("assigned")]
    public async Task<ActionResult<List<MobileAssignedReadingSheetDto>>> Assigned()
    {
        var (readerId, rejection) = await ReaderAccess();
        if (rejection != null) return rejection;

        var assigned = await sheets.GetByAssignedTo(readerId);
        return Ok(assigned
            .Where(x => x.AssignedTo == readerId && x.Status != ReadingStatus.Deleted)
            .OrderBy(x => x.Id)
            .Select(x => new MobileAssignedReadingSheetDto(x.Id, x.Name, x.BillingDate,
                x.DueDate, x.DisconnectionDate, x.BillingPeriodStart, x.ZoneBookId,
                x.SeqFrom, x.SeqTo, x.Status)).ToList());
    }

    [HttpGet("{id:int}/download")]
    public async Task<ActionResult<MobileReadingSheetDownloadDto>> Download(int id)
    {
        var (readerId, rejection) = await ReaderAccess();
        if (rejection != null) return rejection;
        if (id <= 0) return BadRequest();
        var sheet = await sheets.Get(id);
        if (sheet == null) return NotFound();
        if (sheet.AssignedTo != readerId) return Forbid();
        if (sheet.Status == ReadingStatus.Deleted) return NotFound();
        var zoneBook = await zoneBooks.Get(sheet.ZoneBookId);
        if (zoneBook == null || sheet.BillingDate == null)
            return Conflict(new { message = "The reading sheet zone/book or billing schedule is incomplete." });

        var saved = (await readings.GetByReadingSheetId(id))
            .Where(r => r.ReadingSheetId == sheet.Id && r.Status != ReadingStatus.Deleted).ToList();
        if (saved.GroupBy(r => r.AccountId).Any(g => g.Count() > 1))
            return Conflict(new { message = "The reading sheet has duplicate account readings." });
        var roster = (await accounts.GetReadingSheetRoster(sheet)).ToDictionary(a => a.Id);
        var requiredAccounts = roster.Keys.ToHashSet();
        // Historical membership comes from saved readings, even if an account moved or closed.
        foreach (var reading in saved.Where(r => !roster.ContainsKey(r.AccountId)))
        {
            var account = await accounts.Get(checked((int)reading.AccountId));
            if (account == null) return Conflict(new { message = "A recorded account is missing." });
            roster.Add(account.Id, account);
        }
        var result = new List<MobileDownloadAccountDto>();
        // ponytail: sequential per-account lookups reuse existing services; batch them if measured sheet-download latency requires it.
        foreach (var account in roster.Values.OrderBy(a => a.Sequence).ThenBy(a => a.Id))
        {
            var current = saved.SingleOrDefault(r => r.AccountId == account.Id);
            var previous = await readings.GetPreviousForSheet(account.Id, id);
            var customer = await customers.Get(checked((int)account.CustomerId));
            var name = customer == null ? account.AccountNumber : string.Join(" ",
                new[] { customer.Firstname, customer.Middlename, customer.Lastname }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var address = string.Join(" ", new[] { account.UnitNumber, account.Building,
                account.HouseNumber, account.Street, account.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x)));
            result.Add(new(account.Id, account.AccountNumber, name, account.MeterNumber, address,
                sheet.ZoneBookId, zoneBook.Zone, zoneBook.Book, account.Sequence,
                account.Classification, account.MeterSize,
                current?.PreviousReading ?? previous?.CurrentReading ?? account.InitialReading,
                previous?.DateCreated, current?.Id, current?.CurrentReading,
                current?.DateCreated, current?.Status, current?.Remarks ?? "", requiredAccounts.Contains(account.Id)));
        }
        return Ok(new MobileReadingSheetDownloadDto(new(sheet.Id, sheet.Name, sheet.BillingDate,
            sheet.DueDate, sheet.DisconnectionDate, sheet.BillingPeriodStart, sheet.ZoneBookId,
            sheet.SeqFrom, sheet.SeqTo, sheet.AssignedTo, sheet.Status, sheet.DateCreated,
            sheet.DateUpload), result));
    }

    private async Task<(int ReaderId, ActionResult? Rejection)> ReaderAccess()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
                CultureInfo.InvariantCulture, out var readerId) || readerId <= 0)
            return (0, Challenge());
        var reader = await users.Get(readerId);
        return reader == null || reader.Role != (int)UserRole.MeterReader
            || reader.IsActive != true || reader.IsVerified != true
            ? (readerId, Forbid()) : (readerId, null);
    }
}
