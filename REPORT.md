# Rapport — övning 5.3: MFA med TOTP, kontolåsning och recovery codes

Kurs: IT-säkerhet för utvecklare. Applikationen är en Blazor Web App på .NET 10
med ASP.NET Core Identity och EF Core (SQLite).

## Vad som är gjort

| Steg | Var i koden | Hur det är verifierat |
|------|-------------|------------------------|
| TOTP aktiverat och verifierat | `Components/Account/Pages/Manage/EnableAuthenticator.razor`, `Program.cs` (`AddDefaultTokenProviders`) | Manuellt med Microsoft Authenticator, samt `TotpTests` som räknar fram en kod enligt RFC 6238 ur den delade hemligheten och låter Identity validera den |
| Kontolåsning efter fem försök | `Program.cs` (`options.Lockout`) och `Components/Account/Pages/Login.razor` (`lockoutOnFailure: true`) | `LockoutTests`: fyra försök låser inte, det femte låser, rätt lösenord nekas under låsningen, och låsningen släpper när tiden gått ut |
| Recovery codes, hashade och engångs | `Data/HashedRecoveryCodeUserStore.cs`, registrerad i `Program.cs` | `RecoveryCodeTests`: tio unika koder, varje kod fungerar exakt en gång, och ingen kod finns i klartext i `AspNetUserTokens` |

Kör testerna med:

```powershell
dotnet test tests/MfaLab.Tests
```

## Motivering 1: varför TOTP och inte SMS i den här applikationen

Jag valde TOTP därför att andrafaktorn då är en hemlighet som aldrig lämnar
telefonen, medan ett SMS färdas genom en kedja av operatörer och nät som varken
jag eller användaren kontrollerar. Den kedjan går att angripa på flera sätt:
SIM-kapning där angriparen övertalar operatören att flytta numret till ett eget
kort, avlyssning via svagheter i SS7, och koder som lyser upp på en låst skärm.
TOTP tar bort hela den kanalen, eftersom hemligheten delas en enda gång vid
registreringen och koderna sedan räknas fram lokalt i telefonen helt utan
nätverk. Det TOTP däremot inte skyddar mot är en phishing-proxy som i realtid
skickar vidare den kod användaren just skrivit in, och där är TOTP inte starkare
än SMS — det problemet löser bara ursprungsbunden inloggning som passkeys, vilket
den här mallen redan har stöd för. Ändå är TOTP förstahandsvalet i just den här
appen: det kostar ingenting per inloggning, kräver inget telefonnummer och är
oberoende av operatörer, och för den som inte kan installera en app är SMS
fortfarande långt bättre än ingen andra faktor alls.

## Motivering 2: hur jag landade i fem försök och femton minuter

Fem försök och femton minuter är en avvägning mellan att stoppa gissning och att
inte göra kontot obrukbart. Fem räcker för en användare som råkar skriva ett
gammalt lösenord eller har fel tangentbordslayout, samtidigt som det stryper
gissningstakten till som mest tjugo försök i timmen — en angripare som behöver
miljontals försök kommer aldrig i mål. Femton minuter valdes för att kostnaden
ska vara kännbar för angriparen men uthärdlig för den riktiga användaren, som kan
vänta ut låsningen utan att kontakta support. Den verkliga risken med en hårdare
policy är att låsningen blir ett vapen: vem som helst kan låsa ut vem som helst
genom att skriva fel lösenord fem gånger mot en känd e-postadress, så en
permanent eller flera timmar lång låsning vore en gratis överbelastningsattack
mot enskilda användare. Därför är låsningen tidsbegränsad och självläkande i
stället för permanent, och låsningssidan talar om exakt hur länge den gäller så
att den utelåste vet när det är lönt att försöka igen.

## Kompletterande noteringar

Utanför de två motiveringarna, saker som blev tydliga under arbetet:

**Låsningen gäller mer än lösenordssteget.** Eftersom inställningarna sitter på
`IdentityOptions.Lockout` räknar `SignInManager` upp misslyckade försök även på
`/Account/LoginWith2fa` och vid fel recovery code. Brute force mot en sexsiffrig
TOTP-kod är alltså också täckt, vilket är viktigt: en miljon möjliga koder är
inte mycket om man får gissa fritt.

**Recovery codes låg i klartext i mallen.** Identitys inbyggda EF-store sparar de
tio koderna som en semikolonseparerad klartextsträng i `AspNetUserTokens`. Det
gör en databasdump till tio färdiga andrafaktorer per användare, alltså en väg
förbi hela MFA-implementationen med bara ett lösenord. `HashedRecoveryCodeUserStore`
lagrar i stället en PBKDF2-hash per kod via Identitys egen `IPasswordHasher`. En
recovery code är bara tio tecken ur ett alfabet på 26, knappt 47 bitars entropi,
vilket en GPU hade betat av på timmar mot en rak SHA-256 — det är iterationerna i
nyckelhärledningen som gör den attacken ohållbar.

**TOTP-hemligheten går inte att hasha.** Till skillnad från lösenord och recovery
codes måste servern kunna räkna fram koden själv, så den delade hemligheten
ligger läsbar i `AspNetUserTokens`. Den som kommer åt databasen kan därför
generera giltiga TOTP-koder. I en skarp app hör den kolumnen hemma bakom
kryptering i vila, med nyckeln i en nyckelhanterare i stället för i samma databas.

**Låsningssidan avslöjar att kontot finns.** `/Account/Lockout` går bara att nå
för en e-postadress som faktiskt är registrerad, medan ett okänt konto ger det
generiska "Invalid login attempt". Det är en medveten kompromiss här: alternativet
vore att aldrig berätta att kontot är låst, vilket gör låsningen obegriplig för
den drabbade. I en app där enbart existensen av ett konto är känslig information
skulle jag välja tvärtom.

## Bevis: skärmbild av lyckad inloggning

Skärmbilden av den lyckade tvåfaktorsinloggningen ligger i `docs/`.
