Run from the workspace root:

```powershell
dotnet run --project tests/ServiceRequestRegression -- .
```

Requires local MySQL and the Data project's user secret (or `SERVICE_REQUEST_CONNECTION`). All writes use connection-local temporary tables. Existing active users are read only. Tests cover migration reruns, preserved job numbers and non-customer details, status and installation ID mapping, create/edit/complete, summary queries, customer account validation, installation creation/completion, and rollback after a partial save fails.
