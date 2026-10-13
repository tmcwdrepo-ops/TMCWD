# B6 reusable recovery reconciliation manifest

Prepared 2026-10-09. **Template only; NOT EXECUTED.** Complete only after separate data-copy authorization and verified isolated restore. Ordinary console output must contain only object counts, match/failure categories and manifest checksums. Store financial values, identifiers, raw row material and security comparisons privately on approved encrypted storage outside Git.

## Capture and comparison protocol

1. Record approval/operator, capture UTC, source host/port/schema/UUID/version, isolated restore UUID/port/version, dump SHA-256, byte length and restore exit code. Source expected: 127.0.0.1:3307/tmcwd, UUID `c3f950fe-b565-11f1-b6a6-30560f67a354`. Restore expected: separate 3308 instance, `tmcwd_restore_test`, scheduler OFF.
2. Verify complete catalog visibility with the authorized operator. Freeze original operational writers, scheduled writers and DDL while capturing baseline and dump; do not rely on unrelated connections' different snapshot times. Record the maintenance interval. Preserve the frozen baseline when source writes resume.
3. Execute only read-only SELECT/SHOW commands against each selected schema. Do not call application endpoints or stored business routines. Require compatible charset, timezone, SQL mode and case behavior. Never change either database to make reconciliation pass.
4. Save protected original/restored result sets with checksums. Compare exact decimals, nulls, raw bytes and timestamps. Log only pass/fail, not sensitive differences. Stop on any unexplained difference; counts and totals alone do not constitute a successful recovery.

## Complete catalog and row counts

Capture schema default charset/collation and ordered metadata from `information_schema.tables`, `columns`, `statistics`, `table_constraints`, `key_column_usage`, `referential_constraints`, `check_constraints`, `routines`, `parameters`, `triggers`, `events` and `views`, filtered to `DATABASE()`. Explicitly retain engines, column types/defaults/nullability/generated expressions, indexes/order/uniqueness, constraints, object bodies, definers, security mode and event status/schedules. Insufficient metadata privileges are a failure, not an empty inventory.

Generate read-only commands for **every** base table and view; review and execute the returned commands separately, never through unrestricted evaluation:

```sql
SELECT CONCAT('SELECT COUNT(*) AS RowCount FROM `',
              REPLACE(table_name,'`','``'),'`;') AS CountCommand,
       CONCAT('SHOW CREATE TABLE `',REPLACE(table_name,'`','``'),'`;') AS DefinitionCommand
FROM information_schema.tables
WHERE table_schema=DATABASE() AND table_type='BASE TABLE'
ORDER BY table_name;

SELECT CONCAT('SHOW CREATE VIEW `',REPLACE(table_name,'`','``'),'`;') AS DefinitionCommand
FROM information_schema.views WHERE table_schema=DATABASE() ORDER BY table_name;
SELECT CONCAT('SHOW CREATE ',routine_type,' `',REPLACE(routine_name,'`','``'),'`;') AS DefinitionCommand
FROM information_schema.routines WHERE routine_schema=DATABASE() ORDER BY routine_type,routine_name;
SELECT CONCAT('SHOW CREATE TRIGGER `',REPLACE(trigger_name,'`','``'),'`;') AS DefinitionCommand
FROM information_schema.triggers WHERE trigger_schema=DATABASE() ORDER BY trigger_name;
SELECT CONCAT('SHOW CREATE EVENT `',REPLACE(event_name,'`','``'),'`;') AS DefinitionCommand
FROM information_schema.events WHERE event_schema=DATABASE() ORDER BY event_name;
```

Compare SHOW CREATE outputs, including complete definitions, with only the explicitly mapped **schema identifier** (`tmcwd` → `tmcwd_restore_test`) accounted for. Do not replace substrings in literals, snapshots, routine bodies or stored row values. Capture AUTO_INCREMENT state too. If a stored definition still requires the original schema, document/resolve restore compatibility under separate authorization rather than silently changing it.

For every table, a private comparison must include every column, including audit identities, Created/Updated timestamps, security fields, hashes and tokens. Order by a complete unique key; for keyless tables compare a multiset with duplicate multiplicities. Use unambiguous canonical serialization: explicit null marker, type marker, byte length and exact bytes per field; binary/UTF-8 text represented losslessly, fixed-scale decimal invariant formatting, temporal precision retained. Generate keyed HMAC-SHA256 row digests with an ephemeral comparison key kept outside Git/logs; compare ordered digests/multiplicities, never output security material. Record this comparator's actual implementation/version and synthetic edge-case validation before accepting its result. No comparator or row capture has been executed in B6. The manifest is a reusable operational report, not a claim of automated full-row verification.

## Financial SELECTs

Confirm actual mapped columns before running; a missing table/column is an unexplained schema difference, not a reason to skip a check. Include users, customers, accounts, billings, readings, reading_sheets, payments, penalties, other_charges and other_charge_installments in the all-table counts. Also include payment_receipts, invoice_cancellations, invoice numbering/configuration and adjustment tables wherever present.

```sql
SELECT Status,COUNT(*) AS RowsCount,SUM(TotalBillAmount) AS TotalBillAmount,
 SUM(RemainingAmount) AS RemainingAmount,SUM(BillArrearsAmount) AS BillArrearsAmount,
 SUM(BillingAdjustment) AS BillingAdjustment,SUM(OtherCharges) AS OtherCharges
FROM billings GROUP BY Status ORDER BY Status;
SELECT Success+0 AS SuccessFlag,Method,ChargesOnly+0 AS ChargesOnlyFlag,
 COUNT(*) AS RowsCount,SUM(PaidAmount) AS PaidAmount,SUM(BalanceBefore) AS BalanceBefore
FROM payments GROUP BY Success,Method,ChargesOnly ORDER BY Success,Method,ChargesOnly;
SELECT PaymentStatus,COUNT(*) AS RowsCount,SUM(Amount) AS Amount
FROM penalties GROUP BY PaymentStatus ORDER BY PaymentStatus;
SELECT PaymentStatus,IsActive+0 AS ActiveFlag,COUNT(*) AS RowsCount,SUM(Amount) AS ContractAmount
FROM other_charges GROUP BY PaymentStatus,IsActive ORDER BY PaymentStatus,IsActive;
SELECT PaymentStatus,COUNT(*) AS RowsCount,SUM(Amount) AS InstallmentAmount
FROM other_charge_installments GROUP BY PaymentStatus ORDER BY PaymentStatus;
SELECT Role,IsActive+0 AS ActiveFlag,IsVerified+0 AS VerifiedFlag,COUNT(*) AS RowsCount
FROM users GROUP BY Role,IsActive,IsVerified ORDER BY Role,IsActive,IsVerified;

SELECT COUNT(*) AS BillingCount,
 SUM(InvoiceSnapshotJson IS NULL) AS NullSnapshots,
 SUM(InvoiceSnapshotJson IS NOT NULL AND OCTET_LENGTH(InvoiceSnapshotJson)=0) AS EmptySnapshots,
 SUM(InvoiceSnapshotJson IS NOT NULL AND JSON_VALID(InvoiceSnapshotJson)=1) AS ValidJsonSnapshots,
 SUM(ServiceInvoiceNumber IS NOT NULL AND ServiceInvoiceNumber<>'') AS NumberedInvoices
FROM billings;
SELECT Id,AccountId,ReadingId,BillingReferenceId,ServiceInvoiceNumber,
 OCTET_LENGTH(InvoiceSnapshotJson) AS SnapshotBytes,
 SHA2(CAST(InvoiceSnapshotJson AS BINARY),256) AS RawSnapshotSha256
FROM billings ORDER BY Id;
SELECT ServiceInvoiceNumber,COUNT(*) AS Occurrences
FROM billings WHERE ServiceInvoiceNumber IS NOT NULL AND ServiceInvoiceNumber<>''
GROUP BY ServiceInvoiceNumber ORDER BY ServiceInvoiceNumber;
SELECT PaymentReference,COUNT(*) AS Occurrences
FROM payment_receipts WHERE PaymentReference IS NOT NULL AND PaymentReference<>''
GROUP BY PaymentReference ORDER BY PaymentReference;
SELECT PaymentReference,COUNT(*) AS ChildPaymentRows,SUM(PaidAmount) AS PaidAmount
FROM payments GROUP BY PaymentReference ORDER BY PaymentReference;
```

`OfficialInvoiceSnapshot` is the serialized contract; its persisted raw column is **billings.InvoiceSnapshotJson**, not a column named OfficialInvoiceSnapshot. Hash exact bytes, not reparsed/reserialized JSON. Preserve legacy null/invalid snapshots as baseline findings; never repair them during recovery validation. Issued invoice/receipt uniqueness must match original evidence and any preexisting duplicates must be explained. Shared payment references across allocation child rows are legitimate; do not require payments.PaymentReference to be unique. Compare external receipt references and their method/status context privately too.

## Sign-off record

| Gate | Original evidence | Restored evidence | Difference / disposition |
|---|---|---|---|
| Identity, isolation, scheduler OFF | NOT CAPTURED | NOT CAPTURED | Pending |
| All catalog objects, exact schema/indexes/constraints/collations | NOT CAPTURED | NOT CAPTURED | Pending |
| Every base-table count | NOT CAPTURED | NOT CAPTURED | Pending |
| Complete private row digests/audit/timestamps/security comparison | NOT CAPTURED | NOT CAPTURED | Pending |
| Financial totals/status distributions | NOT CAPTURED | NOT CAPTURED | Pending |
| Invoice/receipt identity and multiplicities | NOT CAPTURED | NOT CAPTURED | Pending |
| Snapshot presence and exact raw byte hashes | NOT CAPTURED | NOT CAPTURED | Pending |
| Independent artifact checksum, restore exit, operator approval | NOT CAPTURED | NOT CAPTURED | Pending |

Recovery verdict stays **NOT VERIFIED** until every required comparison has actual evidence and zero unexplained differences. Neither a checksum nor successful import alone is PASS.
