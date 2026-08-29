using Microsoft.AspNetCore.Identity;
using MfaLab.Data;

namespace MfaLab.Tests;

/// <summary>
/// Steg 2 till 5: att TOTP verkligen fungerar mot en autentiseringsapp.
/// </summary>
public sealed class TotpTests(MfaLabAppFactory factory) : IClassFixture<MfaLabAppFactory>
{
    [Fact]
    public async Task Anvandaren_far_en_base32_nyckel_som_gar_att_lasa_in_i_appen()
    {
        await factory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);

            // Samma anrop som EnableAuthenticator.razor gör innan QR-koden ritas.
            var nyckel = await scope.UserManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(nyckel))
            {
                await scope.UserManager.ResetAuthenticatorKeyAsync(user);
                nyckel = await scope.UserManager.GetAuthenticatorKeyAsync(user);
            }

            Assert.False(string.IsNullOrEmpty(nyckel));
            Assert.All(nyckel!, tecken => Assert.Contains(tecken, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
            Assert.True(nyckel!.Length >= 16, $"Nyckeln var bara {nyckel.Length} tecken.");
        });
    }

    [Fact]
    public async Task En_kod_raknad_ur_hemligheten_godkanns_och_slar_pa_tvafaktor()
    {
        await factory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);
            var nyckel = await HamtaNyckelAsync(scope.UserManager, user);

            var kod = Totp.Kod(nyckel, Totp.AktuelltTidsfonster(DateTimeOffset.UtcNow));

            var giltig = await scope.UserManager.VerifyTwoFactorTokenAsync(
                user, scope.UserManager.Options.Tokens.AuthenticatorTokenProvider, kod);

            Assert.True(giltig, "Identity underkände en kod som räknats fram enligt RFC 6238.");

            await scope.UserManager.SetTwoFactorEnabledAsync(user, true);
            Assert.True(await scope.UserManager.GetTwoFactorEnabledAsync(user));
        });
    }

    [Fact]
    public async Task Fel_kod_underkanns()
    {
        await factory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);
            var nyckel = await HamtaNyckelAsync(scope.UserManager, user);

            var riktigKod = Totp.Kod(nyckel, Totp.AktuelltTidsfonster(DateTimeOffset.UtcNow));
            var felKod = (int.Parse(riktigKod) + 1) % 1_000_000;

            var giltig = await scope.UserManager.VerifyTwoFactorTokenAsync(
                user, scope.UserManager.Options.Tokens.AuthenticatorTokenProvider, felKod.ToString("D6"));

            Assert.False(giltig);
        });
    }

    [Fact]
    public async Task En_kod_fran_ett_passerat_tidsfonster_underkanns()
    {
        await factory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);
            var nyckel = await HamtaNyckelAsync(scope.UserManager, user);

            // Identity accepterar plus/minus två tidsfönster för klockglapp.
            // Tio fönster bakåt är fem minuter gammalt och ska vara dött, annars
            // hade en avlyssnad kod gått att spela upp långt efteråt.
            var gammalKod = Totp.Kod(nyckel, Totp.AktuelltTidsfonster(DateTimeOffset.UtcNow) - 10);

            var giltig = await scope.UserManager.VerifyTwoFactorTokenAsync(
                user, scope.UserManager.Options.Tokens.AuthenticatorTokenProvider, gammalKod);

            Assert.False(giltig, "En fem minuter gammal kod godkändes, tidsfönstret är för brett.");
        });
    }

    private static async Task<string> HamtaNyckelAsync(
        UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        await userManager.ResetAuthenticatorKeyAsync(user);
        return (await userManager.GetAuthenticatorKeyAsync(user))!;
    }
}
