using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TMCWD.Administration;
using TMCWD.Application.Models;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
using TMCWD.Model.Billing.Requests;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;
namespace TMCWD.Application.Controllers
{
    public class CollectionsController : Controller
    {
        #region constructors

        private readonly AuthenticatedUserService _user;
        private readonly BillingTransaction _billingTrans;
        private readonly PenaltyTransaction _penaltyTrans;
        private readonly AccountTransaction _accountTrans;
        private readonly CustomerTransaction _customerTrans;
        private readonly ReadingTransaction _readingTrans;
        private readonly ReadingSheetTransaction _readingSheetTrans;
        private readonly PaymentTransaction _paymentTrans;
        private readonly CollectionRemittanceTransaction _remittanceTrans;
        private readonly OtherChargeTransaction _otherChargeTrans;
        private readonly OtherFeeTypeTransaction _otherFeeTypeTrans;
        private readonly UserTransaction _userTrans;
        private readonly BatchPaymentUploadTransaction _batchUploadTrans;
        private readonly WebService _api;
        private readonly ILogger<CollectionsController> _logger;

        #endregion

        #region methods

        public CollectionsController(AuthenticatedUserService user,
            BillingTransaction billingTrans,
            PenaltyTransaction penaltyTrans,
            AccountTransaction accountTrans,
            CustomerTransaction customerTrans,
            ReadingTransaction readingTrans,
            ReadingSheetTransaction readingSheetTrans,
            PaymentTransaction paymentTrans,
            CollectionRemittanceTransaction remittanceTrans,
            OtherChargeTransaction otherChargeTrans,
            OtherFeeTypeTransaction otherFeeTypeTrans,
            UserTransaction userTrans,
            BatchPaymentUploadTransaction batchUploadTrans,
            WebService api,
            ILogger<CollectionsController> logger)
        {
            _user = user;
            _billingTrans = billingTrans;
            _penaltyTrans = penaltyTrans;
            _accountTrans = accountTrans;
            _customerTrans = customerTrans;
            _readingTrans = readingTrans;
            _readingSheetTrans = readingSheetTrans;
            _paymentTrans = paymentTrans;
            _remittanceTrans = remittanceTrans;
            _otherChargeTrans = otherChargeTrans;
            _otherFeeTypeTrans = otherFeeTypeTrans;
            _userTrans = userTrans;
            _batchUploadTrans = batchUploadTrans;
            _api = api;
            _logger = logger;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (_user.User.Id <= 0)
            {
                context.Result = Request.Headers.Accept.Any(value =>
                        value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
                    ? RedirectToAction("Index", "Home")
                    : Unauthorized();
                return;
            }

            if (_user.User.Role != (int)UserRole.Cashier && _user.User.Role != (int)UserRole.SuperAdmin)
            {
                context.Result = Forbid();
                return;
            }

            base.OnActionExecuting(context);
        }

        public IActionResult Index() => View("Collections");

        [HttpGet]
        public IActionResult CurrentCollector()
        {
            var user = _user.User;
            if (user == null || user.Id <= 0 || string.IsNullOrWhiteSpace(user.Name))
                return NoContent();

            return Ok(new
            {
                id = user.Id,
                name = user.Name,
                date = DateTime.Today.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet]
        public async Task<IActionResult> Monitoring()
        {
            var collector = _user.User;
            if (collector == null || collector.Id <= 0)
                return Unauthorized();

            var payments = await _paymentTrans.GetPaymentHistoryByCashier(collector.Id);
            var successfulCashPayments = payments
                .Where(payment => payment.Success && payment.Method == (int)PaymentMethod.Cash && payment.DateCreated.Date == DateTime.Today)
                .OrderBy(payment => payment.DateCreated)
                .ToList();

            if (successfulCashPayments.Count == 0)
                return Ok(new List<object>());

            return Ok(new[]
            {
                new
                {
                    collector = collector.Name,
                    orFrom = successfulCashPayments.First().PaymentReference,
                    orTo = successfulCashPayments.Last().PaymentReference,
                    cashOnHand = successfulCashPayments.Sum(payment => payment.PaidAmount)
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> CollectionList(DateTime date)
        {
            var collector = _user.User;
            if (collector == null || collector.Id <= 0)
                return Unauthorized();

            var payments = await _paymentTrans.GetPaymentHistoryByCashier(collector.Id);
            var paymentRows = payments
                .Where(payment => payment.Success && payment.DateCreated.Date == date.Date)
                .OrderByDescending(payment => payment.DateCreated)
                .ToList();
            var billings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var result = new List<object>();

            foreach (var payment in paymentRows)
            {
                var billing = billings.FirstOrDefault(item =>
                    string.Equals(item.BillingReferenceId, payment.BillingReference,
                        StringComparison.OrdinalIgnoreCase));
                if (billing == null) continue;

                var account = await _accountTrans.Get((int)billing.AccountId);
                var customer = account == null ? null : await _customerTrans.Get(account.CustomerId);

                result.Add(new
                {
                    invoiceNo = payment.PaymentReference,
                    account = customer?.FullName ?? account?.AccountNumber ?? payment.BillingReference,
                    totalAmount = payment.PaidAmount,
                    transactionType = ((PaymentMethod)payment.Method).ToString()
                });
            }

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> RemittanceExpected(DateTime date)
        {
            var collector = _user.User;
            if (collector == null || collector.Id <= 0)
                return Unauthorized();

            var payments = await _paymentTrans.GetPaymentHistoryByCashier(collector.Id);
            var dailyPayments = payments
                .Where(payment => payment.Success && payment.DateCreated.Date == date.Date)
                .OrderBy(payment => payment.DateCreated)
                .ToList();

            return Ok(new
            {
                orStart = dailyPayments.FirstOrDefault()?.PaymentReference ?? string.Empty,
                orEnd = dailyPayments.LastOrDefault()?.PaymentReference ?? string.Empty,
                cashOnHand = dailyPayments
                    .Where(payment => payment.Method == (int)PaymentMethod.Cash)
                    .Sum(payment => payment.PaidAmount),
                bills = dailyPayments
                    .Where(payment => payment.Method == (int)PaymentMethod.Cash)
                    .Sum(payment => payment.PaidAmount),
                check = dailyPayments
                    .Where(payment => payment.Method == (int)PaymentMethod.Check)
                    .Sum(payment => payment.PaidAmount),
                online = dailyPayments
                    .Where(payment => payment.Method == (int)PaymentMethod.EWallet)
                    .Sum(payment => payment.PaidAmount),
                totalCollected = dailyPayments.Sum(payment => payment.PaidAmount),
                paymentCount = dailyPayments.Select(payment => payment.PaymentReference).Distinct().Count()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay([FromBody] CollectionPaymentRequest request)
        {
            if (_user.User == null || _user.User.Id <= 0)
                return Unauthorized(new { success = false, message = "A valid cashier session is required." });
            if (!ModelState.IsValid || request == null || !Enum.IsDefined(typeof(PaymentMethod), request.Method))
                return BadRequest(new { success = false, message = "Please select a payment method." });

            var result = await _paymentTrans.ProcessCollectionPayment(_user.User.Id, request);
            if (!result.Success)
                return StatusCode(result.StatusCode, new { success = false, message = result.Message });

            return Ok(new { success = true, paymentReference = result.PaymentReference });
        }

        [HttpGet]
        public async Task<IActionResult> SearchInvoices(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(Array.Empty<InvoiceRecord>());
            return Ok(await _paymentTrans.SearchInvoices(q.Trim()));
        }

        [HttpGet]
        public async Task<IActionResult> Invoice(string invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return BadRequest(new { message = "Invoice number is required." });
            var invoice = await _paymentTrans.GetInvoice(invoiceNumber.Trim());
            return invoice == null
                ? NotFound(new { message = "Invoice was not found." })
                : Ok(invoice);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelInvoice([FromBody] CancelInvoiceRequest request)
        {
            var result = await _paymentTrans.CancelInvoice(_user.User.Id, request);
            return result.Success
                ? Ok(new { success = true })
                : StatusCode(result.StatusCode, new { success = false, message = result.Message });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayOtherCharges(
            [FromBody] CollectionOtherChargePaymentRequest request)
        {
            if (_user.User == null || _user.User.Id <= 0)
                return Unauthorized(new { success = false, message = "A valid cashier session is required." });
            if (request == null || !Enum.IsDefined(typeof(PaymentMethod), request.Method))
                return BadRequest(new { success = false, message = "Please select a valid payment method." });

            var inner = new TMCWD.Model.Billing.Requests.OtherChargePaymentRequest
            {
                ExternalReference = request.ExternalReference,
                Method         = request.Method,
                AmountTendered = request.AmountTendered,
                InstallmentIds = request.InstallmentIds
            };

            var result = await _otherChargeTrans.ProcessPayment(_user.User.Id, inner);
            if (!result.Success)
                return StatusCode(result.StatusCode, new { success = false, message = result.Message });

            return Ok(new { success = true, invoiceNumber = result.InvoiceNumber });
        }

        /// <summary>
        /// Lightweight account search used by the Other Charges modal.
        /// Returns account number, name, TIN, and address only.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AccountLookup(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Ok(new List<object>());

            var accounts = await _accountTrans.Search(q.Trim());
            if (accounts == null || !accounts.Any())
                return Ok(new List<object>());

            var result = new List<object>();
            foreach (var account in accounts)
            {
                var customer = await _customerTrans.Get(account.CustomerId);
                result.Add(new
                {
                    accountNumber = account.AccountNumber,
                    name          = customer != null
                        ? $"{customer.Lastname?.Trim()}, {string.Join(" ", new[] { customer.Firstname, customer.Middlename }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim()}".Trim()
                        : account.AccountNumber,
                    tin     = string.Empty,
                    address = $"{account.HouseNumber} {account.Street}, {account.Barangay}".Trim()
                });
            }
            return Ok(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRemittance([FromBody] CollectionRemittanceRequest request)        {
            if (_user.User == null || _user.User.Id <= 0)
                return Unauthorized(new { success = false, message = "A valid collector session is required." });

            try
            {
                request.CollectorId = _user.User.Id;
                var saved = await _remittanceTrans.Save(request);
                return saved.Success
                    ? Ok(new { success = true })
                    : StatusCode(502, new { success = false, message = saved.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Search accounts for the Collections page with billing and penalty information.
        /// Returns account details, unpaid bills, and payment status.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchAccounts(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Ok(new List<object>());

            var accounts = await _accountTrans.Search(q.Trim());
            if (accounts == null || !accounts.Any())
                return Ok(new List<object>());

            var result = new List<object>();
            var feeTypes = await _otherFeeTypeTrans.GetAll() ?? new List<OtherFeeType>();
            var feeTypeNames = feeTypes.ToDictionary(x => (int)x.Id, x => x.Name);

            foreach (var account in accounts)
            {
                // Get customer information for the account name
                var customer = await _customerTrans.Get(account.CustomerId);
                var accountName = customer != null 
                    ? customer.FullName
                    : "Unknown Customer";

                // Get all billings for this account using GetByAccountId
                var billings = await _billingTrans.GetByAccountId(account.Id);

                // A paid water bill can still anchor collectible installments.
                var unpaidBills = billings?
                    .OrderBy(b => b.BillingPeriod)
                    .ToList() ?? new List<Model.Billing.Interfaces.BillingBase>();

                // Calculate total amounts
                decimal totalBillAmount = 0;
                decimal totalMeterMaintenance = 0;
                decimal totalPenalty = 0;
                decimal totalOtherCharges = 0;
                decimal totalAmountDue = 0;
                var bills = new List<object>();
                var accountOtherCharges = new List<object>();

                foreach (var billing in unpaidBills)
                {
                    CollectionBill? summary;
                    try { summary = await _billingTrans.GetCollectionBill(billing.BillingReferenceId); }
                    catch (HttpRequestException error) when (error.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                    {
                        var authorization = _api.Client.DefaultRequestHeaders.Authorization;
                        _logger.LogWarning("Data API {Endpoint} returned {Status}; authorization present: {Present}, scheme: {Scheme}, user: {UserId}, role: {Role}",
                            "api/Billing/Collection/" + Uri.EscapeDataString(billing.BillingReferenceId),
                            (int)error.StatusCode.Value, authorization != null, authorization?.Scheme, _user.User.Id, _user.User.Role);
                        return StatusCode((int)error.StatusCode.Value, new { message = error.StatusCode == System.Net.HttpStatusCode.Unauthorized
                            ? "Data API authentication failed. Check the shared JWT configuration and restart both applications."
                            : "The Data API denied access to Collection details." });
                    }
                    catch (HttpRequestException error) when (error.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        return Conflict(new { message = "An Other Charge has an invalid installment ledger. Contact the billing administrator." });
                    }
                    if (summary == null || summary.AmountDue <= 0m) continue;
                    totalAmountDue += summary.AmountDue;

                    // The API returns currently collectible installment rows only.
                    var activeUnpaidCharges = summary.CollectibleOtherCharges;
                    var otherChargeAmount = summary.OtherCharges;
                    totalOtherCharges += otherChargeAmount;
                    accountOtherCharges.AddRange(activeUnpaidCharges.Select(c => (object)new
                    {
                        id = c.Id,
                        installmentId = c.InstallmentId,
                        installmentNumber = c.InstallmentNumber,
                        dueBillingPeriod = c.DueBillingPeriod.ToString("yyyy-MM"),
                        chargeType = feeTypeNames.GetValueOrDefault(c.Type, $"Charge #{c.Type}")
                            + $" — installment {c.InstallmentNumber}/{c.PayableIn}",
                        amount = c.Amount,
                        payableIn = c.PayableIn,
                        particulars = c.Particulars,
                        billingReferenceId = c.BillingReferenceId
                    }));

                    totalBillAmount += summary.BillAmount ?? 0m;
                    totalMeterMaintenance += summary.MeterMaintenanceFee ?? 0m;
                    totalPenalty += summary.Penalty;

                    bills.Add(new
                    {
                        billingReferenceId = billing.BillingReferenceId,
                        installmentIds = activeUnpaidCharges.Select(c => c.InstallmentId).ToList(),
                        serviceInvoiceNumber = summary.ServiceInvoiceNumber,
                        paymentReference = summary.PaymentReference,
                        billingPeriod = summary.BillingDate.ToString("yyyy-MM"),
                        dueDate = summary.DueDate?.ToString("yyyy-MM-dd"),
                        disconnectionDate = summary.DisconnectionDate?.ToString("yyyy-MM-dd"),
                        billAmount = summary.BillAmount,
                        meterMaintenanceFee = summary.MeterMaintenanceFee,
                        otherCharges = otherChargeAmount,
                        billingAdjustment = summary.BillingAdjustment,
                        remainingAmount = summary.RemainingAmount,
                        amountDue = summary.AmountDue,
                        totalDue = summary.TotalDue,
                        penaltyAfterDue = summary.PenaltyAfterDue,
                        totalAfterDue = summary.TotalAfterDue,
                        penalty = summary.Penalty,
                        status = billing.PaymentStatus.ToString()
                    });
                }

                result.Add(new
                {
                    accountId = account.Id,
                    accountNumber = account.AccountNumber,
                    meterNumber = account.MeterNumber,
                    classification = account.Classification.ToString(),
                    accountName,
                    address = $"{account.HouseNumber} {account.Street}, {account.Barangay}".Trim(),
                    totalBillAmount,
                    totalMeterMaintenance,
                    totalPenalty,
                    otherChargesBalance = totalOtherCharges,
                    otherCharges = accountOtherCharges,
                    totalDue = totalAmountDue,
                    unpaidCount = bills.Count,
                    bills
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Returns the list of collectors (users who have processed at least one payment)
        /// for the report filter dropdown.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Collectors()
        {
            // Fetch all users in the system — whoever is a collector/cashier.
            var allUsers = await _userTrans.GetUsers() ?? new List<User>();
            return Ok(allUsers.Select(u => new { id = u.Id, name = u.Name }));
        }

        // ---------------------------------------------------------------
        //  Collection Report helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Builds the flat list of collection rows for the given date / collector / sort criteria.
        /// Shared by both the PDF and Excel export actions.
        /// </summary>
        private async Task<List<CollectionReportRow>> BuildReportRows(CollectionReportRequest req)
        {
            // Single-query: fetch only payments in the requested date range.
            var payments = await _paymentTrans.GetByDateRange(
                req.FromDate, req.ToDate, req.CollectorId);

            if (payments.Count == 0)
                return new List<CollectionReportRow>();

            // Resolve user names
            var allUsers = await _userTrans.GetUsers() ?? new List<User>();
            var userById = allUsers.ToDictionary(u => (long)u.Id, u => u.Name);

            // Resolve billing → account → customer in bulk
            var billingRefs = payments.Select(p => p.BillingReference).Distinct().ToList();
            var allBillings = await _billingTrans.GetAll()
                              ?? new List<Model.Billing.Interfaces.BillingBase>();
            var billingByRef = allBillings
                .Where(b => billingRefs.Contains(b.BillingReferenceId))
                .GroupBy(b => b.BillingReferenceId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var accountIds = billingByRef.Values
                .Select(b => (int)b.AccountId).Distinct().ToList();
            var accountLookup  = new Dictionary<int, Account>();
            var customerLookup = new Dictionary<int, string>();

            foreach (var accId in accountIds)
            {
                var acc = await _accountTrans.Get(accId);
                if (acc == null) continue;
                accountLookup[accId] = acc;
                var cust = await _customerTrans.Get(acc.CustomerId);
                if (cust != null)
                    customerLookup[accId] = $"{cust.Lastname?.Trim()}, {string.Join(" ", new[] { cust.Firstname, cust.Middlename }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim()}".Trim();
            }

            var rows = payments.Select(p =>
            {
                billingByRef.TryGetValue(p.BillingReference, out var billing);
                var accId = billing != null ? (int)billing.AccountId : 0;
                accountLookup.TryGetValue(accId, out var accountObj);
                var account = accountObj as TMCWD.Model.CustomerSupport.Account;
                customerLookup.TryGetValue(accId, out var holder);

                return new CollectionReportRow
                {
                    OrNumber      = p.PaymentReference,
                    AccountNumber = account?.AccountNumber ?? p.BillingReference,
                    AccountHolder = holder ?? account?.AccountNumber ?? p.BillingReference,
                    Amount        = p.PaidAmount,
                    Method        = Enum.IsDefined(typeof(PaymentMethod), p.Method)
                                    ? ((PaymentMethod)p.Method).ToString() : "Unknown",
                    CollectorName = userById.TryGetValue(p.CreatedBy, out var nm)
                                    ? nm : $"User #{p.CreatedBy}",
                    DateCollected = p.DateCreated
                };
            }).ToList();

            rows = req.SortBy == "accountNo"
                ? rows.OrderBy(r => r.AccountNumber).ThenBy(r => r.DateCollected).ToList()
                : rows.OrderBy(r => r.OrNumber).ToList();

            return rows;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CollectionReportPdf([FromBody] CollectionReportRequest req)
        {
            if (req == null || req.ToDate < req.FromDate)
                return BadRequest(new { message = "Invalid date range." });

            var rows  = await BuildReportRows(req);
            var total = rows.Sum(r => r.Amount);

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:Arial,sans-serif;font-size:11px;margin:20px}");
            sb.AppendLine("h2{text-align:center;margin-bottom:4px}");
            sb.AppendLine("p.sub{text-align:center;margin:0 0 12px;font-size:10px;color:#555}");
            sb.AppendLine("table{width:100%;border-collapse:collapse}");
            sb.AppendLine("th{background:#1a4b8c;color:#fff;padding:5px 8px;text-align:left;font-size:10px}");
            sb.AppendLine("td{padding:4px 8px;border-bottom:1px solid #ddd;font-size:10px}");
            sb.AppendLine("tr:nth-child(even){background:#f4f8ff}");
            sb.AppendLine(".amt{text-align:right;font-family:monospace}");
            sb.AppendLine(".total-row td{font-weight:bold;border-top:2px solid #1a4b8c}");
            sb.AppendLine("@media print{@page{size:A4 landscape}}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h2>TMCWD Collection Report</h2>");
            sb.AppendLine($"<p class='sub'>Period: {req.FromDate:MMM d, yyyy} – {req.ToDate:MMM d, yyyy}" +
                          (req.CollectorId > 0 ? $" &nbsp;|&nbsp; Collector ID: {req.CollectorId}" : "") +
                          $" &nbsp;|&nbsp; Sorted by: {(req.SortBy == "accountNo" ? "Account No." : "OR No.")}</p>");
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>OR No.</th><th>Account No.</th><th>Account Holder</th>" +
                          "<th>Method</th><th>Collector</th><th>Date</th><th class='amt'>Amount</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var r in rows)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{HtmlEncode(r.OrNumber)}</td>");
                sb.AppendLine($"<td>{HtmlEncode(r.AccountNumber)}</td>");
                sb.AppendLine($"<td>{HtmlEncode(r.AccountHolder)}</td>");
                sb.AppendLine($"<td>{HtmlEncode(r.Method)}</td>");
                sb.AppendLine($"<td>{HtmlEncode(r.CollectorName)}</td>");
                sb.AppendLine($"<td>{r.DateCollected:MMM d, yyyy hh:mm tt}</td>");
                sb.AppendLine($"<td class='amt'>{r.Amount:N2}</td>");
                sb.AppendLine("</tr>");
            }

            // Totals row
            sb.AppendLine($"<tr class='total-row'>");
            sb.AppendLine($"<td colspan='6'>TOTAL ({rows.Count} transaction{(rows.Count == 1 ? "" : "s")})</td>");
            sb.AppendLine($"<td class='amt'>{total:N2}</td>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</tbody></table></body></html>");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var filename = $"collection-report-{req.FromDate:yyyyMMdd}-{req.ToDate:yyyyMMdd}.html";
            return File(bytes, "text/html", filename);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CollectionReportExcel([FromBody] CollectionReportRequest req)
        {
            if (req == null || req.ToDate < req.FromDate)
                return BadRequest(new { message = "Invalid date range." });

            var rows  = await BuildReportRows(req);
            var total = rows.Sum(r => r.Amount);

            var sb = new StringBuilder();
            // UTF-8 BOM so Excel opens the CSV correctly
            sb.AppendLine("OR No.,Account No.,Account Holder,Method,Collector,Date,Amount (PHP)");

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    CsvCell(r.OrNumber),
                    CsvCell(r.AccountNumber),
                    CsvCell(r.AccountHolder),
                    CsvCell(r.Method),
                    CsvCell(r.CollectorName),
                    CsvCell(r.DateCollected.ToString("yyyy-MM-dd HH:mm:ss")),
                    r.Amount.ToString("0.00")));
            }

            // Summary row
            sb.AppendLine($",,,,Total ({rows.Count} transactions),," + total.ToString("0.00"));

            // Prepend UTF-8 BOM (EF BB BF) for Excel auto-detect
            var bom   = new byte[] { 0xEF, 0xBB, 0xBF };
            var body  = Encoding.UTF8.GetBytes(sb.ToString());
            var bytes = bom.Concat(body).ToArray();

            var filename = $"collection-report-{req.FromDate:yyyyMMdd}-{req.ToDate:yyyyMMdd}.csv";
            return File(bytes, "text/csv", filename);
        }

        // CSV helper: wrap value in quotes and escape internal quotes
        private static string CsvCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // Minimal HTML encoder (avoids adding HtmlEncoder DI dependency)
        private static string HtmlEncode(string value) =>
            (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");

        // ---------------------------------------------------------------
        //  Batch Payments
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns batch-upload log rows for a date range.
        /// Consumed by the Batch Payments modal on open.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> BatchPayments(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var dateFrom = from?.Date ?? DateTime.Today.AddDays(-30);
            var dateTo   = to?.Date   ?? DateTime.Today;
            var uploads  = await _batchUploadTrans.GetByDateRange(dateFrom, dateTo);
            return Ok(uploads.Select(u => new
            {
                id           = u.Id.ToString(),
                filename     = u.Filename,
                channel      = u.Channel,
                status       = u.Status,
                dateUploaded = u.DateCreated.ToString("yyyy-MM-dd")
            }));
        }

        /// <summary>
        /// Receives a CSV file, records the upload, and returns the saved row.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchPaymentUpload(IFormFile file, [FromForm] string channel)
        {
            if (_user.User == null || _user.User.Id <= 0)
                return Unauthorized(new { success = false, message = "A valid cashier session is required." });
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file was uploaded." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".csv" && ext != ".txt")
                return BadRequest(new { success = false, message = "Only CSV files are accepted." });

            // Count data rows (excluding header)
            int rowCount = 0;
            using (var reader = new System.IO.StreamReader(file.OpenReadStream()))
            {
                bool first = true;
                while (await reader.ReadLineAsync() is { } line)
                {
                    if (first) { first = false; continue; } // skip header
                    if (!string.IsNullOrWhiteSpace(line)) rowCount++;
                }
            }

            var result = await _batchUploadTrans.Save(
                _user.User.Id,
                file.FileName,
                string.IsNullOrWhiteSpace(channel) ? "Manual" : channel.Trim(),
                rowCount);

            if (!result.Success)
                return StatusCode(502, new { success = false, message = result.Message });

            var r = result.Record!;
            return Ok(new
            {
                success      = true,
                id           = r.Id.ToString(),
                filename     = r.Filename,
                channel      = r.Channel,
                status       = r.Status,
                dateUploaded = r.DateCreated.ToString("yyyy-MM-dd")
            });
        }

        /// <summary>Returns summary detail for a single batch upload record.</summary>
        [HttpGet]
        public async Task<IActionResult> BatchPaymentDetail(long id)
        {
            var upload = await _batchUploadTrans.Get(id);
            if (upload == null) return NotFound(new { message = "Batch upload record not found." });
            return Ok(new
            {
                id           = upload.Id.ToString(),
                filename     = upload.Filename,
                channel      = upload.Channel,
                status       = upload.Status,
                rowCount     = upload.RowCount,
                dateUploaded = upload.DateCreated.ToString("yyyy-MM-dd HH:mm")
            });
        }

        #endregion
    }
}
