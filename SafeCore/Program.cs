using System.Text;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SafeCore.DeviceManagement.Domain;
using SafeCore.IAM.Application;
using SafeCore.IAM.Infrastructure;
using SafeCore.IdentityAccess.Domain;
using SafeCore.RiskDetection.Application;
using SafeCore.SensorDataIngestion.Application;
using SafeCore.SensorDataIngestion.Infrastructure;
using SafeCore.Shared.Data;
using SafeCore.Shared.Contracts;
using SafeCore.Locations.Application;
using SafeCore.MonitoringConfiguration.Application;
using SafeCore.DeviceManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Set ConnectionStrings__DefaultConnection before starting SafeCore.");
connectionString ??= "Server=localhost;Database=safecore_development;User ID=not-configured;Password=not-configured;";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        if (IPAddress.TryParse(proxy, out var address))
            options.KnownProxies.Add(address);
});
builder.Services.AddDbContext<SafeCoreDbContext>(options => options.UseMySQL(connectionString));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<ISensorReadingHistory, EfSensorReadingHistory>();
builder.Services.AddScoped<ISafetyAssessmentService, SafetyAssessmentService>();
builder.Services.AddScoped<SafetyConfigurationStore>();
builder.Services.AddScoped<CriticalAlertNotifier>();
builder.Services.AddScoped<LocationAccessService>();
builder.Services.AddHostedService<DeviceHeartbeatMonitor>();
builder.Services.AddOptions<SafetyThresholds>()
    .BindConfiguration(SafetyThresholds.SectionName)
    .Validate(options => options.TemperatureCelsius is > -55 and <= 125, "SafetyThresholds:TemperatureCelsius must be within the DS18B20 range.")
    .Validate(options => options.GasAdcRiseCounts is > 0 and <= 4095, "SafetyThresholds:GasAdcRiseCounts must be in the MQ-2 ADC range.")
    .Validate(options => options.AccelerationMagnitudeDeltaCounts is > 0 and <= 56755, "SafetyThresholds:AccelerationMagnitudeDeltaCounts must be in the MPU6050 raw-count range.")
    .Validate(options => options.ImpactAlertMilliseconds is >= 100 and <= 10000, "SafetyThresholds:ImpactAlertMilliseconds must be between 100 and 10000.")
    .ValidateOnStart();

builder.Services.AddIdentityCore<User>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<SafeCoreDbContext>()
    .AddSignInManager();

builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
    .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32, "Jwt:SigningKey must be at least 32 UTF-8 bytes.")
    .Validate(options => options.AccessTokenMinutes is >= 5 and <= 60, "Jwt:AccessTokenMinutes must be between 5 and 60.")
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Set Jwt__SigningKey to a cryptographically random secret of at least 32 UTF-8 bytes.");
var key = Encoding.UTF8.GetBytes(jwt.SigningKey);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("DeviceOrOperator", policy => policy.RequireRole("Device", "Administrator", "Operator"));
});
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IdentitySeeder>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<SafeCoreDbContext>();
    await migrationDb.Database.MigrateAsync();
    return;
}

await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
    }
    catch (Exception exception) when (app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning(exception, "IAM seeding was skipped because the development database is unavailable. Swagger remains available.");
    }
    if (!app.Environment.IsDevelopment())
        await scope.ServiceProvider.GetRequiredService<SafetyConfigurationStore>().GetAsync();
}
app.UseExceptionHandler();
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var principal = context.User;
    if (principal.Identity?.IsAuthenticated == true)
    {
        var userIdValue = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdValue, out var userId))
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<User>>();
            var account = await userManager.FindByIdAsync(userId.ToString());
            var stamp = principal.FindFirst("security_stamp")?.Value;
            if (account is null || account.Status != UserStatuses.Active || stamp != account.SecurityStamp)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            if (account.MustChangePassword
                && !context.Request.Path.StartsWithSegments("/api/iam/auth/change-password")
                && !context.Request.Path.StartsWithSegments("/api/iam/auth/me"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { title = "Password change required.", changePasswordEndpoint = "/api/iam/auth/change-password" });
                return;
            }
        }
    }
    await next();
});
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (SafeCoreDbContext dbContext, CancellationToken cancellationToken) =>
    await dbContext.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable)).AllowAnonymous();

app.Run();

public partial class Program;
