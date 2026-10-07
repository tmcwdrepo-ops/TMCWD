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
builder.Services.AddScoped<WebService>(services =>
{
    var service = new WebService(services.GetRequiredService<IHttpClientFactory>());
    var user = services.GetRequiredService<AuthenticatedUserService>().User;
    if (user.Id > 0 && user.Role.HasValue)
    {
        // Bridge the trusted server session to the API; never share user headers across requests.
        var config = services.GetRequiredService<IConfiguration>();
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor {
            Subject = new System.Security.Claims.ClaimsIdentity([
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role.Value.ToString())]),
            Issuer = config["Jwt:Issuer"], Audience = config["Jwt:Audience"], Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256)
        });
        service.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", handler.WriteToken(token));
    }
    return service;
});
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
