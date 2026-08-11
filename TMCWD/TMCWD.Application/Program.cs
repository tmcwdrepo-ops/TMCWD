using TMCWD.Administration;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Engineering;
using TMCWD.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<AuthenticatedUserService>();
builder.Services.AddSingleton<WebService>();
builder.Services.AddTransient<UserTransaction>();
builder.Services.AddTransient<InspectionTypeTransaction>();
builder.Services.AddTransient<AccountTransaction>();
builder.Services.AddTransient<CustomerTransaction>();
builder.Services.AddTransient<RequestTransaction>();
builder.Services.AddTransient<InventoryTransaction>();
builder.Services.AddTransient<RecommendationTransaction>();
builder.Services.AddTransient<MaterialTransaction>();
builder.Services.AddTransient<OtherFeeTypeTransaction>();
builder.Services.AddTransient<FindingTransaction>();
builder.Services.AddTransient<ApplicationLoginTransaction>();
builder.Services.AddTransient<JobOrderTransaction>();
builder.Services.AddTransient<ApprovalHistoryTransaction>();
builder.Services.AddTransient<RequestFileTransaction>();
builder.Services.AddTransient<ReadingTransaction>();
builder.Services.AddTransient<ReadingSheetTransaction>();
builder.Services.AddTransient<ZoneBookTransaction>();
builder.Services.AddTransient<BillingTransaction>();
builder.Services.AddTransient<PenaltyTransaction>();
builder.Services.AddTransient<ReadingSheetTemplateTransaction>();
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddHttpClient("TmcWdApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["webServiceLocation"] ?? "");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = false;
    options.Cookie.Name = "TMCWD.Session";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.UseStaticFiles();


app.Run();
