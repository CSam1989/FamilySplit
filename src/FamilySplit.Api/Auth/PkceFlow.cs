using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace FamilySplit.Api.Auth;

/// <summary>
/// Holds the per-login PKCE state. Lives inside an encrypted, HttpOnly,
/// SameSite=Lax cookie for ~10 minutes during the round-trip to Google.
/// </summary>
public record OAuthFlowState(string State, string CodeVerifier, string ReturnUrl);

/// <summary>
/// Generates PKCE values and serialises <see cref="OAuthFlowState"/> into an
/// encrypted cookie payload using ASP.NET Core Data Protection. The verifier
/// never appears in a URL or in the browser's JS-accessible storage.
/// </summary>
public class PkceFlow
{
    private readonly IDataProtector _protector;

    public PkceFlow(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("FamilySplit.OAuth.v1");
    }

    public OAuthFlowState NewFlow(string returnUrl) => new(
        State: RandomBase64Url(32),
        CodeVerifier: RandomBase64Url(32),
        ReturnUrl: returnUrl);

    public string DeriveCodeChallenge(string codeVerifier)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier), hash);
        return Base64UrlEncode(hash);
    }

    public string Protect(OAuthFlowState state)
    {
        var payload = $"{state.State}|{state.CodeVerifier}|{state.ReturnUrl}";
        return _protector.Protect(payload);
    }

    public OAuthFlowState? Unprotect(string? protectedPayload)
    {
        if (string.IsNullOrWhiteSpace(protectedPayload)) return null;
        try
        {
            var plain = _protector.Unprotect(protectedPayload);
            var parts = plain.Split('|', 3);
            if (parts.Length != 3) return null;
            return new OAuthFlowState(parts[0], parts[1], parts[2]);
        }
        catch (CryptographicException)
        {
            return null; // cookie was tampered with, swapped, or signed by a previous key set
        }
    }

    private static string RandomBase64Url(int byteLength)
    {
        Span<byte> bytes = stackalloc byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
