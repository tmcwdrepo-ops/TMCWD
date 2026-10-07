using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TMCWD.Data.Controllers;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Utility.Encryption;

// Real HTTP, controllers and bearer middleware; synthetic services, no database writes.
var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
const string password = "Synthetic regression password";
const string issuer = "synthetic-tmcwd";
const string audience = "synthetic-reader";
var reader = new User { Id = 10, Email = "reader@example.invalid", Name = "Synthetic reader",
    Role = (int)UserRole.MeterReader, IsActive = true, IsVerified = true,
    Password = StringEncyption.Encrypt(password) };
var sheets = new List<ReadingSheet>
{
    new() { Id = 1, AssignedTo = 10, Status = ReadingStatus.Created, Name = "Created", ZoneBookId = 1 },
    new() { Id = 2, AssignedTo = 10, Status = ReadingStatus.InProgress, ZoneBookId = 1 },
    new() { Id = 3, AssignedTo = 10, Status = ReadingStatus.Completed, ZoneBookId = 1 },
    new() { Id = 4, AssignedTo = 10, Status = ReadingStatus.Deleted, ZoneBookId = 1 },
    new() { Id = 5, AssignedTo = 11, Status = ReadingStatus.Created, ZoneBookId = 1 }
};
var requestedReaders = new List<int>();
var users = DispatchProxy.Create<IUserService, SyntheticService>();
((SyntheticService)(object)users).Call = (method, args) => method switch
{
    nameof(IUserService.GetByEmail) => Task.FromResult<User?>(Equals(args[0], reader.Email) ? reader : null),
    nameof(IUserService.Get) => Task.FromResult<User?>((int)args[0]! == 10 ? reader : null),
    _ => throw new NotSupportedException(method)
};
var readings = DispatchProxy.Create<IReadingSheetService, SyntheticService>();
((SyntheticService)(object)readings).Call = (method, args) =>
{
    if (method == nameof(IReadingSheetService.Get))
        return Task.FromResult(sheets.FirstOrDefault(s => s.Id == (int)args[0]!));
    if (method != nameof(IReadingSheetService.GetByAssignedTo)) throw new NotSupportedException(method);
    requestedReaders.Add((int)args[0]!);
    // Include an unrelated row to test the controller's boundary as well as its service argument.
    return Task.FromResult(sheets.ToList());
};
var builder = WebApplication.CreateBuilder();
var downloadAccounts = new List<Account>
{
    new() { Id = 20, CustomerId = 30, AccountNumber = "SYN-20", MeterNumber = "M-20",
        ZoneBookId = 1, Sequence = 10, Status = (int)AccountStatus.Active, InitialReading = 7,
        MeterSize = 0.5m, Street = "Synthetic Street" },
    new() { Id = 21, CustomerId = 30, AccountNumber = "SYN-21", ZoneBookId = 1,
        Sequence = 11, Status = (int)AccountStatus.Active, InitialReading = 9 }
};
var downloadReadings = new List<Reading>();
var soaBillings = new List<Billing>();
var billingService = DispatchProxy.Create<IBillingService, SyntheticService>();
((SyntheticService)(object)billingService).Call = (method, args) => method switch
{
    nameof(IBillingService.GetByAccountId) => Task.FromResult(soaBillings.Where(b => b.AccountId == (int)args[0]!).ToList()),
    nameof(IBillingService.GetByServiceInvoiceNumber) => Task.FromResult(soaBillings.SingleOrDefault(b => b.ServiceInvoiceNumber == (string)args[0]!)),
    nameof(IBillingService.GetCollectionBill) => Task.FromResult<TMCWD.Model.Billing.CollectionBill?>(new() { BillingReferenceId = (string)args[0]!, AmountDue = 190m }),
    _ => throw new NotSupportedException(method)
};
builder.Services.AddSingleton(billingService);
var cashierWrites = new List<int>();
var paymentsService = DispatchProxy.Create<IPaymentService, SyntheticService>();
((SyntheticService)(object)paymentsService).Call = (method, args) => {
    if (method == nameof(IPaymentService.ProcessCollectionPayment)) {
        cashierWrites.Add((int)args[0]!);
        return Task.FromResult(new List<Payment> { new() { PaymentReference = "SYNTHETIC-PAYMENT", PaidAmount = 190m } });
    }
    throw new NotSupportedException(method);
};
builder.Services.AddSingleton(paymentsService);
var penaltyService = DispatchProxy.Create<IPenaltyService, SyntheticService>();
((SyntheticService)(object)penaltyService).Call = (method, args) => {
    if (method == nameof(IPenaltyService.GetByReference)) return Task.FromResult(new List<Penalty>());
    if (method == nameof(IPenaltyService.PostCharges)) {
        cashierWrites.Add((int)args[0]!);
        return Task.FromResult(new TMCWD.Model.Billing.PostPenaltyChargesResult());
    }
    throw new NotSupportedException(method);
};
builder.Services.AddSingleton(penaltyService);
var configWrites = 0;
var birService = DispatchProxy.Create<IBirInvoiceConfigService, SyntheticService>();
((SyntheticService)(object)birService).Call = (method, args) => method switch
{
    nameof(IBirInvoiceConfigService.GetActive) => Task.FromResult<BirInvoiceConfig?>(null),
    nameof(IBirInvoiceConfigService.GetAll) => Task.FromResult(new List<BirInvoiceConfig>()),
    nameof(IBirInvoiceConfigService.SaveUpdate) => SaveConfig(args),
    _ => throw new NotSupportedException(method)
};
Task<BirInvoiceConfig> SaveConfig(object?[] args)
{
    Check((int)args[0]! == 10, "BIR modification identity must come from claims.");
    configWrites++;
    return Task.FromResult((BirInvoiceConfig)args[1]!);
}
builder.Services.AddSingleton(birService);
var accountService = DispatchProxy.Create<IAccountService, SyntheticService>();
((SyntheticService)(object)accountService).Call = (method, args) => method switch
{
    nameof(IAccountService.GetByZoneBookAndSequence) => Task.FromResult<IEnumerable<Account>>(downloadAccounts.ToList()),
    nameof(IAccountService.GetReadingSheetRoster) => Task.FromResult(downloadAccounts
        .Where(a => a.ZoneBookId == ((ReadingSheet)args[0]!).ZoneBookId
            && a.Status == (int)AccountStatus.Active
            && (((ReadingSheet)args[0]!).SeqFrom.GetValueOrDefault() == 0 || a.Sequence >= ((ReadingSheet)args[0]!).SeqFrom)
            && (((ReadingSheet)args[0]!).SeqTo.GetValueOrDefault() == 0 || a.Sequence <= ((ReadingSheet)args[0]!).SeqTo)).ToList()),
    nameof(IAccountService.Get) => Task.FromResult(downloadAccounts.FirstOrDefault(a => a.Id == (int)args[0]!)),
    _ => throw new NotSupportedException(method)
};
var readingService = DispatchProxy.Create<IReadingService, SyntheticService>();
Task<Reading> UploadSynthetic(object?[] args)
{
    var sheet = sheets.SingleOrDefault(s => s.Id == (int)args[1]!);
    if (sheet == null || sheet.Status == ReadingStatus.Deleted) throw new KeyNotFoundException();
    if (sheet.AssignedTo != (int)args[0]!) throw new UnauthorizedAccessException();
    var upload = (TMCWD.Model.Billing.Requests.MobileReadingUpload)args[2]!;
    if (!accountService.GetReadingSheetRoster(sheet).Result.Any(a => a.Id == upload.AccountId)) throw new ArgumentException("Outside roster.");
    return Task.FromResult(new Reading { Id = 300, AccountId = upload.AccountId, ReadingSheetId = sheet.Id,
        CurrentReading = upload.CurrentReading!.Value, Remarks = upload.Remarks });
}
((SyntheticService)(object)readingService).Call = (method, args) => method switch
{
    nameof(IReadingService.Sync) => UploadSynthetic(args),
    nameof(IReadingService.GetByReadingSheetId) => Task.FromResult(downloadReadings.Where(r => r.ReadingSheetId == (int)args[0]!).ToList()),
    nameof(IReadingService.GetPreviousForSheet) => Task.FromResult<Reading?>((long)args[0]! == 20
        ? new Reading { Id = 100, CurrentReading = 40, DateCreated = new DateTime(2026, 1, 1) } : null),
    _ => throw new NotSupportedException(method)
};
var zoneService = DispatchProxy.Create<IZoneBookService, SyntheticService>();
((SyntheticService)(object)zoneService).Call = (_, _) => Task.FromResult(new ZoneBook { Id = 1, Zone = 2, Book = 3 });
var customerService = DispatchProxy.Create<ICustomerService, SyntheticService>();
((SyntheticService)(object)customerService).Call = (_, _) => Task.FromResult<Customer?>(new Customer
    { Id = 30, Firstname = "Synthetic", Lastname = "Customer", Email = "private@example.invalid" });
foreach (var sheet in sheets) { sheet.BillingDate = new DateTime(2026, 2, 1); sheet.SeqFrom = 10; sheet.SeqTo = 10; }
builder.Services.AddSingleton(accountService);
builder.Services.AddSingleton(readingService);
builder.Services.AddSingleton(zoneService);
builder.Services.AddSingleton(customerService);
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
builder.Services.AddSingleton(users);
builder.Services.AddSingleton(readings);
builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.IncludeErrorDetails = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization(options => {
    options.AddPolicy(nameof(UserRole.MeterReader), policy => policy.RequireAuthenticatedUser().RequireRole(((int)UserRole.MeterReader).ToString()));
    options.AddPolicy("Collection", policy => policy.RequireAuthenticatedUser().RequireRole(((int)UserRole.Cashier).ToString(), ((int)UserRole.SuperAdmin).ToString()));
    options.AddPolicy("BillingOperations", policy => policy.RequireAuthenticatedUser().RequireRole(
        ((int)UserRole.Cashier).ToString(), ((int)UserRole.SuperAdmin).ToString(),
        ((int)UserRole.CustomerRepresentative).ToString(), ((int)UserRole.CustomerServiceOic).ToString()));
    options.AddPolicy(nameof(UserRole.SuperAdmin), policy => policy.RequireAuthenticatedUser().RequireRole(((int)UserRole.SuperAdmin).ToString()));
});
await using var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.StartAsync();
try
{
    using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    using var login = await client.PostAsJsonAsync("/api/auth/meter-reader/login", new { identifier = reader.Email, password });
    Check(login.StatusCode == HttpStatusCode.OK, "Existing Data login must succeed.");
    var session = await login.Content.ReadFromJsonAsync<JsonElement>();
    Check(session.GetProperty("userId").GetInt32() == 10 && session.GetProperty("role").GetInt32() == 4,
        "Login must preserve reader identity and role.");
    Check(session.GetProperty("name").GetString() == reader.Name && session.GetProperty("email").GetString() == reader.Email,
        "Login metadata changed.");
    Check(session.EnumerateObject().Count() == 6 && !session.TryGetProperty("password", out _), "Login contract changed or leaks secrets.");
    var token = session.GetProperty("token").GetString()!;
    var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
    Check(session.GetProperty("expiresAt").GetDateTime() == jwt.ValidTo
        && jwt.ValidTo > DateTime.UtcNow.AddHours(11) && jwt.ValidTo <= DateTime.UtcNow.AddHours(12),
        "Data login expiry must preserve twelve hours and match the signed token.");

    async Task Status(string? bearer, HttpStatusCode expected, string description, string suffix = "")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/reading-sheet/mobile/assigned" + suffix);
        if (bearer != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await client.SendAsync(request);
        Check(response.StatusCode == expected, description);
        if (expected == HttpStatusCode.Unauthorized)
            Check(response.Headers.WwwAuthenticate.Any(x => x.Scheme == "Bearer"), "401 must provide a bearer challenge.");
        if (expected == HttpStatusCode.OK)
        {
            var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
            Check(rows.EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).SequenceEqual(new long[] { 1, 2, 3 }),
                "Only this reader's non-deleted sheets may be returned, including completed sheets.");
            Check(rows[0].EnumerateObject().Count() == 10 && !rows[0].TryGetProperty("createdBy", out _)
                && !rows[0].TryGetProperty("assignedTo", out _), "Mobile DTO must not expose the persistence entity.");
        }
    }
    await Status(token, HttpStatusCode.OK, "Login-issued token rejected.");
    await Status(token, HttpStatusCode.OK, "Query-supplied user ID must not change authorization.", "?userId=11&assignedTo=11");
    Check(requestedReaders.SequenceEqual(new[] { 10, 10 }), "Service identity must come from validated claims.");
    await Status(null, HttpStatusCode.Unauthorized, "Missing token accepted.");
    await Status("invalid", HttpStatusCode.Unauthorized, "Invalid token accepted.");
    await Status(Token(expires: DateTime.UtcNow.AddMinutes(-1)), HttpStatusCode.Unauthorized, "Expired token accepted.");
    await Status(Token(signingKey: key + "different"), HttpStatusCode.Unauthorized, "Wrong signing key accepted.");
    await Status(Token(tokenIssuer: "different"), HttpStatusCode.Unauthorized, "Wrong issuer accepted.");
    await Status(Token(tokenAudience: "different"), HttpStatusCode.Unauthorized, "Wrong audience accepted.");
    var pieces = token.Split('.');
    pieces[1] = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("{\"sub\":\"11\",\"role\":\"4\"}"));
    await Status(string.Join('.', pieces), HttpStatusCode.Unauthorized, "Tampered identity accepted.");
    await Status(Token(role: UserRole.Cashier), HttpStatusCode.Forbidden, "Non-reader token accepted.");
    await Status(Token(identity: "0"), HttpStatusCode.Unauthorized, "Nonpositive identity accepted.");
    await Status(Token(identity: "invalid"), HttpStatusCode.Unauthorized, "Malformed identity accepted.");
    await Status(Token(identity: ""), HttpStatusCode.Unauthorized, "Missing identity accepted.");
    await Status(Token(identity: "11"), HttpStatusCode.Forbidden, "Missing reader accepted.");
    Check(requestedReaders.Count == 2, "Rejected requests must not query reading sheets.");

    reader.IsActive = false;
    await Status(token, HttpStatusCode.Forbidden, "Existing token retained disabled-reader access.");
    reader.IsActive = true;
    reader.Role = (int)UserRole.Cashier;
    await Status(token, HttpStatusCode.Forbidden, "Existing token retained changed-role access.");
    reader.Role = (int)UserRole.MeterReader;
    reader.IsVerified = false;
    await Status(token, HttpStatusCode.Forbidden, "Unverified reader accepted.");
    reader.IsVerified = true;
    foreach (var input in new[] { new { identifier = reader.Email, password = "wrong" }, new { identifier = "unknown@example.invalid", password } })
    {
        using var denied = await client.PostAsJsonAsync("/api/auth/meter-reader/login", input);
        Check(denied.StatusCode == HttpStatusCode.Unauthorized, "Legacy credential rejection changed.");
    }
    async Task<JsonElement> Download(int id, string? bearer, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/reading-sheet/mobile/{id}/download?userId=11");
        if (bearer != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await client.SendAsync(request);
        Check(response.StatusCode == expected, $"Download {id}: expected {expected}, received {response.StatusCode}.");
        return expected == HttpStatusCode.OK ? await response.Content.ReadFromJsonAsync<JsonElement>() : default;
    }
    var created = await Download(1, token, HttpStatusCode.OK);
    Check(created.EnumerateObject().Count() == 2 && created.GetProperty("readingSheet").EnumerateObject().Count() == 13,
        "Download envelope/sheet shape changed.");
    var accountRow = created.GetProperty("accounts")[0];
    Check(created.GetProperty("accounts").GetArrayLength() == 1 && accountRow.EnumerateObject().Count() == 19
        && accountRow.GetProperty("accountId").GetInt64() == 20
        && accountRow.GetProperty("previousReading").GetDecimal() == 40
        && accountRow.GetProperty("previousReadingDate").GetDateTime() == new DateTime(2026, 1, 1)
        && accountRow.GetProperty("meterNumber").GetString() == "M-20"
        && accountRow.GetProperty("name").GetString() == "Synthetic Customer"
        && accountRow.GetProperty("readingId").ValueKind == JsonValueKind.Null
        && !accountRow.TryGetProperty("email", out _), "Created roster/previous reading/DTO boundary failed.");
    downloadReadings.Add(new Reading { Id = 200, AccountId = 20, ReadingSheetId = 2,
        PreviousReading = 35, CurrentReading = 50, Status = ReadingStatus.InProgress });
    var inProgress = await Download(2, token, HttpStatusCode.OK);
    Check(inProgress.GetProperty("accounts")[0].GetProperty("previousReading").GetDecimal() == 35
        && inProgress.GetProperty("accounts")[0].GetProperty("currentReading").GetDecimal() == 50,
        "Saved reading baseline/value must be preserved.");
    downloadReadings.Add(new Reading { Id = 201, AccountId = 21, ReadingSheetId = 3,
        PreviousReading = 9, CurrentReading = 12, Status = ReadingStatus.Completed });
    var completed = await Download(3, token, HttpStatusCode.OK);
    Check(completed.GetProperty("accounts").EnumerateArray().Select(a => a.GetProperty("accountId").GetInt64())
        .SequenceEqual(new long[] { 20, 21 }),
        "Completed downloads must include the active roster as well as historical saved membership.");
    Check(completed.GetProperty("accounts")[0].GetProperty("inRoster").GetBoolean()
        && !completed.GetProperty("accounts")[1].GetProperty("inRoster").GetBoolean(),
        "Historical saved membership must not replace authoritative roster membership.");
    await Download(5, token, HttpStatusCode.Forbidden);
    await Download(4, token, HttpStatusCode.NotFound);
    await Download(999, token, HttpStatusCode.NotFound);
    await Download(1, null, HttpStatusCode.Unauthorized);
    await Download(1, "invalid", HttpStatusCode.Unauthorized);
    await Download(1, Token(expires: DateTime.UtcNow.AddMinutes(-1)), HttpStatusCode.Unauthorized);
    await Download(1, Token(role: UserRole.Cashier), HttpStatusCode.Forbidden);
    sheets[0].SeqFrom = 11; sheets[0].SeqTo = 11;
    var baseline = await Download(1, token, HttpStatusCode.OK);
    Check(baseline.GetProperty("accounts").GetArrayLength() == 1
        && baseline.GetProperty("accounts")[0].GetProperty("accountId").GetInt64() == 21
        && baseline.GetProperty("accounts")[0].GetProperty("readingId").ValueKind == JsonValueKind.Null
        && baseline.GetProperty("accounts")[0].GetProperty("previousReading").GetDecimal() == 9
        && baseline.GetProperty("accounts")[0].GetProperty("previousReadingDate").ValueKind == JsonValueKind.Null,
        "A newly active roster account with no reading history must be included with its installation baseline and no previous date.");
    async Task UploadStatus(string? bearer, int sheetId, long accountId, HttpStatusCode expected, string? current = "40")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reading-sheet/mobile/{sheetId}/readings?userId=11");
        if (bearer != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        request.Content = JsonContent.Create(new { accountId, currentReading = current, remarks = "", readingDate = "2026-01-31T00:00:00Z", readerId = 11 });
        using var response = await client.SendAsync(request);
        Check(response.StatusCode == expected, $"Upload status {response.StatusCode}, expected {expected}.");
        if (expected == HttpStatusCode.OK)
        {
            var receipt = await response.Content.ReadFromJsonAsync<JsonElement>();
            Check(receipt.GetProperty("readingId").GetInt64() == 300 && receipt.GetProperty("currentReading").GetString() == current
                && receipt.GetProperty("remarks").GetString() == "", "Upload confirmation changed or zero was lost.");
        }
    }
    await UploadStatus(token, 1, 21, HttpStatusCode.OK, "0");
    await UploadStatus(token, 1, 21, HttpStatusCode.OK, "0");
    await UploadStatus(null, 1, 21, HttpStatusCode.Unauthorized);
    await UploadStatus("invalid", 1, 21, HttpStatusCode.Unauthorized);
    await UploadStatus(Token(expires: DateTime.UtcNow.AddMinutes(-1)), 1, 21, HttpStatusCode.Unauthorized);
    await UploadStatus(Token(role: UserRole.Cashier), 1, 21, HttpStatusCode.Forbidden);
    await UploadStatus(token, 5, 21, HttpStatusCode.Forbidden);
    await UploadStatus(token, 4, 21, HttpStatusCode.NotFound);
    await UploadStatus(token, 1, 20, HttpStatusCode.BadRequest);
    await UploadStatus(token, 1, 21, HttpStatusCode.BadRequest, null);
    await UploadStatus(token, 1, 21, HttpStatusCode.BadRequest, "not-a-number");
    Console.WriteLine("PASS: Phase 4B HTTP bearer/role/claims, assignment, deletion, roster, decimal/zero validation and confirmation contract.");
    using (var removedCompletion = new HttpRequestMessage(HttpMethod.Post, "/api/reading-sheet/mobile/1/complete")) {
        removedCompletion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(removedCompletion);
        Check(response.StatusCode == HttpStatusCode.NotFound, "Mobile must not expose a final completion endpoint.");
    }
    async Task<JsonElement> Soa(int sheetId, int accountId, string? bearer, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/reading-sheet/mobile/{sheetId}/accounts/{accountId}/soa?userId=11");
        if (bearer != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await client.SendAsync(request);
        Check(response.StatusCode == expected, $"SOA {sheetId}/{accountId}: expected {expected}, received {response.StatusCode}.");
        return expected is HttpStatusCode.OK or HttpStatusCode.Conflict
            ? await response.Content.ReadFromJsonAsync<JsonElement>() : default;
    }
    sheets[1].SeqFrom = 1; sheets[1].SeqTo = 20;
    var unavailable = await Soa(2, 20, token, HttpStatusCode.Conflict);
    Check(unavailable.GetProperty("code").GetString() == "not_posted", "Unposted SOA must be explicitly unavailable.");
    downloadReadings.Single(r => r.Id == 200).Status = ReadingStatus.Completed;
    downloadReadings.Single(r => r.Id == 200).IsCompleted = true;
    soaBillings.Add(new Billing { Id = 400, ReadingId = 200, AccountId = 20, BillingReferenceId = "SYN-SOA",
        TotalBillAmount = 123.45m, RemainingAmount = 120.25m, OtherCharges = 4m, BillingAdjustment = -2m,
        PaymentStatus = (int)PaymentStatus.Unpaid, BillingPeriod = new DateTime(2026, 2, 1) });
    var official = await Soa(2, 20, token, HttpStatusCode.OK);
    Check(official.GetProperty("totalBillAmount").GetDecimal() == 123.45m
        && official.GetProperty("remainingAmount").GetDecimal() == 120.25m
        && official.GetProperty("billingAdjustment").GetDecimal() == -2m
        && official.GetProperty("otherCharges").GetDecimal() == 4m
        && official.GetProperty("paymentStatus").GetInt32() == (int)PaymentStatus.Unpaid
        && official.GetProperty("name").GetString() == "Synthetic Customer"
        && official.GetProperty("usage").GetDecimal() == 15m
        && official.EnumerateObject().Count() == 41
        && official.GetProperty("penaltyAfterDueAmount").ValueKind == JsonValueKind.Null
        && official.GetProperty("currentPenaltyAmount").ValueKind == JsonValueKind.Null
        && !official.TryGetProperty("email", out _) && !official.TryGetProperty("createdBy", out _), "SOA contract/amounts/privacy failed.");
    Check((await Soa(2, 20, token, HttpStatusCode.OK)).ToString() == official.ToString(), "Repeated SOA GET changed data.");
    soaBillings[0].PaymentStatus = (int)PaymentStatus.Paid;
    Check((await Soa(2, 20, token, HttpStatusCode.OK)).GetProperty("paymentStatus").GetInt32() == (int)PaymentStatus.Paid,
        "SOA did not preserve paid status.");
    downloadAccounts[1].Sequence = 21;
    await Soa(2, 21, token, HttpStatusCode.NotFound);
    await Soa(2, 999, token, HttpStatusCode.NotFound);
    await Soa(5, 20, token, HttpStatusCode.Forbidden);
    await Soa(4, 20, token, HttpStatusCode.NotFound);
    await Soa(999, 20, token, HttpStatusCode.NotFound);
    await Soa(2, 20, null, HttpStatusCode.Unauthorized);
    await Soa(2, 20, "invalid", HttpStatusCode.Unauthorized);
    await Soa(2, 20, Token(expires: DateTime.UtcNow.AddMinutes(-1)), HttpStatusCode.Unauthorized);
    await Soa(2, 20, Token(role: UserRole.Cashier), HttpStatusCode.Forbidden);
    Check(sheets[1].Status == ReadingStatus.InProgress && sheets[1].DateUpload == null && soaBillings.Count == 1,
        "SOA changed posting/sheet state.");
    Console.WriteLine("PASS: Phase 6A SOA bearer/role/claims, assignment, deletion, sequence scope, unposted result, persisted amounts, paid/unpaid and DTO privacy.");
    soaBillings[0].ServiceInvoiceNumber = "TEST-000001";
    foreach (var role in Enum.GetValues<UserRole>())
    {
        using var lookup = new HttpRequestMessage(HttpMethod.Get, "/api/collection/service-invoice/TEST-000001");
        lookup.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role: role));
        using var response = await client.SendAsync(lookup);
        Check(response.StatusCode == (role is UserRole.Cashier or UserRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden),
            $"Collection invoice authorization failed for {role}.");
        using var update = new HttpRequestMessage(HttpMethod.Post, "/api/BirInvoiceConfig/active?userId=11");
        update.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(role: role));
        update.Content = JsonContent.Create(new TMCWD.Model.Billing.BirInvoiceConfigDto(0, "SYN-PTU", new DateOnly(2026, 1, 1), 1, 999, null, null, "PREFIX-", 3));
        using var saved = await client.SendAsync(update);
        Check(saved.StatusCode == (role == UserRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden),
            $"BIR config modification authorization failed for {role}.");
    }
    Check(configWrites == 1, "An unauthorized role modified BIR config.");
    using (var missingAuth = await client.GetAsync("/api/collection/service-invoice/TEST-000001"))
        Check(missingAuth.StatusCode == HttpStatusCode.Unauthorized, "Collection lookup accepted missing authentication.");
    Console.WriteLine("PASS: Phase 6D collection allows only Cashier/SuperAdmin; BIR modification allows only SuperAdmin; reader/other roles denied and claims identity preserved.");
    foreach (var role in Enum.GetValues<UserRole>()) {
        using var payRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Payment/ProcessCollection/999") {
            Content = JsonContent.Create(new TMCWD.Model.Billing.Requests.CollectionPaymentRequest {
                InvoiceNumber = "SYNTHETIC-PAYMENT", Method = PaymentMethod.Cash, AmountTendered = 190m,
                Bills = new() { new() { BillingReferenceId = "SYNTHETIC", PaidAmount = 190m } } }) };
        payRequest.Headers.Authorization = new("Bearer", Token(role: role));
        using var response = await client.SendAsync(payRequest);
        Check(response.StatusCode == (role is UserRole.Cashier or UserRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden),
            $"Payment API authorization failed for {role}.");
        using var summaryRequest = new HttpRequestMessage(HttpMethod.Get, "/api/Billing/Collection/SYNTHETIC");
        summaryRequest.Headers.Authorization = new("Bearer", Token(role: role));
        using var summaryResponse = await client.SendAsync(summaryRequest);
        Check(summaryResponse.StatusCode == (role is UserRole.Cashier or UserRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden),
            $"Collection summary authorization failed for {role}.");
        using var penaltyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/Penalty/PostCharges/999") {
            Content = JsonContent.Create(new TMCWD.Model.Billing.PostPenaltyChargesRequest { BillingReferenceIds = new() { "SYNTHETIC" } }) };
        penaltyRequest.Headers.Authorization = new("Bearer", Token(role: role));
        using var penaltyResponse = await client.SendAsync(penaltyRequest);
        Check(penaltyResponse.StatusCode == (role is UserRole.Cashier or UserRole.SuperAdmin or UserRole.CustomerRepresentative or UserRole.CustomerServiceOic
            ? HttpStatusCode.OK : HttpStatusCode.Forbidden), $"Penalty posting authorization failed for {role}.");
    }
    Check(cashierWrites.Count == 6 && cashierWrites.All(id => id == 10), "Financial write identity trusted a route user ID.");
    using (var unauthenticatedPay = await client.PostAsJsonAsync("/api/Payment/ProcessCollection/999", new { }))
        Check(unauthenticatedPay.StatusCode == HttpStatusCode.Unauthorized, "Payment API accepted missing authentication.");
    Console.WriteLine("PASS: Collection/payment summaries and penalty posting enforce staff roles and derive write identity from claims.");
    downloadAccounts.Clear(); downloadReadings.Clear();
    Check((await Download(1, token, HttpStatusCode.OK)).GetProperty("accounts").GetArrayLength() == 0,
        "Empty download must succeed with an empty account list.");
    Console.WriteLine("PASS: Phase 3B download authorization, DTO, created/in-progress/completed membership, saved values, baseline and empty accounts.");
    sheets.Clear();
    using var emptyRequest = new HttpRequestMessage(HttpMethod.Get, "/api/reading-sheet/mobile/assigned");
    emptyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    using var empty = await client.SendAsync(emptyRequest);
    Check(empty.StatusCode == HttpStatusCode.OK && (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength() == 0,
        "No assigned sheets must return 200 with an empty array.");
    Console.WriteLine("PASS: Data login contract/legacy password, issued JWT, 401/403, signature/issuer/audience/expiry, claim identity, reader/deleted filtering, DTO, current reader access, and empty list.");
}
finally { await app.StopAsync(); }

string Token(UserRole role = UserRole.MeterReader, string identity = "10", DateTime? expires = null,
    string? signingKey = null, string tokenIssuer = issuer, string tokenAudience = audience)
{
    var claims = new List<Claim> { new(ClaimTypes.Role, ((int)role).ToString()) };
    if (identity.Length > 0) claims.Add(new(ClaimTypes.NameIdentifier, identity));
    return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(tokenIssuer, tokenAudience, claims,
        notBefore: DateTime.UtcNow.AddHours(-1), expires: expires ?? DateTime.UtcNow.AddHours(1),
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? key)), SecurityAlgorithms.HmacSha256)));
}
static void Check(bool result, string description)
{
    if (!result) throw new Exception(description);
}
public class SyntheticService : DispatchProxy
{
    public Func<string, object?[], object?> Call = (_, _) => throw new NotSupportedException();
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!.Name, args ?? []);
}
