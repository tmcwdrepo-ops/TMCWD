# Meter Reader Phase 3A

Run from the repository root:

```powershell
dotnet run --project tests/MeterReaderAssignedRegression
```

The console regression starts a loopback HTTP server with the actual Data API
controllers and ASP.NET Core bearer middleware. Synthetic user/ReadingSheet services
avoid database writes. It checks login compatibility, token validation, 401/403,
claims-based assignment, deleted-sheet exclusion, current reader access, the mobile
DTO and empty results. It does not exercise a deployed server or MySQL.

## Configuration

The Data API now requires `Jwt:Key`, `Jwt:Issuer` and `Jwt:Audience` at startup.
Supply these through deployment configuration, .NET user-secrets or environment
variables (`Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`). Use a cryptographically random
key of at least 32 UTF-8 bytes. Never commit the key. Existing database configuration
is still required.

Use the same values as the MVC Application if accepting its existing reader JWTs.
Data-issued tokens retain the existing 12-hour expiry; MVC-issued tokens retain
their existing expiry. Previously issued opaque Data tokens require a fresh login.
Legacy password compatibility and the six-field login response are preserved.

`GET /api/reading-sheet/mobile/assigned` is hosted by **TMCWD.Data**. The existing
Flutter Phase 2 README points login at MVC; Phase 3A does not change that app's URL
or add an assigned-sheet UI. A future client must address the Data endpoint and use
a JWT issued with matching configuration.

The endpoint uses validated reader identity, requires Role 4 and a currently active,
verified reader, and calls `GetByAssignedTo`. It excludes Deleted (4), retains
Completed (3), and returns an empty array when there is no work. DTO fields are
`id`, `name`, `billingDate`, `dueDate`, `disconnectionDate`, `billingPeriodStart`,
`zoneBookId`, `seqFrom`, `seqTo`, `status`. No accounts or persistence graphs are returned.

## Manual PowerShell check

Start the configured Data API first. Use HTTPS for deployed servers; this local URL
is for development only. The password is prompted securely and the token is kept
in memory rather than printed:

```powershell
$api = 'http://localhost:5178'
$readerPassword = Read-Host 'Meter Reader password' -AsSecureString
try {
    $body = @{
        identifier = 'meter.reader1@gmail.com'
        password = [System.Net.NetworkCredential]::new('', $readerPassword).Password
    } | ConvertTo-Json
    $session = Invoke-RestMethod -Method Post -Uri "$api/api/auth/meter-reader/login" -ContentType 'application/json' -Body $body
} finally {
    $body = $null
    $readerPassword.Dispose()
}
if ($session.userId -ne 10 -or $session.role -ne 4) { throw 'Unexpected reader identity.' }
$bearerToken = $session.token
$headers = @{ Authorization = "Bearer $bearerToken" }
$assigned = Invoke-RestMethod -Method Get -Uri "$api/api/reading-sheet/mobile/assigned" -Headers $headers
$assigned | Format-Table id, name, status, billingDate
```

Unauthenticated GET must return 401. A valid non-reader JWT must return 403.
## Phase 3B download

`GET /api/reading-sheet/mobile/{id}/download` returns `{ readingSheet, accounts }`.
The sheet contains the 13 requested schedule, assignment, status and timestamp fields.
Account fields are `accountId`, `accountNumber`, `name`, `meterNumber`, `address`,
`zoneBookId`, `zone`, `book`, `sequence`, `classification`, `meterSize`,
`previousReading`, `previousReadingDate`, `readingId`, `currentReading`, `readingDate`,
and `readingStatus`. Current reading fields are nullable when no reading exists.

Created/InProgress sheets use active zone/book accounts within the sequence bounds
(null/zero means unbounded), plus accounts already represented by saved readings.
Completed sheets also use the active zone/book/sequence roster, including accounts
without readings. Saved reading membership additionally preserves accounts that have
since moved or closed. Account identity/address metadata reflects current records;
the database has no historical account snapshot here. Download performs no writes.

Saved previous values are authoritative. Otherwise `ReadingService.GetPreviousForSheet`
uses the most recent earlier sheet billing date, excluding deleted sheets/readings,
and falls back to AccountService's installation baseline. The previous reading date
is nullable when no historical reading exists. Future readings cannot supply the baseline.

Missing/deleted sheets return 404, another reader's sheet returns 403, missing or
invalid bearer authentication returns 401. Missing billing date/zone-book, missing
recorded accounts or duplicate sheet readings return 409 rather than a partial download.
The focused HTTP regression covers these phase boundaries with synthetic services;
`tests/BillingRegression` also checks the actual historical-reading query against
connection-local MySQL temporary tables, including later/same-period/deleted history.

After the login steps above:

```powershell
$sheetId = Read-Host 'Assigned ReadingSheet ID'
$download = Invoke-RestMethod -Method Get -Uri "$api/api/reading-sheet/mobile/$sheetId/download" -Headers $headers
$download.readingSheet | Format-List
$download.accounts | Select-Object accountId, accountNumber, sequence, previousReading, previousReadingDate
```

No Phase 3C/3D, Flutter UI, SQLite, meter entry or billing/printing changes are included.
