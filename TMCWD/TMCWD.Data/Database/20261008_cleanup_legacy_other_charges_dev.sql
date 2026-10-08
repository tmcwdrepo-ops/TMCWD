-- DEVELOPMENT / SYNTHETIC DATA ONLY. Explicit operator execution, NEVER startup.
-- Authorized local tmcwd audit: charges 1,2,3,4,5,6,8,9,10,11;
-- charge-only stubs 8,9,10,15,16; payments 5,6,7; receipts matched by reference.
-- Operator must verify localhost and set @tmcwd_confirmed_synthetic = 1.
-- Stops on changed data or shared/dependent financial history. No water reconstruction.
CREATE TEMPORARY TABLE phase6g_cleanup_guard (ok INT NOT NULL CHECK (ok=1));
-- Run with a stop-on-error client; roll back on any failed guard.
-- Guard: Confirmed local synthetic tmcwd only
INSERT INTO phase6g_cleanup_guard SELECT IF(DATABASE() <> 'tmcwd' OR COALESCE(@tmcwd_confirmed_synthetic,0) <> 1,0,1);
    START TRANSACTION;
    SELECT Id AS locked_charge FROM other_charges ORDER BY Id FOR UPDATE;
    SELECT Id AS locked_stub FROM Billings WHERE Id IN (8,9,10,15,16) ORDER BY Id FOR UPDATE;
    SELECT Id AS locked_payment FROM payments WHERE Id IN (5,6,7) ORDER BY Id FOR UPDATE;
-- Guard: Obsolete charge set differs from development audit
INSERT INTO phase6g_cleanup_guard SELECT IF((SELECT COUNT(*) FROM other_charges) <> 10
       OR (SELECT COUNT(*) FROM other_charges WHERE Id IN (1,2,3,4,5,6,8,9,10,11)
           AND SettledPaymentId IS NULL AND
           ((Id IN (1,2,3,4,10) AND PayableIn='Cash') OR (Id=5 AND PayableIn='3 months')
             OR (Id IN (6,8,9,11) AND COALESCE(PayableIn,'')=''))
           AND PaymentStatus=IF(Id IN (6,8,10,11),2,1)
           AND IsActive=IF(Id IN (1,3,4,5),0,1)) <> 10
       OR (SELECT COUNT(*) FROM other_charges c JOIN Billings b ON b.BillingReferenceId=c.BillingReferenceId
           WHERE (c.Id=8 AND b.Id=8) OR (c.Id IN (1,3,4,5,6) AND b.Id=9)
              OR (c.Id=2 AND b.Id=10) OR (c.Id=9 AND b.Id=15) OR (c.Id IN (10,11) AND b.Id=16)) <> 10,0,1);
-- Guard: Synthetic stub state differs from audit
INSERT INTO phase6g_cleanup_guard SELECT IF((SELECT COUNT(*) FROM Billings WHERE Id IN (8,9,10,15,16)
         AND ReadingId=0 AND JobOrderId=0 AND InvoiceSnapshotJson IS NULL AND ServiceInvoiceNumber IS NULL
         AND MaterialsAmount=0 AND Penalties=0 AND BillingAdjustment=0 AND COALESCE(BillArrearsAmount,0)=0
         AND ((Id=8 AND AccountId=42 AND OtherCharges=0 AND TotalBillAmount=0 AND RemainingAmount=0 AND Status=2)
           OR (Id=9 AND AccountId=44 AND OtherCharges=2000 AND TotalBillAmount=2000 AND RemainingAmount=0 AND Status=2)
           OR (Id=10 AND AccountId=45 AND OtherCharges=0 AND TotalBillAmount=0 AND RemainingAmount=0 AND Status=1)
           OR (Id=15 AND AccountId=43 AND OtherCharges=2000 AND TotalBillAmount=2000 AND RemainingAmount=2000 AND Status=1)
           OR (Id=16 AND AccountId=46 AND OtherCharges=3815 AND TotalBillAmount=3815 AND RemainingAmount=0 AND Status=2))) <> 5,0,1);
-- Guard: Payment/receipt history differs or is shared
INSERT INTO phase6g_cleanup_guard SELECT IF((SELECT COUNT(*) FROM payments p JOIN Billings b ON p.BillingReference=b.BillingReferenceId WHERE b.Id IN (8,9,10,15,16)) <> 3
       OR (SELECT COUNT(*) FROM payments p JOIN Billings b ON p.BillingReference=b.BillingReferenceId
           WHERE p.Success=1 AND p.Method=1 AND p.BalanceBefore IS NULL
           AND ((p.Id=5 AND b.Id=8 AND p.PaidAmount=2000 AND p.PaymentReference='OCO-1790830722476')
             OR (p.Id=6 AND b.Id=16 AND p.PaidAmount=3815 AND p.PaymentReference='CR005')
             OR (p.Id=7 AND b.Id=9 AND p.PaidAmount=2000 AND p.PaymentReference='CR006'))) <> 3
       OR (SELECT COUNT(*) FROM payments WHERE PaymentReference IN ('OCO-1790830722476','CR005','CR006')) <> 3
       OR (SELECT COUNT(*) FROM payment_receipts WHERE PaymentReference IN ('OCO-1790830722476','CR005','CR006')) <> 3,0,1);
-- Guard: Unexpected dependent financial history; inspect before cleanup
INSERT INTO phase6g_cleanup_guard SELECT IF(EXISTS(SELECT 1 FROM penalties WHERE BillingReferenceId IN ('B215F10C62FF','BC0EF7B237D6','6C54C23568B2','D49D975F72E0','461DD1D7C878') OR SettledPaymentId IN (5,6,7))
       OR EXISTS(SELECT 1 FROM billing_adjustments WHERE BillingReferenceId IN ('B215F10C62FF','BC0EF7B237D6','6C54C23568B2','D49D975F72E0','461DD1D7C878'))
       OR EXISTS(SELECT 1 FROM senior_citizen_discounts WHERE BillingReferenceId IN ('B215F10C62FF','BC0EF7B237D6','6C54C23568B2','D49D975F72E0','461DD1D7C878'))
       OR EXISTS(SELECT 1 FROM penalty_waiver_requests WHERE BillingReferenceId IN ('B215F10C62FF','BC0EF7B237D6','6C54C23568B2','D49D975F72E0','461DD1D7C878'))
       OR EXISTS(SELECT 1 FROM invoice_cancellations WHERE PaymentReference IN ('OCO-1790830722476','CR005','CR006'))
       OR EXISTS(SELECT 1 FROM payment_checks WHERE PaymentReference IN ('OCO-1790830722476','CR005','CR006'))
       OR EXISTS(SELECT 1 FROM ewallet_transaction WHERE PaymentReference IN ('OCO-1790830722476','CR005','CR006'))
       OR EXISTS(SELECT 1 FROM wallet_transactions WHERE PaymentReference IN ('OCO-1790830722476','CR005','CR006')),0,1);
    SELECT (SELECT COUNT(*) FROM other_charges) AS charges_before,
           (SELECT COUNT(*) FROM payments) AS payments_before,
           (SELECT COUNT(*) FROM payment_receipts) AS receipts_before,
           (SELECT COUNT(*) FROM Billings) AS billings_before;
    -- Clear only snapshots that explicitly link to the removed artifacts.
    UPDATE water_connection_applications SET charging_data=NULL,
        stage=IF(stage='payment','charging',stage)
      WHERE id IN (2,10,11,14) AND JSON_VALID(charging_data)
        AND JSON_UNQUOTE(JSON_EXTRACT(charging_data,'$.reference')) IN ('B215F10C62FF','BC0EF7B237D6','6C54C23568B2','D49D975F72E0','461DD1D7C878');
    SELECT ROW_COUNT() AS application_charging_snapshots_cleared;
    UPDATE water_connection_applications SET payment_data=NULL
      WHERE id IN (2,10,11,14) AND JSON_VALID(payment_data)
        AND JSON_UNQUOTE(JSON_EXTRACT(payment_data,'$.reference')) IN ('OCO-1790830722476','CR005','CR006');
    SELECT ROW_COUNT() AS application_payment_snapshots_cleared;
    DELETE FROM other_charges WHERE Id IN (1,2,3,4,5,6,8,9,10,11);
    SELECT ROW_COUNT() AS charges_removed;
    DELETE FROM payments WHERE Id IN (5,6,7);
    SELECT ROW_COUNT() AS payments_removed;
    DELETE FROM payment_receipts WHERE PaymentReference IN ('OCO-1790830722476','CR005','CR006');
    SELECT ROW_COUNT() AS receipts_removed;
    DELETE FROM Billings WHERE Id IN (8,9,10,15,16);
    SELECT ROW_COUNT() AS stubs_removed;
    COMMIT;
DROP TEMPORARY TABLE phase6g_cleanup_guard;
