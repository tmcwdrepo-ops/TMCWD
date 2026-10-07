# TMCWD Meter Reader Mobile — Phases 1 and 2

The app now has a login form, HTTPS API authentication, secure session storage,
offline restoration of unexpired sessions, sign out and reader-specific SQLite
files. Phase 3D adds transactional sheet/account downloads and offline opening.
Meter entry, sync, billing and printing remain in their later phases.

## Phase 2 files and purpose

- `lib/features/auth/login_screen.dart`: accessible email/user-ID and password
  inputs, validation, busy state and visible errors; clears the password after
  each attempt.
- `lib/features/auth/auth_service.dart`: native Dart HTTPS requests, timeout
  handling, redirect rejection and secure token/session persistence.
- `lib/data/models/auth_session.dart`: validated session metadata and a database
  filename partitioned by API deployment and authenticated reader.
- `lib/main.dart`: restores sessions, locks expired sessions and handles resume.
- `lib/features/auth/reader_home.dart`: opens only the reader's database and signs
  out without deleting readings.
- `lib/data/local/local_database.dart`: adds a filename option; schema version 1
  and existing tables remain unchanged.
- `pubspec.yaml`: adds only
  [flutter_secure_storage](https://pub.dev/packages/flutter_secure_storage) for
  platform encrypted storage. Networking uses `dart:io`.
- `tool/setup-android.ps1`: generates Android host files, disables backup, grants
  Internet permission and sets Android minimum SDK to at least 23, as required
  by secure storage.
- `test/auth_test.dart`: request/response, secure-storage failure, offline restore,
  expiry, deployment isolation and login form regression checks.
- `TMCWD/TMCWD.Application/Controllers/AuthController.cs`: related backend change
  adding the dedicated reader login route, reusing existing transactions.
- `tests/MeterReaderAuthRegression`: executable synthetic checks of the actual
  authentication transactions/controller and JWT validation; no live data.

## API contract

For Phase 3C, configure the HTTPS base address of **TMCWD.Data**, which now hosts
meter-reader login, assigned sheets and sheet download. A trailing slash is
normalized; an application path prefix is supported. Use an email login with Data.

```http
POST /api/auth/meter-reader/login
Content-Type: application/json

{"identifier":"existing-login-email-or-numeric-user-ID","password":"password"}
```

Success returns `token`, `userId`, `role`, `email`, `name` and UTC
`expiresAt`. The server requires an active `UserRole.MeterReader` (4) and derives
identity from verified credentials. Numeric identifiers resolve through the
existing user lookup and must match the authenticated user's ID.

TMCWD has email and numeric user IDs, **no separate employee-ID field**.
The UI therefore says “Username or TMCWD user ID”; external employee numbers
are not treated as TMCWD IDs. The existing `POST /api/auth/login` contract remains
available for its other consumers. Password verification and the existing
30-day JWT lifetime are reused.

The MVC host requires its existing `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` and
`webServiceLocation` configuration. Keep signing keys and database credentials
in environment variables or .NET user-secrets. Do not embed them in Flutter.
Use an HTTPS certificate trusted by the Android device; there is no certificate
validation bypass or cleartext fallback.

## Setup and checks (from repository root)

Install current stable Flutter and the Android SDK; put Flutter on PATH.
If no Android host exists yet:

```powershell
powershell -ExecutionPolicy Bypass -File .\TMCWD.MeterReader.Mobile\tool\setup-android.ps1
```

The setup script refuses to overwrite an existing Android folder. If one was
generated with the Phase 1 script, set `android:allowBackup="false"`, add
`android.permission.INTERNET` in its main manifest, and set
`minSdk = maxOf(flutter.minSdkVersion, 23)` in `android/app/build.gradle.kts`.

```powershell
node TMCWD.MeterReader.Mobile/tool/schema-regression.cjs
dotnet build TMCWD/TMCWD.slnx --artifacts-path .tmp/meter-reader-auth
dotnet build tests/MeterReaderAuthRegression --artifacts-path .tmp/meter-reader-auth
dotnet .tmp/meter-reader-auth/bin/MeterReaderAuthRegression/debug/MeterReaderAuthRegression.dll
Set-Location TMCWD.MeterReader.Mobile
flutter pub get
dart format lib test
flutter analyze
flutter test
flutter build apk --debug --dart-define=API_BASE_URL=https://your-tmcwd-host/
flutter run --dart-define=API_BASE_URL=https://your-tmcwd-host/
```

Replace the example URL with the actual trusted HTTPS MVC host. No server address
or user is hard-coded. An absent/invalid build configuration shows a visible error.

The ignored `.android-template` is retained for inspection. Commit generated
Android source files, `.metadata` and resolved `pubspec.lock`; exclude build outputs.
The APK and platform secure storage still need verification on an Android device.

## Session and local data behavior

First login and re-login after expiry require the network. A saved unexpired
session restores without contacting the server. Passwords are never retained in
SQLite, secure storage or logs. Token and reader metadata are stored together in
one secure-storage entry; a failed write is a failed login. A failed secure
deletion displays an error and does not report successful sign out.

Expired or corrupt stored sessions are rejected. The running app locks on expiry
and rechecks expiry when resumed. There is no refresh-token protocol in the
existing backend; log in again when the thirty-day session expires.
Offline restoration cannot detect server-side deactivation until connectivity
returns; later mobile API routes must recheck reader authorization.

Each API deployment/reader combination gets its own database. Logout closes
that database and removes only the secure session; it never deletes unsynced
readings. The original Phase 1 unscoped `tmcwd_meter_reader.db` is retained but
not opened by the authenticated UI. No automatic transfer of its records occurs.

## Phase 1 storage contracts

Seven tables store sheets, accounts, readings, official billing rates, SOA bill
snapshots, sync queue entries and printer settings. The bundled schema includes
foreign keys, indexes, local uniqueness constraints and unsynced deletion
protection. Schema version 1 is unchanged. Future upgrades must explicitly
migrate existing rows without resetting the database; downgrades are rejected.

Local relations use SQLite IDs; explicit server IDs identify TMCWD records.
Sheet `assigned_to` retains the server reader ID. Phase 3 must also enforce
assignment server-side; client partitioning alone is not server authorization.

Amounts, readings, meter sizes and rates are decimal strings, preserving .NET
decimal precision. Phase 6 must use inspected official billing rules and rounding,
not Dart doubles. No rates or due-date calculations are implemented yet.
Dates are ISO 8601 strings; billing calendar dates remain date-only.

Current reading and consumption are nullable for unreadable meters. Phase 4 must
validate conditions, generate a valid UUID for each new reading and save its
reading/queue entry in one transaction. Never replace-on-conflict or delete an
unsynchronized reading. Phase 5 still needs server idempotency and recovery of
interrupted `syncing` entries.

Bills store a separate server total and review flag. There is no paid flag,
official receipt workflow or printing implementation. Print count supports
future reprints without duplicate bill records. Bill items remain deferred
until actual billing requirements need them.

## Phase 3C: assigned-sheet preview

ReaderHome now requests `GET /api/reading-sheet/mobile/assigned` using the existing
secure session bearer token. It shows sheet ID/name/status/billing date and
zoneBookId. The assigned endpoint does not return zone/book numbers or an account
count. Download / Open calls `GET /api/reading-sheet/mobile/{id}/download` and shows
account count, identity, meter/address, zone/book/sequence and saved reading values.
This is a read-only preview in memory. No downloads are written to SQLite.

The UI handles loading, empty results, network/server failures with retry,
unauthorized access, and expired sessions with a Sign in again action. Existing
secure session storage, expiry handling and reader database partitioning remain.

Verification commands:

```powershell
flutter test
flutter analyze
flutter build apk --debug --dart-define=API_BASE_URL=http://10.0.2.2:5178
```

Manual emulator check (start Data in another terminal with its configured secrets):

```powershell
dotnet run --project TMCWD/TMCWD.Data --launch-profile http
cd TMCWD.MeterReader.Mobile
flutter run -d emulator-5554 --dart-define=API_BASE_URL=http://10.0.2.2:5178
```

Sign in inside the emulator using a valid Role 4 email account (for example
meter.reader1@gmail.com); do not put a real password into command lines or logs.
Verify the sheet list, tap Download / Open, and inspect account details. Test Refresh,
empty results where available, and retry after temporarily disconnecting networking.
A 401 must offer Sign in again; a 403 must display unauthorized access.
Phase 3D persistence, meter entry, billing, SOA, printing and sync are not included.

Verified Phase 3C on 2026-10-05: 11 Flutter tests passed, analysis reported no issues,
APK debug build succeeded, and the emulator displayed a real Created sheet (ID 2)
with its billing date and zoneBookId. Download / Open displayed 41 returned
accounts from the local API on port 5178. Real credentials were entered only in the
emulator. Debug Android permits local HTTP traffic; release HTTPS defaults remain.

## Phase 3D: downloaded work in SQLite

Downloads now save the sheet, roster and server-saved readings in one SQLite
transaction. ReaderHome shows **Downloaded / Offline available** and **Open offline**.
Open offline reads SQLite without making an HTTP request. Refresh re-downloads the
sheet; network/save failure retains the previous download and offers saved accounts.
Assigned-list failure shows cached sheets with a visible connection notice. The
existing secure session must remain valid; expiry and 401/403 handling are preserved.

Schema version is now **2**, with a forward migration from version 1. No tables are
replaced or added. Added fields: reading_sheets.disconnection_date/date_created/
date_upload, accounts.previous_reading_date/in_roster. downloaded_at records only a
committed complete download. Existing reader/deployment filenames are unchanged.

Account identity and metadata are refreshed by stable sheet/server-account keys.
Local IDs and reading UUIDs are retained on repeated download. Server-saved readings
are cached as synced snapshots; unread accounts need no fabricated reading row.
Pending/syncing/failed reading rows are never overwritten, including their reading
values, remarks, timestamps and sync state. Queued work is retained. Accounts no
longer in the server roster are marked out of roster rather than deleted; those
with unsynced work remain visible. No sync or meter-entry feature is introduced.

New coverage in test/download_persistence_test.dart checks HTTP-to-SQLite saving,
idempotency, transaction rollback, offline opening, all unsynced states, omitted
roster accounts, reader partitions and version-1 migration with existing queued work.

Manual emulator verification:

1. Start the configured Data API on port 5178.
2. Run `flutter run -d emulator-5554 --dart-define=API_BASE_URL=http://10.0.2.2:5178`.
3. Sign in inside the emulator and tap Download / Open. Wait for Downloaded / Offline
   available and inspect the account roster.
4. Return to the list; the sheet should show Open offline. Open it without refresh.
5. Disable emulator Wi-Fi and mobile data (or stop the local API). Refresh the list;
   cached sheets must remain with a connection notice. Open offline must show accounts.
6. Close and reopen the app while offline; with an unexpired saved session, the
   downloaded sheet and accounts must still be available.
7. Restore connectivity and refresh/re-download. Counts must not increase from
   duplicate rows. Changing readers must leave the original reader database intact.

No Phase 4 entry/editing, sync/submission, billing, SOA or printing is included.

Phase 3D verified on 2026-10-05: all 16 Flutter tests passed, analysis found no issues,
and the debug APK built successfully. On the emulator, Sheet 4 saved 41 accounts.
With Wi-Fi and mobile data disabled, the app was restarted, cached sheets remained
visible with a connection notice, and Open offline reopened all 41 accounts from
SQLite. Original emulator network settings were restored afterward.
