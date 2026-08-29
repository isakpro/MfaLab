using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MfaLab.Data;

namespace MfaLab.Tests;

/// <summary>
/// Steg 8: tio recovery codes, lagrade hashade, var och en giltig exakt en gång.
/// </summary>
public sealed class RecoveryCodeTests(MfaLabAppFactory factory) : IClassFixture<MfaLabAppFactory>
{
    private const int AntalKoder = 10;

    [Fact]
    public async Task Tio_koder_genereras()
    {
        await factory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);

            var koder = (await scope.UserManager
                .GenerateNewTwoFactorRecoveryCodesAsync(user, AntalKoder))!.ToArray();

            Assert.Equal(AntalKoder, koder.Length);
            Assert.Equal(AntalKoder, koder.Distinct().Count());
            Assert.Equal(AntalKoder, await scope.UserManager.CountRecoveryCodesAsync(user));
        });
    }

    [Fact]
    public async Task En_kod_fungerar_exakt_en_gang()
    {
        await factory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);
            var koder = (await scope.UserManager
                .GenerateNewTwoFactorRecoveryCodesAsync(user, AntalKoder))!.ToArray();

            var forsta = await scope.UserManager.RedeemTwoFactorRecoveryCodeAsync(user, koder[0]);
            Assert.True(forsta.Succeeded, "Koden godkändes inte ens första gången.");
            Assert.Equal(AntalKoder - 1, await scope.UserManager.CountRecoveryCodesAsync(user));

            var andra = await scope.UserManager.RedeemTwoFactorRecoveryCodeAsync(user, koder[0]);
            Assert.False(andra.Succeeded, "Samma kod gick att använda en andra gång.");
            Assert.Equal(AntalKoder - 1, await scope.UserManager.CountRecoveryCodesAsync(user));

            // De övriga koderna ska vara orörda av att en av dem förbrukats.
            var nasta = await scope.UserManager.RedeemTwoFactorRecoveryCodeAsync(user, koder[1]);
            Assert.True(nasta.Succeeded);
            Assert.Equal(AntalKoder - 2, await scope.UserManager.CountRecoveryCodesAsync(user));
        });
    }

    [Fact]
    public async Task Appen_anvander_storen_som_hashar_koderna()
    {
        await factory.InScopeAsync(scope =>
        {
            // Om registreringen i Program.cs faller bort hamnar koderna i
            // klartext igen utan att något annat test märker det.
            var store = scope.Services.GetRequiredService<IUserStore<ApplicationUser>>();
            Assert.IsType<HashedRecoveryCodeUserStore>(store);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Koden_fungerar_aven_med_gemener_och_mellanslag()
    {
        await factory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);
            var koder = (await scope.UserManager
                .GenerateNewTwoFactorRecoveryCodesAsync(user, AntalKoder))!.ToArray();

            var slarvigtInskriven = $" {koder[0].ToLowerInvariant()} ";

            var resultat = await scope.UserManager
                .RedeemTwoFactorRecoveryCodeAsync(user, slarvigtInskriven);

            Assert.True(resultat.Succeeded);
        });
    }

    [Fact]
    public async Task Koderna_lagras_inte_i_klartext()
    {
        await factory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);
            var koder = (await scope.UserManager
                .GenerateNewTwoFactorRecoveryCodesAsync(user, AntalKoder))!.ToArray();

            var lagrat = await scope.Db.UserTokens
                .Where(t => t.UserId == user.Id)
                .Select(t => t.Value)
                .ToListAsync();

            Assert.NotEmpty(lagrat);

            var allaVarden = string.Join('\n', lagrat);
            foreach (var kod in koder)
            {
                Assert.DoesNotContain(kod, allaVarden, StringComparison.OrdinalIgnoreCase);
            }
        });
    }
}
