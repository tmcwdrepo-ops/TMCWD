# Billing regression checks

Run the browser-side behavior checks from the repository root:

```powershell
node tests/BillingRegression/browser-regression.cjs
node tests/BillingRegression/penalty-charging-regression.cjs
node tests/BillingRegression/customer-information-browser-regression.cjs
node tests/BillingRegression/disconnection-browser-regression.cjs
```

Run the database checks with a local MySQL connection configured through
`BILLING_TEST_CONNECTION` or the Data project's local configuration:

```powershell
dotnet run --project tests/BillingRegression -- .
```

The database checks create connection-local temporary copies of the required
application tables, seed synthetic records, and invoke the real services. They
never insert into, update, or delete persistent customer tables. The connection
must point to localhost and permit creating temporary tables. Closing the
connection removes the temporary tables. Do not put credentials in source files.

Covered database behaviors: duplicate saves, invalid readings, zero consumption,
repeat posting, legacy duplicates, recovery of an already-created bill, rollback
after SQL execution, and protection of paid penalties. Concurrent requests still
need a separate integration check; these temporary-table tests are sequential.

Without a configured connection, database checks report SKIPPED and exit with
code 2. This is not a passing test result.

Phase 6J source/fixture verification: add `--verify-disconnection-workflow`.
Without `--no-migrations`, this also executes the additive event script twice
and six real two-connection races against owned prefixed tables in local
`tmcwd_test`, then drops only those owned tables. Adding
`--verify-development-deployment` additionally compares development counts,
sums, issued snapshot digests and inspected schemas read-only before/after.
No Phase 6J development schema rollout or confirmation-role decision is implied.

Penalty checks also cover 10% calculation and rounding, SQL translation, strict due-date eligibility, paid/waived exclusions, duplicate posting, failed-post rollback, and stale collection amounts. Calculation and SQL translation checks run without a database; persistence checks require the configured local connection.

Posting security checks use real HTTP, bearer middleware, Data controllers and posting services against the same connection-local synthetic tables. They cover rejected roles/identities with unchanged financial state, forged audit IDs, allowed billing roles, retry/rollback and the actual MVC session-to-API client.

B3 adds Users/password and password-free server login, Account/Customer request allowlists, the narrow zero-value ChargeAnchor command, inspection/template authorization, all-role shared-table quarantines, and actual MVC anti-forgery/API bridge checks. These fixtures remain connection-local in `tmcwd_test`; the MVC test host uses ephemeral data-protection keys. `--no-migrations` is required for pre-deployment checks.

B4 enumerates the actual Data query routes and rejects anonymous/inappropriate-role HTTP requests for each. Its separate host uses the production persisted-token validator, current synthetic user records and login fingerprints. It checks stale JWT/MVC sessions, password changes, reader assignment, current posting and immutable financial reads. Earlier B2/B3 hosts still isolate signature/role-policy/command behavior using their original synthetic claims; B4 supplies the persisted-identity checks. No operational user or financial record is changed.

To verify posting and financial behavior without executing migration scripts, use:

```powershell
dotnet run --project tests/BillingRegression -- . --no-migrations
```

This mode requires the dedicated local `tmcwd_test` database, builds temporary fixtures from the canonical EF model, and explicitly excludes migration checks and invoice/payment concurrency suites that execute migration scripts. Those exclusions are reported as NOT RUN, not PASS.
