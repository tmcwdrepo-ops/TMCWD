    using Microsoft.EntityFrameworkCore;
    using MySqlConnector;
    using TMCWD.Data.Context;
    using TMCWD.Data.Services;
    using TMCWD.Model.Billing.Interfaces;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.IdentityModel.Tokens;
    using System.Globalization;
    using System.Text;

    var builder = WebApplication.CreateBuilder(args);

    // Keep local development logging self-contained. The Windows Event Log
    // provider can require elevated permissions when its source is missing.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();

    // Add services to the container.

    // Add CORS policy
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowApplication", policy =>
        {
            policy.WithOrigins("http://localhost:5054")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    builder.Services.AddControllers();
    var jwtKey = builder.Configuration["Jwt:Key"];
    var jwtIssuer = builder.Configuration["Jwt:Issuer"];
    var jwtAudience = builder.Configuration["Jwt:Audience"];
    if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32
        || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
        throw new InvalidOperationException(
            "Configure Jwt:Key (at least 32 UTF-8 bytes), Jwt:Issuer and Jwt:Audience using environment variables or user-secrets.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.IncludeErrorDetails = false;
            options.Events = new JwtBearerEvents { OnTokenValidated = PersistedTokenValidation.Validate };
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwtIssuer,
                ValidateAudience = true, ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true, RequireExpirationTime = true,
                RequireSignedTokens = true, ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero
            };
        });
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        // shortcut: Legacy writes stay disabled, enable only after Finance/Admin or provider trust rules are approved and implemented.
        options.AddPolicy("PendingAuthorizationDecision", policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(_ => false));
        options.AddPolicy(nameof(UserRole.MeterReader), policy => policy.RequireAuthenticatedUser()
            .RequireRole(((int)UserRole.MeterReader).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("Collection", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.Cashier).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy(nameof(UserRole.SuperAdmin), policy => policy.RequireAuthenticatedUser()
            .RequireRole(((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("BillingOperations", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.CustomerRepresentative).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.CustomerServiceOic).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.Cashier).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("CustomerService", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.CustomerRepresentative).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.CustomerServiceOic).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("ConfirmDisconnection", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.CustomerServiceOic).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("ConfirmReconnection", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.CustomerServiceOic).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture)));
    });
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection is not configured. Configure it with .NET user-secrets for local development.");
    builder.Services.AddDbContext<UserDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
    builder.Services.AddScoped<IRequestService, RequestService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<IInspectionTypeService, InspectionTypeService>();
    builder.Services.AddScoped<IInventoryService, InventoryService>();
    builder.Services.AddScoped<IRequestDetailService, RequestDetailService>();
    builder.Services.AddScoped<IRecommendationService, RecommendationService>();
    builder.Services.AddScoped<IInspectionReportService, InspectionReportService>();
    builder.Services.AddScoped<IMaterialService, MaterialService>();
    builder.Services.AddScoped<IWorkflowService, WorkflowService>();
    builder.Services.AddScoped<IFindingService, FindingService>();
    builder.Services.AddScoped<IJobOrderService, JobOrderService>();
    builder.Services.AddScoped<IApprovalHistoryService, ApprovalHistoryService>();
    builder.Services.AddScoped<IRequestFileService, RequestFileService>();
    builder.Services.AddScoped<IBillingService, BillingService>();
    builder.Services.AddScoped<AccountLedgerService>();
    builder.Services.AddScoped<DisconnectionMonitoringService>();
    builder.Services.AddScoped<IReadingService, ReadingService>();
    builder.Services.AddScoped<IWaterRateService, WaterRateService>(); 
    builder.Services.AddScoped<IMeterMaintenanceRateService, MeterMaintenanceRateService>();
    builder.Services.AddScoped<IBillingAdjustmentService, BillingAdjustmentService>();
    builder.Services.AddScoped<IDebitCreditMemoService, DebitCreditMemoService>();
    builder.Services.AddScoped<IReadingSheetService, ReadingSheetService>();
    
    builder.Services.AddScoped<IZoneBookService, ZoneBookService>();
    builder.Services.AddScoped<IReadingSheetTemplateService, ReadingSheetTemplateService>();
    builder.Services.AddScoped<IChargeTypeService, ChargeTypeService>();
    builder.Services.AddScoped<IPenaltyService, PenaltyService>();
    builder.Services.AddScoped<IOtherChargeService, OtherChargeService>();
    builder.Services.AddScoped<IOtherFeeTypeService, OtherFeeTypeService>();
    builder.Services.AddScoped<IAdvancePaymentService, AdvancePaymentService>();
    builder.Services.AddScoped<IPaymentCheckService, PaymentCheckService>();
    builder.Services.AddScoped<IPaymentService, PaymentService>();
    builder.Services.AddScoped<IBatchPaymentUploadService, BatchPaymentUploadService>();
    // ── Phase 6D ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<IBirInvoiceConfigService, BirInvoiceConfigService>();

    builder.Services.AddEndpointsApiExplorer();
    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Use CORS
    app.UseCors("AllowApplication");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
