using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PolicyCompliance.Core.Agents.FraudAnomalyRiskAgent;
using PolicyCompliance.Core.Services;
using PolicyCompliance.Core.Tools;
using PolicyCompliance.Core.Validators;
using PolicyCompliance.Infrastructure.Data;
using PolicyCompliance.Infrastructure.Seed;
using PolicyCompliance.Infrastructure.Services;
using PolicyCompliance.Infrastructure.Tools;

var builder = WebApplication.CreateBuilder(args);

// 1. Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

// 2. Core & Infrastructure Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPolicyManagementService, PolicyManagementService>();
builder.Services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
builder.Services.AddScoped<IPolicyValidationEngine, PolicyValidationEngine>();
builder.Services.AddScoped<IComplianceAgentTools, ComplianceAgentTools>();
builder.Services.AddScoped<IFraudAnomalyRiskAgent, FraudAnomalyRiskAgent>();
builder.Services.AddScoped<IPolicyComplianceWorkflowService, PolicyComplianceWorkflowService>();

// 3. JWT Authentication & Authorization
var jwtKey = builder.Configuration["Jwt:Key"] ?? "CorporateSpendAndComplianceSuperSecretKey2026!#$SecureKey";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PolicyComplianceApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PolicyComplianceClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 4. CORS for React & Flutter
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebAndMobile", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Corporate Spend Management - Policy & Compliance API",
        Version = "v1",
        Description = "Production API for Expense Policy Management, Deterministic Validation, Fraud/Anomaly-Risk Agent Subsystem, and Manager Review Queue."
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and your token.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

var app = builder.Build();

// Auto-migrate & seed database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating or seeding the database.");
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowWebAndMobile");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
