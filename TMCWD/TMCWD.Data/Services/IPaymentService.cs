using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IPaymentService
    {

        public Task<Payment> Get(int id);

        public Task<Payment> GetByPaymentReference(string referenceId);

        public Task<List<Payment>> GetByBillingReference(string referenceId);

        public Task<List<Payment>> GetByMethod(int methodId);

        public Task<List<Payment>> GetByCashier(int userId);

        public Task<List<Payment>> GetByMacAddress(string macAddress);

        public Task<Payment> SaveUpdate(int userId, Payment payment);

        public Task<List<Payment>> ProcessCollectionPayment(
            int userId,
            TMCWD.Model.Billing.Requests.CollectionPaymentRequest request);

        public Task<List<TMCWD.Model.Billing.InvoiceRecord>> SearchInvoices(string query, int limit = 50);

        public Task<TMCWD.Model.Billing.InvoiceRecord?> GetInvoice(string invoiceNumber);

        public Task CancelInvoice(int userId, TMCWD.Model.Billing.CancelInvoiceRequest request);

        /// <summary>Returns all successful payments within an inclusive date range.</summary>
        public Task<List<Payment>> GetByDateRange(DateTime from, DateTime to, int cashierId = 0);

    }
}
