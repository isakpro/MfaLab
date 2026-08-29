# MfaLab

Lösning på **övning 5.3: MFA i .NET med TOTP, kontolåsning och recovery codes** i
kursen IT-säkerhet för utvecklare.

En Blazor Web App på .NET 10 med ASP.NET Core Identity och EF Core (SQLite), där
tvåfaktor med autentiseringsapp, kontolåsning och hashade recovery codes är på
plats och täckt av tester. Motiveringarna för valen finns i [REPORT.md](REPORT.md).

## Kom igång

### Förutsättningar

- .NET 10 SDK
- En autentiseringsapp i mobilen, till exempel Microsoft Authenticator eller Google Authenticator

### Kör appen

```powershell
dotnet run
```

Öppna adressen som skrivs ut (till exempel `https://localhost:7288`). Logga in med
testanvändaren nedan och gå sedan till **Konto → Two-factor authentication → Set up
authenticator app**.

### Kör testerna

```powershell
dotnet test tests/MfaLab.Tests
```

Testerna startar den riktiga appen ur `Program.cs` med `WebApplicationFactory` mot
en egen SQLite-fil, så att de ser exakt den Identity-konfiguration applikationen
kör med.

### Testanvändare

En användare sås automatiskt vid första starten, så du slipper registrera dig först.

| Fält | Värde |
|------|-------|
| E-post | `test@minapp.se` |
| Lösenord | `Sommar2024!` |

## Vad som är löst var

| Steg i övningen | Var i koden |
|-----------------|-------------|
| Steg 2–5, aktivera och verifiera TOTP | `Components/Account/Pages/Manage/EnableAuthenticator.razor` hämtar nyckeln, bygger otpauth-URI:n och verifierar koden med `VerifyTwoFactorTokenAsync`. `Program.cs` registrerar `AuthenticatorTokenProvider` via `AddDefaultTokenProviders`. Kör flödet i appen under **Konto → Two-factor authentication**. |
| Steg 7, kontolåsning | Två ställen. **1)** `Program.cs`: `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`, `AllowedForNewUsers = true`. **2)** `Components/Account/Pages/Login.razor`: `lockoutOnFailure: true` i `PasswordSignInAsync`. Låsningssidan visar policyn direkt ur `IdentityOptions`. |
| Steg 8, recovery codes | `Data/HashedRecoveryCodeUserStore.cs` lagrar koderna som PBKDF2-hashar i stället för mallens klartext, och löser in varje kod exakt en gång. `Components/Account/Pages/Manage/GenerateRecoveryCodes.razor` genererar tio och visar dem en enda gång. |
| Steg 9, motiveringarna | [REPORT.md](REPORT.md) |

## Bra att veta

### QR-koden renderas serverside

Standardmallen från Microsoft renderar ingen QR-kod, den visar bara en URI och
hänvisar till ett externt bibliotek. Det här repot genererar QR-koden serverside
med QRCoder och visar den som en färdig bild på uppsättningssidan. Ingen CDN,
inget JavaScript-bibliotek att haka i. Issuer i otpauth-URI:n är satt till
`MfaLab`, så att kontot får ett begripligt namn i autentiseringsappen.

### AddIdentityCore i stället för AddIdentity

Uppgiftstexten visar `AddIdentity<ApplicationUser, IdentityRole>(...)`.
Blazor-mallen registrerar Identity med `AddIdentityCore<ApplicationUser>(...)` i
stället, vilket är det normala för en Blazor-app utan roller. Lockout-inställningarna
sätts på exakt samma `options.Lockout`-objekt, så resonemanget i uppgiften gäller
oförändrat.

### Recovery codes lagras hashade

Identitys inbyggda EF-store sparar de tio koderna som en semikolonseparerad
klartextsträng i `AspNetUserTokens`, vilket gör en databasdump till tio färdiga
andrafaktorer per användare. `HashedRecoveryCodeUserStore` ärver EF-storen och
skriver om `ReplaceCodesAsync`, `RedeemCodeAsync` och `CountCodesAsync` så att
varje kod lagras hashad. Resonemanget finns i [REPORT.md](REPORT.md).

### Databasen

SQLite-filen skapas automatiskt vid första körningen. Vill du börja om från en
ren databas, stäng appen och radera `.db`-filen, så sås testanvändaren på nytt
vid nästa start.
