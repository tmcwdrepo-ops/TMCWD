using System.Net.Http.Json;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class DebitCreditMemoTransaction
    {
        private readonly WebService _service;

        public DebitCreditMemoTransaction(WebService service)
        {
            _service = service;
        }

        public async Task<DebitCreditMemo?> Save(int userId, DebitCreditMemo memo)
        {
            var response = await _service.Client.PostAsJsonAsync($"api/DebitCreditMemo/Save/{userId}", memo);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<DebitCreditMemo>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }
}
