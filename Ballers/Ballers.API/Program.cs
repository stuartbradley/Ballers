using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading.RateLimiting;
using Serilog.Events;

// Create wwwroot + uploads/teams BEFORE CreateBuilder so WebRootFileProvider doesn't throw
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "teams"));

var builder = WebApplication.CreateBuilder(args);

var logDir = Path.Combine(AppContext.BaseDirectory, "logs");

builder.Host.UseSerilog((_, lc) => lc
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.File(
        Path.Combine(logDir, "debug-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        restrictedToMinimumLevel: LogEventLevel.Debug,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(logDir, "error-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        restrictedToMinimumLevel: LogEventLevel.Error,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

// Add services to the container.

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Baller.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
    options.LoginPath = "/api/auth/login";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);

    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = 401;
        return Task.CompletedTask;
    };

    // Without this, a signed-in user who lacks the role is redirected to the
    // default access-denied page, which does not exist here — so the caller sees
    // a misleading 404 instead of "you are not allowed". Matches the 401 rewrite
    // above: an API answers with a status code, not a redirect.
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

builder.Services.AddHttpClient("Anthropic");
builder.Services.AddSingleton<ImageModerationService>();
builder.Services.AddScoped<IFairplayService, FairplayService>();
builder.Services.AddScoped<IPenaltyService, PenaltyService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IAdminOverviewService, AdminOverviewService>();
builder.Services.AddScoped<IManagerDashboardService, ManagerDashboardService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IFixtureService, FixtureService>();
builder.Services.AddScoped<ISeasonService, SeasonService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IKnockoutService, KnockoutService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILeagueSettingsService, LeagueSettingsService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITotwService, TotwService>();
builder.Services.AddScoped<IMotdService, MotdService>();
builder.Services.AddScoped<IGalleryStorageService, GalleryStorageService>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationChecker, NotificationChecker>();
builder.Services.AddHostedService<Ballers.API.BackgroundServices.NotificationBackgroundService>();
builder.Services.AddScoped<IPlayerCardService, PlayerCardService>();
builder.Services.AddScoped<IPlayerPointsService, PlayerPointsService>();
builder.Services.AddScoped<IPlayerAccountService, PlayerAccountService>();
builder.Services.AddHostedService<Ballers.API.BackgroundServices.PlayerPointsBackgroundService>();

// Player sign-up is the only endpoint on the site that writes without a login,
// so it gets a cap. Partitioned per caller rather than one bucket for everyone:
// a single league-wide limit would lock out a whole squad signing up together on
// the first night of a season. A tripped limit answers 429 rather than queueing,
// so a flood cannot tie up request threads.
// Configurable so the end-to-end suite, which legitimately signs up a dozen
// players from one address in a single run, can raise it without weakening the
// live limit.
var signupPermitLimit = builder.Configuration.GetValue<int?>("RateLimits:PlayerSignupPerWindow") ?? 5;
var signupWindowMinutes = builder.Configuration.GetValue<int?>("RateLimits:PlayerSignupWindowMinutes") ?? 15;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("player-signup", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = signupPermitLimit,
                Window = TimeSpan.FromMinutes(signupWindowMinutes),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUI",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    opts.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseHttpsRedirection();

app.UseRouting();

// CORS must run before the static file middleware, otherwise uploaded team
// badges under /uploads are served without an Access-Control-Allow-Origin
// header. The UI can still display them, but the team sheet export fetches
// each image to inline it as a data URI, and that fetch is blocked.
app.UseCors("AllowUI");

var webRoot = app.Environment.WebRootPath
    ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot),
    RequestPath = ""
});

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.Seed(scope.ServiceProvider);

    if (app.Configuration.GetValue<bool>("Seeding:EnableFakeData"))
        await DevSeeder.SeedAsync(scope.ServiceProvider);
}

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
