using TMCWD.Application;
using TMCWD.Administration;
using TMCWD.CustomerSupport;
using TMCWD.Engineering;
using TMCWD.Services;
using TMCWD.Billing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"])
    || Encoding.UTF8.GetByteCount(builder.Configuration["Jwt:Key"] ?? "") < 32
    || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"])
    || string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]))
    throw new InvalidOperationException("Configure matching Jwt:Key (at least 32 UTF-8 bytes), Jwt:Issuer and Jwt:Audience for MVC and Data using environment variables or user-secrets.");

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthenticatedUserService>(services =>
{
    var authenticatedUser = new AuthenticatedUserService();
    var httpContext = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var serializedUser = httpContext?.Session.GetString(AuthenticatedUserService.SessionKey);

    if (!string.IsNullOrWhiteSpace(serializedUser))
    {
        try
        {
            var user = JsonSerializer.Deserialize<TMCWD.Model.Administrator.User>(serializedUser);
            if (user != null && user.Id > 0)
                authenticatedUser.SetUser(user);
        }
        catch (JsonException)
        {
            httpContext?.Session.Remove(AuthenticatedUserService.SessionKey);
        }
    }

    return authenticatedUser;
});
builder.Services.AddSessionApiClient();
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
builder.Services.AddTransient<DashboardTransaction>();
builder.Services.AddTransient<JobOrderTransaction>();
builder.Services.AddTransient<ApprovalHistoryTransaction>();
builder.Services.AddTransient<RequestFileTransaction>();
builder.Services.AddTransient<BillingAdjustmentTransaction>();
builder.Services.AddTransient<DebitCreditMemoTransaction>();
builder.Services.AddTransient<BillingTransaction>();
builder.Services.AddTransient<PenaltyTransaction>();
builder.Services.AddTransient<PaymentTransaction>();
builder.Services.AddTransient<WaterRateTransaction>(); 
builder.Services.AddTransient<ReadingSheetTransaction>();
builder.Services.AddTransient<ZoneBookTransaction>();
builder.Services.AddTransient<ReadingTransaction>();
builder.Services.AddTransient<ReadingSheetTemplateTransaction>();
builder.Services.AddTransient<OtherChargeTransaction>();
builder.Services.AddTransient<CollectionRemittanceTransaction>();
builder.Services.AddTransient<BatchPaymentUploadTransaction>();

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
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Name = "TMCWD.Session";
});

// Add JWT authentication 
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
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

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.UseStaticFiles();


app.Run();
