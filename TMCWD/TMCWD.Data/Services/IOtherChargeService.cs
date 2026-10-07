    using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IOtherChargeService
    {
        public Task<OtherCharge> Get(int id);

        public Task<List<OtherCharge>> GetAll();

        public Task<List<OtherCharge>> GetByReference(string referenceId);

        public Task<OtherCharge> SaveUpdate(int userId, OtherCharge otherCharge);

        public Task<OtherCharge> Deactivate(int id, int userId);

        public Task<int> PostToCollection(int userId, List<int> otherChargeIds);

        /// <summary>
        /// Processes payment for a set of other-charge records identified by their IDs.
        /// Marks each charge as Paid, writes a Payment row, and returns the new invoice number.
        /// </summary>
        public Task<string> ProcessPayment(
            int userId,
            TMCWD.Model.Billing.Requests.OtherChargePaymentRequest request);

    }
}
