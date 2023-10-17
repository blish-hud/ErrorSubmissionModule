using Gw2Sharp.WebApi.Http;
using Gw2Sharp.WebApi.Middleware;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using System.Net;
using System.Linq;

namespace BhModule.Community.ErrorSubmissionModule.WebHooks {
    internal class WebStatMiddleware : IWebApiMiddleware {

        private EtmConfig _config;
        private ErrorSubmissionModule _errorSubmissionModule;

        string _apiAddressList = null;

        public WebStatMiddleware(EtmConfig config, ErrorSubmissionModule errorSubmissionModule) {
            _config = config;
            _errorSubmissionModule = errorSubmissionModule;

            PopulateApiHost();
        }

        private void PopulateApiHost() {
            try {
                var hostEntry = Dns.GetHostEntry("api.guildwars2.com");
                _apiAddressList = string.Join(",", hostEntry.AddressList.OrderBy(a => a.Address));
            } catch (Exception) { /* NOOP */ }
        }

        private bool IsSuccessStatusCode(HttpStatusCode statusCode) {
            // We only have access to the StatusCode, unfortunately.
            var asInt = (int)statusCode;
            return asInt >= 200 && asInt <= 299;
        }

        public async Task<IWebApiResponse> OnRequestAsync(MiddlewareContext context, Func<MiddlewareContext, CancellationToken, Task<IWebApiResponse>> callNext, CancellationToken cancellationToken = default) {
            var requestTimer = Stopwatch.StartNew();
            var response = await callNext(context, cancellationToken).ConfigureAwait(false);

            try {
                // Only account for requests entirely from the API.
                if (response.CacheState == CacheState.FromLive) {
                    if (!_config.ApiReportSuccess && IsSuccessStatusCode(response.StatusCode)) {
                        return response;
                    }

#pragma warning disable CS4014 // Intentional.
                    _config.ApiReportUri.WithHeaders(new {
                        api_endpoint = context.Request.Options.EndpointPath,
                        api_rtt = requestTimer.ElapsedMilliseconds,
                        api_rc = (int)response.StatusCode,
                        api_hosts = _apiAddressList,
                        etm_version = _errorSubmissionModule.Version.ToString(),
                    }, true).GetAsync();
#pragma warning restore CS4014
                }
            } catch (Exception) { /* NOOP */ }

            return response;
        }
    }
}
