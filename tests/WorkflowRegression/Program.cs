using System.Net;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

// Capture real transaction requests without a server or database.
using var handler = new CaptureHandler();
using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
var service = new WebService(new ClientFactory(client));
var requests = new RequestTransaction(service);
var detail = new RequestDetail { RequestId = 7, RequestTypeId = 1, InspectionTypeDetail = "Inspect meter" };
await Check(() => new AdvancePaymentTransaction(service).GetByAccount(7), "GET", "/api/AdvancePayment/GetByAccount/7");
await Check(() => new BillingTransaction(service).GetByReference("BILL 7"), "GET", "/api/Billing/GetByBillingReference/BILL%207", "{}");
await Check(() => new ChargeTypeTransaction(service).GetByClassificationId(1), "GET", "/api/ChargeType/GetByClassification/1");
await Check(() => new ReadingTransaction(service).GetByReader(9), "GET", "/api/Reading/GetByReader/9");
await Check(() => requests.SaveUpdateRequestDetail(9, 7, detail), "POST", "/api/7/RequestDetail/SaveUpdate/9", "{}");
await Check(() => requests.SaveMulipleRequestDetail(9, 7, [detail]), "POST", "/api/7/RequestDetail/SaveMultiple/9");
await Check(() => requests.DeleteRequestDetails(7), "DELETE", "/api/7/RequestDetail/DeleteDetails", "true");
foreach (var invalid in new[] { new RequestDetail { RequestId = 7, RequestTypeId = 1, InspectionTypeDetail = " " },
    new RequestDetail { RequestId = 7, RequestTypeId = 0, InspectionTypeDetail = "Inspect" },
    new RequestDetail { RequestId = 0, RequestTypeId = 1, InspectionTypeDetail = "Inspect" } })
{
    await Reject(() => requests.SaveUpdateRequestDetail(9, 7, invalid));
    await Reject(() => requests.SaveMulipleRequestDetail(9, 7, [invalid]));
}
Console.WriteLine("PASS: 7 HTTP route checks and 6 invalid-detail checks; no database writes.");

async Task Check(Func<Task> action, string method, string route, string json = "[]")
{
    handler.Json = json;
    handler.Request = null;
    await action();
    if (handler.Request != (method, route)) throw new Exception($"Expected {method} {route}; received {handler.Request}.");
}

async Task Reject(Func<Task> action)
{
    handler.Request = null;
    try { await action(); }
    catch (Exception) when (handler.Request == null) { return; }
    throw new Exception("Invalid request details must be rejected before sending HTTP requests.");
}

sealed class ClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

sealed class CaptureHandler : HttpMessageHandler
{
    public string Json = "[]";
    public (string Method, string Path)? Request;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = (request.Method.Method, request.RequestUri!.AbsolutePath);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json, System.Text.Encoding.UTF8, "application/json") });
    }
}
