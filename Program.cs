using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MfaLab.Components;
using MfaLab.Components.Account;
using MfaLab.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Avstängt i övningen så att en ny eller sådd användare kan logga in
        // direkt. E-postutskicket är en no-op här, det finns ingen brevlåda
        // att bekräfta i.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

        // TODO steg 7: aktivera kontolåsning här. Samma options-objekt som i
        // uppgiftstexten, även om vi registrerar Identity med AddIdentityCore
        // i stället för AddIdentity.
        // options.Lockout.MaxFailedAccessAttempts = 5;
        // options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        // options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    // Steg 2: AddDefaultTokenProviders registrerar AuthenticatorTokenProvider, den
    // som räknar fram och validerar de sexsiffriga TOTP-koderna (RFC 6238, HMAC-SHA1,
    // 30 sekunders tidsfönster). Utan den fungerar varken GetAuthenticatorKeyAsync,
    // VerifyTwoFactorTokenAsync eller inloggningen via /Account/LoginWith2fa.
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// Skapa databasen och så en testanvändare så att du kan logga in direkt och
// börja med TOTP-uppsättningen. Inloggning: test@minapp.se / Sommar2024!
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    const string epost = "test@minapp.se";
    if (await userManager.FindByEmailAsync(epost) is null)
    {
        var user = new ApplicationUser { UserName = epost, Email = epost, EmailConfirmed = true };
        await userManager.CreateAsync(user, "Sommar2024!");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
