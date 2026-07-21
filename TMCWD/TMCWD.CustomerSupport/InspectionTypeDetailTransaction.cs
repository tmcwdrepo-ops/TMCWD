using System;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Interfaces;
using TMCWD.Utility.Generic;

namespace TMCWD.CustomerSupport
{
    internal class InspectionTypeDetailTransaction : TransactionBase
    {

        #region fields

        private const string _apiRoute = "api/ÌncidentTypeDetail/";
        private const string _saveUpdateUrl = $"{_apiRoute}SaveUpdate";
        private const string _getDetailUrl = $"{_apiRoute}GetDetail";

        #endregion

        #region constructors
        public InspectionTypeDetailTransaction() { }
        #endregion

        #region public methods

        public bool SaveUpdate(InspectionTypeDetail detail)
        {
            bool isSuccess = false;

            try
            {

                if (detail.InspectionTypeId == 0) throw new Exception("No selected inspection type id");
                if (string.IsNullOrEmpty(detail.Detail.Trim())) throw new Exception("inspection type detail is required");
                if (detail.Id > 0) detail.DateUpdated = DateTime.Now;
                else detail.DateCreated = DateTime.Now;

                isSuccess = Task.Run(() => SaveUpdateTask(detail)).GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                Logger.Log(ErrorModule.CustomerSupport, ErrorType.Error, ex.Message);
            }

            return isSuccess;
        }

        public InspectionTypeDetail? GetInspectionTypeDetail(int inspectionTypeId)
        {
            InspectionTypeDetail? detail = new();

            try
            {
                if (inspectionTypeId <= 0) throw new Exception("Inspection type id is required for getting detail");
                detail = Task.Run(() => GetInspectionTypeDetailTask(inspectionTypeId)).GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                Logger.Log(ErrorModule.Administration, ErrorType.Error, ex.Message);
            }

            return detail;
        }

        #endregion

        #region private methods

        private async Task<bool> SaveUpdateTask(InspectionTypeDetail inspectionTypeDetail)
        {
            bool isSuccess = false;

            try
            {
                using(HttpClient client = new())
                {
                    client.BaseAddress = new Uri(this.BaseUrl);
                    
                    HttpContent content = JsonContent.Create(inspectionTypeDetail);

                    using (var response = await client.PostAsync(_saveUpdateUrl, content))
                    {

                        string result = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            isSuccess = result.ToLower() == "true";
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logger.Log(ErrorModule.CustomerSupport, ErrorType.Error, ex.Message);
            }

            return isSuccess;
        }

        private async Task<InspectionTypeDetail?> GetInspectionTypeDetailTask(int inspectionTypeId)
        {
            InspectionTypeDetail? detail = new();

            try
            {
                using(HttpClient client = new())
                {
                    client.BaseAddress = new Uri(this.BaseUrl);
                    string url = QueryHelpers.AddQueryString(_getDetailUrl, "id", inspectionTypeId.ToString());
                    using(var response = await client.GetAsync(url))
                    {
                        string data = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode) throw new Exception(data);
                        detail = JsonSerializer.Deserialize<InspectionTypeDetail>(data);
                    }

                }
            }
            catch(Exception ex)
            {
                Logger.Log(ErrorModule.CustomerSupport, ErrorType.Error, ex.Message);
            }

            return detail;
        }

        #endregion


    }
}
