using System.Security.Cryptography;

namespace FamilySplit.Features.Groups.Shared;

/// <summary>
/// Pure generator for group invite codes: a cryptographically-random 8-character code
/// (≈40 bits) from an alphabet that omits visually ambiguous glyphs (no 0/O/1/I). Uniqueness
/// against the <c>groups.invite_code</c> unique index is enforced separately by the data gateway.
/// </summary>
public static class InviteCodeGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I

    public static string NewCode() =>
        string.Create(8, Chars, static (span, alphabet) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        });
}
