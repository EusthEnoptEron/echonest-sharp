using System;
using System.Net.Http;

namespace EchoNest
{
    public sealed class EchoNestSession : IDisposable
    {
        #region Fields

        private const string BaseUrl = "http://developer.echonest.com/api/v4/";
        private readonly string _apiKey;

        private HttpClient _httpClient;
        private EchoNestHttpHandler _httpHandler;

        #endregion Fields

        #region Constructors

        public EchoNestSession(string apiKey, bool balanced = false)
        {
            _apiKey = apiKey;
            _httpHandler = new EchoNestHttpHandler(20) { Balanced = balanced };
            _httpClient = new HttpClient(_httpHandler) { BaseAddress = new Uri(BaseUrl) };
            _httpClient.MaxResponseContentBufferSize = int.MaxValue;
        }

        #endregion Constructors

        #region Methods

        void IDisposable.Dispose()
        {
            if (_httpClient == null)
            {
                return;
            }

            _httpClient.Dispose();
            _httpClient = null;
        }

        public T Query<T>() where T : EchoNestService, new()
        {
            return new T { ApiKey = _apiKey, HttpClient = _httpClient };
        }

        #endregion Methods
    }
}