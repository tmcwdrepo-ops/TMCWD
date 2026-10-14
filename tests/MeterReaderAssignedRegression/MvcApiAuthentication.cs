using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TMCWD.Application;
using TMCWD.Billing;
using TMCWD.Services;

static class MvcApiAuthentication
{
    public static async Task RunLive(Uri address, string root)
    {
        if (!address.IsLoopback) throw new Exception("Live authentication probes are restricted to the local API.");
        var config = new ConfigurationBuilder().SetBasePath(Path.Combine(root, "TMCWD/TMCWD.Application"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(ApiClientRegistration).Assembly).AddEnvironmentVariables().Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        var user = new AuthenticatedUserService();
        // Read-only authentication probe; never submit a payment or financial write.
        user.SetUser(new TMCWD.Model.Administrator.User { Id = 1, Role = (int)UserRole.SuperAdmin });
        services.AddSingleton(user);
        services.AddHttpClient("TmcWdApi", client => client.BaseAddress = address);
        services.AddSessionApiClient();
        services.AddTransient<BillingTransaction>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var web = scope.ServiceProvider.GetRequiredService<WebService>();
        using var bills = await web.Client.GetAsync("api/Billing/GetAll");
        bills.EnsureSuccessStatusCode();
        using var json = System.Text.Json.JsonDocument.Parse(await bills.Content.ReadAsStringAsync());
        var first = json.RootElement.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            throw new Exception("No existing bill available for the read-only live API probe.");
        var reference = first.GetProperty("billingReferenceId").GetString()!;
        if (await scope.ServiceProvider.GetRequiredService<BillingTransaction>().GetCollectionBill(reference) == null)
            throw new Exception("Live Collection bill could not be retrieved.");
        foreach (var (role, expected) in new[] { (UserRole.Billing, HttpStatusCode.OK), (UserRole.MeterReader, HttpStatusCode.Forbidden) }) {
            user.SetUser(new TMCWD.Model.Administrator.User { Id = 1, Role = (int)role });
            await using var roleScope = provider.CreateAsyncScope();
            using var response = await roleScope.ServiceProvider.GetRequiredService<WebService>().Client
                .GetAsync("api/Billing/Collection/" + Uri.EscapeDataString(reference));
            if (response.StatusCode != expected) throw new Exception($"Live Collection authorization failed for {role}.");
        }
        web.Client.DefaultRequestHeaders.Authorization = null;
        using var missing = await web.Client.GetAsync("api/Billing/Collection/" + Uri.EscapeDataString(reference));
        if (missing.StatusCode != HttpStatusCode.Unauthorized) throw new Exception("Live API accepted no bearer token.");
        Console.WriteLine("PASS: live local Data Collection GET with actual MVC development JWT configuration: SuperAdmin/Cashier 200, MeterReader 403, no authentication 401; read-only, no record identifiers or token logged.");
    }

    public static async Task Run(Uri address, string key, string issuer, string audience)
    {
        foreach (var scenario in new (UserRole Role, string Issuer, string Audience, string Key, HttpStatusCode Status)[] {
            (Role: UserRole.Billing, Issuer: issuer, Audience: audience, Key: key, Status: HttpStatusCode.OK),
            (UserRole.BillingHead, issuer, audience, key, HttpStatusCode.OK),
            (UserRole.SuperAdmin, issuer, audience, key, HttpStatusCode.OK),
            (UserRole.GeneralServices, issuer, audience, key, HttpStatusCode.Forbidden),
            (UserRole.Accounting, issuer, audience, key, HttpStatusCode.Forbidden),
            (UserRole.MeterReader, issuer, audience, key, HttpStatusCode.Forbidden),
            (UserRole.Billing, "wrong-issuer", audience, key, HttpStatusCode.Unauthorized),
            (UserRole.Billing, issuer, "wrong-audience", key, HttpStatusCode.Unauthorized),
            (UserRole.Billing, issuer, audience, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), HttpStatusCode.Unauthorized)
        }) {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Jwt:Key"] = scenario.Key, ["Jwt:Issuer"] = scenario.Issuer, ["Jwt:Audience"] = scenario.Audience
            }).Build());
            var user = new AuthenticatedUserService();
            user.SecurityStamp = "synthetic-authentication-stamp";
            user.SetUser(new TMCWD.Model.Administrator.User { Id = 10, Role = (int)scenario.Role });
            services.AddSingleton(user);
            services.AddHttpClient("TmcWdApi", client => client.BaseAddress = address);
            // Exercise the exact MVC registration, not a second test-only token issuer.
            services.AddSessionApiClient();
            services.AddTransient<BillingTransaction>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var web = scope.ServiceProvider.GetRequiredService<WebService>();
            var header = web.Client.DefaultRequestHeaders.Authorization;
            if (header?.Scheme != "Bearer") throw new Exception("MVC omitted its bearer header.");
            var token = new JwtSecurityTokenHandler().ReadJwtToken(header.Parameter);
            if (token.ValidTo <= DateTime.UtcNow || token.ValidTo > DateTime.UtcNow.AddMinutes(6)
                || !token.Claims.Any(c => c.Type == "nameid" && c.Value == "10")
                || !token.Claims.Any(c => c.Type == "role" && c.Value == ((int)scenario.Role).ToString()))
                throw new Exception("MVC token identity, role or expiration is incorrect.");
            HttpStatusCode status;
            try {
                var bill = await scope.ServiceProvider.GetRequiredService<BillingTransaction>().GetCollectionBill("SYNTHETIC");
                if (bill?.AmountDue != 190m) throw new Exception("MVC lost the authenticated Collection response.");
                status = HttpStatusCode.OK;
            }
            catch (HttpRequestException error) when (error.StatusCode.HasValue) { status = error.StatusCode.Value; }
            if (status != scenario.Status) throw new Exception($"MVC-to-Data JWT scenario returned {status}, expected {scenario.Status}.");
            await using var another = provider.CreateAsyncScope();
            if (ReferenceEquals(web, another.ServiceProvider.GetRequiredService<WebService>()))
                throw new Exception("API client leaked across MVC request scopes.");
        }
        await CheckControlledErrors();
        await CheckCashierTotals();
        Console.WriteLine("PASS: actual MVC client registration -> Data JWT middleware: Billing/BillingHead/SuperAdmin 200, reader/GSO/Accounting 403, wrong issuer/audience/key 401; claims, expiry and request isolation; controlled MVC 401/403 responses.");
    }

    static async Task CheckCashierTotals()
    {
        foreach (var cashier in new[] { 7, 12 }) {
            using var client = new HttpClient(new CashierTotalsHandler(cashier)) { BaseAddress = new Uri("http://localhost/") };
            var web = new WebService(new ClientFactory(client));
            var user = new AuthenticatedUserService();
            user.SetUser(new TMCWD.Model.Administrator.User { Id = cashier, Role = (int)UserRole.Billing, Name = "Cashier " + cashier });
            var services = new ServiceCollection().AddLogging().AddSingleton(user).AddSingleton(web);
            foreach (var parameter in typeof(TMCWD.Application.Controllers.CollectionsController).GetConstructors().Single().GetParameters())
                if (parameter.ParameterType.Namespace is "TMCWD.Billing" or "TMCWD.Administration" or "TMCWD.CustomerSupport") services.AddTransient(parameter.ParameterType);
            await using var provider = services.BuildServiceProvider();
            var controller = ActivatorUtilities.CreateInstance<TMCWD.Application.Controllers.CollectionsController>(provider);
            var current = (Microsoft.AspNetCore.Mvc.OkObjectResult)controller.CurrentCollector();
            var identity = System.Text.Json.JsonSerializer.SerializeToElement(current.Value);
            if (identity.GetProperty("id").GetInt32() != cashier) throw new Exception("CurrentCollector leaked another session.");
            var result = (Microsoft.AspNetCore.Mvc.OkObjectResult)await controller.RemittanceExpected(DateTime.Today);
            var totals = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
            if (totals.GetProperty("cashOnHand").GetDecimal() != cashier * 10m
                || totals.GetProperty("check").GetDecimal() != 20m || totals.GetProperty("online").GetDecimal() != 30m
                || totals.GetProperty("totalCollected").GetDecimal() != cashier * 10m + 50m || totals.GetProperty("paymentCount").GetInt32() != 3)
                throw new Exception("MVC daily totals mixed cashier, day, cancellation or payment methods.");
            var monitor = (Microsoft.AspNetCore.Mvc.OkObjectResult)await controller.Monitoring();
            var rows = System.Text.Json.JsonSerializer.SerializeToElement(monitor.Value);
            if (rows.GetArrayLength() != 1 || rows[0].GetProperty("cashOnHand").GetDecimal() != cashier * 10m)
                throw new Exception("Monitoring used all-time or another cashier's cash.");
        }
        Console.WriteLine("PASS: MVC two cashier sessions have independent identity and daily Cash/Check/Online/receipt counts; canceled and prior-day payments excluded.");
    }

    sealed class CashierTotalsHandler(int cashier) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath != "/api/Payment/GetByCashier/" + cashier) throw new Exception("MVC requested another cashier's history.");
            var rows = new[] {
                new TMCWD.Model.Billing.PaymentHistoryRecord { PaymentReference = "CASH", CreatedBy = cashier, Method = 1, PaidAmount = cashier * 5m, Success = true, DateCreated = DateTime.Today },
                new() { PaymentReference = "CASH", CreatedBy = cashier, Method = 1, PaidAmount = cashier * 5m, Success = true, DateCreated = DateTime.Today },
                new() { PaymentReference = "CHECK", CreatedBy = cashier, Method = 2, PaidAmount = 20m, Success = true, DateCreated = DateTime.Today },
                new() { PaymentReference = "ONLINE", CreatedBy = cashier, Method = 3, PaidAmount = 30m, Success = true, DateCreated = DateTime.Today },
                new() { PaymentReference = "CANCELED", CreatedBy = cashier, Method = 1, PaidAmount = 999m, Success = false, DateCreated = DateTime.Today },
                new() { PaymentReference = "YESTERDAY", CreatedBy = cashier, Method = 1, PaidAmount = 888m, Success = true, DateCreated = DateTime.Today.AddDays(-1) } };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(rows), System.Text.Encoding.UTF8, "application/json") });
        }
    }

    static async Task CheckControlledErrors()
    {
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }) {
            using var client = new HttpClient(new FailureHandler(status)) { BaseAddress = new Uri("http://localhost/") };
            var web = new WebService(new ClientFactory(client));
            var user = new AuthenticatedUserService();
            user.SetUser(new TMCWD.Model.Administrator.User { Id = 1, Role = (int)UserRole.SuperAdmin });
            var services = new ServiceCollection().AddLogging().AddSingleton(user).AddSingleton(web);
            var controllerType = typeof(TMCWD.Application.Controllers.CollectionsController);
            foreach (var parameter in controllerType.GetConstructors().Single().GetParameters())
                if (parameter.ParameterType.Namespace is "TMCWD.Billing" or "TMCWD.Administration" or "TMCWD.CustomerSupport")
                    services.AddTransient(parameter.ParameterType);
            await using var provider = services.BuildServiceProvider();
            var controller = ActivatorUtilities.CreateInstance<TMCWD.Application.Controllers.CollectionsController>(provider);
            var result = await controller.SearchAccounts("SYN") as Microsoft.AspNetCore.Mvc.ObjectResult;
            if (result?.StatusCode != (int)status || result.Value == null)
                throw new Exception("MVC did not preserve the upstream authentication/authorization status.");
        }
    }

    sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
    sealed class FailureHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path.Contains("/Account/Search") ? "[{\"id\":20,\"accountNumber\":\"SYN\",\"customerId\":30}]"
                : path.Contains("/Customer/Get") ? "{\"id\":30,\"firstname\":\"Synthetic\",\"lastname\":\"Customer\"}"
                : path.Contains("/Billing/GetByAccountId") ? "[{\"billingReferenceId\":\"SYN\",\"paymentStatus\":1}]" : "[]";
            return Task.FromResult(new HttpResponseMessage(path.Contains("/Billing/Collection/") ? status : HttpStatusCode.OK) {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
