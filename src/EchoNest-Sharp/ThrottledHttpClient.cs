using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EchoNest
{
    public class ThrottledHttpClient : HttpClient
    {
        private int ratelimit;
        private int ratelimitRemaining;
        private DateTime lastRequest;

        public bool Throttled {get; set;}

        public ThrottledHttpClient(int ratelimit)
        {
            this.ratelimit = ratelimit;
            ratelimitRemaining = ratelimit;
            lastRequest = DateTime.MinValue;

            Throttled = true;
        }

        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (!Throttled) return base.SendAsync(request, cancellationToken);
            else
            {
                if (ratelimitRemaining == 0)
                    Thread.Sleep(lastRequest.AddMinutes(1) - DateTime.Now);

                var response = base.SendAsync(request, cancellationToken);

                response.Wait();

                var result = response.Result;

                IEnumerable<string> values;
                if (result.Headers.TryGetValues("X-Ratelimit", out values))
                {
                    ratelimit = int.Parse(values.First());
                }
                if (result.Headers.TryGetValues("X-Ratelimit-Remaining", out values))
                {
                    ratelimitRemaining = int.Parse(values.First());
                }


                return response;
            }
        }
    }
}
