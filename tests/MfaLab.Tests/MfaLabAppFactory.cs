using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MfaLab.Data;

namespace MfaLab.Tests;

/// <summary>
/// Startar den riktiga appen ur Program.cs, men mot en egen SQLite-fil per
/// testklass. Poängen är att testerna ska se exakt den Identity-konfiguration
/// som applikationen kör med, inte en kopia av inställningarna som kan hamna
/// i otakt med Program.cs.
/// </summary>
public sealed class MfaLabAppFactory : WebApplicationFactory<Program>
{
    private readonly string dbPath =
        Path.Combine(Path.GetTempPath(), $"mfalab-tests-{Guid.NewGuid():N}.db");

    private readonly Action<IServiceCollection>? configureTestServices;

    // xUnit kräver att en IClassFixture har exakt en publik, parameterlös
    // konstruktor. Varianten med extra tjänstekonfiguration görs därför via
    // fabriksmetoden Med nedan i stället för via en andra konstruktor.
    public MfaLabAppFactory() { }

    private MfaLabAppFactory(Action<IServiceCollection> configureTestServices)
        => this.configureTestServices = configureTestServices;

    public static MfaLabAppFactory Med(Action<IServiceCollection> configureTestServices)
        => new(configureTestServices);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"DataSource={dbPath}");

        if (configureTestServices is not null)
        {
            builder.ConfigureTestServices(configureTestServices);
        }
    }

    /// <summary>
    /// Kör en testkropp i en egen DI-scope, på samma sätt som en request gör.
    /// </summary>
    public async Task InScopeAsync(Func<TestScope, Task> body)
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        var signInManager = sp.GetRequiredService<SignInManager<ApplicationUser>>();

        // SignInManager hämtar normalt sin HttpContext ur IHttpContextAccessor.
        // Utanför en request finns ingen, så vi sätter en tom med scopets
        // tjänster. Testerna nedan använder CheckPasswordSignInAsync, som inte
        // rör kakor, men utan Context kastar SignInManager om något gör det.
        signInManager.Context = new DefaultHttpContext { RequestServices = sp };

        await body(new TestScope(
            sp,
            sp.GetRequiredService<UserManager<ApplicationUser>>(),
            signInManager,
            sp.GetRequiredService<ApplicationDbContext>()));
    }

    /// <summary>Skapar en ny, bekräftad testanvändare med unik e-post.</summary>
    public static async Task<ApplicationUser> SkapaAnvandareAsync(
        UserManager<ApplicationUser> userManager, string losenord = TestData.Losenord)
    {
        var epost = $"test-{Guid.NewGuid():N}@minapp.se";
        var user = new ApplicationUser { UserName = epost, Email = epost, EmailConfirmed = true };

        var result = await userManager.CreateAsync(user, losenord);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        return user;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            try { File.Delete(dbPath); } catch (IOException) { /* filen är låst, städas av OS */ }
        }
    }
}

public sealed record TestScope(
    IServiceProvider Services,
    UserManager<ApplicationUser> UserManager,
    SignInManager<ApplicationUser> SignInManager,
    ApplicationDbContext Db);

public static class TestData
{
    public const string Losenord = "Sommar2024!";
    public const string FelLosenord = "Fel-losenord-1!";
}
