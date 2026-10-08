# MVC → Data authentication correction

## Confirmed cause

Before the correction, the effective local Development configurations differed:

| Setting | MVC Application | Data API |
|---|---|---|
| Issuer | `TMCWD` from Application appsettings.json | `TMCWD.Data` from user-secrets |
| Audience | `TMCWDMobile` from Application appsettings.json | `TMCWD.MeterReader.Mobile` from user-secrets |
| Signing key source | Application appsettings.json | `TMCWD.Data-local-development` user-secrets |
| Signing key comparison | Different from Data's key | Different from Application's key |

Keys were compared only in memory; no keys, JWTs, passwords or connection strings were printed. Process, Windows User and Machine environments had no `Jwt__Issuer`, `Jwt__Audience` or `Jwt__Key` overrides. Neither launch profile supplies JWT overrides; both select Development. Neither Program adds custom configuration providers. The inspected Development appsettings did not override these JWT settings.

Application's scoped WebService already created a five-minute signed token with trusted session user ID and role and attached a Bearer header. BillingTransaction used that same injected client. The original header was confirmed in code, not captured from the failed request's wire traffic. A header containing a token signed with the wrong key/issuer/audience still produces 401; Collection role authorization happens only after authentication succeeds.

Both middleware pipelines already place UseAuthentication before UseAuthorization and controller mapping. Data already validates issuer, audience, signature, expiration and HS256. None of those checks or staff role policies was weakened.

## Correction and configuration ownership

Both projects now declare the same development `UserSecretsId`: `TMCWD.Data-local-development`. Application's conflicting appsettings JWT block, including its file-based signing key, was removed. Data's existing secret values were preserved, maintaining the existing mobile issuer/audience/key.

Both Development projects consequently load:

- `Jwt:Issuer` = `TMCWD.Data`
- `Jwt:Audience` = `TMCWD.MeterReader.Mobile`
- `Jwt:Key` = the existing shared user-secret value, at least 32 UTF-8 bytes

No secret values were copied into source files or a second secret store. The shared local store is `%APPDATA%/Microsoft/UserSecrets/TMCWD.Data-local-development/secrets.json`. Configure it through .NET user-secrets; do not commit or print it. For production, provide matching `Jwt__Issuer`, `Jwt__Audience` and `Jwt__Key` through secure deployment configuration to both processes. User-secrets are Development-only.

Application now validates required configuration at startup with a safe configuration error. The existing scoped client registration was moved unchanged into `ApiClientRegistration.AddSessionApiClient` so the integration regression exercises the actual production registration. Tokens still derive their identity from AuthenticatedUserService populated from the server session. No token generation was added to BillingTransaction.

Collections search catches upstream HttpRequestException with status 401/403, returns that status with a controlled JSON message, and logs only endpoint, response status, header-presence boolean, scheme, user ID and role. It does not log the exception, raw header or token. Authentication failures are not returned as successful empty lists.

## Files changed

- `TMCWD/TMCWD.Application/TMCWD.Application.csproj` — shared development secret-store ID.
- `TMCWD/TMCWD.Application/appsettings.json` — removed conflicting JWT configuration and file-based signing key.
- `TMCWD/TMCWD.Application/Program.cs` — required configuration check and existing registration call.
- `TMCWD/TMCWD.Application/ApiClientRegistration.cs` — existing scoped client/token registration, now directly exercised by tests.
- `TMCWD/TMCWD.Application/Controllers/CollectionsController.cs` — controlled upstream 401/403 response and safe diagnostics.
- `tests/MeterReaderAssignedRegression/MeterReaderAssignedRegression.csproj` — reference Application for its actual registration/controller.
- `tests/MeterReaderAssignedRegression/Program.cs` — run integration checks and optional read-only live probe.
- `tests/MeterReaderAssignedRegression/MvcApiAuthentication.cs` — actual MVC registration → Data bearer middleware, wrong configuration, scoped isolation, claims/expiry, controlled MVC response tests and optional live local API probe.
- `docs/mvc-data-jwt-authentication.md` — this report and configuration/manual verification instructions.

Data Program, BillingTransaction, payment/penalty/billing rules, Service Invoice values and Flutter were not changed.

## Verification

- Solution build passed: zero errors, 206 warnings. Existing nullability warnings remain visible; regression builds also report the existing NU1903 advisory for System.Security.Cryptography.Xml 8.0.3.
- Actual MVC registration → synthetic HTTP Data controllers/middleware passed: Cashier/SuperAdmin 200, MeterReader 403, wrong issuer/audience/key 401. Tests verify token claims, expiration, request-scoped clients and controlled MVC 401/403 responses. Existing expiration/signature/role/claims/SOA/BIR regressions also pass.
- Read-only requests against the **running local Data API** using Application's actual Development configuration passed: SuperAdmin/Cashier 200, MeterReader 403, missing token 401. No financial writes were sent. This uses a trusted synthetic server-side identity for an authentication probe; it does not claim a browser login was performed.
- BillingRegression passed: 71 database checks, 5 pure rounding cases, 2 MySQL query shapes, invoice numbering/migration rerun and two-connection payment concurrency checks. Database writes were restricted to synthetic tables inside local `tmcwd_test`.
- Browser regressions: 25 passed; four Collections headless browser fixture scenarios passed. These browser fixtures do not replace a full deployed browser login check.

Build/test logs: `.tmp/jwt-final-build.log`, `.tmp/jwt-live-regression.log`, `.tmp/jwt-billing-check.log`, `.tmp/jwt-browser.log`. Separate build outputs were used to avoid DLL locks in the running applications.

## Remaining manual verification

The already-running MVC process has its previous configuration in memory. Restart the development applications in Visual Studio or stop/restart their existing `dotnet run` sessions so the current build and shared user-secrets are loaded. Their existing http launch profiles set Development.

1. Start Data with `dotnet run --project TMCWD/TMCWD.Data --launch-profile http`.
2. Start MVC with `dotnet run --project TMCWD/TMCWD.Application --launch-profile http`.
3. Sign in through the browser as SuperAdmin, open Collections, search an account, and confirm details render without the developer exception. Repeat as Cashier.
4. Confirm the browser's `/Collections/SearchAccounts` response is 200. No payment submission is necessary for this authentication check.
5. A missing API token must remain 401; a valid Meter Reader token must remain 403 on Collection endpoints. Both were verified against the running local API.

The existing signing key was not rotated here: rotating it requires coordinating both applications and existing mobile sessions. Removing a file-based key from the working file does not purge repository history; secure deployment key rotation remains separate work.
