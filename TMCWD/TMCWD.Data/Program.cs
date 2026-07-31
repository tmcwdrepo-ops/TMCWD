    using Microsoft.EntityFrameworkCore;
    using MySqlConnector;
    using TMCWD.Data.Context;
    using TMCWD.Data.Services;
    using TMCWD.Model.Billing.Interfaces;

    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.

    builder.Services.AddControllers();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
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
builder.Services.AddScoped<IReadingSheetService, ReadingSheetService>();
builder.Services.AddScoped<IChargeTypeService, ChargeTypeService>();
builder.Services.AddScoped<IPenaltyService, PenaltyService>();
builder.Services.AddScoped<IOtherChargeService, OtherChargeService>();
builder.Services.AddScoped<IBillingAdjustmentService, BillingAdjustmentService>();
builder.Services.AddScoped<IAdvancePaymentService, AdvancePaymentService>();
builder.Services.AddScoped<IPaymentCheckService, PaymentCheckService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IReadingService, ReadingService>();
builder.Services.AddScoped<IReadingSheetService, ReadingSheetService>();
builder.Services.AddScoped<IZoneBookService, ZoneBookService>();

    builder.Services.AddScoped<IZoneBookService, ZoneBookService>();
    builder.Services.AddScoped<IReadingSheetTemplateService, ReadingSheetTemplateService>();
    builder.Services.AddScoped<IChargeTypeService, ChargeTypeService>();
    builder.Services.AddScoped<IPenaltyService, PenaltyService>();
    builder.Services.AddScoped<IOtherChargeService, OtherChargeService>();
    builder.Services.AddScoped<IBillingAdjustmentService, BillingAdjustmentService>();
    builder.Services.AddScoped<IAdvancePaymentService, AdvancePaymentService>();
    builder.Services.AddScoped<IPaymentCheckService, PaymentCheckService>();
    builder.Services.AddScoped<IPaymentService, PaymentService>();

    builder.Services.AddEndpointsApiExplorer();
    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
