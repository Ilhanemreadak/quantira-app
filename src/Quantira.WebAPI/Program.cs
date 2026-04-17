using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Quantira.Application;
using Quantira.Infrastructure;
using Quantira.Infrastructure.AI;
using Quantira.Infrastructure.Persistence;
using Quantira.WebAPI.Configuration;
using Quantira.WebAPI.Controllers;
using Quantira.WebAPI.Hangfire;
using Quantira.WebAPI.Hubs;
using Quantira.WebAPI.Middleware;
using Scalar.AspNetCore;
using Serilog;
using System.Text;

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

    builder.Services
        .AddOptions<HangfireSettings>()
        .Bind(builder.Configuration.GetSection(HangfireSettings.SectionName))
        .Validate(settings => settings.WorkerCount > 0,
            "Hangfire worker count must be greater than zero.")
        .Validate(settings =>
                !settings.Dashboard.Enabled
                || !string.IsNullOrWhiteSpace(settings.Dashboard.Path),
            "Hangfire dashboard path is required when the dashboard is enabled.")
        .Validate(settings =>
                !settings.Dashboard.Enabled
                || settings.Dashboard.Path.StartsWith('/'),
            "Hangfire dashboard path must start with '/'.")
        .ValidateOnStart();

    builder.Services.AddSingleton<HangfireDashboardBasicAuthFilter>();

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
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Components ??= new OpenApiComponents();

            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Name = "Authorization"
                }
            };

            return Task.CompletedTask;
        });
    });

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
    var hangfireSettings = app.Services
        .GetRequiredService<IOptions<HangfireSettings>>()
        .Value;

    if (hangfireSettings.Dashboard.Enabled)
    {
        if (!hangfireSettings.Dashboard.HasCredentialsConfigured())
        {
            app.Logger.LogWarning(
                "Hangfire dashboard is enabled but credentials are missing. Configure Hangfire:Dashboard:Username and Hangfire:Dashboard:Password to enable it.");
        }
        else
        {
            app.UseHangfireDashboard(hangfireSettings.Dashboard.Path, new DashboardOptions
            {
                Authorization = [app.Services.GetRequiredService<HangfireDashboardBasicAuthFilter>()],
                IsReadOnlyFunc = _ => hangfireSettings.Dashboard.IsReadOnly,
                DashboardTitle = "Quantira Hangfire"
            });

            app.Logger.LogInformation(
                "Hangfire dashboard mapped at {Path}. ReadOnly: {ReadOnly}.",
                hangfireSettings.Dashboard.Path,
                hangfireSettings.Dashboard.IsReadOnly);
        }
    }

    app.MapControllers();
    app.MapHub<PriceHub>("/hubs/price");
    app.MapHealthChecks("/health");

    // ── Recurring jobs ───────────────────────────────────────────────
    // Call with full namespace to avoid ambiguous reference errors.
    var jobManager = app.Services.GetRequiredService<IRecurringJobManager>();
    Quantira.Infrastructure.DependencyInjection.RegisterRecurringJobs(jobManager);

    // ── Seed asset catalogue on first startup ────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider
            .GetRequiredService<QuantiraDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<Program>>();

        await Quantira.Infrastructure.Persistence.Seed.AssetSeeder
            .SeedAsync(context, logger);
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Quantira API failed to start.");
}
finally
{
    await Log.CloseAndFlushAsync();
}