using System.Net.Http.Json;
using Ballers.Client.Models;
using Ballers.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Ballers.Client.Auth;

public class AuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http)
    {
        _http = http;
    }

    public async Task<bool> Login(LoginRequest request)
    {

        var response = await _http.PostAsJsonAsync("api/auth/login", request);

        return response.IsSuccessStatusCode;
    }

    public async Task Logout()
    {
        await _http.PostAsync("api/auth/logout", null);
    }

    public async Task<UserInfo?> GetCurrentUser()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<UserInfo>("api/auth/me");
            return response;
        }
        catch
        {
            // network failure or not logged in
            return null;
        }
    }

}


