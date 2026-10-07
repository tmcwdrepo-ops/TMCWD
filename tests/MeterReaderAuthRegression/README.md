# Meter-reader authentication regression

Run from the repository root:

```powershell
dotnet run --project tests/MeterReaderAuthRegression
```

No database or live server is used. Synthetic HTTP responses exercise the real
UserTransaction, ApplicationLoginTransaction, AuthController and JWT validator.
Checks cover email/user-ID login, signed identity and expiry, invalid credentials,
inactive/null-active users, non-reader roles, mismatched ID lookup, validation,
and the preserved general login route.

If the running MVC application locks its build outputs, use isolated artifacts:

```powershell
dotnet build tests/MeterReaderAuthRegression --artifacts-path .tmp/meter-reader-auth
dotnet .tmp/meter-reader-auth/bin/MeterReaderAuthRegression/debug/MeterReaderAuthRegression.dll
```

