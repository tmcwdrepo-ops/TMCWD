# TMCWD permanent architecture rules

These rules apply throughout this repository unless a more specific `AGENTS.md`
exists in a subdirectory. Explicit user instructions take precedence.

## System architecture

TMCWD uses a layered architecture.

Main layers:

- `TMCWD/TMCWD.Application`
  - ASP.NET Core MVC application
  - Razor views
  - MVC controllers
  - browser JavaScript/CSS
  - user-facing web application

- `TMCWD/TMCWD.Services`
  - shared HTTP/API client services
  - authenticated-user services
  - communication between Application and Data API where applicable

- `TMCWD/TMCWD.Billing`
  - billing domain transactions
  - billing workflow/business rules

- `TMCWD/TMCWD.Administration`
  - administrative domain transactions

- `TMCWD/TMCWD.CustomerSupport`
  - customer-support domain transactions

- `TMCWD/TMCWD.Engineering`
  - engineering/domain transactions

- `TMCWD/TMCWD.Data`
  - ASP.NET Core API
  - API controllers
  - persistence services
  - EF Core DbContexts
  - entities
  - MySQL access
  - database scripts

- `TMCWD/TMCWD.Model`
  - shared request/response models
  - DTOs
  - enums
  - shared contracts

- `TMCWD/TMCWD.Utility`
  - shared utilities that do not belong to a domain layer

- `TMCWD.MeterReader.Mobile`
  - Flutter/Dart Android application
  - meter-reader authentication
  - offline local SQLite storage
  - assigned reading-sheet workflow
  - reading entry in later phases
  - billing/SOA presentation in later phases
  - Bluetooth thermal printing in later phases

The normal web flow is:

View / JavaScript
    -> MVC controller
    -> domain transaction or shared HTTP service
    -> TMCWD.Data API
    -> persistence service
    -> MySQL

Do not bypass this structure without a concrete reason.

The mobile flow is:

Flutter UI
    -> Flutter repository/service
    -> authenticated HTTP API
    -> TMCWD.Data controller
    -> existing domain/persistence services
    -> MySQL

The mobile application must never connect directly to MySQL.

## Default implementation approach: Ponytail full

Before editing:

1. Understand the complete affected flow.
2. Search all callers of shared code.
3. Reuse existing behavior and services before creating new ones.
4. Fix a defect at its common source when possible.
5. Choose the smallest complete change that satisfies the requirement.

Prefer in this order:

1. existing project code
2. .NET / Dart / Flutter standard functionality
3. dependencies already installed in the repository
4. a new dependency only when there is a concrete need

Do not add:
- speculative architecture
- unnecessary wrappers
- duplicate services
- duplicate DTOs
- frameworks without a specific requirement
- abstractions that only move code without simplifying the system

Never shorten away:
- validation
- authentication
- authorization
- error handling
- data integrity
- accessibility
- offline data safety
- billing rules
- workflow rules

A deliberate temporary shortcut must use a `ponytail:` comment that states:
- the limitation
- why it exists
- when it should be replaced

## Backend boundaries

Persistence belongs in `TMCWD.Data`.

Do not:
- access MySQL directly from MVC controllers
- access MySQL directly from Razor views
- access MySQL directly from browser JavaScript
- access MySQL directly from Flutter
- move persistence into UI projects

Controllers should remain thin where practical.

Business logic should reuse the appropriate existing:
- transaction class
- service
- shared model
- persistence service

Do not duplicate persistence queries when an existing service method already
represents the requested behavior.

Use asynchronous APIs for asynchronous I/O.

Keep nullable contracts accurate.

Do not suppress nullability warnings merely to make a build pass.

## API design

For new API endpoints:

- use explicit HTTP methods
- use stable routes
- validate request models server-side
- return appropriate status codes
- use DTOs for external/mobile contracts when exposing entities would leak
  unnecessary persistence details
- avoid serializing full EF graphs
- avoid exposing navigation cycles
- avoid exposing internal fields unless required by the client

Typical status behavior:

- `200` successful request
- `201` successful resource creation where appropriate
- `400` invalid request
- `401` unauthenticated
- `403` authenticated but unauthorized
- `404` resource not found
- `409` conflict/duplicate operation where appropriate
- `500` unexpected server failure

Do not expose:
- password hashes
- secrets
- connection strings
- internal tokens
- unrelated customer details
- internal EF implementation details

## Authentication and authorization

Authentication is a server boundary.

Never authorize a protected operation solely using:
- a user ID from a request body
- a user ID from a query string
- a role supplied by the client
- a claim that the server did not validate

Derive authenticated-user identity from server-validated authentication context.

For bearer authentication:
- tokens must be server-verifiable
- validate expiry
- validate issuer/audience/signing rules if JWT is used
- use secrets from configuration/environment/user-secrets
- never hardcode production signing secrets

Authorization must happen on the server even if the UI hides unauthorized actions.

Meter Reader:
- Role `4` represents Meter Reader unless the existing shared enum defines the
  canonical value
- use the shared enum when available rather than scattering magic numbers
- a meter reader may access only reading sheets assigned to that authenticated user

Current legacy password compatibility must be preserved unless the user explicitly
requests a migration.

Do not silently change existing password hashing/storage as part of unrelated work.

## Configuration and secrets

Never commit:
- database passwords
- JWT secrets
- API secrets
- private keys
- production credentials
- tokens

Use:
- environment variables
- .NET user-secrets for local development
- deployment configuration

Do not print secrets in:
- logs
- test output
- exception messages
- Codex final summaries

Development-only values must be clearly identified as development-only.

## Database and persistence rules

MySQL schema changes must follow the repository's established database-script approach.

Put schema changes under:

`TMCWD/TMCWD.Data/Database`

Use dated/descriptive scripts consistent with nearby files.

Database changes must:
- account for existing rows
- be safe to rerun where practical
- preserve historical data
- avoid destructive startup migrations
- avoid dropping/recreating production tables as a shortcut

Use parameterized SQL.

Use EF Core through existing contexts/services where that is the established pattern.

Keep related writes atomic.

For financial or workflow writes:
- use transactions where partial completion would corrupt state
- protect against duplicate requests
- preserve rollback behavior

Do not modify live customer, billing, payment, or operational data merely to test code
unless the user explicitly authorizes that scope.

Use synthetic/local test data.

## Billing integrity

Existing billing logic is authoritative unless explicitly changed.

Preserve:
- water-rate rules
- meter-size rules
- account-type rules
- minimum charges
- consumption brackets
- billing-period behavior
- reading-date rules
- billing-date rules
- due-date rules
- penalties
- paid exclusions
- waived exclusions
- adjustment behavior
- historical rate/application behavior

Use `decimal` for money.

Do not use floating-point arithmetic for currency.

Match the existing rounding policy.

Do not recompute historical bills using current rates unless existing business logic
explicitly requires it.

Where rate snapshots exist or are introduced, preserve historical reproducibility.

## Confirmed Service Invoice and penalty rules

- Consumption PeriodFrom is the immediately preceding non-deleted authoritative
  Reading.DateCreated for the same Account, strictly before the current reading.
  PeriodTo is the current posted Reading.DateCreated; no prior reading means null.
  Never use sheet scheduling fields or subtract a fixed number of days.
- BillingDate = ReadingDate + 1 day; DueDate = BillingDate + 15 days.
  Use the sheet's authoritative DisconnectionDate.
- PartialPost freezes official consumption dates and water-charge components in
  InvoiceSnapshotJson. Issued snapshots are immutable; SOA GET never rewrites them.
- Penalty is 10% of water-only snapshot BillAmount, rounded using
  LatePaymentPenalty.Calculate. Never use TotalDue or TotalBillAmount as the base.
  Maintenance, PCA, arrears, miscellaneous charges and adjustments are excluded.
  TotalAfterDue = TotalDue + informational PenaltyAfterDue.
  If a historical bill has no authoritative water-charge snapshot, reject penalty
  posting safely; never derive a base from totals or current rates.
- Persisted late penalties apply strictly after the due date, never on it.
  Informational after-due amounts do not create penalties during SOA GET.
- Group maintenance under customer-facing Other Charges, count it once, and
  preserve the existing internal Billing.OtherCharges storage meaning.
- Flutter only displays server-issued/cached amounts, dates, invoice identity and
  QR payload. No local billing or consumption-period reconstruction.
- Invoice footer displays PTU No., Date Issued and width-formatted Series only;
  prefix/width/effective dates and reading-sheet sequence ranges are internal.

## Reading Sheet architecture

ReadingSheet workflow uses the existing domain models/services.

Canonical status meaning unless an existing shared enum is authoritative:

- `1` Created
- `2` InProgress
- `3` Completed
- `4` Deleted

Do not create parallel reading-sheet models/services when the existing system already
contains the required behavior.

Reuse existing:
- ReadingSheet service
- Reading service
- ZoneBook service
- Account service
- billing/rate service

A mobile reader must only receive ReadingSheets assigned to that authenticated user.

Deleted ReadingSheets must not appear as active work.

Do not overwrite completed or historically significant reading data during refresh or
download.

## Mobile application architecture

`TMCWD.MeterReader.Mobile` is offline-first.

Flutter communicates only with the TMCWD API.

Never connect Flutter directly to MySQL.

Current local persistence uses `sqflite`.

Do not replace it with:
- Drift
- Hive
- Isar
- Realm
- another ORM/database

unless explicitly requested.

Local data belongs to the deployment + reader partition defined by the existing
mobile database naming logic.

Changing users must not delete another reader's local data.

Authentication/session secrets belong in secure storage, not SQLite.

The mobile application should continue to start and display downloaded work even if
the network is unavailable.

## Mobile offline-first rules

Network data is not automatically more authoritative than unsynced local work.

A refresh failure must never erase valid downloaded data.

A partial download must never leave a reading sheet in a falsely complete state.

When downloading a ReadingSheet and its accounts:
- use a local transaction where practical
- upsert deterministically
- make repeated downloads idempotent
- do not create duplicate accounts/readings
- preserve unsynced local fields
- do not overwrite locally-entered unsynced reading values with stale server data

Do not use "delete all and insert again" if local unsynced data may exist.

If server and local fields have different ownership, keep that ownership explicit.

Example ownership:
- server-owned: customer/account identity, assignment, billing period
- mobile-owned until sync: current reading, local remarks, local sync status

Never silently discard unsynced meter-reader work.

## Mobile local database changes

Before changing SQLite schema:

1. inspect the existing schema
2. inspect current schema version
3. inspect existing migration behavior
4. identify whether a schema change is actually required

If a change is required:
- increment the schema version
- add a forward migration
- preserve existing records
- add/update migration tests
- keep the canonical `schema.sql` synchronized with the latest schema

Do not solve migration problems by deleting the local database.

Do not wipe user data during app startup.

## Mobile sync rules

Sync operations must be:
- retryable
- idempotent where possible
- safe after app restarts
- explicit about local/server state

Use stable identifiers.

Do not use display names as sync keys.

Do not mark a record synced before the server confirms success.

When a sync fails:
- preserve the local record
- preserve the sync-pending state
- show a recoverable error
- allow retry

Avoid duplicate server-side reading submissions.

Use idempotency keys or existing unique constraints where appropriate.

## Mobile phase boundaries

Unless explicitly requested, keep implementation within the current phase.

Phase 1:
- app shell
- local database
- local models
- offline infrastructure

Phase 2:
- meter-reader authentication
- secure session storage
- offline session restore

Phase 3:
- authenticated assigned reading sheets
- ReadingSheet download
- account download
- offline ReadingSheet persistence
- assigned-sheet list
- download/open/continue states

Phase 4:
- meter-reading entry
- reading validation
- local draft preservation
- reading sync

Later phases:
- billing calculation
- rate snapshot usage
- SOA generation
- Bluetooth thermal printing
- final completion/submission workflow

Do not implement later-phase features unless explicitly requested.

## Flutter code rules

Follow existing project structure and naming.

Prefer:
- small widgets
- explicit models
- repositories/services for I/O
- dependency injection only where the project already uses it or where it solves a
  concrete testing/ownership problem
- immutable models where practical

Do not place network calls directly inside UI build methods.

Do not place raw SQL throughout UI code.

Keep:
- API concerns in API/service/repository layers
- SQLite concerns in local data layer
- UI state in the appropriate feature layer

Handle:
- loading
- empty state
- offline state
- error state
- retry
- session expiry

Do not report failed network/database work as successful.

## UI rules

Reuse the existing visual system.

For web:
- reuse shared layouts
- reuse existing CSS/JS conventions
- preserve accessibility
- preserve keyboard navigation
- preserve focus behavior
- preserve responsive layouts

For Flutter:
- reuse Material theme/components already present
- keep touch targets usable
- avoid tiny controls
- keep errors visible
- support keyboard/IME behavior
- avoid blocking the whole screen unnecessarily

Render untrusted text safely.

Do not interpolate untrusted content into HTML.

## Error handling

Expected user-facing failures should be handled explicitly.

Do not expose raw stack traces to normal users.

Backend logs may contain diagnostic context but must not contain:
- passwords
- raw auth tokens
- secrets
- unnecessary customer PII

Client errors should be understandable and recoverable.

Offline is a normal operating condition for the Meter Reader app, not an exception.

## Logging

Log enough to diagnose:
- endpoint
- operation
- safe resource identifiers
- status/failure category

Do not log:
- passwords
- bearer tokens
- JWT signing secrets
- connection strings
- full sensitive customer records

Use existing logging infrastructure instead of ad-hoc production file writes where
possible.

## Testing philosophy

Test observable behavior, not implementation text.

Prefer the smallest regression test that protects the behavior being changed.

Backend/API tests should cover authorization and data boundaries when relevant.

Mobile tests should cover:
- parsing
- persistence
- migrations
- idempotent download behavior
- preservation of offline data
- preservation of unsynced work
- authenticated request headers where practical

Do not treat a skipped test as a pass.

Do not claim a test/build passed unless it was actually run.

## Verification

Run only checks relevant to the change, but verify every affected layer.

Repository/.NET checks may include:

```powershell
dotnet build TMCWD/TMCWD.slnx
node tests/BillingRegression/browser-regression.cjs
node tests/BillingRegression/penalty-charging-regression.cjs
dotnet run --project tests/BillingRegression -- .
dotnet run --project tests/ServiceRequestRegression -- .
