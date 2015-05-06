using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchoNest
{
    public class EchoNestHttpHandler : HttpClientHandler
    {
        private volatile bool _running = false;
        private int _ratelimit;
        private int _ratelimitRemaining;
        private DateTime _lastRequest;
        private object _waitForTurn = new object();

        public EchoNestHttpHandler(int ratelimit)
            : base()
        {
            this._ratelimit = ratelimit;
            _ratelimitRemaining = ratelimit;
            _lastRequest = DateTime.MinValue;
        }

        /// <summary>
        /// Gets or sets whether or not the requests should be sent in a ratelimit-aware interval.
        /// </summary>
        public bool Balanced
        {
            get;
            set;
        }

        private double Interval
        {
            get
            {
                return 60000.0 / _ratelimit;
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task<HttpResponseMessage>.Factory.StartNew(delegate
            {
                lock (_waitForTurn)
                {
                    while (_running)
                    {
                        Monitor.Wait(_waitForTurn);
                    }

                    _running = true;
                }

                try
                {
                    // Throttle request speed if need be
                    if (Balanced)
                    {
                        var timeToSleep = TimeSpan.FromMilliseconds(Interval) - (DateTime.Now - _lastRequest);
                        if (timeToSleep.TotalMilliseconds > 0)
                        {
                            Thread.Sleep(timeToSleep);
                        }
                    }
                    else if (_ratelimitRemaining <= 0)
                    {
                        // ratelimit reached, sleep for a minute.
                        Thread.Sleep(60000);
                    }

                    // Try to get a response
                    HttpResponseMessage response;
                    while (!HandleRequest(request, cancellationToken, out response))
                    {
                        Thread.Sleep(5000);
                    }

                    return response;
                }
                finally
                {
                    lock (_waitForTurn)
                    {
                        _running = false;
                        Monitor.Pulse(_waitForTurn);
                    }
                }
            });
        }

        /// <summary>
        /// Fires a request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="response">The response to the request.</param>
        /// <returns>Whether or not the ratelimit was reached.</returns>
        private bool HandleRequest(HttpRequestMessage request, CancellationToken cancellationToken, out HttpResponseMessage response)
        {
            response = base.SendAsync(request, cancellationToken).Result;
            _lastRequest = DateTime.Now;

            IEnumerable<string> values;
            if (response.Headers.TryGetValues("X-Ratelimit-Limit", out values))
            {
                _ratelimit = int.Parse(values.First());
            }

            if (response.Headers.TryGetValues("X-Ratelimit-Remaining", out values))
            {
                _ratelimitRemaining = int.Parse(values.First());
            }

            if ((int)response.StatusCode == 429)
            {
                // Rate limit reached
                return false;
            }
            
            return true;
        }
    }
}
