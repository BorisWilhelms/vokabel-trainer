namespace VokabelTrainer.Client.Services;

using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using VokabelTrainer.Shared.Dtos.Auth;
using VokabelTrainer.Shared.Models;

public class AuthStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private AuthResponse? _currentUser;
    private bool _initialized;

    public AuthResponse? CurrentUser => _currentUser;

    public void SetUser(AuthResponse? user)
    {
        _currentUser = user;
        _initialized = true;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;
            try
            {
                var response = await http.GetAsync("api/auth/me");
                if (response.IsSuccessStatusCode)
                {
                    _currentUser = await response.Content.ReadFromJsonAsync<AuthResponse>();
                }
            }
            catch
            {
                // Not authenticated or server unreachable
            }
        }

        ClaimsPrincipal principal;
        if (_currentUser is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, _currentUser.UserId.ToString()),
                new(ClaimTypes.Name, _currentUser.Username),
                new(ClaimTypes.Role, _currentUser.Role.ToString())
            };
            principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
        }
        else
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity());
        }
        return new AuthenticationState(principal);
    }

    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;
}
