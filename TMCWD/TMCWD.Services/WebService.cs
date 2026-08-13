using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace TMCWD.Services
{
    public class WebService
    {

        private HttpClient _httpClient;
        public WebService(IHttpClientFactory httpClientFactory) 
        { 
            _httpClient = httpClientFactory.CreateClient("TmcWdApi");
        }

        public HttpClient Client 
        { get
            {
                return _httpClient;
            }
        }

        public void SetClient(HttpClient client)
        {
            this._httpClient = client;
        }
    }
}
