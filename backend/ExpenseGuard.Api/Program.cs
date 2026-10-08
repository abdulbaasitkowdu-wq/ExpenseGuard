using System.Text;
using System.Threading.RateLimiting;
using ExpenseGuard.Api.Auth;
using ExpenseGuard.Api.Data;
using ExpenseGuard.Api.Infrastructure;
using ExpenseGuard.Api.Models;
using ExpenseGuard.Api.Reimbursements;
using ExpenseGuard.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
builder.Configuration.AddEnvironmentVariables();

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext().WriteTo.Console());
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentEmployee, HeaderCurrentEmployee>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IPurchaseRequestIntakeCoordinator, PurchaseRequestIntakeCoordinator>();
builder.Services.AddScoped<IPurchaseRequestService, PurchaseRequestService>();
builder.Services.AddScoped<IWorkflowLedger, WorkflowLedger>();
builder.Services.AddScoped<IClaimIntakeCoordinator, ClaimIntakeCoordinator>();
builder.Services.AddHttpClient<IClaimReviewClient, LangGraphClaimReviewClient>(client =>
{
    var baseUrl = builder.Configuration["LangGraph:BaseUrl"] ?? "http://127.0.0.1:8088";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<IClaimService, ClaimService>();
builder.Services.AddScoped<IRequestHistoryService, RequestHistoryService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPolicyService, PolicyService>();
builder.Services.AddScoped<IFraudService, FraudService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
var cloudinaryReady = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME"))
    && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CLOUDINARY_UPLOAD_PRESET"));
var ocrReady = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCR_SPACE_API_KEY"));
if (builder.Environment.IsEnvironment("Testing") || !cloudinaryReady || !ocrReady)
{
    builder.Services.AddSingleton<IReceiptStorage, FakeReceiptStorage>();
    builder.Services.AddSingleton<IReceiptOcr, FakeReceiptOcr>();
}
else
{
    builder.Services.AddHttpClient<IReceiptStorage, CloudinaryReceiptStorage>();
    builder.Services.AddHttpClient<IReceiptOcr, OcrSpaceReceiptOcr>();
}
builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Set Jwt__SigningKey to an environment-only secret of at least 32 bytes.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanApprove", p => p.RequireRole(RoleNames.Manager, RoleNames.DepartmentHead, RoleNames.Finance, RoleNames.Admin));
    options.AddPolicy("FinanceOnly", p => p.RequireRole(RoleNames.Finance, RoleNames.Admin));
    options.AddPolicy("AdminOnly", p => p.RequireRole(RoleNames.Admin));
    options.AddPolicy("OwnReimbursement", p => p.AddRequirements(new OwnsReimbursementRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, OwnsReimbursementHandler>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IReimbursementService, ReimbursementService>();
builder.Services.AddScoped<IBudgetGateway, BudgetServiceGateway>();
builder.Services.AddSingleton<IPaymentProvider, DeterministicPaymentProvider>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options => options.AddPolicy("Clients", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
            }));
});
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DemoDataSeeder.SeedAsync(db).GetAwaiter().GetResult();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
}
app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseCors("Clients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;
