using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace MfaLab.Data;

/// <summary>
/// Steg 8: recovery codes ska ligga hashade i databasen.
///
/// Identitys inbyggda EF-store lägger recovery codes i klartext, som en enda
/// semikolonseparerad sträng i AspNetUserTokens. Den som kommer åt en
/// databasdump får alltså tio färdiga andrafaktorer per användare, och kan gå
/// förbi MFA helt med bara ett lösenord. Den här storen sparar i stället en
/// hash per kod och jämför vid inlösen.
///
/// Hashningen görs med Identitys egen <see cref="IPasswordHasher{TUser}"/>,
/// alltså PBKDF2 med salt per kod. En recovery code är bara tio tecken ur ett
/// 26-teckens alfabet, knappt 47 bitars entropi, vilket en GPU betar av på
/// timmar mot en rak SHA-256. Nyckelhärledningens iterationer är det som gör
/// den attacken ohållbar. Priset är att en inlösen måste jämföra mot upp till
/// tio hashar, men det är en operation som sker sällan och som dessutom är
/// begränsad av kontolåsningen.
/// </summary>
public sealed class HashedRecoveryCodeUserStore(
    ApplicationDbContext context,
    IPasswordHasher<ApplicationUser> hasher,
    IdentityErrorDescriber? describer = null)
    : UserOnlyStore<ApplicationUser, ApplicationDbContext, string>(context, describer)
{
    // Samma token som basklassen använder, så att lagringsplatsen är oförändrad.
    private const string InternalLoginProvider = "[AspNetUserStore]";
    private const string RecoveryCodeTokenName = "RecoveryCodes";
    private const char Separator = ';';

    public override Task ReplaceCodesAsync(
        ApplicationUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(recoveryCodes);

        var hashar = recoveryCodes.Select(kod => hasher.HashPassword(user, Normalisera(kod)));

        return SetTokenAsync(
            user, InternalLoginProvider, RecoveryCodeTokenName, string.Join(Separator, hashar), cancellationToken);
    }

    public override async Task<bool> RedeemCodeAsync(
        ApplicationUser user, string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(code);

        var hashar = await LasHasharAsync(user, cancellationToken);
        var normaliserad = Normalisera(code);

        // Alla hashar jämförs även efter en träff, så att svarstiden inte
        // avslöjar vilken av koderna som stämde.
        var traff = -1;
        for (var i = 0; i < hashar.Count; i++)
        {
            var resultat = hasher.VerifyHashedPassword(user, hashar[i], normaliserad);
            if (resultat != PasswordVerificationResult.Failed && traff < 0)
            {
                traff = i;
            }
        }

        if (traff < 0)
        {
            return false;
        }

        // Koden är förbrukad i och med att den lösts in en gång.
        hashar.RemoveAt(traff);

        await SetTokenAsync(
            user, InternalLoginProvider, RecoveryCodeTokenName, string.Join(Separator, hashar), cancellationToken);

        return true;
    }

    public override async Task<int> CountCodesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(user);

        return (await LasHasharAsync(user, cancellationToken)).Count;
    }

    private async Task<List<string>> LasHasharAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var lagrat = await GetTokenAsync(
            user, InternalLoginProvider, RecoveryCodeTokenName, cancellationToken);

        return string.IsNullOrEmpty(lagrat)
            ? []
            : [.. lagrat.Split(Separator, StringSplitOptions.RemoveEmptyEntries)];
    }

    // Koderna genereras versala med ett bindestreck i mitten. Vi normaliserar
    // både vid lagring och vid inlösen, så att gemener och inklistrade
    // mellanslag inte gör en giltig kod obrukbar.
    private static string Normalisera(string kod)
        => kod.Replace(" ", string.Empty).Trim().ToUpperInvariant();
}
