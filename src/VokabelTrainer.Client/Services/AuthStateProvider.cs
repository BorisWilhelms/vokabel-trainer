namespace VokabelTrainer.Client.Services;

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using VokabelTrainer.Shared.Dtos.Auth;
using VokabelTrainer.Shared.Models;

public class AuthStateProvider : AuthenticationStateProvider
{
    private AuthResponse? _currentUser;

    public AuthResponse? CurrentUser => _currentUser;

    public void SetUser(AuthResponse? user)
    {
        _currentUser = user;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsPrincipal principal;
        if (_currentUser is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, _currentUser.Username),
                new(ClaimTypes.Role, _currentUser.Role.ToString())
            };
            principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
        }
        else
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity());
        }
        return Task.FromResult(new AuthenticationState(principal));
    }

    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;
}
