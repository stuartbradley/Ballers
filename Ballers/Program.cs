
using Ballers;
using Ballers.Auth;
using Ballers.Client.Auth;
using Ballers.Infrastructure;
using Ballers.Services;
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
builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<LeagueService>();
builder.Services.AddScoped<StatService>();
builder.Services.AddScoped<PenaltyService>();
builder.Services.AddScoped<FairplayService>();
builder.Services.AddScoped<TeamService>();
await builder.Build().RunAsync();
