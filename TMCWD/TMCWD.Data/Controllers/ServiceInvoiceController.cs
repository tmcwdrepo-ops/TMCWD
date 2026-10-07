using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers;

/// <summary>
/// Authenticated collection-side lookup by ServiceInvoiceNumber.
/// Resolves a service invoice to its billing and account details so a
/// cashier can confirm the bill before processing payment.
/// This endpoint does NOT process payment.
/// </summary>
[ApiController]
[Route("api/collection")]
[Authorize(Policy = "Collection")]
public class ServiceInvoiceController(
    IBillingService billings,
    IAccountService accounts,
    ICustomerService customers,
    IBirInvoiceConfigService birConfig) : ControllerBase
{
    /// <summary>
    /// GET /api/collection/service-invoice/{serviceInvoiceNumber}
    ///
    /// Returns authoritative billing + account information for the given
    /// service invoice number. Only Cashier and SuperAdmin may access it.
    /// </summary>
    [HttpGet("service-invoice/{serviceInvoiceNumber}")]
    public async Task<IActionResult> GetByServiceInvoice(string serviceInvoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(serviceInvoiceNumber))
            return BadRequest("Service invoice number is required.");

        var billing = await billings.GetByServiceInvoiceNumber(serviceInvoiceNumber.Trim());
        if (billing == null)
            return NotFound("No billing record found for this service invoice number.");

        var account = await accounts.Get(checked((int)billing.AccountId));
        if (account == null)
            return NotFound("The account linked to this billing no longer exists.");

        var customer = await customers.Get(checked((int)account.CustomerId));
        var name = customer == null
            ? account.AccountNumber
            : string.Join(" ", new[] { customer.Firstname, customer.Middlename, customer.Lastname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        var address = string.Join(" ",
            new[] { account.UnitNumber, account.Building, account.HouseNumber, account.Street, account.Barangay }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        // Return only safe public identifiers — no personal data beyond what
        // the cashier needs to confirm the right bill.
        return Ok(new
        {
            serviceInvoiceNumber = billing.ServiceInvoiceNumber,
            billingReferenceId   = billing.BillingReferenceId,   // internal key, returned for cashier tooling
            billingId            = billing.Id,
            accountId            = billing.AccountId,
            accountNumber        = account.AccountNumber,
            name,
            address,
            meterNumber          = account.MeterNumber,
            billingPeriod        = billing.BillingPeriod,
            totalBillAmount      = billing.TotalBillAmount,
            remainingAmount      = billing.RemainingAmount,
            paymentStatus        = ((PaymentStatus)billing.PaymentStatus).ToString(),
            paymentTransactionId = string.IsNullOrWhiteSpace(billing.PaymentTransactionId)
                                       ? null
                                       : billing.PaymentTransactionId,
        });
    }
}
