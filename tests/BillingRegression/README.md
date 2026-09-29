# Billing regression checks

Run the browser-side behavior checks from the repository root:

```powershell
node tests/BillingRegression/browser-regression.cjs
node tests/BillingRegression/penalty-charging-regression.cjs
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

Penalty checks also cover 10% calculation and rounding, SQL translation, strict due-date eligibility, paid/waived exclusions, duplicate posting, failed-post rollback, and stale collection amounts. Calculation and SQL translation checks run without a database; persistence checks require the configured local connection.
