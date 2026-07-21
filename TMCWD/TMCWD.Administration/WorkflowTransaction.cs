using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Administrator;
using System.Text.Json;
using System.Net.Http.Json;
using TMCWD.Services;


namespace TMCWD.Administration
{
    public class WorkflowTransaction
    {

        private readonly WebService _webService;

        public WorkflowTransaction(WebService webService) { _webService = webService; }

        public Workflow ConvertJsonToWorkflow(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Workflow>(json, serializerOptions) ?? new();
        }

        public List<Workflow> ConvertJsonToWorkflows(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Workflow>>(json, serializerOptions) ?? new();
        }

        public async Task<Workflow> Get(int id)
        {
            if (id <= 0) throw new Exception("Id is required");

            var response = await _webService.Client.GetAsync($"api/Workflow/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToWorkflow(data);
        }

        public async Task<List<Workflow>> GetAll()
        {
            var response = await _webService.Client.GetAsync("api/Workflow/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToWorkflows(data);
        }

        public async Task<Workflow> SaveUpdate(int userId, Workflow workflow)
        {
            var content = JsonContent.Create(workflow);
            var response = await _webService.Client.PostAsync($"api/Workflow/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToWorkflow(data);
        }

        public async Task<bool> Delete(int id)
        {
            var response = await _webService.Client.DeleteAsync($"api/Workflow/Delete/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) return false;
            return data.ToLower().Equals("true");
        }

    }
}
