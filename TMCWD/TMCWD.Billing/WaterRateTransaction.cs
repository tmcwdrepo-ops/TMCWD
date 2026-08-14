using System.Globalization;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class WaterRateTransaction
    {
        private readonly WebService _service;

        public WaterRateTransaction(WebService service)
        {
            _service = service;
        }

        private WaterRate ConvertJsonToWaterRate(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<WaterRate>(
                json,
                options) ?? new WaterRate();
        }

        private List<WaterRate> ConvertJsonToWaterRates(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<WaterRate>>(
                json,
                options) ?? new List<WaterRate>();
        }

        public async Task<WaterRate> Get(int id)
        {
            var response = await _service.Client.GetAsync(
                $"api/WaterRate/Get/{id}");

            if (!response.IsSuccessStatusCode)
                return null;

            var data = await response.Content.ReadAsStringAsync();

            return ConvertJsonToWaterRate(data);
        }

        public async Task<List<WaterRate>> GetAll()
        {
            var response = await _service.Client.GetAsync(
                "api/WaterRate/GetAll");

            if (!response.IsSuccessStatusCode)
                return new List<WaterRate>();

            var data = await response.Content.ReadAsStringAsync();

            return ConvertJsonToWaterRates(data);
        }

        public async Task<List<WaterRate>> GetByClassification(
            AccountClassification classification)
        {
            var response = await _service.Client.GetAsync(
                $"api/WaterRate/GetByClassification/{(int)classification}");

            if (!response.IsSuccessStatusCode)
                return new List<WaterRate>();

            var data = await response.Content.ReadAsStringAsync();

            return ConvertJsonToWaterRates(data);
        }

        public async Task<WaterRate> GetByClassificationAndMeterSize(
            AccountClassification classification,
            decimal meterSize)
        {
            var formattedMeterSize =
                meterSize.ToString(CultureInfo.InvariantCulture);

            var response = await _service.Client.GetAsync(
                $"api/WaterRate/GetByClassificationAndMeterSize/" +
                $"{(int)classification}/{formattedMeterSize}");

            if (!response.IsSuccessStatusCode)
                return null;

            var data = await response.Content.ReadAsStringAsync();

            return ConvertJsonToWaterRate(data);
        }
    }
}