using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Portfolio.Web.Auth;

/// <summary>
/// Blazor authentication state backed by a JWT kept in (encrypted) browser localStorage.
/// The token is re-validated (signature + expiry) every time the state is requested.
/// </summary>
public class JwtAuthStateProvider(ProtectedLocalStorage storage, TokenService tokens, ILogger<JwtAuthStateProvider> logger)
    : AuthenticationStateProvider
{
    private const string StorageKey = "admin_jwt";
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private string? _cachedToken;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            if (_cachedToken is null)
            {
                var result = await storage.GetAsync<string>(StorageKey);
                _cachedToken = result.Success ? result.Value : null;
            }
        }
        catch (InvalidOperationException)
        {
            // JS interop is unavailable during prerendering: treat as anonymous for now.
            return Anonymous;
        }
        catch (Exception ex)
        {
            // Corrupted / undecryptable value (e.g. data-protection keys rotated).
            logger.LogDebug(ex, "Não foi possível ler o token armazenado.");
            await SafeDeleteAsync();
            return Anonymous;
        }

        var principal = tokens.ValidateToken(_cachedToken);
        if (principal is null)
        {
            if (_cachedToken is not null) await SafeDeleteAsync();
            return Anonymous;
        }

        return new AuthenticationState(principal);
    }

    /// <summary>Raw token, e.g. to call the protected REST API.</summary>
    public string? CurrentToken => _cachedToken;

    public async Task SignInAsync(string token)
    {
        await storage.SetAsync(StorageKey, token);
        _cachedToken = token;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        await SafeDeleteAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }

    private async Task SafeDeleteAsync()
    {
        _cachedToken = null;
        try { await storage.DeleteAsync(StorageKey); }
        catch (Exception ex) { logger.LogDebug(ex, "Falha ao remover token."); }
    }
}
