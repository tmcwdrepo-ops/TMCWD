using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IBillingService
    {

        public Task<Billing> Get(int id);

        public Task<List<Billing>> GetAll();

        public Task<List<Billing>> GetByAccountId(int accountId);

        public Task<List<Billing>> GetByJobOrderId(int jobOrderId);

        public Task<Billing> GetByBillingReference(string billingReferenceId);
        Task<TMCWD.Model.Billing.CollectionBill?> GetCollectionBill(string billingReferenceId);

        public Task<Billing> GetByPaymentTransactionId(string paymentTransactionId);

        public Task<Billing> GetByReadingId(int readingId);

        public Task<List<Billing>> GetByReadings(int[] readingIds);

        public Task<Billing> SaveUpdate(int userId, Billing billing);
        Task<Billing> CreateChargeAnchor(int userId, int accountId);

        /// <summary>
        /// Returns the billing whose ServiceInvoiceNumber exactly matches the
        /// supplied value, or null if none exists. Used by the collection
        /// lookup endpoint.
        /// </summary>
        public Task<Billing?> GetByServiceInvoiceNumber(string serviceInvoiceNumber);

        /// <summary>
        /// Allocates the next service invoice number from the
        /// service_invoice_seq counter and assigns it to the supplied billing
        /// row.  Idempotent: if the row already has a ServiceInvoiceNumber it
        /// is returned unchanged.  Must be called inside an existing database
        /// transaction so the counter lock is held for the full operation.
        /// </summary>
        public Task<string> AssignServiceInvoiceNumber(Billing billing);

    }
}
