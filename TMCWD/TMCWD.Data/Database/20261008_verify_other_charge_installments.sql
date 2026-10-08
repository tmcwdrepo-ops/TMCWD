-- Read-only integrity checks. Expected: all anomaly counts = 0.
-- Draft parents may have no children. Parent Amount is contractual, never collectible.
SELECT COUNT(*) AS invalid_posted_plans
FROM other_charges c LEFT JOIN (
    SELECT OtherChargeId, COUNT(*) AS child_count, SUM(Amount) AS contractual_total,
           MIN(InstallmentNumber) AS first_number, MAX(InstallmentNumber) AS last_number
    FROM other_charge_installments GROUP BY OtherChargeId
) i ON i.OtherChargeId=c.Id
WHERE c.PaymentStatus<>0 AND (c.InstallmentCount IS NULL OR c.InstallmentCount<1
    OR COALESCE(i.child_count,0)<>c.InstallmentCount OR i.contractual_total<>c.Amount
    OR i.first_number<>1 OR i.last_number<>c.InstallmentCount);

SELECT COUNT(*) AS orphan_installments FROM other_charge_installments i
LEFT JOIN other_charges c ON c.Id=i.OtherChargeId WHERE c.Id IS NULL;
SELECT COUNT(*) AS invalid_installment_settlements FROM other_charge_installments i
LEFT JOIN payments p ON p.Id=i.SettledPaymentId
WHERE (i.PaymentStatus=2 AND (p.Id IS NULL OR p.Success=0))
   OR (i.PaymentStatus<>2 AND i.SettledPaymentId IS NOT NULL);
SELECT COUNT(*) AS obsolete_parent_settlement_links FROM other_charges WHERE SettledPaymentId IS NOT NULL;
SELECT COUNT(*) AS dangling_penalty_payments FROM penalties c
LEFT JOIN payments p ON p.Id=c.SettledPaymentId WHERE c.SettledPaymentId IS NOT NULL AND p.Id IS NULL;
SELECT COUNT(*) AS payments_without_receipts FROM payments p
LEFT JOIN payment_receipts r ON r.PaymentReference=p.PaymentReference WHERE r.Id IS NULL;
SELECT COUNT(*) AS receipts_without_payment_lines FROM payment_receipts r
WHERE NOT EXISTS(SELECT 1 FROM payments p WHERE p.PaymentReference=r.PaymentReference);

-- Audited development artifacts: run immediately after the targeted cleanup.
-- No cleanup statement here; these IDs must not be treated as legacy in future deployments.
SELECT COUNT(*) AS obsolete_dev_charges FROM other_charges WHERE Id IN (1,2,3,4,5,6,8,9,10,11);
SELECT COUNT(*) AS obsolete_dev_stubs FROM Billings WHERE Id IN (8,9,10,15,16);
SELECT COUNT(*) AS obsolete_dev_payments FROM payments WHERE Id IN (5,6,7);
SELECT COUNT(*) AS obsolete_dev_receipts FROM payment_receipts
WHERE PaymentReference IN ('OCO-1790830722476','CR005','CR006');
SELECT COUNT(*) AS obsolete_dev_billing_links FROM Billings
WHERE BillingReferenceId IN ('B215F10C62FF','BC0EF7B237D6','6C54C23568B2','D49D975F72E0','461DD1D7C878')
   OR PaymentTransactionId IN ('OCO-1790830722476','CR005','CR006');
