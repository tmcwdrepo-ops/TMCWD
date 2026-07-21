using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Services
{
    public class WebService
    {

        private HttpClient _httpClient;
        public WebService() 
        { 
            _httpClient = new HttpClient();
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
