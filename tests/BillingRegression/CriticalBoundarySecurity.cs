using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Utility.Encryption;

static class CriticalBoundarySecurity
{
    public static async Task Run(UserDbContext db, HttpClient client,
        Func<UserRole, string?, string> token, Func<string, Func<Task>, Task> test,
        Func<string?, HttpStatusCode, string, HttpMethod, HttpContent?, Task> reject)
    {
        HttpContent Body(string value) => new StringContent(value, Encoding.UTF8, "application/json");
        async Task<HttpResponseMessage> Send(string path, object body, UserRole? role = null, string? id = "1", HttpMethod? method = null)
        {
            using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            if (role.HasValue) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token(role.Value, id));
            return await client.SendAsync(request);
        }
        const string userPath = "api/Users/SaveUpdate/999";
        const string userBody = "{\"id\":1,\"name\":\"Updated CSR\",\"email\":\"csr@test.invalid\",\"role\":2,\"isActive\":true}";
        const string customerPath = "api/Customer/SaveUpdate/999";
        const string customerBody = "{\"id\":1,\"firstname\":\"Updated\",\"lastname\":\"Tester\",\"phoneNumber\":\"09000000000\",\"email\":\"synthetic@test.invalid\",\"isActive\":true}";
        const string accountPath = "api/Account/SaveUpdate/999";
        const string accountBody = "{\"id\":1,\"customerId\":1,\"houseNumber\":\"Updated\",\"meterSize\":0.5,\"classification\":1}";
        const string passwordPath = "api/Users/password";
        const string passwordBody = "{\"currentPassword\":\"synthetic-current\",\"newPassword\":\"synthetic-new\",\"confirmPassword\":\"synthetic-new\"}";
        var writes = new (string Path, string Body, UserRole Role, string Id)[] {
            (userPath, userBody, UserRole.SuperAdmin, "3"),
            (customerPath, customerBody, UserRole.CustomerRepresentative, "1"),
            (accountPath, accountBody, UserRole.CustomerRepresentative, "1"),
            ("api/Billing/ChargeAnchor", "{\"accountId\":1}", UserRole.CustomerRepresentative, "1"),
            ("api/InspectionType/SaveUpdate/999", "{\"id\":1,\"name\":\"Updated\",\"isActive\":true}", UserRole.SuperAdmin, "3"),
            ("api/ReadingSheetTemplate/SaveUpdate/999", "{\"name\":\"Synthetic\",\"readerId\":10,\"zoneBookId\":1}", UserRole.CustomerRepresentative, "1"),
            ("api/ReadingSheetTemplate/Deactivate/1/999", "{}", UserRole.CustomerRepresentative, "1") };
        foreach (var item in writes)
        {
            var method = item.Path.Contains("Deactivate") ? HttpMethod.Put : HttpMethod.Post;
            await test($"HTTP B3 anonymous denied without changes: {item.Path}",
                () => reject(null, HttpStatusCode.Unauthorized, item.Path, method, Body(item.Body)));
            await test($"HTTP B3 wrong role denied without changes: {item.Path}",
                () => reject(token(UserRole.MeterReader, "1"), HttpStatusCode.Forbidden, item.Path, method, Body(item.Body)));
            await test($"HTTP B3 invalid identities denied without changes: {item.Path}", async () => {
                foreach (var id in new string?[] { null, "", "bad", "0", "-1", "2147483648" })
                    await reject(token(item.Role, id), HttpStatusCode.Unauthorized, item.Path, method, Body(item.Body));
            });
        }
        await test("HTTP B3 user security fields cannot be overposted", async () => {
            foreach (var extra in new[] { "\"createdBy\":999", "\"updatedBy\":999", "\"dateCreated\":\"2000-01-01\"",
                "\"dateUpdated\":\"2000-01-01\"", "\"isVerified\":true", "\"dateVerified\":\"2000-01-01\"", "\"rememberToken\":\"forged\"" })
                await reject(token(UserRole.SuperAdmin, "3"), HttpStatusCode.BadRequest, userPath, HttpMethod.Post,
                    Body(userBody[..^1] + "," + extra + "}"));
        });
        await test("HTTP B3 self escalation, peer admin and untrusted administrative identity are denied", async () => {
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.Forbidden, userPath, HttpMethod.Post,
                Body(userBody.Replace("\"role\":2", "\"role\":1")));
            await reject(token(UserRole.SuperAdmin, "3"), HttpStatusCode.Forbidden, userPath, HttpMethod.Post,
                Body("{\"id\":3,\"name\":\"Admin\",\"email\":\"admin@test.invalid\",\"role\":2}"));
            await reject(token(UserRole.SuperAdmin, "3"), HttpStatusCode.Forbidden, userPath, HttpMethod.Post,
                Body("{\"id\":3,\"name\":\"Admin\",\"email\":\"admin@test.invalid\",\"isActive\":false}"));
            await reject(token(UserRole.SuperAdmin, "1"), HttpStatusCode.Forbidden, userPath, HttpMethod.Post,
                Body("{\"id\":3,\"name\":\"Admin\",\"email\":\"admin@test.invalid\",\"role\":2}"));
            var admin = await db.Users.SingleAsync(u => u.Id == 3); admin.IsVerified = false; await db.SaveChangesAsync();
            await reject(token(UserRole.SuperAdmin, "3"), HttpStatusCode.Forbidden, userPath, HttpMethod.Post, Body(userBody));
        });
        await test("HTTP B3 authorized user edit uses claim actor and preserves creation, hash and verification", async () => {
            var before = await db.Users.AsNoTracking().SingleAsync(u => u.Id == 1);
            using var response = await Send(userPath, new { id = 1, name = "Updated CSR", email = before.Email,
                role = (int)UserRole.CustomerRepresentative, isActive = true }, UserRole.SuperAdmin, "3");
            Check(response.StatusCode == HttpStatusCode.OK, "Authorized user edit failed.");
            var json = await response.Content.ReadAsStringAsync();
            Check(!json.Contains("password", StringComparison.OrdinalIgnoreCase) && !json.Contains("rememberToken", StringComparison.OrdinalIgnoreCase), "User response leaks authentication material.");
            db.ChangeTracker.Clear(); var after = await db.Users.SingleAsync(u => u.Id == 1);
            Check(after.UpdatedBy == 3 && after.CreatedBy == before.CreatedBy && after.DateCreated == before.DateCreated
                && after.Password == before.Password && after.IsVerified == before.IsVerified, "User security fields or audit changed.");
        });
        await test("HTTP B3 a legitimate administrator cannot modify a peer administrator", async () => {
            // Existing connection-local synthetic staff fixture only; never an operational account.
            (await db.Users.SingleAsync(u => u.Id == 2)).Role = (int)UserRole.SuperAdmin;
            await db.SaveChangesAsync();
            await reject(token(UserRole.SuperAdmin, "3"), HttpStatusCode.Forbidden, userPath, HttpMethod.Post,
                Body("{\"id\":2,\"name\":\"Peer\",\"email\":\"oic@test.invalid\",\"isActive\":false}"));
        });
        await test("B3 profile updates do not restore a concurrently changed password", async () => {
            var staleProfile = await db.Users.SingleAsync(u => u.Id == 1);
            await using var passwordDb = new UserDbContext(new DbContextOptionsBuilder<UserDbContext>()
                .UseMySql(db.Database.GetDbConnection(), new MySqlServerVersion(new Version(8, 0, 46))).Options);
            var passwordAccount = await passwordDb.Users.SingleAsync(u => u.Id == 1);
            passwordAccount.Password = StringEncyption.Encrypt("synthetic-concurrent-password");
            await new UserService(passwordDb).SaveUpdate(1, passwordAccount);
            staleProfile.Name = "Synthetic concurrent profile";
            await new UserService(db).SaveUpdate(3, staleProfile);
            db.ChangeTracker.Clear();
            Check((await db.Users.SingleAsync(u => u.Id == 1)).Password == StringEncyption.Encrypt("synthetic-concurrent-password"),
                "Profile update restored an old password through whole-entity persistence.");
        });
        await test("HTTP B3 explicit administrator staff role/active edit preserves verification", async () => {
            using var response = await Send(userPath, new { id = 1, name = "Updated CSR", email = "csr@test.invalid",
                role = (int)UserRole.CustomerServiceOic, isActive = false }, UserRole.SuperAdmin, "3");
            Check(response.StatusCode == HttpStatusCode.OK, "Explicit administrator staff edit failed."); db.ChangeTracker.Clear();
            var saved = await db.Users.SingleAsync(u => u.Id == 1);
            Check(saved.Role == (int)UserRole.CustomerServiceOic && saved.IsActive == false && saved.IsVerified == true
                && saved.UpdatedBy == 3, "Authorized fields or verification changed unexpectedly.");
        });
        await test("HTTP B3 staff creation hashes the body password and cannot auto-verify", async () => {
            using var response = await Send(userPath, new { name = "Synthetic new staff", email = "new-staff@test.invalid",
                role = (int)UserRole.CustomerRepresentative, isActive = true, password = "synthetic-initial" }, UserRole.SuperAdmin, "3");
            Check(response.StatusCode == HttpStatusCode.OK, "Supported staff creation failed."); db.ChangeTracker.Clear();
            var saved = await db.Users.SingleAsync(u => u.Email == "new-staff@test.invalid");
            Check(saved.Password == StringEncyption.Encrypt("synthetic-initial") && saved.CreatedBy == 3 && saved.UpdatedBy == 3
                && saved.IsVerified == false && saved.DateVerified == null, "Staff creation trusted security metadata.");
        });
        await test("HTTP B3 generic password edits and arbitrary self-service targets are rejected", async () => {
            await reject(token(UserRole.SuperAdmin, "3"), HttpStatusCode.BadRequest, userPath, HttpMethod.Post,
                Body(userBody[..^1] + ",\"password\":\"forged\"}"));
            await reject(null, HttpStatusCode.Unauthorized, passwordPath, HttpMethod.Post, Body(passwordBody));
            foreach (var id in new string?[] { null, "bad", "0", "-1" })
                await reject(token(UserRole.CustomerRepresentative, id), HttpStatusCode.Unauthorized, passwordPath, HttpMethod.Post, Body(passwordBody));
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.Forbidden, passwordPath, HttpMethod.Post, Body(passwordBody));
            foreach (var extra in new[] { "\"id\":2", "\"userId\":3", "\"updatedBy\":999" })
                await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.BadRequest, passwordPath, HttpMethod.Post,
                    Body(passwordBody[..^1] + "," + extra + "}"));
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.BadRequest,
                passwordPath + "?id=2&userId=999", HttpMethod.Post, Body(passwordBody));
            await reject(token(UserRole.SuperAdmin, "3"), HttpStatusCode.NotFound,
                "api/Users/ChangePassword/1/999/retired", HttpMethod.Put, null);
        });
        await test("HTTP B3 body-based self password change preserves legacy hash and claim identity", async () => {
            var account = await db.Users.SingleAsync(u => u.Id == 1);
            account.Password = StringEncyption.Encrypt("synthetic-current"); await db.SaveChangesAsync();
            using var response = await Send(passwordPath, new { currentPassword = "synthetic-current", newPassword = "synthetic-new",
                confirmPassword = "synthetic-new" }, UserRole.CustomerRepresentative);
            Check(response.StatusCode == HttpStatusCode.NoContent, "Supported password change failed.");
            db.ChangeTracker.Clear(); account = await db.Users.SingleAsync(u => u.Id == 1);
            Check(account.Password == StringEncyption.Encrypt("synthetic-new") && account.UpdatedBy == 1,
                "Password hash or audit actor changed.");
            Check((await db.Users.SingleAsync(u => u.Id == 2)).Password == "test", "Password change touched another account.");
        });
        await test("HTTP B3 valid current password does not authorize mismatched confirmation or inactive account", async () => {
            var account = await db.Users.SingleAsync(u => u.Id == 1);
            account.Password = StringEncyption.Encrypt("synthetic-current"); await db.SaveChangesAsync();
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.BadRequest, passwordPath, HttpMethod.Post,
                Body(passwordBody.Replace("\"confirmPassword\":\"synthetic-new\"", "\"confirmPassword\":\"mismatch\"")));
            account = await db.Users.SingleAsync(u => u.Id == 1); account.IsActive = false; await db.SaveChangesAsync();
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.Forbidden, passwordPath, HttpMethod.Post, Body(passwordBody));
        });
        await test("HTTP B3 profile reads require authentication and never expose passwords", async () => {
            await reject(null, HttpStatusCode.Unauthorized, "api/Users/GetUsers", HttpMethod.Get, null);
            using var response = await Send("api/Users/GetUsers", new { }, UserRole.CustomerRepresentative, method: HttpMethod.Get);
            Check(response.StatusCode == HttpStatusCode.OK, "Authorized profile read failed.");
            var data = await response.Content.ReadAsStringAsync();
            Check(!data.Contains("password", StringComparison.OrdinalIgnoreCase) && !data.Contains("rememberToken", StringComparison.OrdinalIgnoreCase), "Profile exposes secrets.");
        });
        await test("HTTP B3 credentials are checked server-side; inactive/unverified login and forged credentials fail", async () => {
            var account = await db.Users.SingleAsync(u => u.Id == 1);
            account.Password = StringEncyption.Encrypt("synthetic-current"); await db.SaveChangesAsync();
            foreach (var identifier in new[] { "csr@test.invalid", "1" }) {
                using var response = await Send("api/auth/login", new { identifier, password = "synthetic-current" });
                Check(response.StatusCode == HttpStatusCode.OK && !(await response.Content.ReadAsStringAsync()).Contains("password", StringComparison.OrdinalIgnoreCase), "Body credential validation failed or exposed password.");
            }
            await reject(null, HttpStatusCode.Unauthorized, "api/auth/login", HttpMethod.Post,
                Body("{\"identifier\":\"1\",\"password\":\"wrong\"}"));
            account = await db.Users.SingleAsync(u => u.Id == 1); account.IsActive = false; await db.SaveChangesAsync();
            await reject(null, HttpStatusCode.Forbidden, "api/auth/login", HttpMethod.Post,
                Body("{\"identifier\":\"1\",\"password\":\"synthetic-current\"}"));
            account = await db.Users.SingleAsync(u => u.Id == 1); account.IsActive = true; account.IsVerified = false; await db.SaveChangesAsync();
            await reject(null, HttpStatusCode.Forbidden, "api/auth/login", HttpMethod.Post,
                Body("{\"identifier\":\"1\",\"password\":\"synthetic-current\"}"));
        });
        foreach (var item in new[] { (customerPath, customerBody), (accountPath, accountBody) })
            await test($"HTTP B3 account/customer protected fields cannot be overposted: {item.Item1}", async () => {
                foreach (var extra in new[] { "\"createdBy\":999", "\"updatedBy\":999", "\"dateCreated\":\"2000-01-01\"",
                    "\"accountNumber\":\"forged\"", "\"status\":3", "\"zoneBookId\":999", "\"sequence\":999", "\"remainingAmount\":999" })
                    await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.BadRequest, item.Item1, HttpMethod.Post,
                        Body(item.Item2[..^1] + "," + extra + "}"));
            });
        await test("HTTP B3 authorized customer edit preserves creation and uses claim actor", async () => {
            var before = await db.Customers.AsNoTracking().SingleAsync();
            using var response = await Send(customerPath, new { id = 1, firstname = "Updated", lastname = "Tester",
                phoneNumber = before.PhoneNumber, email = before.Email, isActive = true }, UserRole.CustomerRepresentative);
            Check(response.StatusCode == HttpStatusCode.OK, "Authorized customer edit failed."); db.ChangeTracker.Clear();
            var after = await db.Customers.SingleAsync();
            Check(after.Firstname == "Updated" && after.Updatedby == 1 && after.CreatedBy == before.CreatedBy
                && after.DateCreated == before.DateCreated, "Customer edit changed creation or trusted forged audit actor.");
        });
        await test("HTTP B3 account address edit preserves authority, financial linkage and creation", async () => {
            var before = await db.Accounts.AsNoTracking().SingleAsync();
            using var response = await Send(accountPath, new { id = 1, customerId = 1, houseNumber = "Updated",
                meterNumber = before.MeterNumber, meterSize = before.MeterSize, classification = before.Classification }, UserRole.CustomerRepresentative);
            Check(response.StatusCode == HttpStatusCode.OK, "Authorized account edit failed."); db.ChangeTracker.Clear();
            var after = await db.Accounts.SingleAsync();
            Check(after.HouseNumber == "Updated" && after.UpdatedBy == 1 && after.AccountNumber == before.AccountNumber
                && after.Status == before.Status && after.ZoneBookId == before.ZoneBookId && after.Sequence == before.Sequence
                && after.CustomerId == before.CustomerId && after.DateCreated == before.DateCreated, "Account authority changed.");
        });
        await test("HTTP B3 account meter/classification and ownership changes cannot bypass service workflow", async () => {
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.Conflict, accountPath, HttpMethod.Post,
                Body(accountBody.Replace("\"classification\":1", "\"classification\":2")));
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.Conflict, accountPath, HttpMethod.Post,
                Body(accountBody.Replace("\"customerId\":1", "\"customerId\":999")));
        });
        await test("HTTP B3 customer/account creation remains supported with server-owned number/status/audit", async () => {
            using var customer = await Send(customerPath, new { firstname = "Synthetic", lastname = "New", phoneNumber = "09000000000",
                email = "new@test.invalid", isActive = true }, UserRole.CustomerRepresentative);
            Check(customer.StatusCode == HttpStatusCode.OK, "Customer creation failed.");
            var id = (await customer.Content.ReadFromJsonAsync<Customer>())!.Id;
            using var account = await Send(accountPath, new { customerId = id, houseNumber = "Synthetic" }, UserRole.CustomerRepresentative);
            Check(account.StatusCode == HttpStatusCode.OK, "Account creation failed.");
            var saved = (await account.Content.ReadFromJsonAsync<Account>())!;
            Check(saved.Status == (int)AccountStatus.Pending && saved.AccountNumber.StartsWith("ACCT")
                && saved.ZoneBookId == 0 && saved.Sequence == 0 && saved.CreatedBy == 1 && saved.UpdatedBy == 1, "New account trusted server-owned fields.");
        });
        await test("HTTP B3 charge anchor is zero-valued, claim-owned and idempotent", async () => {
            using var response = await Send("api/Billing/ChargeAnchor", new { accountId = 1 }, UserRole.CustomerRepresentative);
            Check(response.StatusCode == HttpStatusCode.OK, "Charge anchor failed.");
            var saved = (await response.Content.ReadFromJsonAsync<Billing>())!;
            Check(saved.CreatedBy == 1 && saved.UpdatedBy == 1 && saved.TotalBillAmount == 0 && saved.RemainingAmount == 0
                && saved.ReadingId == 0 && saved.InvoiceSnapshotJson == null && saved.ServiceInvoiceNumber == null
                && saved.PaymentStatus == (int)PaymentStatus.Unpaid, "Charge anchor changed financial contract.");
            using var repeated = await Send("api/Billing/ChargeAnchor", new { accountId = 1 }, UserRole.CustomerRepresentative);
            Check(repeated.StatusCode == HttpStatusCode.OK && (await repeated.Content.ReadFromJsonAsync<Billing>())!.Id == saved.Id
                && await db.Billings.CountAsync() == 1, "Charge anchor duplicate request created another bill.");
        });
        await test("HTTP B3 charge anchor preserves an existing issued invoice, snapshot and balance", async () => {
            db.Billings.Add(new Billing { AccountId = 1, ReadingId = 1, BillingReferenceId = "SYNTHETIC-ISSUED",
                BillingPeriod = DateTime.Now, DateCreated = DateTime.Now, DateUpdated = DateTime.Now,
                PaymentStatus = (int)PaymentStatus.Unpaid, TotalBillAmount = 120, RemainingAmount = 55,
                ServiceInvoiceNumber = "TEST-ISSUED", InvoiceSnapshotJson = "synthetic immutable snapshot", CreatedBy = 2, UpdatedBy = 2 });
            await db.SaveChangesAsync();
            var before = JsonSerializer.Serialize(await db.Billings.AsNoTracking().SingleAsync());
            using var response = await Send("api/Billing/ChargeAnchor", new { accountId = 1 }, UserRole.CustomerRepresentative);
            Check(response.StatusCode == HttpStatusCode.OK && await db.Billings.CountAsync() == 1
                && JsonSerializer.Serialize(await db.Billings.AsNoTracking().SingleAsync()) == before,
                "Charge anchor modified an issued bill or allocated a duplicate invoice.");
        });
        await test("HTTP B3 narrow charge anchor rejects financial/snapshot/audit tampering and nonexistent account", async () => {
            foreach (var extra in new[] { "\"totalBillAmount\":999", "\"remainingAmount\":999", "\"paymentStatus\":2",
                "\"readingId\":999", "\"invoiceSnapshotJson\":\"forged\"", "\"serviceInvoiceNumber\":\"forged\"", "\"createdBy\":999" })
                await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.BadRequest, "api/Billing/ChargeAnchor", HttpMethod.Post,
                    Body("{\"accountId\":1," + extra + "}"));
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.Conflict, "api/Billing/ChargeAnchor", HttpMethod.Post,
                Body("{\"accountId\":999}"));
        });
        await test("HTTP B3 inspection-type edit ignores forged creation/audit fields", async () => {
            var before = await db.InspectionTypes.AsNoTracking().SingleAsync();
            using var response = await Send("api/InspectionType/SaveUpdate/999", new { id = 1, name = "Updated", isActive = true,
                createdBy = 999, updatedBy = 999, dateCreated = new DateTime(2000, 1, 1) }, UserRole.SuperAdmin, "3");
            Check(response.StatusCode == HttpStatusCode.OK, "Inspection-type edit failed."); db.ChangeTracker.Clear();
            var after = await db.InspectionTypes.SingleAsync();
            Check(after.CreatedBy == before.CreatedBy && after.DateCreated == before.DateCreated && after.UpdatedBy == 3, "Inspection-type audit tampering accepted.");
        });
        await test("HTTP B3 reading-sheet template validates assignment and claims on create/deactivate", async () => {
            db.Users.Add(new User { Id = 10, Name = "Synthetic reader", Email = "reader@test.invalid", Role = (int)UserRole.MeterReader,
                IsActive = true, IsVerified = true, Password = "test", DateCreated = DateTime.Now }); await db.SaveChangesAsync();
            using var response = await Send("api/ReadingSheetTemplate/SaveUpdate/999", new { name = "Synthetic", readerId = 10, zoneBookId = 1,
                createdBy = 999, updatedBy = 999, dateCreated = new DateTime(2000, 1, 1) }, UserRole.CustomerRepresentative);
            Check(response.StatusCode == HttpStatusCode.OK, "Template creation failed.");
            var saved = (await response.Content.ReadFromJsonAsync<ReadingSheetTemplate>())!;
            Check(saved.CreatedBy == 1 && saved.UpdatedBy == 1 && saved.DateCreated.Year != 2000, "Template trusted forged actor.");
            using var deactivated = await Send($"api/ReadingSheetTemplate/Deactivate/{saved.Id}/999", new { }, UserRole.CustomerRepresentative, method: HttpMethod.Put);
            Check(deactivated.StatusCode == HttpStatusCode.OK, "Template deactivation failed."); db.ChangeTracker.Clear();
            Check(!(await db.ReadingSheetTemplates.SingleAsync()).IsActive && (await db.ReadingSheetTemplates.SingleAsync()).UpdatedBy == 1, "Template deactivation actor forged.");
            await reject(token(UserRole.CustomerRepresentative, "1"), HttpStatusCode.BadRequest, "api/ReadingSheetTemplate/SaveUpdate/999", HttpMethod.Post,
                Body("{\"name\":\"Invalid\",\"readerId\":1,\"zoneBookId\":1}"));
        });
        var quarantined = new (string Path, HttpMethod Method)[] {
            ("api/Billing/SaveUpdate/999", HttpMethod.Post),
            ("api/1/RequestDetail/SaveUpdate/999", HttpMethod.Post), ("api/1/RequestDetail/SaveMultiple/999", HttpMethod.Post),
            ("api/1/RequestDetail/Delete/1", HttpMethod.Delete), ("api/1/RequestDetail/DeleteDetails", HttpMethod.Delete),
            ("api/RequestFile/SaveUpdate/999/1", HttpMethod.Post), ("api/RequestFile/SaveRange", HttpMethod.Post),
            ("api/1/ApprovalHistory/Save/999", HttpMethod.Post), ("api/1/Finding/SaveUpdate/999", HttpMethod.Post),
            ("api/InspectionReport/SaveUpdate/999", HttpMethod.Post), ("api/1/Material/SaveUpdate/999", HttpMethod.Post),
            ("api/1/Material/UpdateQuantityOrNewUnitCost/999", HttpMethod.Put), ("api/1/Material/Delete/1", HttpMethod.Delete),
            ("api/1/Recommendation/SaveUpdate/999", HttpMethod.Post), ("api/Workflow/SaveUpdate/999", HttpMethod.Post),
            ("api/Workflow/Delete/1", HttpMethod.Delete), ("api/ÌnpectionTypeDetail/SaveUpdate", HttpMethod.Post),
            ("api/Inventory/SaveUpdate/999", HttpMethod.Post) };
        foreach (var item in quarantined)
        {
            await test($"HTTP B3 quarantined boundary rejects anonymous without changes: {item.Path}",
                () => reject(null, HttpStatusCode.Unauthorized, item.Path, item.Method, Body("{}")));
            await test($"HTTP B3 quarantined boundary rejects every role without changes: {item.Path}", async () => {
                foreach (var role in Enum.GetValues<UserRole>())
                    await reject(token(role, role == UserRole.SuperAdmin ? "3" : "1"), HttpStatusCode.Forbidden,
                        item.Path, item.Method, Body("{\"id\":1,\"accountId\":999,\"remainingAmount\":999,\"paymentStatus\":2,\"invoiceSnapshotJson\":\"forged\"}"));
            });
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
