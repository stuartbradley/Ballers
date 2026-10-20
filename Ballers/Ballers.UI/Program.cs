
using Ballers;
using Ballers.Auth;
using Ballers.Client.Auth;
using Ballers.Client.Services;
using Ballers.Infrastructure;
using Ballers.Services;
using Ballers.ViewModels;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CredentialsHandler>();

builder.Services.AddScoped<AuthenticationStateProvider,
    AuthStateProvider>();


var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7075/";

builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<CredentialsHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

builder.Services.AddScoped<FixtureService>();
builder.Services.AddScoped<PlayerService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<ManagerService>();
builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<LeagueService>();
builder.Services.AddScoped<StatService>();
builder.Services.AddScoped<PenaltyService>();
builder.Services.AddScoped<FairplayService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<GalleryService>();
builder.Services.AddScoped<RefereeService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<KnockoutService>();
builder.Services.AddScoped<TotwService>();
builder.Services.AddScoped<MotdService>();
builder.Services.AddScoped<LeagueSettingsService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<PlayerCardService>();
builder.Services.AddScoped<PlayerAccountService>();

// One view model per page instance, so each page starts from fresh state.
builder.Services.AddTransient<FixtureViewModel>();
builder.Services.AddTransient<ManagerHqViewModel>();
builder.Services.AddTransient<KnockoutViewModel>();
builder.Services.AddTransient<AdminSettingsViewModel>();
builder.Services.AddTransient<RefereesViewModel>();
builder.Services.AddTransient<PlayerSignupViewModel>();
builder.Services.AddTransient<TeamSheetViewModel>();
builder.Services.AddTransient<AdminCardsViewModel>();
builder.Services.AddTransient<MyTeamViewModel>();
builder.Services.AddTransient<TeamProfileViewModel>();
builder.Services.AddTransient<MotdAdminViewModel>();
builder.Services.AddTransient<HomeViewModel>();
builder.Services.AddTransient<AdminViewModel>();
builder.Services.AddTransient<AdminSquadsViewModel>();
builder.Services.AddTransient<FixturesViewModel>();
builder.Services.AddTransient<AdminAccountsViewModel>();
builder.Services.AddTransient<ManagerFixturesViewModel>();
builder.Services.AddTransient<TeamOfTheWeekViewModel>();
builder.Services.AddTransient<MySquadViewModel>();
builder.Services.AddTransient<AdminSeasonViewModel>();
builder.Services.AddTransient<AdminGalleriesViewModel>();
builder.Services.AddTransient<MyCardViewModel>();
builder.Services.AddTransient<StatsViewModel>();
builder.Services.AddTransient<GalleryViewModel>();
await builder.Build().RunAsync();
