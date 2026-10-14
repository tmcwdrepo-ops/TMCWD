using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using TMCWD.Application;
using TMCWD.Billing;
using TMCWD.Data.Context;
using TMCWD.Data.Controllers;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Services;

static class PostingSecurity
{
    public static async Task Run(UserDbContext db, ReadingSheetService sheets,
        Func<string, Func<Task>, Task> test, Action<bool> injectFailure)
    {
        // The caller has already restricted this open connection to local tmcwd_test
        // and created every fixture table as a connection-local temporary table.
        if (db.Database.GetDbConnection().Database != "tmcwd_test")
            throw new Exception("Posting security checks require tmcwd_test.");
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        const string issuer = "synthetic-posting";
        const string audience = "synthetic-data";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IReadingSheetService>(sheets);
        builder.Services.AddSingleton<IReadingService>(new ReadingService(db, new AccountService(db)));
        builder.Services.AddSingleton<IAdvancePaymentService>(new AdvancePaymentService(db));
        builder.Services.AddSingleton<IPaymentCheckService>(new PaymentCheckService(db));
        builder.Services.AddSingleton<IChargeTypeService>(new ChargeTypeService(db));
        builder.Services.AddSingleton<IOtherFeeTypeService>(new OtherFeeTypeService(db));
        builder.Services.AddSingleton<IUserService>(new UserService(db));
        builder.Services.AddSingleton<IInspectionTypeService>(new InspectionTypeService(db));
        builder.Services.AddSingleton<IReadingSheetTemplateService>(new ReadingSheetTemplateService(db));
        builder.Services.AddSingleton<IAccountService>(new AccountService(db));
        builder.Services.AddSingleton<IZoneBookService>(new ZoneBookService(db));
        builder.Services.AddSingleton<ICustomerService>(new CustomerService(db));
        builder.Services.AddSingleton<IBillingService>(new BillingService(db));
        builder.Services.AddSingleton(new AccountLedgerService(db, new BillingService(db)));
        builder.Services.AddSingleton(new DisconnectionMonitoringService(db, new BillingService(db)));
        builder.Services.AddControllers().AddApplicationPart(typeof(ReadingSheetController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = issuer,
                ValidateAudience = true, ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization(TMCWD.Data.AuthorizationPolicies.Configure);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        var policyRoutes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Select(e => new { Endpoint = e, Policies = e.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(a => a.Policy).Where(p => p is "Collection" or "BillingOperations").Distinct().ToArray() })
            .Where(e => e.Policies.Length > 0).OrderBy(e => e.Endpoint.RoutePattern.RawText)
            .Select(e => $"| `/{e.Endpoint.RoutePattern.RawText}` | {string.Join('/', e.Endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} | {string.Join(", ", e.Policies)} |");
        await File.WriteAllLinesAsync(".tmp/phase6kb0-policy-routes.md",
            new[] { "| Route | Method | Policy |", "|---|---|---|" }.Concat(policyRoutes));
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            const string posting = "api/ReadingSheet/PartialPostReadingSheet?readingSheetId=1&userId=999";
            string Token(UserRole role, string? id = "1", bool expired = false, bool wrongKey = false)
            {
                var claims = new List<Claim> { new(ClaimTypes.Role, ((int)role).ToString()) };
                if (id != null) claims.Add(new(ClaimTypes.NameIdentifier, id));
                var handler = new JwtSecurityTokenHandler();
                return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims), Issuer = issuer, Audience = audience,
                    NotBefore = DateTime.UtcNow.AddHours(-1),
                    Expires = expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                        wrongKey ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : key)), SecurityAlgorithms.HmacSha256)
                }));
            }
            async Task Prepare()
            {
                (await db.Accounts.SingleAsync()).Status = (int)AccountStatus.Active;
                db.Readings.Add(new Reading { AccountId = 1, ReadingSheetId = 1, PreviousReading = 0,
                    CurrentReading = 10, Status = ReadingStatus.InProgress, DateCreated = DateTime.Now });
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
            async Task<string> State() => JsonSerializer.Serialize(new
            {
                Bills = await db.Billings.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Readings = await db.Readings.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Sheets = await db.ReadingSheets.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Sequence = await db.ServiceInvoiceSeq.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Charges = await db.OtherCharges.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Children = await db.OtherChargeInstallments.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Penalties = await db.Penalties.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Payments = await db.Payments.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Advance = await db.AdvancePayments.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Checks = await db.PaymentChecks.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                ChargeTypes = await db.ChargeTypes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                FeeTypes = await db.OtherFeeTypes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Accounts = await db.Accounts.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Customers = await db.Customers.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Users = await db.Users.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Templates = await db.ReadingSheetTemplates.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                InspectionTypes = await db.InspectionTypes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Details = await db.RequestDetails.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Files = await db.Files.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Histories = await db.ApprovalHistories.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Findings = await db.Findings.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Reports = await db.InspectionReports.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Materials = await db.Materials.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Recommendations = await db.Recommendations.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Workflows = await db.Workflows.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                TypeDetails = await db.InspectonTypeDetails.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
                Inventory = await db.Inventories.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
            });
            async Task Reject(string? token, HttpStatusCode expected, string path = posting,
                HttpMethod? method = null, HttpContent? body = null)
            {
                var before = await State();
                using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path) { Content = body };
                if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request);
                Check(response.StatusCode == expected, $"Posting security returned {response.StatusCode}, expected {expected}.");
                db.ChangeTracker.Clear();
                Check(await State() == before, "Rejected HTTP request changed financial/workflow state or invoice sequence.");
            }
            await CriticalBoundarySecurity.Run(db, client, (role, id) => Token(role, id), test,
                (token, status, path, method, body) => Reject(token, status, path, method, body));
            await MvcBoundarySecurity.Run(db, client.BaseAddress!, key, issuer, audience, test, State);
            foreach (var (firstRole, firstActor, otherRole, otherActor) in new[] {
                (UserRole.CustomerServiceOic, "2", UserRole.SuperAdmin, "3"),
                (UserRole.SuperAdmin, "3", UserRole.CustomerServiceOic, "2") })
            await test("HTTP Phase 6J " + firstRole + " confirmation/audit and cross-role idempotency", async () => {
                foreach (var role in new[] { UserRole.SuperAdmin, UserRole.CustomerRepresentative, UserRole.CustomerServiceOic, UserRole.Billing, UserRole.BillingHead }) {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
                    using var response = await client.GetAsync("api/DisconnectionMonitoring/TEST");
                    Check(response.StatusCode == HttpStatusCode.OK, "Authorized monitoring read rejected.");
                    var data = await response.Content.ReadFromJsonAsync<TMCWD.Model.Billing.DisconnectionMonitoringDto>();
                    Check(data?.AccountNumber == "TEST", "Monitoring selected another account.");
                }
                foreach (var action in new[] { "disconnect", "reconnect" }) {
                    foreach (var role in Enum.GetValues<UserRole>()) {
                        if (role == UserRole.CustomerServiceOic || role == UserRole.SuperAdmin) continue;
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
                        using var denied = await client.PostAsJsonAsync("api/DisconnectionMonitoring/TEST/" + action,
                            new { operationId = Guid.NewGuid(), userId = 999, amount = 1, status = "RECONNECTED" });
                        Check(denied.StatusCode == HttpStatusCode.Forbidden, "Undefined physical confirmation ownership was enabled.");
                    }
                    client.DefaultRequestHeaders.Authorization = null;
                    using var anonymous = await client.PostAsJsonAsync("api/DisconnectionMonitoring/TEST/" + action, new { operationId = Guid.NewGuid() });
                    Check(anonymous.StatusCode == HttpStatusCode.Unauthorized, "Anonymous physical confirmation accepted.");
                }
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Engineer));
                using var wrongRole = await client.GetAsync("api/DisconnectionMonitoring/TEST");
                Check(wrongRole.StatusCode == HttpStatusCode.Forbidden && !await db.ServiceDisconnectionEvents.AnyAsync()
                    && !await db.OtherCharges.AnyAsync(), "Wrong-role read or quarantined writes changed state.");
                var account = await db.Accounts.SingleAsync(); account.Status = (int)AccountStatus.Active;
                var feeType = await db.OtherFeeTypes.SingleAsync(); feeType.Name = "Reconnection Fee";
                var issuance = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-5);
                for (var i = 1; i <= 2; ++i) {
                    var date = issuance.AddMonths(i - 2);
                    var sheetId = i;
                    if (i == 1) { var sheet = await db.ReadingSheets.SingleAsync(); sheet.BillingDate = date; }
                    else db.ReadingSheets.Add(new TMCWD.Data.Entities.ReadingSheet { Id = sheetId, ZoneBookId = 1, Status = ReadingStatus.InProgress, BillingDate = date });
                    db.Readings.Add(new TMCWD.Data.Entities.Reading { Id = 500 + i, AccountId = 1, ReadingSheetId = sheetId, Status = ReadingStatus.Completed, DateCreated = date.AddDays(-1) });
                    db.Billings.Add(new TMCWD.Data.Entities.Billing { Id = 500 + i, AccountId = 1, ReadingId = 500 + i, BillingReferenceId = "HTTP-J" + i,
                        BillingPeriod = date, RemainingAmount = 190, TotalBillAmount = 190, PaymentStatus = (int)PaymentStatus.Unpaid,
                        InvoiceSnapshotJson = "{\"BillAmount\":170,\"MeterMaintenance\":20,\"TotalDue\":190}" });
                }
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(firstRole, firstActor));
                using (var invalid = await client.PostAsJsonAsync("api/DisconnectionMonitoring/TEST/disconnect", new { operationId = Guid.Empty }))
                    Check(invalid.StatusCode == HttpStatusCode.BadRequest, "Missing operation identity accepted.");
                var key6J = Guid.NewGuid(); var beforeUtc = DateTime.UtcNow;
                foreach (var retry in new[] { false, true }) {
                    using var confirmed = await client.PostAsJsonAsync("api/DisconnectionMonitoring/TEST/disconnect",
                        new { operationId = key6J, userId = 999, disconnectedBy = 999, amount = 1, actualDisconnectionDateUtc = "2000-01-01" });
                    Check(confirmed.StatusCode == HttpStatusCode.OK, "Authorized confirmation or replay rejected: " + confirmed.StatusCode);
                }
                db.ChangeTracker.Clear(); var physical = await db.ServiceDisconnectionEvents.SingleAsync();
                Check(physical.DisconnectedBy == int.Parse(firstActor) && physical.ActualDisconnectionDateUtc >= beforeUtc && physical.ActualDisconnectionDateUtc <= DateTime.UtcNow
                    && physical.Id == key6J.ToString("D") && (await db.OtherCharges.SingleAsync()).Amount == 350
                    && (await db.OtherCharges.SingleAsync()).CreatedBy == int.Parse(firstActor) && await db.OtherChargeInstallments.CountAsync() == 1
                    && (await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Suspended,
                    "Forged actor/date/amount accepted or confirmation retry duplicated event/fee.");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(otherRole, otherActor));
                foreach (var crossKey in new[] { key6J, Guid.NewGuid() }) {
                    using var cross = await client.PostAsJsonAsync("api/DisconnectionMonitoring/TEST/disconnect", new { operationId = crossKey, userId = 999 });
                    Check(cross.StatusCode == HttpStatusCode.Conflict, "Already confirmed event admitted a second actor/operation.");
                }
                db.ChangeTracker.Clear();
                Check(await db.ServiceDisconnectionEvents.CountAsync() == 1 && await db.OtherCharges.CountAsync() == 1
                    && (await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Suspended
                    && (await db.ServiceDisconnectionEvents.SingleAsync()).DisconnectedBy == int.Parse(firstActor), "Cross-role attempt duplicated liability or changed original actor/status.");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(firstRole, firstActor));
                using (var unpaid = await client.PostAsJsonAsync("api/DisconnectionMonitoring/TEST/reconnect", new { operationId = Guid.NewGuid(), eligible = true, paid = true }))
                    Check(unpaid.StatusCode == HttpStatusCode.Conflict, "Client eligibility bypassed unpaid obligations.");
                var billingService = new TMCWD.Data.Services.BillingService(db);
                var settlement = new TMCWD.Model.Billing.Requests.CollectionPaymentRequest { Method = PaymentMethod.Cash };
                foreach (var reference in await db.Billings.Where(b => b.AccountId == 1).Select(b => b.BillingReferenceId).ToListAsync()) {
                    var collection = await billingService.GetCollectionBill(reference) ?? throw new Exception("Missing collection.");
                    if (collection.AmountDue > 0) settlement.Bills.Add(new() { BillingReferenceId = reference, PaidAmount = collection.AmountDue,
                        InstallmentIds = collection.CollectibleOtherCharges.Select(c => c.InstallmentId).ToList() });
                }
                settlement.AmountTendered = settlement.Bills.Sum(b => b.PaidAmount);
                await new TMCWD.Data.Services.PaymentService(db).ProcessCollectionPayment(1, settlement);
                db.ChangeTracker.Clear();
                var reconnectKey = Guid.NewGuid(); var reconnectBefore = DateTime.UtcNow; string? reconnectResult = null;
                for (var retry = 0; retry < 2; ++retry) {
                    using var response = await client.PostAsJsonAsync("api/DisconnectionMonitoring/TEST/reconnect",
                        new { operationId = reconnectKey, userId = 999, reconnectedBy = 999, actualReconnectionDateUtc = "2000-01-01", paid = true });
                    Check(response.StatusCode == HttpStatusCode.OK, "Authorized reconnection/replay rejected: " + response.StatusCode);
                    var result = await response.Content.ReadAsStringAsync();
                    if (reconnectResult != null) Check(result == reconnectResult, "Reconnection replay changed original result.");
                    reconnectResult = result;
                }
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(otherRole, otherActor));
                foreach (var crossKey in new[] { reconnectKey, Guid.NewGuid() }) {
                    using var cross = await client.PostAsJsonAsync("api/DisconnectionMonitoring/TEST/reconnect", new { operationId = crossKey });
                    Check(cross.StatusCode == HttpStatusCode.Conflict, "Cross-role reconnection replaced original audit.");
                }
                db.ChangeTracker.Clear(); physical = await db.ServiceDisconnectionEvents.SingleAsync();
                Check(physical.ReconnectedBy == int.Parse(firstActor) && physical.ReconnectionOperationId == reconnectKey.ToString("D")
                    && physical.ActualReconnectionDateUtc >= reconnectBefore && physical.ActualReconnectionDateUtc <= DateTime.UtcNow
                    && (await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Active
                    && await db.OtherCharges.CountAsync() == 1 && await db.OtherChargeInstallments.CountAsync() == 1
                    && physical.SettlementPaymentIdsJson != null, "Reconnection lost server actor/time, settlement evidence, Active status or single-fee history.");
                client.DefaultRequestHeaders.Authorization = null;
            });
            await ReadSessionSecurity.Run(db, sheets, key, issuer, audience, test, State);
            var writes = new (string Path, HttpMethod Method, UserRole Role, string Body)[]
            {
                (Path: "api/Reading/SaveUpdate/999", Method: HttpMethod.Post, Role: UserRole.CustomerRepresentative,
                    Body: JsonSerializer.Serialize(new { accountId = 1, readingSheetId = 1, currentReading = 10,
                        previousReading = 999, status = 3, isCompleted = true, createdBy = 999, updatedBy = 999,
                        dateCreated = DateTime.Now, remarks = "Normal Reading" })),
                ("api/AdvancePayment/SaveUpdate/999", HttpMethod.Post, UserRole.Billing,
                    "{\"accountId\":1,\"amount\":10,\"createdBy\":999,\"updatedBy\":999}"),
                ("api/PaymentCheck/SaveUpdate/999", HttpMethod.Post, UserRole.Billing,
                    "{\"paymentReference\":\"synthetic-check\",\"branch\":\"Synthetic\",\"checkNumber\":\"Synthetic\",\"amount\":10,\"createdBy\":999,\"updatedBy\":999}"),
                ("api/ChargeType/SaveUpdate/999", HttpMethod.Post, UserRole.SuperAdmin,
                    "{\"name\":\"Synthetic\",\"classificationId\":1,\"createdBy\":999,\"updatedBy\":999}"),
                ("api/OtherFeeType/SaveUpdate/999", HttpMethod.Post, UserRole.SuperAdmin,
                    "{\"name\":\"Synthetic\",\"defaultValue\":10,\"createdBy\":999,\"updatedBy\":999}"),
                ("api/OtherFeeType/Delete/1", HttpMethod.Delete, UserRole.SuperAdmin, "")
            };
            foreach (var endpoint in writes)
            {
                HttpContent? Body() => endpoint.Method == HttpMethod.Delete ? null : new StringContent(endpoint.Body, Encoding.UTF8, "application/json");
                await test($"HTTP B2 anonymous rejected without writes: {endpoint.Path}",
                    () => Reject(null, HttpStatusCode.Unauthorized, endpoint.Path, endpoint.Method, Body()));
                await test($"HTTP B2 wrong role rejected without writes: {endpoint.Path}",
                    () => Reject(Token(UserRole.MeterReader), HttpStatusCode.Forbidden, endpoint.Path, endpoint.Method, Body()));
                await test($"HTTP B2 missing/malformed identity rejected without writes: {endpoint.Path}", async () =>
                {
                    foreach (var id in new string?[] { null, "", "bad", "0", "-1", "2147483648" })
                        await Reject(Token(endpoint.Role, id), HttpStatusCode.Unauthorized, endpoint.Path, endpoint.Method, Body());
                });
                await test($"HTTP B2 authorized actor ignores forged route/body identity: {endpoint.Path}", async () =>
                {
                    (await db.Accounts.SingleAsync()).Status = (int)AccountStatus.Active;
                    await db.SaveChangesAsync();
                    using var request = new HttpRequestMessage(endpoint.Method, endpoint.Path) { Content = Body() };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(endpoint.Role));
                    using var response = await client.SendAsync(request);
                    Check(response.StatusCode == HttpStatusCode.OK, $"Authorized B2 write failed: {endpoint.Path} / {response.StatusCode}.");
                    db.ChangeTracker.Clear();
                    if (endpoint.Path.StartsWith("api/Reading/"))
                    {
                        var reading = await db.Readings.SingleAsync();
                        Check(reading.CreatedBy == 1 && reading.PreviousReading == 0
                            && !reading.IsCompleted && reading.Status == ReadingStatus.InProgress,
                            "Web reading trusted server-owned fields or forged actor.");
                    }
                    else if (endpoint.Path.StartsWith("api/AdvancePayment/"))
                        Check((await db.AdvancePayments.SingleAsync()).CreatedBy == 1 && (await db.AdvancePayments.SingleAsync()).UpdatedBy == 1, "Forged advance actor.");
                    else if (endpoint.Path.StartsWith("api/PaymentCheck/"))
                        Check((await db.PaymentChecks.SingleAsync()).CreatedBy == 1 && (await db.PaymentChecks.SingleAsync()).UpdatedBy == 1, "Forged check actor.");
                    else if (endpoint.Path.StartsWith("api/ChargeType/"))
                        Check((await db.ChargeTypes.SingleAsync()).CreatedBy == 1 && (await db.ChargeTypes.SingleAsync()).UpdatedBy == 1, "Forged charge actor.");
                    else if (endpoint.Method == HttpMethod.Post)
                        Check((await db.OtherFeeTypes.SingleAsync(x => x.Name == "Synthetic")).CreatedBy == 1, "Forged fee actor.");
                    else Check(!await db.OtherFeeTypes.AnyAsync(x => x.Id == 1), "Authorized delete did not succeed.");
                });
            }
            foreach (var path in new[]
            {
                "api/Reading/SaveRange", "api/DebitCreditMemo/Save/999", "api/EWalletTransaction/SaveUpdate/999",
                "api/WebHook/SaveUpdate", "api/1/JobOrder/SaveUpdate/999", "api/Request/SaveUpdate/999",
                "api/Dashboard/NewConnection/999", "api/Dashboard/DisconnectionOrder/999", "api/Dashboard/AccountTransfer/999",
                "api/Dashboard/SaveServiceRequest/999", "api/Dashboard/AccomplishServiceRequest/1/999", "api/Dashboard/ServiceRequest/999",
                "api/Dashboard/SaveServiceInspection/999", "api/Dashboard/SaveConnectOrder/999", "api/Dashboard/SaveHouseTapping/999",
                "api/Dashboard/SaveWorkOrder/999", "api/Dashboard/ActivateAccount/999", "api/Dashboard/AdvanceStage/999",
                "api/Dashboard/ServiceSummary", "api/Dashboard/ServiceApplications/installation",
                "api/Dashboard/ServiceRequestsByDescription", "api/Dashboard/WorkOrder/installation/1"
            })
                await test($"HTTP B2 unresolved boundary is blocked for every role without writes: {path}", async () =>
                {
                    var method = path.Contains("/ServiceSummary") || path.Contains("/ServiceApplications/")
                        || path.Contains("/ServiceRequestsByDescription") || path.Contains("/WorkOrder/") ? HttpMethod.Get : HttpMethod.Post;
                    await Reject(null, HttpStatusCode.Unauthorized, path, method);
                    foreach (var role in Enum.GetValues<UserRole>())
                        await Reject(Token(role), HttpStatusCode.Forbidden, path, method);
                    await Reject(Token(UserRole.SuperAdmin, null), HttpStatusCode.Forbidden, path, method);
                    await Reject(Token(UserRole.SuperAdmin, "bad"), HttpStatusCode.Forbidden, path, method);
                });
            await test("HTTP B2 legacy GET payment writes return 405 without writes", async () =>
            {
                foreach (var path in new[] { "api/AdvancePayment/SaveUpdate/999", "api/EWalletTransaction/SaveUpdate/999" })
                    await Reject(Token(UserRole.SuperAdmin), HttpStatusCode.MethodNotAllowed, path, HttpMethod.Get);
            });
            await test("HTTP B2 persistence services require authentication before controller-alias resolution", async () =>
            {
                await Reject(null, HttpStatusCode.Unauthorized, "api/AdvancePaymentService/SaveUpdate/999", HttpMethod.Get);
                await Reject(null, HttpStatusCode.Unauthorized, "api/ChargeTypeService/SaveUpdate/999", HttpMethod.Post);
            });
            await test("HTTP B2 web reading rejects accounts outside the active sequence roster", async () =>
            {
                foreach (var status in new[] { 0, (int)AccountStatus.Active })
                {
                    (await db.Accounts.SingleAsync()).Status = status;
                    (await db.ReadingSheets.SingleAsync()).SeqFrom = 100;
                    await db.SaveChangesAsync();
                    await Reject(Token(UserRole.CustomerRepresentative), HttpStatusCode.BadRequest,
                        writes[0].Path, HttpMethod.Post, new StringContent(writes[0].Body, Encoding.UTF8, "application/json"));
                }
            });
            foreach (var kind in new[] { "OtherFeeType", "ChargeType", "Reading" })
                await test($"HTTP B2 {kind} update preserves creator and derives updater", async () =>
                {
                    (await db.Accounts.SingleAsync()).Status = (int)AccountStatus.Active;
                    string body;
                    if (kind == "OtherFeeType")
                    {
                        var fee = await db.OtherFeeTypes.SingleAsync(x => x.Id == 1);
                        fee.CreatedBy = 2;
                        body = "{\"id\":1,\"name\":\"Changed\",\"defaultValue\":10,\"createdBy\":999,\"updatedBy\":999}";
                    }
                    else if (kind == "ChargeType")
                    {
                        db.ChargeTypes.Add(new ChargeType { Id = 1, Name = "Old", CreatedBy = 2 });
                        body = "{\"id\":1,\"name\":\"Changed\",\"classificationId\":1,\"createdBy\":999,\"updatedBy\":999}";
                    }
                    else
                    {
                        db.Readings.Add(new Reading { Id = 1, AccountId = 1, ReadingSheetId = 1,
                            CurrentReading = 5, PreviousReading = 0, CreatedBy = 2,
                            DateCreated = DateTime.Now, Status = ReadingStatus.InProgress });
                        body = writes[0].Body;
                    }
                    await db.SaveChangesAsync();
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"api/{kind}/SaveUpdate/999")
                    { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.SuperAdmin));
                    using var response = await client.SendAsync(request);
                    Check(response.StatusCode == HttpStatusCode.OK, "Authorized update failed.");
                    db.ChangeTracker.Clear();
                    if (kind == "OtherFeeType") Check((await db.OtherFeeTypes.SingleAsync(x => x.Id == 1)).CreatedBy == 2
                        && (await db.OtherFeeTypes.SingleAsync(x => x.Id == 1)).UpdatedBy == 1, "Fee audit was overwritten.");
                    else if (kind == "ChargeType") Check((await db.ChargeTypes.SingleAsync()).CreatedBy == 2
                        && (await db.ChargeTypes.SingleAsync()).UpdatedBy == 1, "Charge audit was overwritten.");
                    else Check((await db.Readings.SingleAsync()).CreatedBy == 2
                        && (await db.Readings.SingleAsync()).UpdatedBy == 1, "Reading audit was overwritten.");
                });
            foreach (var scenario in new[] { "identity", "ownership", "success" })
                await test($"HTTP B2 legitimate mobile boundary: {scenario}", async () =>
                {
                    var account = await db.Accounts.SingleAsync();
                    account.Status = (int)AccountStatus.Active;
                    (await db.ReadingSheets.SingleAsync()).AssignedTo = 10;
                    db.Users.Add(new User { Id = 10, Name = "Synthetic Reader", Email = "reader@test.invalid",
                        Password = "test", Role = (int)UserRole.MeterReader, IsActive = true, IsVerified = true });
                    db.Users.Add(new User { Id = 11, Name = "Other Reader", Email = "reader2@test.invalid",
                        Password = "test", Role = (int)UserRole.MeterReader, IsActive = true, IsVerified = true });
                    await db.SaveChangesAsync();
                    const string path = "api/reading-sheet/mobile/1/readings?userId=999&requireAssignment=false";
                    var body = JsonSerializer.Serialize(new { accountId = 1, currentReading = 10,
                        remarks = "Normal Reading", readingDate = DateTime.Now, createdBy = 999 });
                    HttpContent Body() => new StringContent(body, Encoding.UTF8, "application/json");
                    if (scenario == "identity")
                    {
                        await Reject(null, HttpStatusCode.Unauthorized, path, HttpMethod.Post, Body());
                        await Reject(Token(UserRole.SuperAdmin), HttpStatusCode.Forbidden, path, HttpMethod.Post, Body());
                        foreach (var id in new string?[] { null, "bad", "0", "-1" })
                            await Reject(Token(UserRole.MeterReader, id), HttpStatusCode.Unauthorized, path, HttpMethod.Post, Body());
                    }
                    else if (scenario == "ownership")
                    {
                        await Reject(Token(UserRole.MeterReader, "11"), HttpStatusCode.Forbidden, path, HttpMethod.Post, Body());
                        (await db.Accounts.SingleAsync()).Status = (int)AccountStatus.Pending;
                        await db.SaveChangesAsync();
                        await Reject(Token(UserRole.MeterReader, "10"), HttpStatusCode.BadRequest, path, HttpMethod.Post, Body());
                    }
                    else
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = Body() };
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.MeterReader, "10"));
                        using var response = await client.SendAsync(request);
                        Check(response.StatusCode == HttpStatusCode.OK && (await db.Readings.SingleAsync()).CreatedBy == 10,
                            "Mobile workflow failed or trusted forged actor.");
                    }
                });
            foreach (var kind in new[] { "AdvancePayment", "PaymentCheck" })
                await test($"HTTP B2 {kind} update preserves creator and financial linkage", async () =>
                {
                    if (kind == "AdvancePayment")
                        db.AdvancePayments.Add(new AdvancePayment { Id = 1, AccountId = 1, Amount = 10, CreatedBy = 2 });
                    else db.PaymentChecks.Add(new PaymentCheck { Id = 1, PaymentReference = "original-reference",
                        Branch = "Synthetic", CheckNumber = "Synthetic", Amount = 10, CreatedBy = 2 });
                    await db.SaveChangesAsync();
                    var body = kind == "AdvancePayment" ? "{\"id\":1,\"accountId\":999,\"amount\":999,\"createdBy\":999,\"updatedBy\":999}"
                        : "{\"id\":1,\"paymentReference\":\"forged\",\"branch\":\"Synthetic\",\"checkNumber\":\"Synthetic\",\"amount\":999,\"createdBy\":999,\"updatedBy\":999}";
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"api/{kind}/SaveUpdate/999")
                    { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Billing));
                    using var response = await client.SendAsync(request);
                    Check(response.StatusCode == HttpStatusCode.OK, "Authorized payment metadata update failed.");
                    db.ChangeTracker.Clear();
                    if (kind == "AdvancePayment")
                    {
                        var saved = await db.AdvancePayments.SingleAsync();
                        Check(saved.CreatedBy == 2 && saved.UpdatedBy == 1 && saved.AccountId == 1 && saved.Amount == 10,
                            "Advance audit or established update semantics changed.");
                    }
                    else
                    {
                        var saved = await db.PaymentChecks.SingleAsync();
                        Check(saved.CreatedBy == 2 && saved.UpdatedBy == 1 && saved.PaymentReference == "original-reference" && saved.Amount == 10,
                            "Check audit or linkage changed.");
                    }
                });
            await test("HTTP B2 a reading cannot move to another account or sheet", async () =>
            {
                await Prepare();
                await Reject(Token(UserRole.CustomerRepresentative), HttpStatusCode.Conflict,
                    writes[0].Path, HttpMethod.Post, new StringContent(
                        JsonSerializer.Serialize(new { id = (await db.Readings.SingleAsync()).Id, accountId = 999, readingSheetId = 1 }),
                        Encoding.UTF8, "application/json"));
            });
            await test("HTTP anonymous posting returns 401 without writes", async () =>
            {
                await Prepare();
                await Reject(null, HttpStatusCode.Unauthorized);
            });
            foreach (var role in new[] { UserRole.Engineer, UserRole.MeterReader, UserRole.Guest,
                UserRole.GeneralServices, UserRole.Accounting, (UserRole)999 })
                await test($"HTTP {role} posting returns 403 without writes", async () =>
                {
                    await Prepare();
                    await Reject(Token(role), HttpStatusCode.Forbidden);
                });
            foreach (var id in new string?[] { null, "", "not-an-id", "0", "-1", "2147483648" })
                await test($"HTTP invalid posting identity ({id ?? "missing"}) returns 401 without writes", async () =>
                {
                    await Prepare();
                    await Reject(Token(UserRole.CustomerRepresentative, id), HttpStatusCode.Unauthorized);
                });
            await test("HTTP expired/invalid-signature tokens return 401 without writes", async () =>
            {
                await Prepare();
                await Reject(Token(UserRole.CustomerRepresentative, expired: true), HttpStatusCode.Unauthorized);
                await Reject(Token(UserRole.CustomerRepresentative, wrongKey: true), HttpStatusCode.Unauthorized);
            });
            foreach (var role in new[] { UserRole.SuperAdmin, UserRole.CustomerRepresentative, UserRole.CustomerServiceOic, UserRole.Billing, UserRole.BillingHead })
                await test($"HTTP {role} posting ignores forged userId and preserves issued bill on retry", async () =>
                {
                    await Prepare();
                    using var request = new HttpRequestMessage(HttpMethod.Post, posting);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
                    using var response = await client.SendAsync(request);
                    using var posted = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    Check(response.StatusCode == HttpStatusCode.OK
                        && posted.RootElement.GetProperty("postedCount").GetInt32() == 1,
                        "Authorized HTTP posting failed.");
                    db.ChangeTracker.Clear();
                    var bill = await db.Billings.AsNoTracking().SingleAsync();
                    var reading = await db.Readings.AsNoTracking().SingleAsync();
                    var snapshot = JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(bill.InvoiceSnapshotJson!)!;
                    Check(bill.CreatedBy == 1 && bill.UpdatedBy == 1 && reading.UpdatedBy == 1
                        && reading.Status == ReadingStatus.Completed && reading.IsCompleted
                        && bill.TotalBillAmount == 120m && bill.RemainingAmount == 120m
                        && snapshot.BillAmount == 100m && snapshot.MeterMaintenance == 20m
                        && !string.IsNullOrEmpty(bill.ServiceInvoiceNumber), "Posting audit identity or financial contract changed.");
                    var beforeRetry = await State();
                    using var retry = new HttpRequestMessage(HttpMethod.Post, posting);
                    retry.Headers.Authorization = request.Headers.Authorization;
                    using var retried = await client.SendAsync(retry);
                    Check(retried.StatusCode == HttpStatusCode.OK && await State() == beforeRetry,
                        "Posting retry changed a financial record, snapshot or invoice sequence.");
                });
            await test("HTTP failure after SQL save rolls back bill, sequence and reading completion", async () =>
            {
                await Prepare();
                injectFailure(true);
                await Reject(Token(UserRole.CustomerRepresentative), HttpStatusCode.Conflict);
                injectFailure(false);
            });
            foreach (var (path, method, body) in new (string, HttpMethod, string?)[]
            {
                ("api/ReadingSheet/CompleteReadingSheet?readingSheetId=1&userId=999", HttpMethod.Post, null),
                ("api/ReadingSheet/UpdateReadingSheetsStatus/999/3?ids=1", HttpMethod.Patch, null),
                ("api/ReadingSheet/SaveUpdate/999", HttpMethod.Post, "{\"id\":1,\"status\":3}"),
                ("api/ReadingSheet/UpdateZoneProgress", HttpMethod.Post, "{\"readingSheetId\":1}")
            })
                await test($"HTTP sheet lifecycle rejects anonymous/wrong role/missing identity: {path.Split('?')[0]}", async () =>
                {
                    await Prepare();
                    HttpContent? Body() => body == null ? null : new StringContent(body, Encoding.UTF8, "application/json");
                    await Reject(null, HttpStatusCode.Unauthorized, path, method, Body());
                    await Reject(Token(UserRole.MeterReader), HttpStatusCode.Forbidden, path, method, Body());
                    await Reject(Token(UserRole.CustomerRepresentative, null), HttpStatusCode.Unauthorized, path, method, Body());
                });
            await test("Actual MVC session client posts with signed identity and forwards 401/403", async () =>
            {
                await Prepare();
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Jwt:Key"] = key, ["Jwt:Issuer"] = issuer, ["Jwt:Audience"] = audience }).Build());
                var user = new AuthenticatedUserService();
                user.SecurityStamp = "synthetic-bridge-stamp";
                services.AddSingleton(user);
                services.AddHttpClient("TmcWdApi", http => http.BaseAddress = client.BaseAddress);
                services.AddSessionApiClient();
                services.AddTransient<ReadingTransaction>();
                foreach (var parameter in typeof(TMCWD.Application.Controllers.ReadingSheetController).GetConstructors().Single().GetParameters())
                    if (parameter.ParameterType != typeof(AuthenticatedUserService)) services.AddTransient(parameter.ParameterType);
                await using var provider = services.BuildServiceProvider();
                foreach (var (id, role, expected) in new[]
                {
                    (0, UserRole.CustomerRepresentative, 401), (1, UserRole.MeterReader, 403),
                    (1, UserRole.CustomerRepresentative, 200)
                })
                {
                    user.SetUser(new TMCWD.Model.Administrator.User { Id = id, Role = (int)role });
                    await using var scope = provider.CreateAsyncScope();
                    var controller = ActivatorUtilities.CreateInstance<TMCWD.Application.Controllers.ReadingSheetController>(scope.ServiceProvider);
                    if (expected == 200)
                    {
                        var reading = await scope.ServiceProvider.GetRequiredService<ReadingTransaction>().SaveUpdate(999,
                            new TMCWD.Model.Billing.Reading { AccountId = 1, ReadingSheetId = 1, CurrentReading = 20,
                                PreviousReading = 999, CreatedBy = 999, DateCreated = DateTime.Now });
                        Check(reading != null && (await db.Readings.SingleAsync()).UpdatedBy == 1,
                            "Signed MVC Reading client did not preserve the claim-derived actor.");
                    }
                    var before = await State();
                    var result = (Microsoft.AspNetCore.Mvc.Infrastructure.IStatusCodeActionResult)await controller.PartialPostReadingSheet(1);
                    Check(result.StatusCode == expected, "MVC changed the Data posting authorization status.");
                    if (expected != 200)
                    {
                        Check(await State() == before, "Rejected MVC request changed financial state.");
                        var completion = (Microsoft.AspNetCore.Mvc.StatusCodeResult)await controller.CompleteReadingSheet(1);
                        Check(completion.StatusCode == expected && await State() == before,
                            "MVC changed completion authorization status or state.");
                    }
                    else
                    {
                        var completion = (Microsoft.AspNetCore.Mvc.OkObjectResult)await controller.CompleteReadingSheet(1);
                        Check(completion.StatusCode == 200
                            && (await db.ReadingSheets.AsNoTracking().SingleAsync()).Status == ReadingStatus.Completed,
                            "Authorized MVC completion failed after posting.");
                    }
                }
                Check((await db.Billings.AsNoTracking().SingleAsync()).CreatedBy == 1,
                    "Actual MVC bridge failed to preserve the signed audit identity.");
            });
        }
        finally { await app.StopAsync(); }
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
