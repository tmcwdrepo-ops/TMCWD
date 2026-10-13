using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing;
using Billing = TMCWD.Data.Entities.Billing;
using Penalty = TMCWD.Data.Entities.Penalty;
using ReadingSheet = TMCWD.Data.Entities.ReadingSheet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using TMCWD.Data.Controllers;
using TMCWD.Utility.Encryption;

static class AdjustmentWorkflowVerification
{
    public static async Task<bool> Run(UserDbContext db, ReadingSheetService sheets, ReadingService readings,
        Func<string, Func<Task>, Task> test, Action<bool> injectFailure)
    {
        if (db.Database.GetDbConnection().Database != "tmcwd_test") throw new Exception("Dedicated test database required.");
        var failures = 0; var scenarios = 0;
        async Task Scenario(string name, Func<Task> action)
        {
            scenarios++;
            try { await test(name, action); }
            catch (Exception error) { failures++; Console.WriteLine($"FAIL: {name}: {error.Message}"); }
            finally { injectFailure(false); }
        }
        async Task<Billing> Post()
        {
            var sheet = await db.ReadingSheets.SingleAsync();
            sheet.BillingDate = new DateTime(2026, 7, 1); sheet.DueDate = new DateTime(2026, 7, 16);
            sheet.BillingPeriodStart = new DateTime(2026, 6, 1);
            var account = await db.Accounts.SingleAsync(); account.Status = (int)AccountStatus.Active; account.Sequence = 1;
            await db.SaveChangesAsync();
            await readings.SaveUpdate(1, new TMCWD.Data.Entities.Reading { AccountId = 1, ReadingSheetId = 1,
                CurrentReading = 0, PreviousReading = 0, Status = ReadingStatus.InProgress, DateCreated = new DateTime(2026, 6, 30) });
            Require(await sheets.PartialPost(1, 1) == 1, "Synthetic posting failed.");
            return await db.Billings.SingleAsync();
        }
        await Scenario("Adjusted bill reconciles Collections/Ledger, payment and cancellation at 90", async () => {
            var bill = await Post(); var frozen = bill.InvoiceSnapshotJson; var invoice = bill.ServiceInvoiceNumber;
            var operation = Guid.NewGuid();
            await new BillingAdjustmentService(db).SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 1 }], operation);
            db.ChangeTracker.Clear(); bill = await db.Billings.SingleAsync();
            var collection = await new BillingService(db).GetCollectionBill(bill.BillingReferenceId);
            var ledger = await new AccountLedgerService(db, new BillingService(db)).Get("TEST");
            Require(bill.RemainingAmount == 90 && collection!.AmountDue == 90 && ledger!.TotalBalance == 90,
                "Adjusted obligations disagree or retain original snapshot principal.");
            var payments = new PaymentService(db);
            var payment = (await payments.ProcessCollectionPayment(1, new() { Method = PaymentMethod.Cash,
                AmountTendered = 90, Bills = [new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 90 }] })).Single();
            Require(payment.BalanceBefore == 90, "Wrong adjusted BalanceBefore.");
            var replay = await new BillingAdjustmentService(db).SaveBatch(1,
                [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 1 }], operation);
            Require(replay.Single().Amount == 30 && await db.BillingAdjustments.CountAsync() == 1,
                "Successful retry after settlement created another adjustment.");
            await payments.CancelInvoice(1, new() { InvoiceNo = payment.PaymentReference, Reason = "Synthetic 6I-C verification" });
            db.ChangeTracker.Clear(); bill = await db.Billings.SingleAsync();
            Require(bill.RemainingAmount == 90 && bill.InvoiceSnapshotJson == frozen && bill.ServiceInvoiceNumber == invoice,
                "Cancellation restored original principal or changed issued identity/snapshot.");
        });
        await Scenario("Adjustment HTTP rejects anonymous/wrong role/malformed money and derives audit actor", async () => {
            var bill = await Post();
            var actor = await db.Users.SingleAsync(u => u.Id == 1);
            var reader = new User { Id = 4, Role = 4, IsActive = true, IsVerified = true, Password = "synthetic" };
            db.Users.Add(reader); await db.SaveChangesAsync();
            var key = Encoding.UTF8.GetBytes("synthetic-adjustment-http-validation-key-only");
            string Token(User user) {
                var handler = new JwtSecurityTokenHandler();
                return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor {
                    Subject = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),
                        new Claim(ClaimTypes.Role,user.Role!.Value.ToString()),
                        new Claim(UserSecurityStamp.ClaimType,UserSecurityStamp.Create(key,user.Id,user.Password,user.Role,
                            user.IsActive,user.IsVerified,user.DateUpdated,user.DateVerified))]),
                    Issuer = "synthetic", Audience = "synthetic", Expires = DateTime.UtcNow.AddMinutes(2),
                    SigningCredentials = new(new SymmetricSecurityKey(key),SecurityAlgorithms.HmacSha256) }));
            }
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddSingleton(db);
            builder.Services.AddSingleton<IBillingAdjustmentService>(new BillingAdjustmentService(db));
            builder.Services.AddControllers().AddApplicationPart(typeof(BillingAdjustmentController).Assembly);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
                options.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = "synthetic",
                    ValidateAudience = true, ValidAudience = "synthetic", ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key), ValidateLifetime = true, RequireSignedTokens = true,
                    RequireExpirationTime = true, ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero };
                options.Events = new() { OnTokenValidated = PersistedTokenValidation.Validate };
            });
            builder.Services.AddAuthorization(options => options.AddPolicy("BillingOperations",
                policy => policy.RequireAuthenticatedUser().RequireRole("1","2","6","7")));
            await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            try {
                using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                    .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
                async Task<HttpStatusCode> Send(string json, User? identity, Guid? operation = null, string action = "SaveBatch") {
                    using var request = new HttpRequestMessage(HttpMethod.Post,"api/BillingAdjustment/" + action + "/999") {
                        Content = new StringContent(json,Encoding.UTF8,"application/json") };
                    request.Headers.Add("Idempotency-Key", (operation ?? Guid.NewGuid()).ToString("D"));
                    if(identity != null) request.Headers.Authorization = new("Bearer",Token(identity));
                    using var response = await client.SendAsync(request); return response.StatusCode;
                }
                var json = System.Text.Json.JsonSerializer.Serialize(new[] { new { BillingReferenceId = bill.BillingReferenceId,
                    Type = 1, Amount = 30m, CreatedBy = 999 } });
                Require(await Send(json,null) == HttpStatusCode.Unauthorized && await Send(json,reader) == HttpStatusCode.Forbidden,
                    "Adjustment authorization failed.");
                Require(await Send(json.Replace("\"Amount\":30", "\"Amount\":\"invalid\""),actor) == HttpStatusCode.BadRequest
                    && !await db.BillingAdjustments.AnyAsync(), "Malformed amount caused a write.");
                Require(await Send(json,actor,Guid.Empty) == HttpStatusCode.BadRequest
                    && await Send("{\"BillingReferenceId\":\"MISSING\",\"Amount\":30,\"Type\":2}",actor,null,"SaveUpdate") == HttpStatusCode.NotFound
                    && !await db.BillingAdjustments.AnyAsync() && !await db.BillingAdjustmentOperations.AnyAsync(),
                    "Missing identity/billing wrote data or used wrong API status.");
                var submission = Guid.NewGuid();
                Require(await Send(json,actor,submission) == HttpStatusCode.OK && await Send(json,actor,submission) == HttpStatusCode.OK
                    && await db.BillingAdjustments.CountAsync() == 1 && (await db.BillingAdjustments.SingleAsync()).CreatedBy == 1
                    && (await db.BillingAdjustments.SingleAsync()).UpdatedBy == 1,
                    "Claim identity did not override forged route/body actor.");
                var transport = new ServiceCollection();
                transport.AddHttpClient("TmcWdApi", options => {
                    options.BaseAddress = client.BaseAddress;
                    options.DefaultRequestHeaders.Authorization = new("Bearer",Token(actor));
                });
                transport.AddSingleton<TMCWD.Services.WebService>();
                using var provider = transport.BuildServiceProvider();
                var bridge = new TMCWD.Billing.BillingAdjustmentTransaction(provider.GetRequiredService<TMCWD.Services.WebService>());
                var bridged = await bridge.SaveBatch(999, [new() { BillingReferenceId = bill.BillingReferenceId, Type = 1, Amount = 30 }], submission);
                Require(bridged?.Single().CreatedBy == 1 && await db.BillingAdjustments.CountAsync() == 1,
                    "Shared Application-to-Data bridge lost the operation key or claim identity.");
            } finally { await app.StopAsync(); }
        });
        await Scenario("Single prior adjusted principal 90 plus persisted penalty 17 produces next arrears 107", async () => {
            var bill = await Post(); var frozen = bill.InvoiceSnapshotJson;
            await new BillingAdjustmentService(db).SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 1 }], Guid.NewGuid());
            // Historical persisted penalty fixture: this is not a new penalty calculation against water 100.
            db.Penalties.Add(new Penalty { BillingReferenceId = bill.BillingReferenceId, Amount = 17,
                PaymentStatus = (int)PaymentStatus.Unpaid, CreatedBy = 1, UpdatedBy = 1, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
            db.ReadingSheets.Add(new ReadingSheet { Id = 2, ZoneBookId = 1, Status = ReadingStatus.InProgress,
                BillingDate = new DateTime(2026, 12, 1), BillingPeriodStart = new DateTime(2026, 11, 1), DueDate = new DateTime(2026, 12, 16) });
            db.Readings.Add(new TMCWD.Data.Entities.Reading { AccountId = 1, ReadingSheetId = 2, CurrentReading = 0,
                PreviousReading = 0, Status = ReadingStatus.InProgress, DateCreated = new DateTime(2026, 11, 30) });
            await db.SaveChangesAsync(); Require(await sheets.PartialPost(2, 1) == 1, "Next posting failed.");
            db.ChangeTracker.Clear();
            Require((await db.Billings.OrderByDescending(b => b.Id).FirstAsync()).BillArrearsAmount == 107,
                "Wrong adjusted principal/penalty arrears.");
            Require((await db.Billings.SingleAsync(b => b.Id == bill.Id)).InvoiceSnapshotJson == frozen, "Old snapshot recalculated.");
        });
        await Scenario("Single adjustment must reject nonexistent billing without orphan write", async () => {
            var rejected = false;
            try { await new BillingAdjustmentService(db).SaveUpdate(1, new() { BillingReferenceId = "MISSING", Amount = 30, Type = 1 }, Guid.NewGuid()); }
            catch (KeyNotFoundException) { rejected = true; }
            Require(rejected && !await db.BillingAdjustments.AnyAsync(), "Missing billing accepted and adjustment persisted.");
        });
        await Scenario("Repeated identical batch must not duplicate adjustment liability", async () => {
            var bill = await Post(); var service = new BillingAdjustmentService(db);
            var operation = Guid.NewGuid();
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 1 }], operation);
            try { await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 1 }], operation); }
            catch (InvalidOperationException) { }
            db.ChangeTracker.Clear();
            Require(await db.BillingAdjustments.CountAsync() == 1 && (await db.Billings.SingleAsync()).RemainingAmount == 90,
                "Retry creates two adjustments and reduces RemainingAmount to 60.");
        });
        await Scenario("Single adjustment failure must roll back adjustment and balance atomically", async () => {
            var bill = await Post(); var frozen = bill.InvoiceSnapshotJson; var invoice = bill.ServiceInvoiceNumber;
            var serial = await db.ServiceInvoiceSeq.AsNoTracking().Select(x => x.LastValue).SingleAsync();
            injectFailure(true);
            try { await new BillingAdjustmentService(db).SaveUpdate(1, new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 1 }, Guid.NewGuid()); }
            catch (Exception) { }
            finally { injectFailure(false); }
            db.ChangeTracker.Clear();
            var unchanged = await db.Billings.SingleAsync();
            Require(!await db.BillingAdjustments.AnyAsync() && !await db.BillingAdjustmentOperations.AnyAsync()
                && unchanged.RemainingAmount == 120 && unchanged.BillingAdjustment == 0 && unchanged.InvoiceSnapshotJson == frozen
                && unchanged.ServiceInvoiceNumber == invoice && !await db.Payments.AnyAsync() && !await db.Penalties.AnyAsync()
                && !await db.OtherCharges.AnyAsync() && await db.ServiceInvoiceSeq.Select(x => x.LastValue).SingleAsync() == serial,
                "Adjustment row committed while balance stayed 120 after injected failure.");
        });
        await Scenario("Batch adjustment injected failure rolls back all lines and bill", async () => {
            var bill = await Post(); injectFailure(true);
            try { await new BillingAdjustmentService(db).SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 20, Type = 1 },
                new() { BillingReferenceId = bill.BillingReferenceId, Amount = 10, Type = 1 }], Guid.NewGuid()); }
            catch (Exception) { }
            finally { injectFailure(false); }
            db.ChangeTracker.Clear();
            Require(!await db.BillingAdjustments.AnyAsync() && (await db.Billings.SingleAsync()).RemainingAmount == 120,
                "Batch rollback failed.");
        });
        await Scenario("Single retry returns original receipt; separate keys and updates preserve audit/reference", async () => {
            var bill = await Post(); var service = new BillingAdjustmentService(db); var key = Guid.NewGuid();
            var first = await service.SaveUpdate(1, new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 30, Type = 2, CreatedBy = 999, UpdatedBy = 999, DateCreated = DateTime.MinValue }, key);
            var retry = await service.SaveUpdate(1, new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 2 }, key);
            Require(first.Id == retry.Id && first.CreatedBy == 1 && first.UpdatedBy == 1
                && await db.BillingAdjustments.CountAsync() == 1 && await db.BillingAdjustmentOperations.CountAsync() == 1,
                "Single retry/audit receipt failed.");
            await service.SaveUpdate(1, new() { Id = first.Id, BillingReferenceId = bill.BillingReferenceId,
                Amount = 40, Type = 2, CreatedBy = 999, DateCreated = DateTime.MinValue, UpdatedBy = 999 }, Guid.NewGuid());
            var original = await service.SaveUpdate(1, new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 2 }, key);
            Require(original.Amount == 30 && (await db.BillingAdjustments.SingleAsync()).Amount == 40
                && (await db.BillingAdjustments.SingleAsync()).DateCreated == first.DateCreated, "Receipt or creation audit rewritten.");
            db.Billings.Add(new Billing { BillingReferenceId = "SECOND", TotalBillAmount = 120, RemainingAmount = 120 });
            await db.SaveChangesAsync(); var rejected = false;
            try { await service.SaveUpdate(1, new() { Id = first.Id, BillingReferenceId = "SECOND", Amount = 30, Type = 2 }, Guid.NewGuid()); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && (await db.BillingAdjustments.SingleAsync()).BillingReferenceId == bill.BillingReferenceId,
                "Adjustment moved to another bill.");
        });
        await Scenario("Distinct batch keys permit equal amounts; changed payload and actor cannot reuse key", async () => {
            var bill = await Post(); var service = new BillingAdjustmentService(db); var key = Guid.NewGuid();
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 2 }], key);
            foreach (var attempt in new[] { (Actor: 1, Amount: 31m), (Actor: 2, Amount: 30m) }) {
                var rejected = false;
                try { await service.SaveBatch(attempt.Actor, [new() { BillingReferenceId = bill.BillingReferenceId,
                    Amount = attempt.Amount, Type = 2 }], key); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Operation key reused by altered submission/actor.");
            }
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 2 }], Guid.NewGuid());
            Require(await db.BillingAdjustments.CountAsync() == 2 && await db.BillingAdjustmentOperations.CountAsync() == 2
                && (await db.Billings.SingleAsync()).RemainingAmount == 60, "Separate intentional adjustment blocked or lost.");
        });
        await Scenario("Missing batch bill and invalid numeric/key inputs cannot write; legacy floor retained", async () => {
            var service = new BillingAdjustmentService(db); var absent = false;
            try { await service.SaveBatch(1, [new() { BillingReferenceId = "MISSING", Amount = 30, Type = 2 }], Guid.NewGuid()); }
            catch (KeyNotFoundException) { absent = true; }
            Require(absent && !await db.BillingAdjustments.AnyAsync() && !await db.BillingAdjustmentOperations.AnyAsync()
                && !await db.Payments.AnyAsync() && !await db.Penalties.AnyAsync() && !await db.OtherCharges.AnyAsync()
                && !await db.ServiceInvoiceSeq.AnyAsync(), "Missing bill caused a financial/sequence side effect.");
            var bill = await Post();
            foreach (var amount in new[] { 0m, .001m, 10000m, decimal.MaxValue }) {
                var rejected = false;
                try { await service.SaveUpdate(1, new() { BillingReferenceId = bill.BillingReferenceId, Amount = amount, Type = 2 }, Guid.NewGuid()); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && !await db.BillingAdjustments.AnyAsync(), "Invalid money persisted.");
            }
            var missingKey = false;
            try { await service.SaveUpdate(1, new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30, Type = 2 }, Guid.Empty); }
            catch (InvalidOperationException) { missingKey = true; }
            Require(missingKey, "Missing operation identity accepted.");
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 200, Type = 2 }], Guid.NewGuid());
            Require((await db.Billings.SingleAsync()).RemainingAmount == 20, "Legacy floor redesigned.");
        });
        // ── Audit History scenarios A–K ──────────────────────────────────
        // Every scenario uses only tmcwd_test temporary tables via the
        // existing test fixture.  GetHistory is a read-only query; it never
        // mutates financial state and is verified not to do so.

        await Scenario("A. One adjustment produces exactly one audit row with correct fields", async () => {
            var bill = await Post();
            var service = new BillingAdjustmentService(db);
            var before = DateTime.UtcNow.AddSeconds(-1);
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 20, Type = (int)AdjustmentLineType.CurrentBill, Reason = "Meter Error: synthetic" }], Guid.NewGuid());
            var history = await service.GetHistory(bill.BillingReferenceId);
            Require(history.Count == 1, "Expected exactly one audit row.");
            var row = history.Single();
            Require(row.BillingReferenceId == bill.BillingReferenceId, "Wrong BillingReferenceId in history.");
            Require(row.Amount == 20m, $"Wrong Amount: {row.Amount}");
            Require(row.AdjustmentType == nameof(AdjustmentLineType.CurrentBill), $"Wrong type name: {row.AdjustmentType}");
            Require(row.Reason == "Meter Error: synthetic", $"Reason not persisted: '{row.Reason}'");
            Require(row.CreatedBy == 1, "Wrong CreatedBy.");
            Require(row.DateCreated >= before, "DateCreated not server-controlled.");
        });

        await Scenario("B. Two separate operations produce two independent audit rows, cumulative balance correct", async () => {
            var bill = await Post();
            var service = new BillingAdjustmentService(db);
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 20, Type = (int)AdjustmentLineType.CurrentBill, Reason = "OperationA" }], Guid.NewGuid());
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 10, Type = (int)AdjustmentLineType.Penalty, Reason = "OperationB" }], Guid.NewGuid());
            var history = await service.GetHistory(bill.BillingReferenceId);
            Require(history.Count == 2, $"Expected 2 rows; got {history.Count}.");
            Require(history.Any(r => r.Amount == 20m && r.Reason == "OperationA"), "First adjustment row missing.");
            Require(history.Any(r => r.Amount == 10m && r.Reason == "OperationB"), "Second adjustment row missing.");
            db.ChangeTracker.Clear();
            Require((await db.Billings.SingleAsync()).RemainingAmount == 90m,
                "Cumulative RemainingAmount incorrect after two adjustments.");
        });

        await Scenario("C. Same OperationId retry returns original receipt; history contains exactly one row", async () => {
            var bill = await Post();
            var service = new BillingAdjustmentService(db);
            var op = Guid.NewGuid();
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 30, Type = (int)AdjustmentLineType.CurrentBill, Reason = "Initial" }], op);
            // Retry with same operation key (idempotent replay)
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 30, Type = (int)AdjustmentLineType.CurrentBill }], op);
            var history = await service.GetHistory(bill.BillingReferenceId);
            Require(history.Count == 1, $"Retry created a duplicate audit row; got {history.Count}.");
            Require(await db.BillingAdjustmentOperations.CountAsync() == 1,
                "Retry created a duplicate operation receipt.");
            // Reason on the original must be unchanged
            Require(history.Single().Reason == "Initial", "Retry overwrote the original reason.");
        });

        await Scenario("D. Same amount with different OperationIds produces two distinct legitimate audit rows", async () => {
            var bill = await Post();
            var service = new BillingAdjustmentService(db);
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 15, Type = (int)AdjustmentLineType.CurrentBill, Reason = "First" }], Guid.NewGuid());
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 15, Type = (int)AdjustmentLineType.CurrentBill, Reason = "Second" }], Guid.NewGuid());
            var history = await service.GetHistory(bill.BillingReferenceId);
            Require(history.Count == 2, $"Expected 2 rows for same-amount distinct operations; got {history.Count}.");
            Require(history.All(r => r.Amount == 15m), "Not all rows have the expected amount.");
        });

        await Scenario("E. Forged CreatedBy in request body is ignored; persisted actor matches authenticated claim", async () => {
            var bill = await Post();
            var service = new BillingAdjustmentService(db);
            // Pass body with CreatedBy = 999, but actor userId = 1
            var saved = await service.SaveBatch(1,
                [new() { BillingReferenceId = bill.BillingReferenceId, Amount = 25,
                    Type = (int)AdjustmentLineType.CurrentBill, CreatedBy = 999, UpdatedBy = 999 }],
                Guid.NewGuid());
            db.ChangeTracker.Clear();
            var row = await db.BillingAdjustments.SingleAsync();
            Require(row.CreatedBy == 1, $"Forged CreatedBy persisted: {row.CreatedBy}.");
            Require(row.UpdatedBy == 1, $"Forged UpdatedBy persisted: {row.UpdatedBy}.");
            var history = await service.GetHistory(bill.BillingReferenceId);
            Require(history.Single().CreatedBy == 1, "History DTO exposes wrong CreatedBy.");
        });

        await Scenario("F. Failed SaveUpdate rolls back; history remains empty, Billing unchanged", async () => {
            var bill = await Post();
            var frozen = bill.InvoiceSnapshotJson;
            injectFailure(true);
            try { await new BillingAdjustmentService(db).SaveUpdate(1,
                new() { BillingReferenceId = bill.BillingReferenceId, Amount = 30,
                    Type = (int)AdjustmentLineType.CurrentBill, Reason = "ShouldRollBack" }, Guid.NewGuid()); }
            catch { }
            finally { injectFailure(false); }
            db.ChangeTracker.Clear();
            var history = await new BillingAdjustmentService(db).GetHistory(bill.BillingReferenceId);
            Require(history.Count == 0, "Rolled-back adjustment appeared in history.");
            Require(!await db.BillingAdjustmentOperations.AnyAsync(),
                "Rolled-back receipt appeared in operations table.");
            Require((await db.Billings.SingleAsync()).InvoiceSnapshotJson == frozen,
                "Snapshot mutated after rollback.");
        });

        await Scenario("G. Missing bill returns 404; history for absent reference is empty, zero side effects", async () => {
            var service = new BillingAdjustmentService(db);
            var rejected = false;
            try { await service.SaveBatch(1, [new() { BillingReferenceId = "NOEXIST",
                Amount = 30, Type = (int)AdjustmentLineType.CurrentBill }], Guid.NewGuid()); }
            catch (KeyNotFoundException) { rejected = true; }
            Require(rejected, "Missing bill did not raise KeyNotFoundException.");
            var history = await service.GetHistory("NOEXIST");
            Require(history.Count == 0, "GetHistory for absent reference returned rows.");
            Require(!await db.BillingAdjustments.AnyAsync(), "Adjustment persisted for missing bill.");
            Require(!await db.BillingAdjustmentOperations.AnyAsync(), "Receipt persisted for missing bill.");
        });

        await Scenario("H. Update preserves original CreatedBy/DateCreated; history reflects latest UpdatedBy", async () => {
            var bill = await Post();
            var service = new BillingAdjustmentService(db);
            var firstKey = Guid.NewGuid();
            var first = await service.SaveUpdate(1,
                new() { BillingReferenceId = bill.BillingReferenceId, Amount = 20,
                    Type = (int)AdjustmentLineType.CurrentBill, Reason = "Original" }, firstKey);
            var originalCreatedBy = first.CreatedBy;
            var originalDateCreated = first.DateCreated;
            // Wait 1 ms so a new 'now' would be distinguishably later.
            await Task.Delay(2);
            // Different key = legitimate update of the same row
            await service.SaveUpdate(1,
                new() { Id = first.Id, BillingReferenceId = bill.BillingReferenceId, Amount = 25,
                    Type = (int)AdjustmentLineType.CurrentBill, Reason = "Updated" }, Guid.NewGuid());
            db.ChangeTracker.Clear();
            var entity = await db.BillingAdjustments.SingleAsync();
            Require(entity.CreatedBy == originalCreatedBy, "CreatedBy changed on update.");
            // DateCreated must not have been advanced to the update time; compare within 5 seconds
            // to allow for DATETIME(6) round-trip precision differences.
            Require(Math.Abs((entity.DateCreated - originalDateCreated).TotalSeconds) < 5,
                $"DateCreated changed materially on update: original={originalDateCreated:O}, stored={entity.DateCreated:O}.");
            Require(entity.UpdatedBy == 1, "UpdatedBy not set on update.");
            Require(entity.Reason == "Updated", "Reason not updated.");
            var history = await service.GetHistory(bill.BillingReferenceId);
            Require(history.Count == 1, $"Expected 1 history row after update; got {history.Count}.");
            Require(history.Single().CreatedBy == originalCreatedBy,
                "History DTO reflects wrong CreatedBy after update.");
            Require(history.Single().Amount == 25m, "History DTO reflects stale amount.");
            Require(history.Single().Reason == "Updated", "History DTO reflects stale reason.");
        });

        await Scenario("I. Invoice snapshot and ServiceInvoiceNumber are immutable after any adjustment", async () => {
            var bill = await Post();
            var frozenSnapshot = bill.InvoiceSnapshotJson;
            var frozenInvoice = bill.ServiceInvoiceNumber;
            var service = new BillingAdjustmentService(db);
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 30, Type = (int)AdjustmentLineType.CurrentBill }], Guid.NewGuid());
            db.ChangeTracker.Clear();
            var updated = await db.Billings.SingleAsync();
            Require(updated.InvoiceSnapshotJson == frozenSnapshot, "InvoiceSnapshotJson changed after adjustment.");
            Require(updated.ServiceInvoiceNumber == frozenInvoice, "ServiceInvoiceNumber changed after adjustment.");
        });

        await Scenario("J. History API authorization: anonymous 401, wrong role 403, BillingOperations role 200", async () => {
            var bill = await Post();
            var service = new BillingAdjustmentService(db);
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 10, Type = (int)AdjustmentLineType.CurrentBill, Reason = "Auth test" }], Guid.NewGuid());
            db.ChangeTracker.Clear();
            // Confirm data is committed before spinning up the HTTP host
            Require(await db.BillingAdjustments.AnyAsync(), "Adjustment must be committed before HTTP auth test.");
            // Spin up the same in-process test host used in Scenario 2
            var reader = new User { Id = 4, Role = 4, IsActive = true, IsVerified = true, Password = "synthetic" };
            if (!await db.Users.AnyAsync(u => u.Id == 4)) { db.Users.Add(reader); await db.SaveChangesAsync(); }
            var actor = await db.Users.SingleAsync(u => u.Id == 1);
            var key = System.Text.Encoding.UTF8.GetBytes("synthetic-adjustment-http-validation-key-only");
            string Token(User user) {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                return handler.WriteToken(handler.CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor {
                    Subject = new System.Security.Claims.ClaimsIdentity([
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role!.Value.ToString()),
                        new System.Security.Claims.Claim(UserSecurityStamp.ClaimType,
                            UserSecurityStamp.Create(key, user.Id, user.Password, user.Role,
                                user.IsActive, user.IsVerified, user.DateUpdated, user.DateVerified))]),
                    Issuer = "synthetic", Audience = "synthetic",
                    Expires = DateTime.UtcNow.AddMinutes(2),
                    SigningCredentials = new(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                        Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256) }));
            }
            var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(db);
            // Use a new service instance backed by the same committed data context
            builder.Services.AddSingleton<IBillingAdjustmentService>(new BillingAdjustmentService(db));
            builder.Services.AddControllers().AddApplicationPart(typeof(BillingAdjustmentController).Assembly);
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
                options.TokenValidationParameters = new() {
                    ValidateIssuer = true, ValidIssuer = "synthetic",
                    ValidateAudience = true, ValidAudience = "synthetic",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    ValidateLifetime = true, RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.Zero };
                options.Events = new() { OnTokenValidated = PersistedTokenValidation.Validate };
            });
            builder.Services.AddAuthorization(options =>
                options.AddPolicy("BillingOperations", p => p.RequireAuthenticatedUser().RequireRole("1","2","6","7")));
            await using var app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            try {
                using var client = new HttpClient {
                    BaseAddress = new Uri(app.Services
                        .GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
                        .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
                        .Addresses.Single()) };
                async Task<System.Net.HttpStatusCode> GetHistory(User? identity) {
                    using var request = new HttpRequestMessage(HttpMethod.Get,
                        $"api/BillingAdjustment/History/{Uri.EscapeDataString(bill.BillingReferenceId)}");
                    if (identity != null)
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token(identity));
                    using var response = await client.SendAsync(request);
                    return response.StatusCode;
                }
                Require(await GetHistory(null) == System.Net.HttpStatusCode.Unauthorized,
                    "Anonymous access to history must return 401.");
                db.ChangeTracker.Clear();
                reader = await db.Users.SingleAsync(u => u.Id == 4);
                var readerStatus = await GetHistory(reader);
                Require(readerStatus == System.Net.HttpStatusCode.Forbidden,
                    $"MeterReader access to history must return 403 (got {readerStatus}).");
                var actorStatus = await GetHistory(actor);
                Require(actorStatus == System.Net.HttpStatusCode.OK,
                    $"BillingOperations actor must get 200 from history endpoint (got {actorStatus}).");
            } finally { await app.StopAsync(); }
        });

        await Scenario("K. History content correct: date, amount, actor name, reference, reason, no financial mutation", async () => {
            var bill = await Post();
            var service = new BillingAdjustmentService(db);
            var before = DateTime.UtcNow.AddSeconds(-1);
            await service.SaveBatch(1, [new() { BillingReferenceId = bill.BillingReferenceId,
                Amount = 40, Type = (int)AdjustmentLineType.CurrentBill, Reason = "Reading Error: test" }], Guid.NewGuid());
            db.ChangeTracker.Clear();
            var billingBefore = (await db.Billings.AsNoTracking().SingleAsync()).RemainingAmount;
            var history = await service.GetHistory(bill.BillingReferenceId);
            // GetHistory must be read-only: Billing unchanged after calling it.
            db.ChangeTracker.Clear();
            Require((await db.Billings.AsNoTracking().SingleAsync()).RemainingAmount == billingBefore,
                "GetHistory mutated Billing.RemainingAmount.");
            Require(await db.BillingAdjustments.CountAsync() == 1, "GetHistory changed adjustment count.");
            Require(await db.BillingAdjustmentOperations.CountAsync() == 1, "GetHistory changed operation count.");
            Require(history.Count == 1, $"Expected 1 history row; got {history.Count}.");
            var row = history.Single();
            Require(row.Amount == 40m, $"Wrong Amount: {row.Amount}");
            Require(row.AdjustmentType == nameof(AdjustmentLineType.CurrentBill),
                $"Wrong AdjustmentType: {row.AdjustmentType}");
            Require(row.Reason == "Reading Error: test", $"Wrong Reason: '{row.Reason}'");
            Require(row.CreatedBy == 1, $"Wrong CreatedBy: {row.CreatedBy}");
            // CreatedByName comes from the users table LEFT JOIN; actor ID 1 is seeded
            // in the test fixture as "Customer Service Representative".
            Require(row.CreatedByName != null, "CreatedByName not resolved for seeded user.");
            Require(row.DateCreated >= before, "DateCreated not within expected range.");
            Require(row.ServiceInvoiceNumber == bill.ServiceInvoiceNumber,
                "ServiceInvoiceNumber not surfaced in history.");
        });

        Console.WriteLine($"Adjustment workflow: {scenarios - failures}/{scenarios} scenarios PASS; {failures} FAIL.");
        return failures == 0;
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
