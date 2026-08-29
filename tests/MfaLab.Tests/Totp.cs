using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace MfaLab.Tests;

/// <summary>
/// Räknar fram TOTP-koder enligt RFC 6238 på exakt samma sätt som Microsoft
/// Authenticator och Google Authenticator gör: base32-avkoda den delade
/// hemligheten, HMAC-SHA1 över tidsfönstret och dynamisk trunkering till sex
/// siffror. Att Identity godkänner en kod som räknats fram här är därför samma
/// sak som att en autentiseringsapp i telefonen hade fungerat.
/// </summary>
public static class Totp
{
    private const int TidsfonsterSekunder = 30;
    private const string Base32Alfabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static long AktuelltTidsfonster(DateTimeOffset nu)
        => nu.ToUnixTimeSeconds() / TidsfonsterSekunder;

    public static string Kod(string base32Hemlighet, long tidsfonster)
    {
        var nyckel = Base32Avkoda(base32Hemlighet);

        Span<byte> raknare = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(raknare, tidsfonster);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(nyckel, raknare, hash);

        // Dynamisk trunkering, RFC 4226 avsnitt 5.4.
        var offset = hash[^1] & 0x0f;
        var binar = ((hash[offset] & 0x7f) << 24)
                  | ((hash[offset + 1] & 0xff) << 16)
                  | ((hash[offset + 2] & 0xff) << 8)
                  | (hash[offset + 3] & 0xff);

        return (binar % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Avkoda(string indata)
    {
        var rensad = indata.Replace(" ", string.Empty).Replace("-", string.Empty)
                           .TrimEnd('=').ToUpperInvariant();

        var bitar = 0;
        var buffert = 0;
        var utdata = new List<byte>(rensad.Length * 5 / 8);

        foreach (var tecken in rensad)
        {
            var varde = Base32Alfabet.IndexOf(tecken);
            if (varde < 0)
            {
                throw new FormatException($"'{tecken}' är inte ett giltigt base32-tecken.");
            }

            buffert = (buffert << 5) | varde;
            bitar += 5;

            if (bitar >= 8)
            {
                bitar -= 8;
                utdata.Add((byte)(buffert >> bitar));
            }
        }

        return [.. utdata];
    }
}
