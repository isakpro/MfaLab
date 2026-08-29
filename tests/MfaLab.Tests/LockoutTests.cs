using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MfaLab.Tests;

/// <summary>
/// Steg 7: kontolåsning, verifierad både före och efter att låsningen släpper.
/// </summary>
public sealed class LockoutTests : IDisposable
{
    // Den riktiga appen, med policyn som står i Program.cs.
    private readonly MfaLabAppFactory skarpFactory = new();

    // Samma app, men med låsningstiden nedskruvad till två sekunder. Enda sättet
    // att på riktigt testa att låsningen släpper när tiden gått ut utan att låta
    // testsviten stå still i femton minuter. Antalet försök är oförändrat.
    private readonly MfaLabAppFactory kortLasningFactory = MfaLabAppFactory.Med(services =>
        services.Configure<IdentityOptions>(o => o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromSeconds(2)));

    [Fact]
    public void Appen_ar_konfigurerad_med_fem_forsok_och_femton_minuter()
    {
        var lockout = skarpFactory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout;

        Assert.Equal(5, lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), lockout.DefaultLockoutTimeSpan);
        Assert.True(lockout.AllowedForNewUsers);
    }

    [Fact]
    public async Task Femte_felaktiga_forsoket_laser_kontot()
    {
        await skarpFactory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);

            for (var forsok = 1; forsok <= 4; forsok++)
            {
                var resultat = await scope.SignInManager.CheckPasswordSignInAsync(
                    user, TestData.FelLosenord, lockoutOnFailure: true);

                Assert.False(resultat.Succeeded);
                Assert.False(resultat.IsLockedOut, $"Kontot låstes redan vid försök {forsok}.");
            }

            var femte = await scope.SignInManager.CheckPasswordSignInAsync(
                user, TestData.FelLosenord, lockoutOnFailure: true);

            Assert.True(femte.IsLockedOut, "Femte felförsöket låste inte kontot.");
            Assert.True(await scope.UserManager.IsLockedOutAsync(user));
        });
    }

    [Fact]
    public async Task Last_konto_nekas_aven_med_ratt_losenord()
    {
        await skarpFactory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);
            await LasKontotAsync(scope, user);

            var medRattLosenord = await scope.SignInManager.CheckPasswordSignInAsync(
                user, TestData.Losenord, lockoutOnFailure: true);

            Assert.False(medRattLosenord.Succeeded);
            Assert.True(medRattLosenord.IsLockedOut);
        });
    }

    [Fact]
    public async Task Lasningen_slapper_nar_tiden_gatt_ut()
    {
        await kortLasningFactory.InScopeAsync(async scope =>
        {
            var user = await MfaLabAppFactory.SkapaAnvandareAsync(scope.UserManager);
            await LasKontotAsync(scope, user);

            Assert.True(await scope.UserManager.IsLockedOutAsync(user));

            // Vänta ut låsningen på riktigt, med riktig klocka.
            await Task.Delay(TimeSpan.FromSeconds(2.5));

            Assert.False(await scope.UserManager.IsLockedOutAsync(user));

            var efterat = await scope.SignInManager.CheckPasswordSignInAsync(
                user, TestData.Losenord, lockoutOnFailure: true);

            Assert.True(efterat.Succeeded, "Rätt lösenord borde fungera när låsningen släppt.");
            Assert.Equal(0, await scope.UserManager.GetAccessFailedCountAsync(user));
        });
    }

    private static async Task LasKontotAsync(TestScope scope, Data.ApplicationUser user)
    {
        var max = scope.UserManager.Options.Lockout.MaxFailedAccessAttempts;

        for (var forsok = 0; forsok < max; forsok++)
        {
            await scope.SignInManager.CheckPasswordSignInAsync(
                user, TestData.FelLosenord, lockoutOnFailure: true);
        }
    }

    public void Dispose()
    {
        skarpFactory.Dispose();
        kortLasningFactory.Dispose();
    }
}
