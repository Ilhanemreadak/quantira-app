using System.Text;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Quantira.Application;
using Quantira.Infrastructure;
using Quantira.Infrastructure.AI;
using Quantira.Infrastructure.Persistence;
using Quantira.WebAPI.Controllers;
using Quantira.WebAPI.Hubs;
using Quantira.WebAPI.Middleware;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ──────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(
            outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

    // ── Application layers ───────────────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddInfrastructureAI(builder.Configuration);

    // ── ASP.NET Core Identity ────────────────────────────────────────
    builder.Services
        .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<QuantiraDbContext>()
        .AddDefaultTokenProviders();

    // ── JWT Authentication ───────────────────────────────────────────
    var jwtSection = builder.Configuration.GetSection("Jwt");
    builder.Services.Configure<JwtOptions>(jwtSection);

    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException(
            "JWT Secret is missing. Run: dotnet user-secrets set \"Jwt:Secret\" \"...\"");

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var token = ctx.Request.Query["access_token"];
                    var path = ctx.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(token) &&
                        path.StartsWithSegments("/hubs"))
                        ctx.Token = token;

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // ── Controllers ──────────────────────────────────────────────────
    builder.Services.AddControllers();

    // ── OpenAPI (Scalar — .NET 10 native, instead of Swashbuckle) ────
    builder.Services.AddOpenApi();

    // ── SignalR ──────────────────────────────────────────────────────
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors =
            builder.Environment.IsDevelopment();
    });

    // ── CORS ─────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
        options.AddPolicy("QuantiraFrontend", policy =>
            policy
                .WithOrigins(
                    "http://localhost:5173",
                    "https://quantira.app")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

    // ── Health Checks ────────────────────────────────────────────────
    builder.Services
        .AddHealthChecks()
        .AddSqlServer(
            connectionString: builder.Configuration.GetConnectionString("SqlServer")!,
            name: "sql-server",
            tags: ["db", "sql"])
        .AddRedis(
            redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
            name: "redis",
            tags: ["cache"]);

    // ── Build ────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Middleware pipeline ──────────────────────────────────────────
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        // Scalar UI: http://localhost:PORT/scalar/v1
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "Quantira API";
            options.Theme = ScalarTheme.DeepSpace;
        });
    }

    app.UseHttpsRedirection();
    app.UseCors("QuantiraFrontend");
    app.UseAuthentication();
    app.UseAuthorization();

    // ── Hangfire Dashboard ───────────────────────────────────────────
    app.UseHangfireDashboard("/jobs", new DashboardOptions
    {
        Authorization = [new HangfireAuthFilter()]
    });

    app.MapControllers();
    app.MapHub<PriceHub>("/hubs/price");
    app.MapHealthChecks("/health");

    // ── Recurring jobs ───────────────────────────────────────────────
    // Call with full namespace to avoid ambiguous reference errors.
    var jobManager = app.Services.GetRequiredService<IRecurringJobManager>();
    Quantira.Infrastructure.DependencyInjection.RegisterRecurringJobs(jobManager);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Quantira API failed to start.");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Restricts Hangfire dashboard access to authenticated users.
/// </summary>
public sealed class HangfireAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true;
    }
}