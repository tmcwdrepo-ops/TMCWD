using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TMCWD.Application;
using TMCWD.Data.Context;
using TMCWD.Services;

static class MvcBoundarySecurity
{
    public static async Task Run(UserDbContext db, Uri dataAddress, string key, string issuer, string audience,
        Func<string, Func<Task>, Task> test, Func<Task<string>> state)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Jwt:Key"] = key, ["Jwt:Issuer"] = issuer, ["Jwt:Audience"] = audience });
        var user = new AuthenticatedUserService();
        user.SecurityStamp = "synthetic-bridge-stamp";
        user.SetUser(new TMCWD.Model.Administrator.User { Id = 3, Role = (int)UserRole.SuperAdmin });
        builder.Services.AddSingleton(user);
        builder.Services.AddHttpClient("TmcWdApi", http => http.BaseAddress = dataAddress);
        builder.Services.AddSessionApiClient();
        var controllers = new[] { typeof(TMCWD.Application.Controllers.AdminController), typeof(TMCWD.Application.Controllers.AccountController),
            typeof(TMCWD.Application.Controllers.CustomerController), typeof(TMCWD.Application.Controllers.CustomerSupportController),
            typeof(TMCWD.Application.Controllers.ReadingSheetController), typeof(TMCWD.Application.Controllers.BillingController),
            typeof(TMCWD.Application.Controllers.CollectionsController) };
        foreach (var dependency in controllers.SelectMany(c => c.GetConstructors().Single().GetParameters())
            .Select(p => p.ParameterType).Where(t => t != typeof(AuthenticatedUserService) && t != typeof(WebService)
                && (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(ILogger<>))).Distinct())
            builder.Services.AddTransient(dependency);
        builder.Services.AddControllersWithViews().PartManager.ApplicationParts.Clear();
        builder.Services.AddControllersWithViews().AddApplicationPart(controllers[0].Assembly);
        builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        await using var app = builder.Build();
        app.MapControllers();
        app.MapControllerRoute("default", "{controller}/{action}");
        // Synthetic host only: supplies a real cookie/token pair for the MVC anti-forgery checks.
        app.MapGet("/synthetic-token", (Microsoft.AspNetCore.Http.HttpContext context, IAntiforgery anti) =>
            anti.GetAndStoreTokens(context).RequestToken!);
        await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            await test("HTTP MVC Collections admits Billing/BillingHead/SuperAdmin and denies other roles", async () => {
                foreach (var role in Enum.GetValues<UserRole>()) {
                    user.SetUser(new TMCWD.Model.Administrator.User { Id = 3, Name = "Synthetic collector", Role = (int)role });
                    using var response = await client.GetAsync("Collections/CurrentCollector");
                    Check(response.StatusCode == (role is UserRole.Billing or UserRole.BillingHead or UserRole.SuperAdmin
                        ? HttpStatusCode.OK : HttpStatusCode.Forbidden), "MVC Collections role boundary changed.");
                }
                user.SetUser(new TMCWD.Model.Administrator.User { Id = 3, Role = (int)UserRole.SuperAdmin });
            });
            var paths = new[] { "Admin/SaveUser", "Admin/DeactivateUser?userId=1", "Admin/SaveUpdateInspectionType",
                "Admin/DeactivateInspectionType?inspectionTypeId=1", "Account/SaveAccount", "Account/AddAccount", "Account/SaveUpdateAccount",
                "Customer/SaveUpdateCustomer", "CustomerSupport/SaveCustomerInformation", "CustomerSupport/SaveAccountInformation",
                "CustomerSupport/SaveCustomer", "CustomerSupport/DeactivateCustomer?customerId=1",
                "CustomerSupport/ConfirmDisconnection?accountNumber=TEST",
                "ReadingSheet/SaveReadingSheetTemplate", "ReadingSheet/DeactivateReadingSheetTemplate?id=1" };
            await test("HTTP MVC AccountLedgerData retains exact account and unknown-account 404", async () => {
                var first = await db.Accounts.SingleAsync();
                first.AccountNumber = "01-010003";
                db.Accounts.Add(new TMCWD.Data.Entities.Account { Id = 10, CustomerId = first.CustomerId,
                    AccountNumber = "01-010010", ZoneBookId = first.ZoneBookId, Classification = first.Classification,
                    MeterSize = first.MeterSize, Status = first.Status, DateCreated = first.DateCreated, DateUpdated = first.DateUpdated });
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                var before = await state();
                foreach (var number in new[] { "01-010010", "01-010003", "01-010010" }) {
                    using var response = await client.GetAsync("Billing/AccountLedgerData?accountNumber=" + number);
                    Check(response.StatusCode == HttpStatusCode.OK, "MVC ledger lookup failed: " + response.StatusCode);
                    var result = await response.Content.ReadFromJsonAsync<TMCWD.Model.Billing.AccountLedgerDto>();
                    Check(result?.Account.AccountNumber == number, "MVC ledger substituted a different account.");
                }
                using var missing = await client.GetAsync("Billing/AccountLedgerData?accountNumber=01-019999");
                Check(missing.StatusCode == HttpStatusCode.NotFound, "Unknown account fell back to another account.");
                Check(await state() == before, "Ledger reads changed persisted state.");
            });
            await test("HTTP B3 MVC writes require anti-forgery before the signed Data API bridge", async () => {
                foreach (var path in paths) {
                    var before = await state();
                    using var response = await client.PostAsJsonAsync(path, new { id = 1, userId = 999 });
                    Check(response.StatusCode == HttpStatusCode.BadRequest, $"MVC accepted missing anti-forgery: {path} / {response.StatusCode}.");
                    db.ChangeTracker.Clear(); Check(await state() == before, "Rejected MVC request changed persisted state.");
                }
            });
            await test("HTTP B3 MVC retired mutating GET actions cannot write", async () => {
                foreach (var path in new[] { "Admin/DeactivateUser?userId=1", "Admin/DeactivateInspectionType?inspectionTypeId=1",
                    "CustomerSupport/DeactivateCustomer?customerId=1", "Account/DeactivateAccount?accountId=1", "CustomerSupport/DeactivateAccount?accountId=1" }) {
                    var before = await state(); using var response = await client.GetAsync(path);
                    Check(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.Conflict,
                        $"MVC GET still permits mutation: {path} / {response.StatusCode}.");
                    db.ChangeTracker.Clear(); Check(await state() == before, "MVC GET changed stored state.");
                }
            });
            await test("HTTP B3 MVC user form keeps plaintext credentials in body and saves using the signed actor", async () => {
                var csrf = await client.GetStringAsync("synthetic-token");
                using var request = new HttpRequestMessage(HttpMethod.Post, "Admin/SaveUser") {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string> {
                        ["AddEditUser.Id"] = "1", ["AddEditUser.Name"] = "MVC Updated", ["AddEditUser.Email"] = "csr@test.invalid",
                        ["AddEditUser.Role"] = "2", ["AddEditUser.IsActive"] = "true", ["AddEditUser.UpdatedBy"] = "999",
                        ["Password"] = "unused", ["ConfirmPassword"] = "unused" }) };
                request.Headers.Add("RequestVerificationToken", csrf);
                using var response = await client.SendAsync(request);
                Check(response.StatusCode == HttpStatusCode.Redirect, $"MVC user form failed: {response.StatusCode}.");
                db.ChangeTracker.Clear(); var saved = await db.Users.SingleAsync(u => u.Id == 1);
                Check(saved.Name == "MVC Updated" && saved.UpdatedBy == 3 && saved.Password == "test", "MVC mapper changed password or audit authority.");
            });
            foreach (var (role, actorId) in new[] { (UserRole.CustomerServiceOic, 2), (UserRole.SuperAdmin, 3) })
            await test("HTTP Phase 6J MVC " + role + " confirmation uses CSRF and the signed claim actor", async () => {
                var account = await db.Accounts.SingleAsync(); account.Status = (int)AccountStatus.Active;
                var feeType = await db.OtherFeeTypes.SingleAsync(); feeType.Name = "Reconnection Fee";
                var issuance = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-5);
                for (var i = 1; i <= 2; ++i) {
                    var date = issuance.AddMonths(i - 2);
                    if (i == 1) (await db.ReadingSheets.SingleAsync()).BillingDate = date;
                    else db.ReadingSheets.Add(new TMCWD.Data.Entities.ReadingSheet { Id = i, ZoneBookId = 1, Status = ReadingStatus.InProgress, BillingDate = date });
                    db.Readings.Add(new TMCWD.Data.Entities.Reading { Id = 600 + i, AccountId = 1, ReadingSheetId = i, Status = ReadingStatus.Completed, DateCreated = date.AddDays(-1) });
                    db.Billings.Add(new TMCWD.Data.Entities.Billing { Id = 600 + i, AccountId = 1, ReadingId = 600 + i, BillingReferenceId = "MVC-J" + i,
                        BillingPeriod = date, RemainingAmount = 190, TotalBillAmount = 190, PaymentStatus = (int)PaymentStatus.Unpaid });
                }
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                var csrf = await client.GetStringAsync("synthetic-token"); var operation = Guid.NewGuid();
                async Task<HttpResponseMessage> Submit() {
                    using var request = new HttpRequestMessage(HttpMethod.Post, "CustomerSupport/ConfirmDisconnection?accountNumber=TEST") {
                        Content = JsonContent.Create(new { operationId = operation, userId = 999, disconnectedBy = 999, amount = 1 }) };
                    request.Headers.Add("RequestVerificationToken", csrf);
                    return await client.SendAsync(request);
                }
                foreach (var deniedRole in new[] { UserRole.Billing, UserRole.BillingHead, UserRole.GeneralServices, UserRole.Accounting, UserRole.CustomerRepresentative, UserRole.MeterReader, UserRole.Engineer, UserRole.Guest }) {
                    user.SetUser(new TMCWD.Model.Administrator.User { Id = 1, Role = (int)deniedRole });
                    using var rejected = await Submit(); Check(rejected.StatusCode == HttpStatusCode.Forbidden, "MVC enabled unauthorized role.");
                    var page = await client.GetStringAsync("CustomerSupport/DisconnectionMonitoring");
                    Check(page.Contains("data-can-disconnect=\"false\""), "Razor allows physical buttons for an unauthorized role.");
                }
                user.SetUser(new TMCWD.Model.Administrator.User { Id = 0 });
                using (var anonymous = await Submit()) Check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "Anonymous MVC physical confirmation accepted.");
                user.SetUser(new TMCWD.Model.Administrator.User { Id = actorId, Role = (int)role });
                try {
                    var page = await client.GetStringAsync("CustomerSupport/DisconnectionMonitoring");
                    Check(page.Contains("data-can-disconnect=\"true\""), "Razor hides physical confirmation from an authorized role.");
                    using var missingCsrf = await client.PostAsJsonAsync("CustomerSupport/ConfirmDisconnection?accountNumber=TEST", new { operationId = operation });
                    Check(missingCsrf.StatusCode == HttpStatusCode.BadRequest, "CS OIC confirmation bypassed CSRF.");
                    for (var i = 0; i < 2; ++i) { using var response = await Submit(); Check(response.StatusCode == HttpStatusCode.OK, "MVC CS OIC bridge/retry failed: " + response.StatusCode); }
                    db.ChangeTracker.Clear(); var physical = await db.ServiceDisconnectionEvents.SingleAsync();
                    Check(physical.Id == operation.ToString("D") && physical.DisconnectedBy == actorId
                        && await db.OtherChargeInstallments.CountAsync() == 1 && (await db.OtherCharges.SingleAsync()).Amount == 350
                        && (await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Suspended,
                        "MVC bridge lost key, claim actor or single-fee integrity.");
                    var billingService = new TMCWD.Data.Services.BillingService(db);
                    var settlement = new TMCWD.Model.Billing.Requests.CollectionPaymentRequest { Method = PaymentMethod.Cash };
                    foreach (var reference in await db.Billings.Where(b => b.AccountId == 1).Select(b => b.BillingReferenceId).ToListAsync()) {
                        var collection = await billingService.GetCollectionBill(reference) ?? throw new Exception("Missing collection.");
                        if (collection.AmountDue > 0) settlement.Bills.Add(new() { BillingReferenceId = reference, PaidAmount = collection.AmountDue,
                            InstallmentIds = collection.CollectibleOtherCharges.Select(c => c.InstallmentId).ToList() });
                    }
                    settlement.AmountTendered = settlement.Bills.Sum(b => b.PaidAmount);
                    await new TMCWD.Data.Services.PaymentService(db).ProcessCollectionPayment(1, settlement); db.ChangeTracker.Clear();
                    var reconnectionKey = Guid.NewGuid(); var beforeReconnection = DateTime.UtcNow;
                    async Task<HttpResponseMessage> Reconnect() {
                        using var request = new HttpRequestMessage(HttpMethod.Post, "CustomerSupport/ConfirmReconnection?accountNumber=TEST") {
                            Content = JsonContent.Create(new { operationId = reconnectionKey, userId = 999, reconnectedBy = 999, actualReconnectionDateUtc = "2000-01-01" }) };
                        request.Headers.Add("RequestVerificationToken", csrf); return await client.SendAsync(request);
                    }
                    foreach (var deniedRole in new[] { UserRole.Billing, UserRole.BillingHead, UserRole.GeneralServices, UserRole.Accounting, UserRole.CustomerRepresentative, UserRole.MeterReader, UserRole.Engineer, UserRole.Guest }) {
                        user.SetUser(new TMCWD.Model.Administrator.User { Id = 1, Role = (int)deniedRole });
                        using var denied = await Reconnect(); Check(denied.StatusCode == HttpStatusCode.Forbidden, "MVC reconnection allowed unauthorized role.");
                        var deniedPage = await client.GetStringAsync("CustomerSupport/DisconnectionMonitoring");
                        Check(deniedPage.Contains("data-can-reconnect=\"false\""), "MVC reconnection permission leaks to denied role.");
                    }
                    user.SetUser(new TMCWD.Model.Administrator.User { Id = 0 });
                    using (var anonymous = await Reconnect()) Check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "Anonymous MVC reconnection accepted.");
                    user.SetUser(new TMCWD.Model.Administrator.User { Id = actorId, Role = (int)role });
                    using (var noCsrf = await client.PostAsJsonAsync("CustomerSupport/ConfirmReconnection?accountNumber=TEST", new { operationId = reconnectionKey }))
                        Check(noCsrf.StatusCode == HttpStatusCode.BadRequest, "MVC reconnection bypassed CSRF.");
                    for (var retry = 0; retry < 2; ++retry) { using var response = await Reconnect(); Check(response.StatusCode == HttpStatusCode.OK, "MVC reconnection bridge/retry failed."); }
                    db.ChangeTracker.Clear(); physical = await db.ServiceDisconnectionEvents.SingleAsync();
                    Check(physical.ReconnectedBy == actorId && physical.ReconnectionOperationId == reconnectionKey.ToString("D")
                        && physical.ActualReconnectionDateUtc >= beforeReconnection && physical.ActualReconnectionDateUtc <= DateTime.UtcNow
                        && (await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Active
                        && await db.OtherCharges.CountAsync() == 1 && await db.OtherChargeInstallments.CountAsync() == 1,
                        "MVC reconnection changed actor/time or duplicated fee instead of restoring Active.");
                } finally { user.SetUser(new TMCWD.Model.Administrator.User { Id = 3, Role = (int)UserRole.SuperAdmin }); }
            });
            await test("HTTP B3 MVC account/customer forms preserve the authorized creation flow", async () => {
                var csrf = await client.GetStringAsync("synthetic-token");
                using var customerRequest = new HttpRequestMessage(HttpMethod.Post, "CustomerSupport/SaveCustomerInformation") {
                    Content = JsonContent.Create(new { id = 1, firstname = "MVC Customer", middlename = "", lastname = "Tester",
                        email = "csr@test.invalid", phoneNumber = "09000000000", isActive = true, createdBy = 999, updatedBy = 999 }) };
                customerRequest.Headers.Add("RequestVerificationToken", csrf);
                using var customerResponse = await client.SendAsync(customerRequest);
                Check(customerResponse.StatusCode == HttpStatusCode.OK, "MVC customer DTO bridge failed.");
                using var accountRequest = new HttpRequestMessage(HttpMethod.Post, "Account/AddAccount") {
                    Content = JsonContent.Create(new { customerId = "1", accountAddress = "Synthetic house" }) };
                accountRequest.Headers.Add("RequestVerificationToken", csrf);
                using var accountResponse = await client.SendAsync(accountRequest);
                Check(accountResponse.StatusCode == HttpStatusCode.OK, $"MVC account creation bridge failed: {accountResponse.StatusCode}.");
                db.ChangeTracker.Clear();
                Check((await db.Customers.SingleAsync()).Updatedby == 3
                    && (await db.Accounts.SingleAsync(a => a.Id != 1)).CreatedBy == 3, "MVC trusted forged audit identity.");
            });
        }
        finally { await app.StopAsync(); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
