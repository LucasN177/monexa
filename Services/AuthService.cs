using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using static Supabase.Gotrue.Constants;

namespace monexa.Services;

/// <summary>
/// Anmeldung über Supabase Auth. Ohne Supabase-Konfiguration läuft die App im Demo-Modus
/// mit einem lokalen Nutzer, der immer angemeldet ist.
/// </summary>
public class AuthService
{
    private readonly Supabase.Client? _client;
    private readonly PreferencesService _preferences;

    public event Action? AuthStateChanged;

    public AuthService(PreferencesService preferences, Supabase.Client? client = null)
    {
        _preferences = preferences;
        _client = client;
        _client?.Auth.AddStateChangedListener((_, _) => AuthStateChanged?.Invoke());
    }

    /// <summary>True, wenn die App mit Supabase verbunden ist (Cloud-Modus).</summary>
    public bool IsCloud => _client != null;

    public bool IsAuthenticated => !IsCloud || _client!.Auth.CurrentSession?.User != null;

    public Guid? UserId =>
        Guid.TryParse(_client?.Auth.CurrentUser?.Id, out var id) ? id : null;

    public string Email => _client?.Auth.CurrentUser?.Email ?? "demo@monexa.local";

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(_preferences.DisplayName) ? _preferences.DisplayName
        : IsCloud ? Email.Split('@')[0]
        : "Demo-Nutzer";

    public DateTime? LastSignIn => _client?.Auth.CurrentUser?.LastSignInAt;

    /// <summary>Übernimmt eine Session aus dem URL-Fragment (Magic-Link / E-Mail-Bestätigung).</summary>
    public async Task<bool> TryHandleRedirectAsync(Uri uri)
    {
        if (_client == null || !uri.Fragment.Contains("access_token")) return false;
        try
        {
            await _client.Auth.GetSessionFromUrl(uri);
            return true;
        }
        catch (GotrueException)
        {
            return false;
        }
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        if (_client == null) return AuthResult.Ok();
        try
        {
            var session = await _client.Auth.SignIn(email, password);
            return session?.User != null ? AuthResult.Ok() : AuthResult.Fail("Anmeldung fehlgeschlagen.");
        }
        catch (GotrueException ex)
        {
            return AuthResult.Fail(Translate(ex));
        }
    }

    public async Task<AuthResult> SignUpAsync(string email, string password, string redirectUrl)
    {
        if (_client == null) return AuthResult.Ok();
        try
        {
            var session = await _client.Auth.SignUp(email, password, new SignUpOptions { RedirectTo = redirectUrl });
            return session?.AccessToken != null
                ? AuthResult.Ok()
                : AuthResult.Ok("Bitte bestätige deine E-Mail-Adresse über den Link, den wir dir geschickt haben.");
        }
        catch (GotrueException ex)
        {
            return AuthResult.Fail(Translate(ex));
        }
    }

    public async Task<AuthResult> SendMagicLinkAsync(string email, string redirectUrl)
    {
        if (_client == null) return AuthResult.Ok();
        try
        {
            await _client.Auth.SendMagicLink(email, new SignInOptions { RedirectTo = redirectUrl });
            return AuthResult.Ok("Wir haben dir einen Anmeldelink per E-Mail geschickt.");
        }
        catch (GotrueException ex)
        {
            return AuthResult.Fail(Translate(ex));
        }
    }

    public async Task<AuthResult> ResetPasswordAsync(string email)
    {
        if (_client == null) return AuthResult.Ok();
        try
        {
            await _client.Auth.ResetPasswordForEmail(email);
            return AuthResult.Ok("Falls ein Konto existiert, wurde eine E-Mail zum Zurücksetzen verschickt.");
        }
        catch (GotrueException ex)
        {
            return AuthResult.Fail(Translate(ex));
        }
    }

    public async Task<AuthResult> ChangePasswordAsync(string newPassword)
    {
        if (_client == null) return AuthResult.Fail("Im Demo-Modus gibt es kein Passwort.");
        try
        {
            await _client.Auth.Update(new UserAttributes { Password = newPassword });
            return AuthResult.Ok("Passwort wurde geändert.");
        }
        catch (GotrueException ex)
        {
            return AuthResult.Fail(Translate(ex));
        }
    }

    public async Task SignOutAsync()
    {
        if (_client == null) return;
        try
        {
            await _client.Auth.SignOut();
        }
        catch (GotrueException)
        {
            // Session ist lokal trotzdem verworfen.
        }
        AuthStateChanged?.Invoke();
    }

    private static string Translate(GotrueException ex) => ex.Reason switch
    {
        FailureHint.Reason.UserBadLogin or FailureHint.Reason.UserBadPassword => "E-Mail oder Passwort ist falsch.",
        FailureHint.Reason.UserBadEmailAddress => "Ungültige E-Mail-Adresse.",
        FailureHint.Reason.UserEmailNotConfirmed => "Bitte bestätige zuerst deine E-Mail-Adresse.",
        FailureHint.Reason.UserAlreadyRegistered => "Für diese E-Mail-Adresse existiert bereits ein Konto.",
        FailureHint.Reason.UserTooManyRequests => "Zu viele Versuche – bitte warte kurz.",
        FailureHint.Reason.Offline => "Keine Verbindung zum Server.",
        _ => "Fehler: " + ex.Message
    };
}

public record AuthResult(bool Success, string? Message)
{
    public static AuthResult Ok(string? message = null) => new(true, message);
    public static AuthResult Fail(string message) => new(false, message);
}
